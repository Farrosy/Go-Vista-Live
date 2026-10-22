// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_EDITOR

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Bindings;
    using Coherence.Tests;
    using Entities;
    using Log;
    using Moq;
    using NUnit.Framework;
    using UnityEngine;
    using Index = Entities.Index;
    using Logger = Log.Logger;

    public class CommandHandlerTests : CoherenceTest
    {
        class MockComponent : MonoBehaviour
        {
            public void C1(CoherenceSync sync) { }

            public void C2(GameObject go) { }

            public void C3(Transform t) { }

            public void C4(Transform t, string m) { }

            public void C5() { }

            public void AlwaysThrows() => throw new("Test Exception");
        }

        class MockComponent2 : MonoBehaviour
        {
            public void C() { }
        }

        private Mock<Logger> loggerMock;
        private Mock<ICoherenceSync> syncMock;
        private MockBridgeBuilder mockBridgeBuilder;
        private Mock<ICoherenceBridge> bridgeMock;
        private MockComponent mockComponent;

        private CommandsHandler commandsHandler;
        private List<Binding> bindings;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            loggerMock = new Mock<Logger>(null, null, null, null);

            using var syncBuilder = new MockSyncBuilder();
            syncMock = syncBuilder.Mock;

            var coherenceSync = syncMock.Object;

            mockBridgeBuilder = new MockBridgeBuilder().SetIsConnected()
                .SetClientID(new(1));
            var bridge = mockBridgeBuilder.Build();
            bridgeMock = mockBridgeBuilder.Mock;

            syncMock.Setup(sync => sync.CoherenceBridge).Returns(bridge);

            var gameObject = new GameObject();
            mockComponent = gameObject.AddComponent<MockComponent>();

            var command1Descriptor = new Descriptor(typeof(MockComponent), typeof(MockComponent).GetMethod(nameof(MockComponent.C1)));
            var command2Descriptor = new Descriptor(typeof(MockComponent), typeof(MockComponent).GetMethod(nameof(MockComponent.C2)));
            var command3Descriptor = new Descriptor(typeof(MockComponent), typeof(MockComponent).GetMethod(nameof(MockComponent.C3)));
            var command4Descriptor = new Descriptor(typeof(MockComponent), typeof(MockComponent).GetMethod(nameof(MockComponent.C4)));
            var command5Descriptor = new Descriptor(typeof(MockComponent), typeof(MockComponent).GetMethod(nameof(MockComponent.C5)));
            var alwaysThrowsDescriptor = new Descriptor(typeof(MockComponent), typeof(MockComponent).GetMethod(nameof(MockComponent.AlwaysThrows)));

            bindings = new()
            {
                new CommandBinding(command1Descriptor, mockComponent),
                new CommandBinding(command2Descriptor, mockComponent),
                new CommandBinding(command3Descriptor, mockComponent),
                new CommandBinding(command4Descriptor, mockComponent),
                new CommandBinding(command5Descriptor, mockComponent),
                new CommandBinding(alwaysThrowsDescriptor, mockComponent),
            };

            commandsHandler = new(coherenceSync, bindings, loggerMock.Object);

            commandsHandler.AddBakedCommand(TypeUtils.CommandName(typeof(MockComponent), nameof(MockComponent.C1)), "(Coherence.Toolkit.CoherenceSync)",
                _ => { }, _ => { }, MessageTarget.StateAuthorityOnly, mockComponent, false);
            commandsHandler.AddBakedCommand(TypeUtils.CommandName(typeof(MockComponent), nameof(MockComponent.C2)), "(UnityEngine.GameObject)",
                _ => { }, _ => { }, MessageTarget.StateAuthorityOnly, mockComponent, false);
            commandsHandler.AddBakedCommand(TypeUtils.CommandName(typeof(MockComponent), nameof(MockComponent.C3)), "(UnityEngine.Transform)",
                _ => { }, _ => { }, MessageTarget.StateAuthorityOnly, mockComponent, false);
            commandsHandler.AddBakedCommand(TypeUtils.CommandName(typeof(MockComponent), nameof(MockComponent.C4)), "(UnityEngine.TransformSystem.String)",
                _ => { }, _ => { }, MessageTarget.StateAuthorityOnly, mockComponent, false);
            commandsHandler.AddBakedCommand(TypeUtils.CommandName(typeof(MockComponent), nameof(MockComponent.C5)), "()",
                _ => { }, _ => { }, MessageTarget.StateAuthorityOnly, mockComponent, false);
            commandsHandler.AddBakedCommand(TypeUtils.CommandName(typeof(MockComponent), nameof(MockComponent.AlwaysThrows)), "()",
                _ => { }, _ => mockComponent.AlwaysThrows(), MessageTarget.StateAuthorityOnly, mockComponent, false);
        }

        [TearDown]
        public override void TearDown()
        {
            mockBridgeBuilder.Dispose();
            base.TearDown();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SendCommand_ShouldValidateIfEntitiesAreInitialized_WithCoherenceSync(bool initialized)
        {
            // Tests that if you try to send a command with a CoherenceSync as an argument
            // that it validates it if it's initialized (has an EntityState)

            // Arrange
            var cs = new GameObject().AddComponent<CoherenceSync>();

            bridgeMock.Setup(bridge => bridge.UnityObjectToEntityId(It.IsAny<ICoherenceSync>()))
                .Returns(initialized ? new Entity((Index)1, 0, Entity.Relative) : Entity.InvalidRelative);

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C1), MessageTarget.StateAuthorityOnly, ChannelID.Default, true, cs);

            // Assert
            Assert.AreEqual(initialized, result);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SendCommand_ShouldValidateIfEntitiesAreInitialized_WithGameObject(bool initialized)
        {
            // Tests that if you try to send a command with a GameObject as an argument
            // that it validates it if it's initialized (has an EntityState)

            // Arrange
            var cs = new GameObject();

            bridgeMock.Setup(bridge => bridge.UnityObjectToEntityId(It.IsAny<GameObject>()))
                .Returns(initialized ? new Entity((Index)1, 0, Entity.Relative) : Entity.InvalidRelative);

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C2), MessageTarget.StateAuthorityOnly, ChannelID.Default, true, cs);

            // Assert
            Assert.AreEqual(initialized, result);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SendCommand_ShouldValidateIfEntitiesAreInitialized_WithTransform(bool initialized)
        {
            // Tests that if you try to send a command with a Transform as an argument
            // that it validates it if it's initialized (has an EntityState)

            // Arrange
            var cs = new GameObject();

            bridgeMock.Setup(bridge => bridge.UnityObjectToEntityId(It.IsAny<Transform>()))
                .Returns(initialized ? new Entity((Index)1, 0, Entity.Relative) : Entity.InvalidRelative);

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C3), MessageTarget.StateAuthorityOnly, ChannelID.Default, true, cs.transform);

            // Assert
            Assert.AreEqual(initialized, result);
        }

        [Test]
        public void SendCommand_ShouldValidateTupleTypes_WhenOnlyTuplesAndCorrect()
        {
            // Tests that if you try to send only tuples as a command arguments and
            // types of tuples matches their values that the command is sent

            // Arrange
            var cs = new GameObject().AddComponent<CoherenceSync>();

            bridgeMock.Setup(bridge => bridge.UnityObjectToEntityId(It.IsAny<ICoherenceSync>()))
                .Returns(new Entity((Index)1, 0, Entity.Relative));

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C1), MessageTarget.StateAuthorityOnly, ChannelID.Default, true, (typeof(CoherenceSync), cs));

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void SendCommand_ShouldValidateTupleTypes_WhenOnlyTuplesAndIncorrect()
        {
            // Tests that if you try to send only tuples as a command arguments and
            // types of tuples DO NOT match their values that the command is NOT sent

            // Arrange
            var cs = new GameObject().AddComponent<CoherenceSync>();

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C1), MessageTarget.StateAuthorityOnly, ChannelID.Default, true, (typeof(Transform), cs));

            // Assert
            Assert.IsFalse(result);
        }

        [Test, Description("SendCommand should catch exceptions thrown by the executed method and log a warning about it.")]
        public void SendCommandUsingBakedScript_Catches_Exceptions()
        {
            commandsHandler.SendCommand(mockComponent.AlwaysThrows, MessageTarget.StateAuthorityOnly, ChannelID.Default);

            loggerMock.Verify(logger => logger.Warning(It.Is<Warning>(id => id == Warning.ToolkitSyncCommandReceiveException), It.IsAny<string>(), It.IsAny<(string key, object value)[]>()));
        }

        [Test]
        public void SendCommand_ShouldValidateTupleTypes_WhenMixTuplesObjectsAndCorrect()
        {
            // Tests that if you try to send tuples mixed with objects as a command arguments and
            // types of tuples matches their values that the command is sent

            // Arrange
            var cs = new GameObject();

            bridgeMock.Setup(bridge => bridge.UnityObjectToEntityId(It.IsAny<Transform>()))
                .Returns(new Entity((Index)1, 0, Entity.Relative));

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C4), MessageTarget.StateAuthorityOnly, ChannelID.Default, true, (typeof(Transform), cs.transform), "m");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void SendCommand_ShouldValidateTupleTypes_WhenMixTuplesObjectsAndIncorrect()
        {
            // Tests that if you try to send tuples mixed with objects as a command arguments and
            // types of tuples DO NOT match their values that the command is NOT sent

            // Arrange
            var cs = new GameObject();

            bridgeMock.Setup(bridge => bridge.UnityObjectToEntityId(It.IsAny<Transform>()))
                .Returns(new Entity((Index)1, 0, Entity.Relative));

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C4), MessageTarget.StateAuthorityOnly, ChannelID.Default, true, (typeof(Transform), cs), "m");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        [Description("SendCommand should work even if there is no CoherenceBridge and the command is targeting all or authority only")]
        public void SendCommand_ShouldSendCommand_WhenOffline()
        {
            // Arrange
            var go = new GameObject();
            var cs = go.AddComponent<CoherenceSync>();
            go.AddComponent<MockComponent>();
            syncMock.Setup(sync => sync.CoherenceBridge).Returns((ICoherenceBridge)null);
            syncMock.Setup(sync => sync.gameObject).Returns(go);

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C1), MessageTarget.StateAuthorityOnly, ChannelID.Default, true, cs);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        [Description("SendCommand will not work if there is no CoherenceBridge and the command is targeting other clients")]
        public void SendCommand_ShouldNotSendCommand_When_Offline_And_TargetOther()
        {
            // Arrange
            var go = new GameObject();
            var cs = go.AddComponent<CoherenceSync>();
            go.AddComponent<MockComponent>();
            syncMock.Setup(sync => sync.CoherenceBridge).Returns((ICoherenceBridge)null);
            syncMock.Setup(sync => sync.gameObject).Returns(go);

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C1), MessageTarget.Other, ChannelID.Default, true, cs);

            // Assert
            Assert.That(result, Is.False);
#if !COHERENCE_DISABLE_LOG_WARNING
            loggerMock.Verify(logger => logger.Warning(It.Is<Warning>(id => id == Warning.ToolkitCommandBridgeDisconnected), It.IsAny<string>()));
#endif
        }

        [Test]
        [Description("SendCommand with Action should work even if there is no CoherenceBridge and the command is targeting all or authority only")]
        public void SendCommand_ShouldSendActionCommand_WhenOffline()
        {
            // Arrange
            var go = new GameObject();
            var cs = go.AddComponent<CoherenceSync>();
            go.AddComponent<MockComponent>();
            syncMock.Setup(sync => sync.CoherenceBridge).Returns((ICoherenceBridge)null);
            syncMock.Setup(sync => sync.gameObject).Returns(go);

            // Act
            var result = commandsHandler.SendCommand(mockComponent.C5, MessageTarget.StateAuthorityOnly, ChannelID.Default);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        [Description("SendCommand with Action with parameters should work even if there is no CoherenceBridge and the command is targeting all or authority only")]
        public void SendCommand_ShouldSendActionWithParamsCommand_WhenOffline()
        {
            // Arrange
            var go = new GameObject();
            var cs = go.AddComponent<CoherenceSync>();
            go.AddComponent<MockComponent>();
            syncMock.Setup(sync => sync.CoherenceBridge).Returns((ICoherenceBridge)null);
            syncMock.Setup(sync => sync.gameObject).Returns(go);

            // Act
            var args = new ValueTuple<Type, object>[]
            {
                new() { Item1 = typeof(CoherenceSync), Item2 = cs },
            };
            var result = commandsHandler.SendCommand<CoherenceSync>(mockComponent.C1, MessageTarget.StateAuthorityOnly, ChannelID.Default, args);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        [Description("SendCommand with Action with parameters should work even if there is no CoherenceBridge and the command is targeting all or authority only")]
        public void SendCommand_ShouldSendActionWithMultipleParamsCommand_WhenOffline()
        {
            // Arrange
            var go = new GameObject();
            var cs = go.AddComponent<CoherenceSync>();
            go.AddComponent<MockComponent>();
            syncMock.Setup(sync => sync.CoherenceBridge).Returns((ICoherenceBridge)null);
            syncMock.Setup(sync => sync.gameObject).Returns(go);

            // Act
            var args = new ValueTuple<Type, object>[]
            {
                new() { Item1 = typeof(Transform), Item2 = go.transform },
                new() { Item1 = typeof(string), Item2 = go.name },
            };
            var result = commandsHandler.SendCommand<Transform, string>(mockComponent.C4, MessageTarget.StateAuthorityOnly, ChannelID.Default, args);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test, Description("SendCommand should not throw when the CoherenceSync is destroyed and Name is accessed for error logging.")]
        public void SendCommand_ShouldNotThrow_WhenSyncIsDestroyed()
        {
            // Arrange - simulate a destroyed Unity object: IsDestroyed returns true,
            // and accessing .name throws MissingReferenceException (as Unity does).
            syncMock.Setup(sync => sync.IsDestroyed).Returns(true);
            syncMock.Setup(sync => sync.name).Throws(new MissingReferenceException());

            // Act - send invalid command to trigger Name access in the error message path.
            var result = commandsHandler.SendCommand(typeof(MockComponent), "InvalidMethodName", MessageTarget.All, ChannelID.Default, false, Array.Empty<object>());

            // Assert
            Assert.That(result, Is.False);
#if !COHERENCE_DISABLE_LOG_ERROR
            loggerMock.Verify(log => log.Error(
                It.Is<Error>(id => id == Error.ToolkitSyncCommandInvalidName),
                It.Is<string>(msg => msg.Contains("'null'"))));
#endif
        }

        [Test]
        public void SendCommand_Should_Fail_When_No_Bindings_Exist_For_Target_Type()
        {
            var result = commandsHandler.SendCommand(typeof(MockComponent2), nameof(MockComponent2.C), MessageTarget.StateAuthorityOnly, ChannelID.Default, false, Array.Empty<object>());

            Assert.That(result, Is.False);
#if !COHERENCE_DISABLE_LOG_ERROR
            loggerMock.Verify(log => log.Error(
                It.Is<Error>(id => id == Error.ToolkitSyncCommandMissingComponent), It.IsAny<string>()));
#endif
        }

        [Test]
        public void SendCommand_Targeting_Valid_Bindings_Should_Work()
        {
            var result1 = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C1), MessageTarget.StateAuthorityOnly, ChannelID.Default, false, (typeof(CoherenceSync), null));
            var result2 = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C2), MessageTarget.StateAuthorityOnly, ChannelID.Default, false, (typeof(GameObject), null));
            var result3 = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C3), MessageTarget.StateAuthorityOnly, ChannelID.Default, false, (typeof(Transform), null));
            var result4 = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C4), MessageTarget.StateAuthorityOnly, ChannelID.Default, false, (typeof(Transform), null), (typeof(string), null));
            var result5 = commandsHandler.SendCommand(typeof(MockComponent), nameof(MockComponent.C5), MessageTarget.StateAuthorityOnly, ChannelID.Default, false, Array.Empty<object>());

            Assert.That(result1, Is.True);
            Assert.That(result2, Is.True);
            Assert.That(result3, Is.True);
            Assert.That(result4, Is.True);
            Assert.That(result5, Is.True);
        }

        [Test]
        public void SendCommand_ShouldFailWithProperErrorMessage_WhenMissingBinding()
        {
            // Arrange
            var go = new GameObject();
            _ = go.AddComponent<CoherenceSync>();
            go.AddComponent<MockComponent>();
            syncMock.Setup(sync => sync.CoherenceBridge).Returns((ICoherenceBridge)null);
            syncMock.Setup(sync => sync.gameObject).Returns(go);

            // Act
            var result = commandsHandler.SendCommand(typeof(MockComponent), "InvalidMethodName", MessageTarget.All, ChannelID.Default, false, Array.Empty<object>());

            // Assert
            Assert.That(result, Is.False);
#if !COHERENCE_DISABLE_LOG_ERROR
            loggerMock.Verify(log => log.Error(
                It.Is<Error>(id => id == Error.ToolkitSyncCommandInvalidName),
                It.Is<string>(msg => msg.StartsWith("Unable to send command 'Coherence.Toolkit.Tests.CommandHandlerTests.MockComponent.InvalidMethodName'"))));
#endif
        }
    }
}

#endif // UNITY_EDITOR

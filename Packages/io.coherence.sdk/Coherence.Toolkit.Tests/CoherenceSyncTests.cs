// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Bindings;
    using Coherence.Common.Tests;
    using Coherence.Tests;
    using Connection;
    using Entities;
    using Log;
    using Moq;
    using NUnit.Framework;
    using ProtocolDef;
    using UnityEngine;
    using UnityEngine.TestTools;
    using Index = Entities.Index;
    using Object = UnityEngine.Object;

    public class CoherenceSyncTests : CoherenceTest
    {
        private static object[] commonTestCases =
        {
            new object[] {MessageTarget.StateAuthorityOnly, MessageTarget.StateAuthorityOnly, true},
            new object[] {MessageTarget.StateAuthorityOnly, MessageTarget.All, true},
            new object[] {MessageTarget.StateAuthorityOnly, MessageTarget.Other, true},
            new object[] {MessageTarget.All, MessageTarget.StateAuthorityOnly, false},
            new object[] {MessageTarget.All, MessageTarget.All, true},
            new object[] {MessageTarget.All, MessageTarget.Other, true},
            new object[] {MessageTarget.Other, MessageTarget.StateAuthorityOnly, false},
            new object[] {MessageTarget.Other, MessageTarget.All, true},
            new object[] {MessageTarget.Other, MessageTarget.Other, true},
        };

        [Test]
        [TestCaseSource(nameof(commonTestCases))]
        public void ReceiveCommand_Baked_HandlesRouting(MessageTarget commandTarget, MessageTarget routing, bool expectReceived)
        {
            // Arrange
            var mockSync = new Mock<ICoherenceSync>();

            var bakedScript = new CoherenceSyncBakedMock();
            var bridgeGo = new GameObject();
            var bridge = bridgeGo.AddComponent<CoherenceBridge>();

            mockSync.Setup(cs => cs.BakedScript).Returns(bakedScript);
            mockSync.Setup(cs => cs.EntityState).Returns(new NetworkEntityState(Entity.InvalidRelative, AuthorityType.Full, false, false, mockSync.Object, String.Empty));
            mockSync.Setup(cs => cs.CoherenceBridge).Returns(bridge);

            var command = routing switch
            {
                MessageTarget.StateAuthorityOnly => StateAuthorityOnlyCommand.Create(TestSchema.DataFactory, Entity.InvalidRelative, commandTarget),
                MessageTarget.InputAuthorityOnly => InputAuthorityOnlyCommand.Create(TestSchema.DataFactory, Entity.InvalidRelative, commandTarget),
                MessageTarget.All => AllCommand.Create(TestSchema.DataFactory, Entity.InvalidRelative, commandTarget),
                MessageTarget.Other => OtherCommand.Create(TestSchema.DataFactory, Entity.InvalidRelative, commandTarget),
                _ => throw new ArgumentOutOfRangeException(nameof(routing), routing, null)
            };

            CommandsHandler handler = new CommandsHandler(mockSync.Object, new List<Binding>(), new UnityLogger());
            // Act
            handler.HandleCommand(command);

            // Assert
            Assert.That(bakedScript.TimesCalled(nameof(CoherenceSyncBaked.ReceiveCommand)), Is.EqualTo(expectReceived ? 1 : 0));
        }

        [Test]
        [Description("CoherenceBridge instance will not be destroyed when the updater is null")]
        public void HandleConnected_HandleNetworkedDestruction_BridgeNotDestroyed_WhenUpdaterIsNull()
        {
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            using var mockBridgeBuilder = new MockBridgeBuilder();
            var bridge = mockBridgeBuilder.Build();

            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;

            sync.SetBridge(bridge);

            // Casts to interface needed because sync.CoherenceBridge returns instance of CoherenceBridge
            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.Not.Null);
            ((ICoherenceSync)sync).HandleNetworkedDestruction(false);
            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.Not.Null);
        }

        [Test]
        [Description("The current bridge connection is set to null when a destruction is called for over the network")]
        public void HandleConnected_HandleNetworkedDestruction_BridgeDestroyed_WhenUpdaterIsNotNull()
        {
            var updaterMock = new Mock<ICoherenceSyncUpdater>();
            var taggedForNetworkedDestruction = false;
            updaterMock.SetupGet(u => u.TaggedForNetworkedDestruction).Returns(() => taggedForNetworkedDestruction);
            updaterMock.SetupSet(u => u.TaggedForNetworkedDestruction = It.IsAny<bool>()).Callback<bool>(value => taggedForNetworkedDestruction = value);

            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            sync.SetUpdater(updaterMock.Object);

            using var mockBridgeBuilder = new MockBridgeBuilder();
            var bridge = mockBridgeBuilder.Build();

            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;

            sync.SetBridge(bridge);

            // Casts to interface needed because sync.CoherenceBridge returns instance of CoherenceBridge
            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.Not.Null);
            ((ICoherenceSync)sync).HandleNetworkedDestruction(false);
            Assert.That(sync.IsBeingSynced, Is.False);
            updaterMock.VerifySet(u => u.TaggedForNetworkedDestruction = true, Times.Once());
        }

        [Test]
        [Description("Syncing network entity state is not called when there is no bridge")]
        public void HandleConnected_BridgeIsNull_SyncNetworkEntityState_NotCalled()
        {
            var updaterMock = new Mock<ICoherenceSyncUpdater>();
            updaterMock.Setup(u => u.TaggedForNetworkedDestruction).Returns(false);

            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            sync.SetUpdater(updaterMock.Object);

            using var mockBridgeBuilder = new MockBridgeBuilder();
            mockBridgeBuilder.SetupEntitiesManager(x => x.SetSyncNetworkEntityStateThrows(new ArgumentNullException()));
            mockBridgeBuilder.Build();

            Assert.That(sync.CoherenceBridge, Is.Null);

            sync.EntityState = new(new Entity(0,0,false), AuthorityType.Input, false, true, sync, "uuid");
            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;

            mockBridgeBuilder.RaiseOnConnectedInternal();
            mockBridgeBuilder.MockEntitiesManagerBuilder.Mock.Verify(m => m.SyncNetworkEntityState(It.IsAny<ICoherenceSync>()), Times.Never);
        }

        [Test]
        [Description("Syncing the entity state happens on a new connection with a valid bridge")]
        public void HandleConnected_BridgeIsNotNull_SyncNetworkEntityState_Called()
        {
            var updaterMock = new Mock<ICoherenceSyncUpdater>();
            updaterMock.Setup(u => u.TaggedForNetworkedDestruction).Returns(false);

            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            sync.SetUpdater(updaterMock.Object);
            var componentUpdates = new ComponentUpdates();

            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetupEntitiesManager(x => x.SetSyncNetworkEntityStateReturns(()=> (null, componentUpdates, 0, false)))
                .SetIsConnected(true)
                .SetClientID(new(1));

            mockBridgeBuilder.EntitiesManager.SetClient(mockBridgeBuilder.Client);

            var bridge = mockBridgeBuilder.Build();
            sync.SetBridge(bridge);
            sync.ConnectBridge(bridge);
            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.Not.Null);

            sync.EntityState = new NetworkEntityState(new Entity(0, 0, false), AuthorityType.Input, false, true, sync, "uuid");
            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;
            mockBridgeBuilder.RaiseOnConnectedInternal();
            mockBridgeBuilder.MockEntitiesManagerBuilder.Mock.Verify(m => m.SyncNetworkEntityState(It.IsAny<ICoherenceSync>()), Times.Once);
        }

        [Test]
        [Description("Ensure that CoherenceSync.CoherenceBridge will continue to return the bridge " +
                     "that the CoherenceSync was connected to even after the networked entity has been destroyed.\n\n" +
                     "This can help avoid resource leaks if other components try to access the CoherenceSync's bridge " +
                     "after the entity has been destroyed - e.g. to unsubscribe event handlers from CoherenceBridge's events.")]
        public void HandleNetworkedDestruction_Does_Not_Set_Bridge_To_Null()
        {
            var updaterMock = new Mock<ICoherenceSyncUpdater>();
            updaterMock.Setup(u => u.TaggedForNetworkedDestruction).Returns(false);
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            sync.SetUpdater(updaterMock.Object);
            using var mockBridgeBuilder = new MockBridgeBuilder();
            var bridge = mockBridgeBuilder.Build();
            sync.CoherenceSyncConfig = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync.CoherenceSyncConfig.IncludeInSchema = true;
            sync.CoherenceSyncConfig.Instantiator = new Mock<INetworkObjectInstantiator>().Object;
            sync.SetBridge(bridge);

            ((ICoherenceSync)sync).HandleNetworkedDestruction(false);

            Assert.That(((ICoherenceSync)sync).CoherenceBridge, Is.EqualTo(bridge));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ConnectedEntityChanged_Does_Not_Raise_ConnectedEntityChangeOverride_If_Connected_Entity_Did_Not_Change(bool hasCoherenceNode, bool parentIsNull)
        {
            var entity = parentIsNull ? Entity.InvalidRelative : new((Index)1u, 1, isAbsolute: false);
            var newParentEntity = entity;
            var oldParentEntity = entity;
            var parentSync = parentIsNull ? CoherenceSync.Create() : null;
            using var mockBridgeBuilder = new MockBridgeBuilder();
            mockBridgeBuilder.GetCoherenceSyncForEntityReturns(e => e == entity ? parentSync : null);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            ((ICoherenceSync)sync).ConnectedEntityChanged(oldParentEntity, out _);
            var wasEventRaised = false;
            sync.ConnectedEntityChangeOverride += _ => wasEventRaised = true;

            ((ICoherenceSync)sync).ConnectedEntityChanged(newParentEntity, out _);

            Assert.That(wasEventRaised, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConnectedEntityChanged_Raises_ConnectedEntityChangeOverride_If_Connected_Entity_Changed_And_No_CoherenceNode_Is_Attached(bool newParentIsNull)
        {
            var notNullEntity = new Entity((Index)1u, 1, isAbsolute: false);
            var newParentEntity = newParentIsNull ? Entity.InvalidRelative : notNullEntity;
            var oldParentEntity = newParentIsNull ? notNullEntity : Entity.InvalidRelative;
            var oldParentSync = newParentIsNull ? CoherenceSync.Create() : null;
            var newParentSync = newParentIsNull ? null : CoherenceSync.Create();
            using var mockBridgeBuilder = new MockBridgeBuilder();
            mockBridgeBuilder.GetCoherenceSyncForEntityReturns(e => e == newParentEntity ? newParentSync : e == oldParentEntity ? oldParentSync : null);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            ((ICoherenceSync)sync).ConnectedEntityChanged(oldParentEntity, out _);
            var wasEventRaised = false;
            sync.ConnectedEntityChangeOverride += _ => wasEventRaised = true;

            ((ICoherenceSync)sync).ConnectedEntityChanged(newParentEntity, out _);

            Assert.That(wasEventRaised, Is.True);
        }

        [TestCase(AuthorityType.None), TestCase(AuthorityType.Input)]
        public async Task RequestAuthorityAsync_For_State_Succeeds_When_OnStateAuthority_Is_Raised(AuthorityType initialAuthorityType)
        {
            const AuthorityType requestedAuthorityType = AuthorityType.State;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, initialAuthorityType, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), requestedAuthorityType)).Verifiable();

            try
            {
                var requestTask = sync.RequestAuthorityAsync(requestedAuthorityType);

                sync.EntityState = new(entity, requestedAuthorityType, entityState.IsOrphaned, entityState.NetworkInstantiated, sync, entityState.CoherenceUUID);
                sync.OnStateAuthority.Invoke();

                var result = await requestTask;
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.Success));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.None), TestCase(AuthorityType.Input)]
        public async Task RequestAuthorityAsync_For_Full_Succeeds_When_OnStateAuthority_Is_Raised(AuthorityType initialAuthorityType)
        {
            const AuthorityType requestedAuthorityType = AuthorityType.Full;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var bridge = mockBridgeBuilder.Build();
            var sync = CoherenceSync.Create(bridge: bridge, updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, initialAuthorityType, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), requestedAuthorityType)).Verifiable();

            try
            {
                var requestTask = sync.RequestAuthorityAsync(requestedAuthorityType);

                sync.EntityState = new(entity, requestedAuthorityType, entityState.IsOrphaned, entityState.NetworkInstantiated, sync, entityState.CoherenceUUID);
                sync.OnStateAuthority.Invoke();

                var result = await requestTask;
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.Success));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.None), TestCase(AuthorityType.State)]
        public async Task RequestAuthorityAsync_For_Full_Succeeds_When_OnInputAuthority_Is_Raised(AuthorityType initialAuthorityType)
        {
            const AuthorityType requestedAuthorityType = AuthorityType.Full;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var bridge = mockBridgeBuilder.Build();
            var sync = CoherenceSync.Create(bridge: bridge, updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, initialAuthorityType, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), requestedAuthorityType)).Verifiable();

            try
            {
                var requestTask = sync.RequestAuthorityAsync(requestedAuthorityType);

                sync.EntityState = new(entity, requestedAuthorityType, entityState.IsOrphaned, entityState.NetworkInstantiated, sync, entityState.CoherenceUUID);
                sync.OnInputAuthority.Invoke();

                var result = await requestTask;
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.Success));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.None), TestCase(AuthorityType.State)]
        public async Task RequestAuthorityAsync_For_Input_Succeeds_When_OnInputAuthority_Is_Raised(AuthorityType initialAuthorityType)
        {
            const AuthorityType requestedAuthorityType = AuthorityType.Input;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, initialAuthorityType, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), requestedAuthorityType)).Verifiable();

            try
            {
                var requestTask = sync.RequestAuthorityAsync(requestedAuthorityType);

                sync.EntityState = new(entity, requestedAuthorityType, entityState.IsOrphaned, entityState.NetworkInstantiated, sync, entityState.CoherenceUUID);
                sync.OnInputAuthority.Invoke();

                var result = await requestTask;
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.Success));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.Input)]
        [TestCase(AuthorityType.State)]
        public async Task RequestAuthorityAsync_For_Partial_Authority_Fails_When_Already_Have_Full_Input(AuthorityType requestedAuthorityType)
        {
            const AuthorityType initialAuthorityType = AuthorityType.Full;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, initialAuthorityType, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), requestedAuthorityType)).Verifiable();

            try
            {
                var result = await sync.RequestAuthorityAsync(requestedAuthorityType);

                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.AlreadyHasAuthorityError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.None), TestCase(AuthorityType.Input), TestCase(AuthorityType.State), TestCase(AuthorityType.Full)]
        public async Task RequestAuthorityAsync_For_None_Fails(AuthorityType initialAuthorityType)
        {
            const AuthorityType requestedAuthorityType = AuthorityType.None;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            sync.EntityState = new(Entity.InvalidRelative, initialAuthorityType, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>())).Verifiable();

            try
            {
                var result = await sync.RequestAuthorityAsync(requestedAuthorityType);

                clientMock.Verify(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>()), Times.Never);
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.InvalidAuthorityTypeError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.None), TestCase(AuthorityType.Input), TestCase(AuthorityType.State), TestCase(AuthorityType.Full)]
        public async Task RequestAuthorityAsync_Fails_If_Not_Synchronized_With_Network(AuthorityType initialAuthorityType)
        {
            const AuthorityType requestedAuthorityType = AuthorityType.None;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            sync.EntityState = null;
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>())).Verifiable();

            try
            {
                var result = await sync.RequestAuthorityAsync(requestedAuthorityType);

                clientMock.Verify(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>()), Times.Never);
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.EntityNotSynchronizedWithNetwork));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.Input), TestCase(AuthorityType.State), TestCase(AuthorityType.Full)]
        public async Task RequestAuthorityAsync_Fails_When_Current_Authority_Equals_Requested_Authority(AuthorityType authorityType)
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            sync.EntityState = new(Entity.InvalidRelative, authorityType, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>())).Verifiable();

            try
            {
                var result = await sync.RequestAuthorityAsync(authorityType);

                clientMock.Verify(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>()), Times.Never);
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.AlreadyHasAuthorityError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.Input), TestCase(AuthorityType.State), TestCase(AuthorityType.Full)]
        public async Task RequestAuthorityAsync_Fails_When_OnAuthorityRequestRejected_Is_Raised(AuthorityType requestedAuthorityType)
        {
            const AuthorityType initialAuthorityType = AuthorityType.None;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            sync.EntityState = new(Entity.InvalidRelative, initialAuthorityType, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>())).Verifiable();

            try
            {
                var requestTask = sync.RequestAuthorityAsync(requestedAuthorityType);

                sync.OnAuthorityRequestRejected.Invoke(requestedAuthorityType);

                var result = await requestTask;
                clientMock.Verify(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>()), Times.Once);
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.RequestRejectedError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.Input, CoherenceSync.SimulationType.ClientSide, false)]
        [TestCase(AuthorityType.Input, CoherenceSync.SimulationType.ServerSide, false)]
        [TestCase(AuthorityType.Input, CoherenceSync.SimulationType.ServerSideWithClientInput, false)]
        [TestCase(AuthorityType.State, CoherenceSync.SimulationType.ClientSide, false)]
        [TestCase(AuthorityType.State, CoherenceSync.SimulationType.ServerSide, false)]
        [TestCase(AuthorityType.State, CoherenceSync.SimulationType.ServerSideWithClientInput, false)]
        [TestCase(AuthorityType.Full, CoherenceSync.SimulationType.ClientSide, false)]
        [TestCase(AuthorityType.Full, CoherenceSync.SimulationType.ServerSide, false)]
        [TestCase(AuthorityType.Full, CoherenceSync.SimulationType.ServerSideWithClientInput, false)]
        [TestCase(AuthorityType.Input, CoherenceSync.SimulationType.ClientSide, true)]
        [TestCase(AuthorityType.Input, CoherenceSync.SimulationType.ServerSide, true)]
        [TestCase(AuthorityType.Input, CoherenceSync.SimulationType.ServerSideWithClientInput, true)]
        [TestCase(AuthorityType.State, CoherenceSync.SimulationType.ClientSide, true)]
        [TestCase(AuthorityType.State, CoherenceSync.SimulationType.ServerSide, true)]
        [TestCase(AuthorityType.State, CoherenceSync.SimulationType.ServerSideWithClientInput, true)]
        [TestCase(AuthorityType.Full, CoherenceSync.SimulationType.ClientSide, true)]
        [TestCase(AuthorityType.Full, CoherenceSync.SimulationType.ServerSide, true)]
        [TestCase(AuthorityType.Full, CoherenceSync.SimulationType.ServerSideWithClientInput, true)]
        public async Task RequestAuthorityAsync_Fails_When_AuthorityTransfer_Is_NotTransferable(AuthorityType authorityType, CoherenceSync.SimulationType simulationType, bool isSimulatorOrHost)
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: isSimulatorOrHost);
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build());
            sync.AuthorityTransfer = CoherenceSync.AuthorityTransferType.NotTransferable;
            sync.simulationType = simulationType;
            sync.EntityState = new(Entity.InvalidRelative, AuthorityType.None, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>())).Verifiable();

            try
            {
                var result = await sync.RequestAuthorityAsync(authorityType);

                clientMock.Verify(c => c.SendAuthorityRequest(It.IsAny<Entity>(), It.IsAny<AuthorityType>()), Times.Never);
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.EntityNotTransferableError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        public async Task RequestAuthorityAsync_Can_Be_Canceled()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            const AuthorityType requestedAuthorityType = AuthorityType.Full;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.None, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), requestedAuthorityType)).Verifiable();

            try
            {
                var requestTask = sync.RequestAuthorityAsync(requestedAuthorityType, cancellationTokenSource.Token);

                cancellationTokenSource.Cancel();

                var result = await requestTask;
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.Canceled));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        [Description("BakeBindings should skip a CommandBinding whose component is missing and log a warning.")]
        public void BakeBindings_SkipsCommandBinding_WhenComponentIsMissing()
        {
            var go = new GameObject();
            try
            {
                var sync = go.AddComponent<CoherenceSync>();
                sync.logger = logger;
                sync.Bindings.Add(new NullComponentCommandBinding());
                sync.BakeBindings();
                Assert.That(logger.GetCountForWarningID(Warning.ToolkitBindingMissingComponent), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        [Ignore("This test is very slow because CoherenceSync.RequestAuthorityTimeoutAfterSeconds is 5 seconds. If the timeout is made configurable this test can be modified to complete faster and enabled.")]
        public async Task RequestAuthorityAsync_Fails_With_TimeoutError_After_Five_Seconds()
        {
            const AuthorityType requestedAuthorityType = AuthorityType.Full;
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.None, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityRequest(It.IsAny<Entity>(), requestedAuthorityType)).Verifiable();

            try
            {
                var requestTask = sync.RequestAuthorityAsync(requestedAuthorityType);

                var result = await requestTask;
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.TimeoutError));
                Assert.That(result.Warning, Is.EqualTo(Warning.ToolkitSyncAuthorityRequestRejected));
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.Full)]
        [TestCase(AuthorityType.State)]
        [TestCase(AuthorityType.Input)]
        public async Task TransferAuthorityAsync_Succeeds_When_OnAuthTransferComplete_Is_Raised(AuthorityType transferredAuthorityType)
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var bridge = mockBridgeBuilder.Build();
            var sync = CoherenceSync.Create(bridge: bridge, updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.Full, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);
            mockBridgeBuilder.Mock.Setup(x => x.GetCoherenceSyncForEntity(entity)).Returns(sync);

            try
            {
                var targetClientID = new ClientID(42);
                var transferTask = sync.TransferAuthorityAsync(targetClientID, transferredAuthorityType);

                var result = await transferTask;
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.Success));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.Input), TestCase(AuthorityType.State), TestCase(AuthorityType.Full)]
        public async Task TransferAuthorityAsync_Fails_When_Entity_Is_NotTransferable(AuthorityType transferredAuthorityType)
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            sync.AuthorityTransfer = CoherenceSync.AuthorityTransferType.NotTransferable;
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.Full, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);

            try
            {
                var targetClientID = new ClientID(42);
                var result = await sync.TransferAuthorityAsync(targetClientID, transferredAuthorityType);

                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.EntityNotTransferableError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.Input), TestCase(AuthorityType.State), TestCase(AuthorityType.Full)]
        public async Task TransferAuthorityAsync_Fails_When_Entity_Is_Not_Synchronized_With_Network(AuthorityType transferredAuthorityType)
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true)
                .SetLogger(logger);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            sync.EntityState = null;

            try
            {
                var targetClientID = new ClientID(42);
                var result = await sync.TransferAuthorityAsync(targetClientID, transferredAuthorityType);

                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.EntityNotSynchronizedWithNetwork));
                Assert.That(logger.GetCountForErrorID(Error.ToolkitAuthorityNullState), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.Input), TestCase(AuthorityType.State), TestCase(AuthorityType.Full)]
        public async Task TransferAuthorityAsync_Fails_When_Entity_Is_Orphaned(AuthorityType transferredAuthorityType)
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.Full, isOrphaned: true, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);

            try
            {
                var targetClientID = new ClientID(42);
                var result = await sync.TransferAuthorityAsync(targetClientID, transferredAuthorityType);

                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.EntityOrphanedError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.Input), TestCase(AuthorityType.State), TestCase(AuthorityType.Full)]
        public async Task TransferAuthorityAsync_Fails_When_Transferring_To_Self(AuthorityType transferredAuthorityType)
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true)
                .SetClientID(new ClientID(1));
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.Full, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);

            try
            {
                var selfClientID = new ClientID(1);
                var result = await sync.TransferAuthorityAsync(selfClientID, transferredAuthorityType);

                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.AlreadyHasAuthorityError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [TestCase(AuthorityType.None, AuthorityType.Input)]
        [TestCase(AuthorityType.None, AuthorityType.State)]
        [TestCase(AuthorityType.None, AuthorityType.Full)]
        [TestCase(AuthorityType.Input, AuthorityType.State)]
        [TestCase(AuthorityType.Input, AuthorityType.Full)]
        public async Task TransferAuthorityAsync_Fails_When_Insufficient_Authority(AuthorityType currentAuthority, AuthorityType transferredAuthorityType)
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, currentAuthority, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);

            try
            {
                var targetClientID = new ClientID(42);
                var result = await sync.TransferAuthorityAsync(targetClientID, transferredAuthorityType);

                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.InvalidAuthorityTypeError));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        public async Task TransferAuthorityAsync_Fails_When_Bridge_Is_Null()
        {
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();

            try
            {
                var targetClientID = new ClientID(42);
                var result = await sync.TransferAuthorityAsync(targetClientID, AuthorityType.Full);

                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.Canceled));
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        public async Task TransferAuthorityAsync_Can_Be_Canceled()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.Full, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);
            mockBridgeBuilder.Mock.Setup(x => x.GetCoherenceSyncForEntity(entity)).Returns(sync);

            try
            {
                cancellationTokenSource.Cancel();
                var transferTask = sync.TransferAuthorityAsync(new ClientID(42), AuthorityType.Full, cancellationTokenSource.Token);

                var result = await transferTask;
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.Canceled));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        [Ignore("This test is very slow because CoherenceSync.RequestAuthorityTimeoutAfterSeconds is 5 seconds. If the timeout is made configurable this test can be modified to complete faster and enabled.")]
        public async Task TransferAuthorityAsync_Fails_With_TimeoutError_After_Five_Seconds()
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var bridge = mockBridgeBuilder.Build();
            var sync = CoherenceSync.Create(bridge: bridge, updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.Full, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            mockBridgeBuilder.Mock.Setup(x => x.GetNetworkEntityStateForEntity(entity)).Returns(entityState);

            // Override the out-param overload to NOT raise events, so the transfer never completes
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAuthorityTransfer(It.IsAny<Entity>(), It.IsAny<ClientID>(), It.IsAny<bool>(), It.IsAny<AuthorityType>(), out It.Ref<SendAuthorityTransferResult>.IsAny))
                .Callback(new SendAuthorityTransferCallbackNoEvents((Entity _, ClientID _, bool _, AuthorityType _, out SendAuthorityTransferResult result) =>
                {
                    result = new(SendAuthorityTransferResultType.Success, null, null);
                }))
                .Returns(true);

            try
            {
                var targetClientID = new ClientID(42);
                var transferTask = sync.TransferAuthorityAsync(targetClientID, AuthorityType.Full);

                var result = await transferTask;
                Assert.That(result.Type, Is.EqualTo(RequestAuthorityResultType.TimeoutError));
                Assert.That(result.Warning, Is.EqualTo(Warning.ToolkitSyncAuthorityRequestRejected));
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        private delegate void SendAuthorityTransferCallbackNoEvents(
            Entity entity,
            ClientID clientId,
            bool authorized,
            AuthorityType authorityType,
            out SendAuthorityTransferResult result);

        [Test]
        public async Task AdoptAsync_Succeeds_When_OnStateAuthority_Is_Raised()
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.None, isOrphaned: true, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAdoptOrphanRequest(It.IsAny<Entity>())).Verifiable();

            try
            {
                var adoptTask = sync.AdoptAsync();

                sync.EntityState = new(entity, AuthorityType.State, entityState.IsOrphaned, entityState.NetworkInstantiated, sync, entityState.CoherenceUUID);
                sync.OnStateAuthority.Invoke();

                var result = await adoptTask;
                Assert.That(result.Type, Is.EqualTo(AdoptResultType.Success));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        public async Task AdoptAsync_Fails_When_Entity_Is_Not_Orphaned()
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.None, isOrphaned: false, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;

            try
            {
                var result = await sync.AdoptAsync();

                Assert.That(result.Type, Is.EqualTo(AdoptResultType.EntityNotOrphanedError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        public async Task AdoptAsync_Fails_When_Entity_Is_Not_Synchronized_With_Network()
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true)
                .SetLogger(logger);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            sync.EntityState = null;

            try
            {
                var result = await sync.AdoptAsync();

                Assert.That(result.Type, Is.EqualTo(AdoptResultType.EntityNotSynchronizedWithNetwork));
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
                Assert.That(logger.GetCountForErrorID(Error.ToolkitAuthorityNullState), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        public async Task AdoptAsync_Fails_When_Bridge_Is_Null()
        {
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();

            try
            {
                var result = await sync.AdoptAsync();

                Assert.That(result.Type, Is.EqualTo(AdoptResultType.Canceled));
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        public async Task AdoptAsync_Fails_When_OnAuthorityRequestRejected_Is_Raised()
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.None, isOrphaned: true, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAdoptOrphanRequest(It.IsAny<Entity>())).Verifiable();

            try
            {
                var adoptTask = sync.AdoptAsync();

                sync.OnAuthorityRequestRejected.Invoke(AuthorityType.State);

                var result = await adoptTask;
                clientMock.Verify(c => c.SendAdoptOrphanRequest(It.IsAny<Entity>()), Times.Once);
                Assert.That(result.Type, Is.EqualTo(AdoptResultType.RequestRejectedError));
                Assert.That(result.Warning, Is.Not.Null);
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        public async Task AdoptAsync_Can_Be_Canceled()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.None, isOrphaned: true, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAdoptOrphanRequest(It.IsAny<Entity>())).Verifiable();

            try
            {
                var adoptTask = sync.AdoptAsync(cancellationTokenSource.Token);

                cancellationTokenSource.Cancel();

                var result = await adoptTask;
                Assert.That(result.Type, Is.EqualTo(AdoptResultType.Canceled));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        public async Task AdoptAsync_Fails_When_Already_Canceled()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.None, isOrphaned: true, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;

            try
            {
                var result = await sync.AdoptAsync(cancellationTokenSource.Token);

                Assert.That(result.Type, Is.EqualTo(AdoptResultType.Canceled));
                Assert.That(result.Warning, Is.Null);
                Assert.That(result.FailureMessage, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }

        [Test]
        [Ignore("This test is very slow because CoherenceSync.RequestAuthorityTimeoutAfterSeconds is 5 seconds. If the timeout is made configurable this test can be modified to complete faster and enabled.")]
        public async Task AdoptAsync_Fails_With_TimeoutError_After_Five_Seconds()
        {
            using var mockBridgeBuilder = new MockBridgeBuilder()
                .SetIsSimulatorOrHost(isSimulatorOrHost: true);
            var updater = new Mock<ICoherenceSyncUpdater>(MockBehavior.Loose).Object;
            var sync = CoherenceSync.Create(bridge: mockBridgeBuilder.Build(), updater: updater);
            var entity = new Entity((Index)1, 1, true);
            var entityState = new NetworkEntityState(entity, AuthorityType.None, isOrphaned: true, networkInstantiated: false, sync, uuid: "uuid");
            sync.EntityState = entityState;

            // Override SendAdoptOrphanRequest to NOT raise events, so the adopt never completes
            var clientMock = mockBridgeBuilder.MockClientBuilder.Mock;
            clientMock.Setup(c => c.SendAdoptOrphanRequest(It.IsAny<Entity>()));

            try
            {
                var adoptTask = sync.AdoptAsync();

                var result = await adoptTask;
                Assert.That(result.Type, Is.EqualTo(AdoptResultType.TimeoutError));
                Assert.That(result.Warning, Is.EqualTo(Warning.ToolkitSyncAuthorityRequestRejected));
                Assert.That(result.FailureMessage, Has.Length.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sync.gameObject);
            }
        }
        private sealed class NullComponentCommandBinding : CommandBinding { }
    }
}

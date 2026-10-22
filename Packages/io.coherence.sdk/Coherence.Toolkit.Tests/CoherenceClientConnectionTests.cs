// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_EDITOR

namespace Coherence.Toolkit.Tests
{
    using Coherence.Tests;
    using Connection;
    using Entities;
    using NUnit.Framework;
    using System.Text.RegularExpressions;
    using UnityEngine;
    using UnityEngine.TestTools;

    public class CoherenceClientConnectionTests : CoherenceTest
    {
        private MockBridgeBuilder mockBridgeBuilder;
        private ICoherenceBridge bridge;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            mockBridgeBuilder = new MockBridgeBuilder();
            bridge = mockBridgeBuilder.Bridge;
        }

        [TearDown]
        public override void TearDown()
        {
            mockBridgeBuilder.Dispose();
            base.TearDown();
        }

        private CoherenceClientConnection CreateConnection(bool isMine, ClientID clientId = default,
            ConnectionType type = ConnectionType.Client, uint scene = 0, Entity? entityId = null) =>
            new(bridge, entityId ?? Entity.InvalidRelative, clientId, type, isMine, scene);

        [Test]
        [Description("ClientId is set from the constructor argument.")]
        public void Should_SetClientId_When_ConstructedWithClientId() =>
            Assert.That(CreateConnection(isMine: false, clientId: new ClientID(42)).ClientId, Is.EqualTo(new ClientID(42)));

        [Test]
        [Description("Type is set from the constructor argument.")]
        public void Should_SetType_When_ConstructedWithConnectionType() =>
            Assert.That(CreateConnection(isMine: false, type: ConnectionType.Simulator).Type, Is.EqualTo(ConnectionType.Simulator));

        [Test]
        [Description("Scene is set from the constructor argument.")]
        public void Should_SetScene_When_ConstructedWithScene() =>
            Assert.That(CreateConnection(isMine: false, scene: 5).Scene, Is.EqualTo(5u));

        [Test]
        [Description("EntityId is set from the constructor argument.")]
        public void Should_SetEntityId_When_ConstructedWithEntityId()
        {
            var entityId = new Entity((Index)1, 0, Entity.Relative);
            var connection = CreateConnection(isMine: false, entityId: entityId);

            Assert.That(connection.EntityId, Is.EqualTo(entityId));
        }

        [Test]
        [Description("IsMyConnection is true when constructed with isMine=true.")]
        public void Should_SetIsMyConnection_When_ConstructedWithIsMyConnectionTrue() =>
            Assert.That(CreateConnection(isMine: true).IsMyConnection, Is.True);

        [Test]
        [Description("IsMyConnection is false when constructed with isMine=false.")]
        public void Should_NotSetIsMyConnection_When_ConstructedWithIsMyConnectionFalse() =>
            Assert.That(CreateConnection(isMine: false).IsMyConnection, Is.False);

        [TestCase(AuthorityType.Full, true)]
        [TestCase(AuthorityType.State, true)]
        [TestCase(AuthorityType.Input, false)]
        [TestCase(AuthorityType.None, false)]
        [Description("IsMyConnection is derived from whether the NetworkEntityState's AuthorityType controls state.")]
        public void Should_SetIsMyConnection_BasedOnWhetherNetworkEntityControlsState(AuthorityType authorityType, bool expectedIsMyConnection)
        {
            var entity = new NetworkEntityState(Entity.InvalidRelative, authorityType, false, false, null, string.Empty);
            var connection = new CoherenceClientConnection(bridge, entity, Entity.InvalidRelative, new ClientID(1), ConnectionType.Client, 0);

            Assert.That(connection.IsMyConnection, Is.EqualTo(expectedIsMyConnection));
        }

        [Test]
        [Description("NetworkEntity is the instance passed to the NetworkEntityState constructor overload.")]
        public void Should_ReturnNetworkEntity_When_ConstructedWithNetworkEntity()
        {
            var entity = new NetworkEntityState(Entity.InvalidRelative, AuthorityType.Full, false, false, null, string.Empty);
            var connection = new CoherenceClientConnection(bridge, entity, Entity.InvalidRelative, new ClientID(1), ConnectionType.Client, 0);

            Assert.That(connection.NetworkEntity, Is.EqualTo(entity));
        }

        [Test]
        [Description("NetworkEntity is null when using the simple constructor that takes no NetworkEntityState.")]
        public void Should_ReturnNullNetworkEntity_When_ConstructedWithoutNetworkEntity() =>
            Assert.That(CreateConnection(isMine: false).NetworkEntity, Is.Null);

        [Test]
        [Description("GameObject is null when there is no NetworkEntityState.")]
        public void Should_ReturnNullGameObject_When_NoNetworkEntity() =>
            Assert.That(CreateConnection(isMine: false).GameObject, Is.Null);

        [Test]
        [Description("GameObject is null when the NetworkEntityState has no Sync component.")]
        public void Should_ReturnNullGameObject_When_NetworkEntityHasNoSync()
        {
            var entity = new NetworkEntityState(Entity.InvalidRelative, AuthorityType.Full, false, false, null, string.Empty);
            var connection = new CoherenceClientConnection(bridge, entity, Entity.InvalidRelative, new ClientID(1), ConnectionType.Client, 0);

            Assert.That(connection.GameObject, Is.Null);
        }

        [Test]
        [Description("CoherenceBridge returns the bridge when client connections are enabled.")]
        public void Should_ReturnBridge_When_ClientConnectionsEnabled() =>
            Assert.That(CreateConnection(isMine: false).CoherenceBridge, Is.EqualTo(bridge));

        [Test]
        [Description("Sync is null when there is no NetworkEntityState.")]
        public void Should_ReturnNullSync_When_NoNetworkEntity() =>
            Assert.That(CreateConnection(isMine: false).Sync, Is.Null);

        [Test]
        [Description("Scene can be updated via its internal setter.")]
        public void Should_UpdateScene_When_SceneSetViaInternalSetter()
        {
            var connection = CreateConnection(isMine: false, scene: 0);
            connection.Scene = 7;

            Assert.That(connection.Scene, Is.EqualTo(7u));
        }

        [Test]
        [Description("SendConnectionSceneUpdate does not update Scene and logs a warning when the connection is not owned by this client.")]
        public void Should_NotUpdateScene_When_SendingConnectionSceneUpdateForNonOwnedConnection()
        {
            var connection = CreateConnection(isMine: false, scene: 1);
            LogAssert.Expect(LogType.Warning, new Regex(".*belongs to someone else.*"));
            connection.SendConnectionSceneUpdate(99);

            Assert.That(connection.Scene, Is.EqualTo(1u));
        }

        [Test]
        [Description("SendConnectionSceneUpdate updates Scene when the connection belongs to this client.")]
        public void Should_UpdateScene_When_SendingConnectionSceneUpdateForOwnedConnection()
        {
            var connection = CreateConnection(isMine: true, scene: 1);
            connection.SendConnectionSceneUpdate(42);

            Assert.That(connection.Scene, Is.EqualTo(42u));
        }
    }
}

#endif

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using Log;
    using Coherence.Tests;
    using Connection;
    using Entities;
    using NUnit.Framework;
    using System.Linq;

    public class CoherenceClientConnectionManagerTests : CoherenceTest
    {
        private MockBridgeBuilder mockBridgeBuilder;
        private ICoherenceBridge bridge;
        private CoherenceClientConnectionManager manager;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            mockBridgeBuilder = new MockBridgeBuilder();
            bridge = mockBridgeBuilder.Bridge;
            manager = new CoherenceClientConnectionManager(bridge, logger);
        }

        [TearDown]
        public override void TearDown()
        {
            mockBridgeBuilder.Dispose();
            base.TearDown();
        }

        private CoherenceClientConnection MakeConnection(
            uint entityIndex,
            uint clientId = 0,
            ConnectionType type = ConnectionType.Client,
            bool isMine = false,
            uint scene = 0) =>
            new(bridge, new Entity((Index)entityIndex, 0, Entity.Relative), new ClientID(clientId), type, isMine,
                scene);

        // --- Add ---

        [Test]
        [Description("ClientConnectionCount increments when a connection is added.")]
        public void Should_IncrementCount_When_ConnectionAdded()
        {
            manager.Add(MakeConnection(1, clientId: 1));
            Assert.That(manager.ClientConnectionCount, Is.EqualTo(1));
        }

        [Test]
        [Description("OnCreated is fired with the added connection.")]
        public void Should_FireOnCreated_When_ConnectionAdded()
        {
            var connection = MakeConnection(1, clientId: 1);
            CoherenceClientConnection received = null;
            manager.OnCreated += c => received = c;

            manager.Add(connection);

            Assert.That(received, Is.EqualTo(connection));
        }

        [Test]
        [Description("A connection with IsMyConnection=true is available via GetMine after being added.")]
        public void Should_TrackAsMyConnection_When_IsMyConnectionTrue()
        {
            manager.Add(MakeConnection(1, clientId: 1, isMine: true));
            Assert.That(manager.GetMine(), Is.Not.Null);
        }

        [Test]
        [Description("A Simulator connection with IsMyConnection=true is available via GetMine.")]
        public void Should_TrackAsMyConnection_When_IsMineTrue_And_TypeIsSimulator()
        {
            manager.Add(MakeConnection(1, clientId: 1, isMine: true, type: ConnectionType.Simulator));
            Assert.That(manager.GetMine(), Is.Not.Null);
        }

        [Test]
        [Description("A Replicator connection with IsMyConnection=true is available via GetMine.")]
        public void Should_TrackAsMyConnection_When_IsMineTrue_And_TypeIsReplicator()
        {
            manager.Add(MakeConnection(1, clientId: 1, isMine: true, type: ConnectionType.Replicator));
            Assert.That(manager.GetMine(), Is.Not.Null);
        }

        [Test]
        [Description("A connection with IsMyConnection=false is not returned by GetMine.")]
        public void Should_NotTrackAsMyConnection_When_IsMyConnectionFalse()
        {
            manager.Add(MakeConnection(1, clientId: 1, isMine: false));
            Assert.That(manager.GetMine(), Is.Null);
        }

        [Test]
        [Description("A Simulator connection is available via GetSimulator after being added.")]
        public void Should_TrackAsSimulatorConnection_When_TypeIsSimulator()
        {
            manager.Add(MakeConnection(1, clientId: 1, type: ConnectionType.Simulator));
            Assert.That(manager.GetSimulator(), Is.Not.Null);
        }

        [Test]
        [Description("A Simulator connection is available via GetSimulator when isMine is true.")]
        public void Should_TrackAsSimulatorConnection_When_TypeIsSimulator_And_IsMineTrue()
        {
            manager.Add(MakeConnection(1, clientId: 1, type: ConnectionType.Simulator, isMine: true));
            Assert.That(manager.GetSimulator(), Is.Not.Null);
        }

        [Test]
        [Description("A non-Simulator connection is not returned by GetSimulator.")]
        public void Should_NotTrackAsSimulatorConnection_When_TypeIsClient()
        {
            manager.Add(MakeConnection(1, clientId: 1, type: ConnectionType.Client));
            Assert.That(manager.GetSimulator(), Is.Null);
        }

        [Test]
        [Description("Adding a connection with a duplicate EntityId logs an error and does not add the connection.")]
        public void Should_LogErrorAndNotAdd_When_DuplicateEntityId()
        {
            var entity = new Entity((Index)1u, 0, Entity.Relative);
            manager.Add(new CoherenceClientConnection(bridge, entity, new ClientID(1), ConnectionType.Client, false,
                0));
            manager.Add(new CoherenceClientConnection(bridge, entity, new ClientID(2), ConnectionType.Client, false,
                0));

            Assert.That(logger.GetCountForErrorID(Error.ToolkitClientConnectionManagerFailedToAddConnection), Is.EqualTo(1));
            Assert.That(manager.ClientConnectionCount, Is.EqualTo(1));
        }

        [Test]
        [Description("Adding a connection with a duplicate ClientId logs an error and does not add the connection.")]
        public void Should_LogErrorAndNotAdd_When_DuplicateClientId()
        {
            manager.Add(new CoherenceClientConnection(bridge, new Entity((Index)1u, 0, Entity.Relative),
                new ClientID(99), ConnectionType.Client, false, 0));
            manager.Add(new CoherenceClientConnection(bridge, new Entity((Index)2u, 0, Entity.Relative),
                new ClientID(99), ConnectionType.Client, false, 0));

            Assert.That(logger.GetCountForErrorID(Error.ToolkitClientConnectionManagerFailedToAddConnection), Is.EqualTo(1));
            Assert.That(manager.ClientConnectionCount, Is.EqualTo(1));
        }

        // --- Remove ---

        [Test]
        [Description("ClientConnectionCount decrements when a connection is removed.")]
        public void Should_DecrementCount_When_ConnectionRemoved()
        {
            var connection = MakeConnection(1, clientId: 1);
            manager.Add(connection);
            manager.Remove(connection.EntityId);
            Assert.That(manager.ClientConnectionCount, Is.EqualTo(0));
        }

        [Test]
        [Description("OnDestroyed is fired with the removed connection.")]
        public void Should_FireOnDestroyed_When_ConnectionRemoved()
        {
            var connection = MakeConnection(1, clientId: 1);
            CoherenceClientConnection received = null;
            manager.OnDestroyed += c => received = c;

            manager.Add(connection);
            manager.Remove(connection.EntityId);

            Assert.That(received, Is.EqualTo(connection));
        }

        [Test]
        [Description("Remove returns true when the connection exists.")]
        public void Should_ReturnTrue_When_RemovingExistingConnection()
        {
            var connection = MakeConnection(1, clientId: 1);
            manager.Add(connection);
            Assert.That(manager.Remove(connection.EntityId), Is.True);
        }

        [Test]
        [Description("Remove returns false when the entity does not exist.")]
        public void Should_ReturnFalse_When_RemovingMissingEntity() =>
            Assert.That(manager.Remove(new Entity((Index)99u, 0, Entity.Relative)), Is.False);

        [Test]
        [Description("GetMine returns null after the local connection is removed.")]
        public void Should_ClearMyConnection_When_MyConnectionRemoved()
        {
            var connection = MakeConnection(1, clientId: 1, isMine: true);
            manager.Add(connection);
            manager.Remove(connection.EntityId);
            Assert.That(manager.GetMine(), Is.Null);
        }

        [Test]
        [Description("GetSimulator returns null after the simulator connection is removed.")]
        public void Should_ClearSimulatorConnection_When_SimulatorConnectionRemoved()
        {
            var connection = MakeConnection(1, clientId: 1, type: ConnectionType.Simulator);
            manager.Add(connection);
            manager.Remove(connection.EntityId);
            Assert.That(manager.GetSimulator(), Is.Null);
        }

        // --- Get ---

        [Test]
        [Description("Get(Entity) returns the matching connection.")]
        public void Should_ReturnConnection_When_GettingByEntityId()
        {
            var connection = MakeConnection(1, clientId: 1);
            manager.Add(connection);
            Assert.That(manager.Get(connection.EntityId), Is.EqualTo(connection));
        }

        [Test]
        [Description("Get(Entity) returns null when the entity does not exist.")]
        public void Should_ReturnNull_When_GettingByMissingEntityId() =>
            Assert.That(manager.Get(new Entity((Index)99u, 0, Entity.Relative)), Is.Null);

        [Test]
        [Description("Get(ClientID) returns the matching connection.")]
        public void Should_ReturnConnection_When_GettingByClientId()
        {
            manager.Add(MakeConnection(1, clientId: 42));
            Assert.That(manager.Get(new ClientID(42)), Is.Not.Null);
        }

        [Test]
        [Description("Get(ClientID) returns null when the ClientID does not exist.")]
        public void Should_ReturnNull_When_GettingByMissingClientId() =>
            Assert.That(manager.Get(new ClientID(99)), Is.Null);

        // --- Enumeration ---

        [Test]
        [Description("GetAll returns all added connections regardless of type.")]
        public void Should_ReturnAllConnections_When_GetAllCalled()
        {
            manager.Add(MakeConnection(1, clientId: 1, type: ConnectionType.Client));
            manager.Add(MakeConnection(2, clientId: 2, type: ConnectionType.Simulator));
            Assert.That(manager.GetAll().Count(), Is.EqualTo(2));
        }

        [Test]
        [Description("GetAllClients returns only Client-type connections.")]
        public void Should_ReturnOnlyClients_When_GetAllClientsCalled()
        {
            manager.Add(MakeConnection(1, clientId: 1, type: ConnectionType.Client));
            manager.Add(MakeConnection(2, clientId: 2, type: ConnectionType.Simulator));
            var clients = manager.GetAllClients().ToList();
            Assert.That(clients, Has.Count.EqualTo(1));
            Assert.That(clients[0].Type, Is.EqualTo(ConnectionType.Client));
        }

        [Test]
        [Description("GetAllSimulators returns only Simulator-type connections.")]
        public void Should_ReturnOnlySimulators_When_GetAllSimulatorsCalled()
        {
            manager.Add(MakeConnection(1, clientId: 1, type: ConnectionType.Client));
            manager.Add(MakeConnection(2, clientId: 2, type: ConnectionType.Simulator));
            var sims = manager.GetAllSimulators().ToList();
            Assert.That(sims, Has.Count.EqualTo(1));
            Assert.That(sims[0].Type, Is.EqualTo(ConnectionType.Simulator));
        }

        [Test]
        [Description("GetOther returns all connections except the local one.")]
        public void Should_ExcludeLocalConnection_When_GetOtherCalled()
        {
            var mine = MakeConnection(1, clientId: 1, isMine: true);
            var other = MakeConnection(2, clientId: 2);
            manager.Add(mine);
            manager.Add(other);
            var others = manager.GetOther().ToList();
            Assert.That(others, Has.Count.EqualTo(1));
            Assert.That(others[0], Is.EqualTo(other));
        }

        [Test]
        [Description("GetOtherClients returns only Client connections that are not the local one.")]
        public void Should_ExcludeLocalConnectionAndSimulators_When_GetOtherClientsCalled()
        {
            var mine = MakeConnection(1, clientId: 1, isMine: true, type: ConnectionType.Client);
            var otherClient = MakeConnection(2, clientId: 2, type: ConnectionType.Client);
            var simulator = MakeConnection(3, clientId: 3, type: ConnectionType.Simulator);
            manager.Add(mine);
            manager.Add(otherClient);
            manager.Add(simulator);
            var result = manager.GetOtherClients().ToList();
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(otherClient));
        }

        [Test]
        [Description("GetOtherSimulators returns only Simulator connections that are not the local one.")]
        public void Should_ExcludeLocalConnectionAndClients_When_GetOtherSimulatorsCalled()
        {
            var mine = MakeConnection(1, clientId: 1, isMine: true, type: ConnectionType.Simulator);
            var otherSim = MakeConnection(2, clientId: 2, type: ConnectionType.Simulator);
            var client = MakeConnection(3, clientId: 3, type: ConnectionType.Client);
            manager.Add(mine);
            manager.Add(otherSim);
            manager.Add(client);
            var result = manager.GetOtherSimulators().ToList();
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(otherSim));
        }

        // --- GetMine / GetSimulator ---

        [Test]
        [Description("GetMine returns null when no local connection has been added.")]
        public void Should_ReturnNull_When_GetMineCalledWithNoConnection() =>
            Assert.That(manager.GetMine(), Is.Null);

        [Test]
        [Description(
            "GetMine returns null when client connections are disabled, even if a local connection was added.")]
        public void Should_ReturnNull_When_GetMineCalledWithClientConnectionsDisabled()
        {
            manager.Add(MakeConnection(1, clientId: 1, isMine: true));
            mockBridgeBuilder.Mock.Setup(x => x.EnableClientConnections).Returns(false);
            Assert.That(manager.GetMine(), Is.Null);
        }

        [Test]
        [Description("GetSimulator returns null when client connections are disabled, even if a simulator was added.")]
        public void Should_ReturnNull_When_GetSimulatorCalledWithClientConnectionsDisabled()
        {
            manager.Add(MakeConnection(1, clientId: 1, type: ConnectionType.Simulator));
            mockBridgeBuilder.Mock.Setup(x => x.EnableClientConnections).Returns(false);
            Assert.That(manager.GetSimulator(), Is.Null);
        }

        // --- HandleGlobalQuerySynced ---

        [Test]
        [Description("OnSynced fires when HandleGlobalQuerySynced is called and client connections are enabled.")]
        public void Should_FireOnSynced_When_HandleGlobalQuerySyncedCalledWithClientConnectionsEnabled()
        {
            CoherenceClientConnectionManager received = null;
            manager.OnSynced += m => received = m;

            manager.HandleGlobalQuerySynced();

            Assert.That(received, Is.EqualTo(manager));
        }

        [Test]
        [Description("OnSynced does not fire when client connections are disabled.")]
        public void Should_NotFireOnSynced_When_ClientConnectionsDisabled()
        {
            mockBridgeBuilder.Mock.Setup(x => x.EnableClientConnections).Returns(false);
            var synced = false;
            manager.OnSynced += _ => synced = true;

            manager.HandleGlobalQuerySynced();

            Assert.That(synced, Is.False);
        }

        // --- SendMessage ---

        [Test]
        [Description("SendMessage(Entity) returns false when the entity is not tracked.")]
        public void Should_ReturnFalse_When_SendMessageTargetsUnknownEntity() =>
            Assert.That(
                manager.SendMessage(typeof(object), "Method", new Entity((Index)99u, 0, Entity.Relative),
                    MessageTarget.All), Is.False);

        [Test]
        [Description("SendMessage(ClientID) returns false when the ClientID is not tracked.")]
        public void Should_ReturnFalse_When_SendMessageTargetsUnknownClientId() =>
            Assert.That(manager.SendMessage(typeof(object), "Method", new ClientID(99), MessageTarget.All), Is.False);
    }
}

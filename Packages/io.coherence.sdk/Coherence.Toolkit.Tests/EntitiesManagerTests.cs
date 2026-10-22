// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Log;
    using Coherence.Tests;
    using Connection;
    using Entities;
    using Moq;
    using NUnit.Framework;
    using ProtocolDef;
    using UnityEditor;
    using UnityEngine;
    using Coherence.Serializer;
    using Coherence.Common.Tests;
    using Object = UnityEngine.Object;

    public class EntitiesManagerTests : CoherenceTest
    {
        private EntitiesManager entitiesManager;
        private Mock<IClient> mockClient;
        private MockBridgeBuilder mockBridgeBuilder;
        private Mock<ICoherenceBridge> mockBridge;
        private ICoherenceBridge bridge;
        private Mock<IClientConnectionManager> mockConnectionManager;
        private CoherenceInputManager inputManager;
        private UniquenessManager uniquenessManager;
        private Action onTearDown;
        private Entity entityA = new((Entities.Index)1, 0, Entity.Relative);
        private Entity entityB = new((Entities.Index)2, 0, Entity.Relative);
        private Entity entityC = new((Entities.Index)3, 0, Entity.Relative);
        private Entity entityD = new((Entities.Index)4, 0, Entity.Relative);
        private Entity entityE = new((Entities.Index)5, 0, Entity.Relative);
        private ClientID client0 = new(1234);
        private int nextEntityIndex;

        // Using a hashset since there are several different access mechanisms and I'm
        // just going to use slow ways to process so I'm not creating parallel dicts like
        // in the actual CCM.
        private HashSet<CoherenceClientConnection> connections;

        [SetUp]
        public override void SetUp()
        {
            nextEntityIndex = 1;
            base.SetUp();

            mockBridgeBuilder = new MockBridgeBuilder().SetClientID(client0);

            bridge = mockBridgeBuilder.Build();
            mockBridge = mockBridgeBuilder.Mock;
            mockClient = mockBridgeBuilder.MockClientBuilder.Mock;

            mockConnectionManager = new(MockBehavior.Strict);

            connections = new();
            var connectionsForNextTest = connections;

            _ = mockConnectionManager.Setup(manager => manager.Add(It.IsAny<CoherenceClientConnection>()))
                .Callback((CoherenceClientConnection connection) =>
                {
                    var wasAdded = connectionsForNextTest.Add(connection);
                    Assert.That(wasAdded, Is.True, "Adding duplicate connection!");
                });
            _ = mockConnectionManager.Setup(manager => manager.Remove(It.IsAny<Entity>()))
                .Returns((Entity id) =>
                {
                    var connection = connectionsForNextTest.FirstOrDefault(conn => conn.EntityId == id);
                    _ = connectionsForNextTest.Remove(connection);

                    return connection != null;
                });
            _ = mockConnectionManager.Setup(manager => manager.TryUpdateConnectionEntity(It.IsAny<Entity>(), It.IsAny<IncomingEntityUpdate>()))
                .Returns((Entity id, IncomingEntityUpdate update) =>
                {
                    return connectionsForNextTest.Any(conn => conn.EntityId == id);
                });

            inputManager = new(bridge);
            uniquenessManager = new(logger);

            entitiesManager = new(
                bridge,
                mockConnectionManager.Object,
                inputManager,
                uniquenessManager,
                logger);
        }

        [TearDown]
        public override void TearDown()
        {
            onTearDown?.Invoke();
            mockBridgeBuilder.Dispose();
            base.TearDown();
        }

        [Test]
        [Description("Basic test for creating the entities manager.")]
        public void Test_CreateEntitiesManager() => entitiesManager.SyncAndSend(0, 0);

        [Test]
        [Description("Verify that attempting to sync an entity when the client is disconnected returns no state.")]
        public void Test_NewSyncEntityNoConnection()
        {
            // Arrange
            MockClientConnected(false);

            using var syncBuilder = new MockSyncBuilder();
            var sync = syncBuilder.Build();

            // Act
            (NetworkEntityState state, ComponentUpdates? updates, uint? LOD, bool disabled, ChannelID? creationChannelID) = entitiesManager.SyncNetworkEntityState(sync);

            // Assert
            Assert.That(state, Is.Null);
            Assert.That(updates, Is.Null);
            Assert.That(LOD, Is.Null);
            Assert.That(disabled, Is.False);
            Assert.That(creationChannelID, Is.Null);
        }

        [Test]
        [Description("Verify that syncing a network entity when connected returns the expected state and data.")]
        public void Test_NewSyncEntity()
        {
            // Arrange
            using var syncBuilder = new MockSyncBuilder();
            var sync = syncBuilder.Build();

            // Act
            (NetworkEntityState state, ComponentUpdates? updates, uint? LOD, bool disabled, ChannelID? creationChannelID) = entitiesManager.SyncNetworkEntityState(sync);

            // Assert
            Assert.That(state, Is.Not.Null);
            Assert.That(updates, Is.Null); // only not null if unsynced.
            Assert.That(LOD, Is.Null);
            Assert.That(disabled, Is.False);
            Assert.That(creationChannelID, Is.Null);
        }

        [Test]
        [Description("Verifies that a network entity can be created")]
        public void Test_CreateNetworkedEntity()
        {
            // Arrange / Act / Assert
            _ = CreateNetworkedEntity(entityA, Entity.InvalidRelative);
        }

        [Test]
        [Description("Verify that EntityCount is increased by one when a networked entity is synchronized.")]
        public void SyncNetworkEntityState_Increases_EntityCount_By_One()
        {
            MockClientConnected(true);
            using var syncBuilder = new MockSyncBuilder();
            var sync = syncBuilder.Build();
            var entityCountBefore = entitiesManager.EntityCount;

            _ = entitiesManager.SyncNetworkEntityState(sync);

            Assert.That(entitiesManager.EntityCount, Is.EqualTo(entityCountBefore + 1));
        }

        [Test]
        [Description("Verify that EntityCount becomes zero when the IClient.OnDisconnected event is raised, on the happy path.")]
        public void WhenClientDisconnects_AllEntitiesAreClearedIfNoExceptionsAreThrown()
        {
            MockClientConnected(true);
            using var syncBuilder = new MockSyncBuilder();
            var sync = syncBuilder.Build();
            _ = entitiesManager.SyncNetworkEntityState(sync);

            mockClient.OnDisconnected();

            Assert.That(entitiesManager.EntityCount, Is.Zero);
        }

        [Test]
        [Description("Verify that EntityCount becomes zero when the IClient.OnDisconnected event is raised, even if an exception is thrown during ICoherenceSync.HandleDisconnected.")]
        public void WhenClientDisconnects_AllEntitiesAreClearedIfCoherenceSyncHandleDisconnectedThrows()
        {
            MockClientConnected(true);
            using var syncBuilder = new MockSyncBuilder().SetHandleDisconnected(new Exception());
            var sync = syncBuilder.Build();
            // State authority is Full by default, which should cause ICoherenceSync.HandleDisconnected to get used instead of CoherenceSyncConfig.Instantiator.Destroy.
            _ = entitiesManager.SyncNetworkEntityState(sync);

            mockClient.OnDisconnected();

            Assert.That(entitiesManager.EntityCount, Is.Zero);
            Assert.That(logger.GetCountForErrorID(Error.ToolkitEntitiesManagerHandleDisconnected), Is.EqualTo(1));
        }

        [Test]
        [Description("Verify that UnsyncedEntityCount becomes zero when the IClient.OnDisconnected event is raised.")]
        public void WhenClientDisconnects_AllUnsyncedEntitiesAreCleared()
        {
            MockClientConnected(true);
            var queue = new Queue<UnsyncedNetworkEntity>(1);
            queue.Enqueue(new() { Updates = ComponentUpdates.New() });
            entitiesManager.unsyncedNetworkEntities[""] = queue;
            entitiesManager.unsyncedNetworkEntitiesByUniqueId[""] = new() { Updates = ComponentUpdates.New() };

            mockClient.OnDisconnected();

            Assert.That(entitiesManager.unsyncedNetworkEntities, Is.Empty);
            Assert.That(entitiesManager.unsyncedNetworkEntitiesByUniqueId, Is.Empty);
        }

        [Test]
        [Description("Verify that EntityCount becomes zero when the IClient.OnDisconnected event is raised, even if an exception is thrown during ICoherenceSync.Config.Instantiator.Destroy.")]
        public void WhenClientDisconnects_AllEntitiesAreClearedIfCoherenceSyncConfigInstantiatorDestroyThrows()
        {
            MockClientConnected(true);
            using var syncBuilder = new MockSyncBuilder().SetInstantiatorDestroy(new());
            var sync = syncBuilder.Build();
            _ = entitiesManager.SyncNetworkEntityState(sync);
            var (entity, state) = entitiesManager.networkEntities.First();
            // Remove state authority so that CoherenceSyncConfig.Instantiator.Destroy gets used instead of ICoherenceSync.HandleDisconnected.
            entitiesManager.networkEntities[entity]
                = new(state.EntityID, AuthorityType.None, state.IsOrphaned, state.NetworkInstantiated, state.Sync, state.CoherenceUUID);

            mockClient.OnDisconnected();

            Assert.That(entitiesManager.EntityCount, Is.Zero);
            Assert.That(logger.GetCountForErrorID(Error.ToolkitEntitiesManagerHandleDisconnected), Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        [Description("Verifies that a connection entity is created correctly when no prefab defined.")]
        public async Task Test_CreateClientConnectionEntity_NoPrefab(bool isMyConnection)
        {
            // Arrange
            // No prefab registered.
            _ = mockConnectionManager.Setup(manager => manager.GetPrefab(It.IsAny<ClientID>(), It.IsAny<ConnectionType>(), It.IsAny<Action<ICoherenceSync>>()))
                .Callback((ClientID id, ConnectionType connType, Action<ICoherenceSync> onLoaded) => onLoaded(null)); // Does not have a client/server prefab bound.

            // Act
            using var networkedEntityBuilder = MockNetworkedEntityBuilder.For(entityA)
                .ClientID(client0)
                .SceneIndex(0);

            if (isMyConnection)
            {
                _ = networkedEntityBuilder.SetHasStateAuthority();
            }

            _ = networkedEntityBuilder.Create(mockClient);

            // EntitiesManager is not guaranteed to register the connection immediately.
            await WaitUntilConnectionsIsNotEmpty(5d);

            // Assert - that the client entity was added to the connections but not the public entities info.
            Assert.That(entitiesManager.EntityCount, Is.EqualTo(0));
            Assert.That(entitiesManager.NetworkEntities, Is.Empty);
            Assert.That(entitiesManager.ContainsEntity(entityA), Is.True);
            Assert.That(entitiesManager.ConnectionEntityID, Is.EqualTo(isMyConnection ? entityA : Entity.InvalidRelative));

            // verify that the connection was added to the connection manager from the entities manager only once.
            mockConnectionManager.Verify(manager =>
                manager.Add(It.Is<CoherenceClientConnection>(connection => connections.Contains(connection))),
                Times.Once);
        }

        [TestCase(true)]
        [TestCase(false)]
        [Description("Verifies that a connection entity is created correctly when a prefab is defined.")]
        public async Task Test_CreateClientConnectionEntity_WithPrefab(bool isMyConnection)
        {
            // Arrange
            // There is a prefab registered.
            _ = mockConnectionManager.Setup(manager => manager.GetPrefab(It.IsAny<ClientID>(), It.IsAny<ConnectionType>(), It.IsAny<Action<ICoherenceSync>>()))
                .Callback((ClientID id, ConnectionType connType, Action<ICoherenceSync> onLoaded) =>
                {
                    var assetID = $"MOCK ASSET: {connType.ToString()}";

                    using var syncBuilder = new MockSyncBuilder().SetAssetID(assetID);
                    var sync = syncBuilder.Build();

                    onLoaded(sync);
                });

            // Act
            using var networkedEntityBuilder = MockNetworkedEntityBuilder.For(entityA)
                .ClientID(client0)
                .SceneIndex(0);

            if (isMyConnection)
            {
                _ = networkedEntityBuilder.SetHasStateAuthority();
            }

            _ = networkedEntityBuilder.Create(mockClient);

            // Assert - that the client entity was added to the connections but not the public entities info.
            Assert.That(entitiesManager.EntityCount, Is.EqualTo(0));
            Assert.That(entitiesManager.NetworkEntities, Is.Empty);
            Assert.That(entitiesManager.ContainsEntity(entityA), Is.True);
            Assert.That(entitiesManager.ConnectionEntityID, Is.EqualTo(isMyConnection ? entityA : Entity.InvalidRelative));

            // EntitiesManager is not guaranteed to register the connection immediately.
            await WaitUntilConnectionsIsNotEmpty(5d);

            // verify that the connection was added to the connection manager from the entities manager only once.
            mockConnectionManager.Verify(manager =>
                manager.Add(It.Is<CoherenceClientConnection>(connection => connections.Contains(connection))),
                Times.Once);
        }

        [Test]
        [Description("Verifies that when a network entity is created that the OnNetworkEntityCreatedInvoke is called.")]
        public void Test_OnNetworkEntityCreated_Is_Invoked()
        {
            // Act
            _ = CreateNetworkedEntity(entityA, Entity.InvalidRelative);

            // Assert - that the right entity was created.
            AssertNetworkEntityCreated(entityA);
        }

        [Test]
        [Description("Verifies that multiple entities can be created.")]
        public void Test_CreateMultipleEntities()
        {
            // Act
            var sync = CreateNetworkedEntity(entityA, Entity.InvalidRelative);
            var stateA = OnPrefabInstantiated(sync).state;

            sync = CreateNetworkedEntity(entityB, Entity.InvalidRelative);
            var stateB = OnPrefabInstantiated(sync).state;

            // Assert
            AssertNetworkEntityCreated(entityA);
            AssertNetworkEntityCreated(entityB);

            Assert.That(entitiesManager.EntityCount, Is.EqualTo(2));
            Assert.That(entitiesManager.NetworkEntities, Has.Member(stateA));
            Assert.That(entitiesManager.NetworkEntities, Has.Member(stateB));
        }

        [Test]
        [Description("Verifies that created entities are unsynced until their state is synced.")]
        public void Test_CreatedEntitySyncedCorrectly()
        {
            // Act 1
            var sync = CreateNetworkedEntity(entityA, Entity.InvalidRelative);

            // Assert 1 - the created entity is not synced
            mockBridge.Verify(bridge =>
                bridge.OnNetworkEntityCreatedInvoke(It.Is<NetworkEntityState>(state =>
                    state.Sync == null)), Times.Once);

            // Act 2 - sync it
            var info = OnPrefabInstantiated(sync);

            // Assert - correctly synced now
            Assert.That(info.state.Sync, Is.EqualTo(sync));
            Assert.False(info.disabled);
        }

        [Test]
        [Description("Verifies that the EM can get the entity state for a synced entity.")]
        public void Test_CanGetEntityStateForSyncedEntity()
        {
            // Act
            var sync = CreateNetworkedEntity(entityA, Entity.InvalidRelative);
            _ = OnPrefabInstantiated(sync);

            // Assert
            var state = entitiesManager.GetNetworkEntityStateForEntity(entityA);
            Assert.That(state, Is.Not.Null);
            Assert.That(state.Sync, Is.EqualTo(sync));
        }

        [Test]
        [Description("Verifies that the returned entity state for synced entities is not null.")]
        public void Test_GetNetworkEntityStateForEntityReturnsState()
        {
            // Act
            var sync = CreateNetworkedEntity(entityA, Entity.InvalidRelative);
            _ = OnPrefabInstantiated(sync);

            // Create a second entity to ensure we get the right one.
            _ = CreateNetworkedEntity(entityB, Entity.InvalidRelative);

            // Assert
            var state = entitiesManager.GetNetworkEntityStateForEntity(entityA);
            Assert.That(state, Is.Not.Null);
            Assert.That(state.EntityID, Is.EqualTo(entityA));
        }

        [Test]
        [Description("Verifies that the returned entity state unknown entities is null.")]
        public void Test_GetNetworkEntityStateForUnknownEntityReturnsNullState()
        {
            // Arrange

            // Act & Assert
            var state = entitiesManager.GetNetworkEntityStateForEntity(new Entity((Entities.Index)66, 0, Entity.Relative));
            Assert.That(state, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        [Description("Verifies that GetCoherenceSyncForEntity returns the correct entity if one has been mapped.")]
        public void Test_GetCoherenceSyncForEntityReturnsCorrectly(bool instantiated)
        {
            // Act
            var sync = CreateNetworkedEntity(entityA, Entity.InvalidRelative);
            if (instantiated)
            {
                _ = OnPrefabInstantiated(sync);
            }

            // Make some extra entities
            _ = CreateNetworkedEntity(entityB, Entity.InvalidRelative);
            var syncC = CreateNetworkedEntity(entityC, Entity.InvalidRelative);
            _ = OnPrefabInstantiated(syncC);

            // Assert
            var returnedSync = entitiesManager.GetCoherenceSyncForEntity(entityA);
            if (instantiated)
            {
                Assert.That(returnedSync, Is.EqualTo(sync));
            }
            else
            {
                Assert.That(returnedSync, Is.Null);
            }
        }

        [Test]
        [Description("Verify that when an entity with a parent is created but the parent isn't " +
            "that the instantiation callback is not called back.")]
        public void Test_DelayInstantiationForMissingParent()
        {
            // Arrange
            var parent = new Entity((Entities.Index)2, 0, Entity.Relative);

            // Act
            var sync = CreateNetworkedEntity(entityA, parent);

            // Assert
            // Instantiation is delayed because the parent is not there yet.
            mockBridge.Verify(bridge =>
                bridge.OnNetworkEntityCreatedInvoke(It.IsAny<NetworkEntityState>()), Times.Never);
        }

        [Test]
        [Description("Verify that a deferred child instantiation waiting for a parent happens after the " +
            "EM is updated and only one at a time.")]
        public void Test_DelayedInstantiationHappensAfterUpdateOnce()
        {
            // Arrange
            var child = entityA;
            var child2 = entityB;
            var parent = entityC;

            var childSync = CreateNetworkedEntity(child, parent);
            _ = CreateNetworkedEntity(child2, child);

            var parentSync = CreateNetworkedEntity(parent, Entity.InvalidRelative);
            _ = OnPrefabInstantiated(parentSync);

            // ensure that neither child was instantiated.
            mockBridge.Verify(bridge =>
                bridge.OnNetworkEntityCreatedInvoke(It.Is<NetworkEntityState>(state => state.EntityID == child)), Times.Never);
            mockBridge.Verify(bridge =>
                bridge.OnNetworkEntityCreatedInvoke(It.Is<NetworkEntityState>(state => state.EntityID == child2)), Times.Never);

            // Act 1 - update causes the child to be instantiated since it was waiting for parent.
            entitiesManager.Update();

            // Assert 1 - only the first child is instantiated (saves from a stack killing chain)
            AssertNetworkEntityCreated(child);

            _ = OnPrefabInstantiated(childSync);

            mockBridge.Verify(bridge =>
                bridge.OnNetworkEntityCreatedInvoke(It.Is<NetworkEntityState>(state => state.EntityID == child2)), Times.Never);

            // Act 2 - update causes the second child to be instantiated since it was waiting for the first.
            entitiesManager.Update();

            // Assert 2
            AssertNetworkEntityCreated(child2);
        }

        [Test]
        [Description("Verify that input authority gained while a child instantiation is deferred " +
            "(waiting for a parent) is reflected once the child is instantiated, rather than dropped.")]
        public void AuthorityGainedWhileInstantiationIsDeferred_IsReflectedOnceInstantiated()
        {
            // Arrange: a remote child whose parent does not exist yet, so its instantiation is
            // deferred. Its create carries no authority (the entity arrived before the transfer).
            var child = entityA;
            var parent = entityC;

            // Make the bridge reflect the EntitiesManager, so the AuthorityManager that the bridge
            // wired to the client resolves entity state the same way the real bridge does.
            mockBridge
                .Setup(b => b.GetNetworkEntityStateForEntity(It.IsAny<Entity>()))
                .Returns((Entity e) => entitiesManager.GetNetworkEntityStateForEntity(e));

            var childSync = CreateNetworkedEntity(child, parent);

            Assume.That(entitiesManager.GetNetworkEntityStateForEntity(child), Is.Null,
                "Child should be deferred (no NetworkEntityState) while its parent is missing.");

            // Act: Core grants Input authority over the child while it is still deferred (the
            // production sequence: the authority arrives as a separate delta after the create).
            mockClient.Setup(c => c.HasAuthorityOverEntity(child, AuthorityType.Input)).Returns(true);
            mockClient.Raise(c => c.OnAuthorityChange += null, new AuthorityChange(child, AuthorityType.Input));

            // The parent arrives and is instantiated, draining the deferred child.
            var parentSync = CreateNetworkedEntity(parent, Entity.InvalidRelative);
            _ = OnPrefabInstantiated(parentSync);
            entitiesManager.Update();
            _ = OnPrefabInstantiated(childSync);

            // Assert: the child instantiated and reflects the Input authority Core granted while it was deferred.
            var state = entitiesManager.GetNetworkEntityStateForEntity(child);
            Assert.That(state, Is.Not.Null, "Child should have instantiated once its parent arrived.");
            Assert.That(state.AuthorityType.Value, Is.EqualTo(AuthorityType.Input),
                $"Core granted Input authority over the entity while it was deferred " +
                $"(HasAuthorityOverEntity returns true), but the Toolkit NetworkEntityState shows " +
                $"{state.AuthorityType.Value}. Core/Toolkit authority desync.");
        }

        [Test, Description("Verify that exceptions thrown during INetworkObjectInstantiator.Instantiate are caught and logged as warnings.")]
        public void Update_Catches_Exceptions()
        {
            var childEntity = entityA;
            var parentEntity = entityC;
            CreateNetworkedEntity(childEntity, parentEntity, setupSyncBuilder: x => x.NetworkObjectInstantiatorBuilder.SetInstantiateResult(() => throw new("Test Exception")));
            CreateNetworkedEntity(parentEntity, Entity.InvalidRelative, setupSyncBuilder: x => x.NetworkObjectInstantiatorBuilder.SetInstantiateResult(() => throw new("Test Exception")));

            // This should trigger INetworkObjectInstantiator.Instantiate because the child was waiting for the parent.
            entitiesManager.Update();

            Assert.That(logger.GetCountForWarningID(Warning.ToolkitSyncInstantiatorException), Is.EqualTo(1));
        }

        [TestCase(true, TestName = "Parent First")]
        [TestCase(false, TestName = "Child First")]
        [Description("Verify that networked parent and child are both created regardless of the order " +
            "of creation from the client.")]
        public void Test_ChildEntityCreatedWithParentInAnyOrder(bool createParentFirst)
        {
            // Arrange
            var child = entityA;
            var parent = new Entity((Entities.Index)2, 0, Entity.Relative);

            // Act
            if (createParentFirst)
            {
                // Parent created and instantiated before the child is created.
                var parentSync = CreateNetworkedEntity(parent, Entity.InvalidRelative);
                _ = OnPrefabInstantiated(parentSync);

                _ = CreateNetworkedEntity(child, parent);
            }
            else
            {
                // Child created before the parent is created and instantiated.
                // expect the child create to defer the OnNetworkEntityCreated callback
                // until the parent is created and instantiated.
                _ = CreateNetworkedEntity(child, parent);

                var parentSync = CreateNetworkedEntity(parent, Entity.InvalidRelative);
                _ = OnPrefabInstantiated(parentSync);

                // need to tick the EM since the child was deferred waiting for the parent's instantiation.
                entitiesManager.Update();
            }

            // Assert
            AssertNetworkEntityCreated(child);
        }

        // https://app.zenhub.com/workspaces/engine-group-5fb3b64dabadec002057e6f2/issues/gh/coherence/unity/5289
        // A -> B -> C where a is the parent root of a chain of references. 3! permutations manually outlined
        // for clarity.
        [TestCase((object)new[] { "A", "B", "C" }, TestName = "Create order: A B C")]
        [TestCase((object)new[] { "A", "C", "B" }, TestName = "Create order: A C B")]
        [TestCase((object)new[] { "B", "A", "C" }, TestName = "Create order: B A C")]
        [TestCase((object)new[] { "B", "C", "A" }, TestName = "Create order: B C A")]
        [TestCase((object)new[] { "C", "A", "B" }, TestName = "Create order: C A B")]
        [TestCase((object)new[] { "C", "B", "A" }, TestName = "Create order: C B A")]
        [Description("Verify that a hierarchy of entities created in any order will eventually resolve itself.")]
        public void Test_HierarchyCreatedInAnyOrder(string[] createOrder)
        {
            // Arrange
            ICoherenceSync bSync = null;

            // Act
            foreach (var create in createOrder)
            {
                switch (create)
                {
                    case "A":
                        var aSync = CreateNetworkedEntity(entityA, Entity.InvalidRelative);
                        _ = OnPrefabInstantiated(aSync);

                        // need to tick the EM since the child was deferred waiting for the parent's instantiation.
                        entitiesManager.Update();
                        break;
                    case "B":
                        bSync = CreateNetworkedEntity(entityB, entityA);
                        break;
                    case "C":
                        _ = CreateNetworkedEntity(entityC, entityB);
                        break;
                }
            }

            AssertNetworkEntityCreated(entityB);
            _ = OnPrefabInstantiated(bSync);

            // need to tick the EM since the child was deferred waiting for the parent's instantiation.
            entitiesManager.Update();

            // Assert
            AssertNetworkEntityCreated(entityC);
        }

        [Test]
        [Description("Verifies that remote entity updaters call InterpolateBindings, InvokeCallbacks" +
            " and ProcessRemoteEntity in the appropriate coherence loop steps.")]
        public void Test_RemoteUpdaterMethodCallsCorrectForDifferentLoops()
        {
            // Arrange - create remote entities (no state authority)
            var updateUpdater = SetupUpdaterMock(entityA, CoherenceSync.InterpolationLoop.Update);
            var lateUpdateUpdater = SetupUpdaterMock(entityB, CoherenceSync.InterpolationLoop.LateUpdate);
            var fixedUpdateUpdater = SetupUpdaterMock(entityC, CoherenceSync.InterpolationLoop.FixedUpdate);
            var updateAndFixedUpdater = SetupUpdaterMock(entityD, CoherenceSync.InterpolationLoop.UpdateAndFixedUpdate);
            var lateUpdateAndFixedUpdateUpdater = SetupUpdaterMock(entityE, CoherenceSync.InterpolationLoop.LateUpdateAndFixedUpdate);

            // Act
            entitiesManager.InterpolateBindings(CoherenceSync.InterpolationLoop.Update);
            entitiesManager.InvokeCallbacks(CoherenceSync.InterpolationLoop.Update);

            entitiesManager.InterpolateBindings(CoherenceSync.InterpolationLoop.LateUpdate);
            entitiesManager.InvokeCallbacks(CoherenceSync.InterpolationLoop.LateUpdate);

            entitiesManager.InterpolateBindings(CoherenceSync.InterpolationLoop.FixedUpdate);
            entitiesManager.InvokeCallbacks(CoherenceSync.InterpolationLoop.FixedUpdate);

            entitiesManager.SyncAndSend(0, 0);

            // Assert that each updater is updated the correct number of times.
            VerifyRemoteUpdaterMock(updateUpdater, 1);
            VerifyRemoteUpdaterMock(lateUpdateUpdater, 1);
            VerifyRemoteUpdaterMock(fixedUpdateUpdater, 1);
            VerifyRemoteUpdaterMock(updateAndFixedUpdater, 2);
            VerifyRemoteUpdaterMock(lateUpdateAndFixedUpdateUpdater, 2);
        }

        [Test]
        [Description("Verifies that state authority entity updaters call SampleBindings" +
            " and SendState in the appropriate coherence loop steps.")]
        public void Test_StateAuthorityUpdaterMethodCallsCorrectForDifferentLoops()
        {
            // Arrange - create state authority entities
            var updateUpdater = SetupUpdaterMock(entityA, CoherenceSync.InterpolationLoop.Update, hasStateAuthority: true);
            var lateUpdateUpdater = SetupUpdaterMock(entityB, CoherenceSync.InterpolationLoop.LateUpdate, hasStateAuthority: true);
            var fixedUpdateUpdater = SetupUpdaterMock(entityC, CoherenceSync.InterpolationLoop.FixedUpdate, hasStateAuthority: true);
            var updateAndFixedUpdater = SetupUpdaterMock(entityD, CoherenceSync.InterpolationLoop.UpdateAndFixedUpdate, hasStateAuthority: true);
            var lateUpdateAndFixedUpdateUpdater = SetupUpdaterMock(entityE, CoherenceSync.InterpolationLoop.LateUpdateAndFixedUpdate, hasStateAuthority: true);

            // Act
            entitiesManager.SampleBindings(CoherenceSync.InterpolationLoop.Update);
            entitiesManager.SampleBindings(CoherenceSync.InterpolationLoop.LateUpdate);
            entitiesManager.SampleBindings(CoherenceSync.InterpolationLoop.FixedUpdate);

            entitiesManager.SyncAndSend(0, 0);

            // Assert that each updater is updated the correct number of times.
            VerifyStateAuthorityUpdaterMock(updateUpdater, 1);
            VerifyStateAuthorityUpdaterMock(lateUpdateUpdater, 1);
            VerifyStateAuthorityUpdaterMock(fixedUpdateUpdater, 1);
            VerifyStateAuthorityUpdaterMock(updateAndFixedUpdater, 2);
            VerifyStateAuthorityUpdaterMock(lateUpdateAndFixedUpdateUpdater, 2);
        }

        [Test]
        [Description("Verifies that when an entity loses state authority via OnAuthorityChanged, " +
            "it moves from the state authority loop to the remote loop.")]
        public void AuthorityChanged_LoseStateAuthority_EntityMovesToRemoteLookup()
        {
            // Arrange - create entity with state authority
            using var builder = SetupUpdaterEntity(entityA, CoherenceSync.InterpolationLoop.Update, hasStateAuthority: true);

            // Act - simulate authority change from Full to None
            builder.Mock.Raise(sync => sync.OnAuthorityChanged += null, new AuthorityChangedEventArgs
            {
                Sync = builder.Sync,
                OldAuthority = AuthorityType.Full,
                NewAuthority = AuthorityType.None,
            });

            entitiesManager.SampleBindings(CoherenceSync.InterpolationLoop.Update);
            entitiesManager.InterpolateBindings(CoherenceSync.InterpolationLoop.Update);
            entitiesManager.InvokeCallbacks(CoherenceSync.InterpolationLoop.Update);
            entitiesManager.SyncAndSend(0, 0);

            // Assert - entity should now respond as a remote entity
            VerifyRemoteUpdaterMock(builder.MockUpdater, 1);
        }

        [Test]
        [Description("Verifies that when an entity gains state authority via OnAuthorityChanged, " +
            "it moves from the remote loop to the state authority loop.")]
        public void AuthorityChanged_GainStateAuthority_EntityMovesToStateAuthorityLookup()
        {
            // Arrange - create entity without state authority (remote)
            using var builder = SetupUpdaterEntity(entityA, CoherenceSync.InterpolationLoop.Update);

            // Act - simulate authority change from None to Full
            builder.Mock.Raise(sync => sync.OnAuthorityChanged += null, new AuthorityChangedEventArgs
            {
                Sync = builder.Sync,
                OldAuthority = AuthorityType.None,
                NewAuthority = AuthorityType.Full,
            });

            entitiesManager.SampleBindings(CoherenceSync.InterpolationLoop.Update);
            entitiesManager.InterpolateBindings(CoherenceSync.InterpolationLoop.Update);
            entitiesManager.InvokeCallbacks(CoherenceSync.InterpolationLoop.Update);
            entitiesManager.SyncAndSend(0, 0);

            // Assert - entity should now respond as a state authority entity
            VerifyStateAuthorityUpdaterMock(builder.MockUpdater, 1);
        }

        [Test]
        [Description("Verifies that when an entity gains input authority via OnAuthorityChanged, " +
            "SendInput is called during SyncAndSend.")]
        public void AuthorityChanged_GainInputAuthority_SendInputIsCalled()
        {
            // Arrange - create entity with state authority only (no input authority)
            var go = new GameObject();
            onTearDown += () => Object.DestroyImmediate(go);
            var input = go.AddComponent<CoherenceInput>();

            using var builder = SetupUpdaterEntity(entityA, CoherenceSync.InterpolationLoop.Update,
                hasStateAuthority: true,
                setupSyncBuilder: builder =>
                {
                    builder.SetAuthorityType(AuthorityType.State);
                    builder.SetHasInput(true);
                });

            _ = builder.Mock.Setup(sync => sync.Input).Returns(input);

            // Verify entity is NOT initially sending input
            entitiesManager.SyncAndSend(0, 0);
            builder.MockUpdater.Verify(u => u.SendInput(), Times.Never);

            // Act - update mock to reflect new authority, then raise event
            _ = builder.Mock.Setup(sync => sync.HasInputAuthority).Returns(true);

            builder.Mock.Raise(sync => sync.OnAuthorityChanged += null, new AuthorityChangedEventArgs
            {
                Sync = builder.Sync,
                OldAuthority = AuthorityType.State,
                NewAuthority = AuthorityType.Full,
            });

            entitiesManager.SyncAndSend(0, 0);

            // Assert - SendInput should be called once (from the second SyncAndSend)
            builder.MockUpdater.Verify(u => u.SendInput(), Times.Once);
        }

        [Test]
        [Description("Verifies that when an entity loses input authority via OnAuthorityChanged, " +
            "SendInput is no longer called during SyncAndSend.")]
        public void AuthorityChanged_LoseInputAuthority_SendInputNotCalled()
        {
            // Arrange - create entity with full authority and input configured.
            // Input must be set up before OnPrefabInstantiated so that
            // EntityRequiresSendingInput returns true at entity registration time.
            var go = new GameObject();
            onTearDown += () => Object.DestroyImmediate(go);
            var input = go.AddComponent<CoherenceInput>();

            using var builder = CreateNetworkedEntityWithBuilder(entityA, Entity.InvalidRelative,
                setupSyncBuilder: builder => builder.SetHasInput(),
                hasStateAuthority: true);

            _ = builder.Mock.Setup(sync => sync.Input).Returns(input);
            _ = builder.MockUpdater.Setup(updater => updater.SendTag());
            _ = builder.MockUpdater.Setup(updater => updater.SendState(0, 0));
            _ = builder.MockUpdater.Setup(updater => updater.SendInput());
            _ = OnPrefabInstantiated(builder.Sync);

            // Verify entity is initially sending input
            entitiesManager.SyncAndSend(0, 0);
            builder.MockUpdater.Verify(u => u.SendInput(), Times.Once);

            // Act - lose input authority (Full -> State)
            builder.Mock.Raise(sync => sync.OnAuthorityChanged += null, new AuthorityChangedEventArgs
            {
                Sync = builder.Sync,
                OldAuthority = AuthorityType.Full,
                NewAuthority = AuthorityType.State,
            });

            entitiesManager.SyncAndSend(0, 0);

            // Assert - SendInput should still be 1 (not called again after losing input authority)
            builder.MockUpdater.Verify(u => u.SendInput(), Times.Once);
        }

        [Test]
        [Description("Verifies that an invalid entity ID is returned if the parameter is null.")]
        public void Test_EntityIDFromUnityTypeWorks_Nulls()
        {
            Assert.That(EntitiesManager.UnityObjectToEntityId((GameObject)null), Is.EqualTo(Entity.InvalidRelative));
            Assert.That(EntitiesManager.UnityObjectToEntityId((Transform)null), Is.EqualTo(Entity.InvalidRelative));
            Assert.That(EntitiesManager.UnityObjectToEntityId((ICoherenceSync)null), Is.EqualTo(Entity.InvalidRelative));
        }

        [Test]
        [Description("Verifies that an invalid entity ID is returned if the parameter doesn't have a CoherenceSync behaviour.")]
        public void Test_EntityIDFromUnityTypeWorks_NoCoherenceSync()
        {
            // A GameObject with no CoherenceSync.
            var go = new GameObject();
            Assert.That(EntitiesManager.UnityObjectToEntityId(go), Is.EqualTo(Entity.InvalidRelative));
            Assert.That(EntitiesManager.UnityObjectToEntityId(go.transform), Is.EqualTo(Entity.InvalidRelative)); ;
        }

        [Test]
        [Description("Verifies that an invalid entity ID is returned if the Unity object has a CoherenceSync but it is not " +
            "Syched on the network yet. This means there is no NetworkEntityState assigned.")]
        public void Test_EntityIDFromUnityTypeWorks_IsNotSynced()
        {
            // A GameObject with an unsynced CoherenceSync
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            Assert.That(EntitiesManager.UnityObjectToEntityId(go), Is.EqualTo(Entity.InvalidRelative));
            Assert.That(EntitiesManager.UnityObjectToEntityId(go.transform), Is.EqualTo(Entity.InvalidRelative));
            Assert.That(EntitiesManager.UnityObjectToEntityId((ICoherenceSync)sync), Is.EqualTo(Entity.InvalidRelative));
        }


        [Test]
        [Description("Verifies that a valid entity ID is returned if the Unity object has a CoherenceSync and is " +
            "Syched on the network. This means there is a NetworkEntityState assigned.")]
        public void Test_EntityIDFromUnityTypeWorks_IsSynced()
        {
            // A GameObject with a "synced" CoherenceSync.
            // Dislike this since it makes assumptions about the content of the sync but
            // doesn't actually set it up correctly.  Can't use a mock for this since
            // can't call AddComponent<ICoherenceSync> with an instantiated mock.
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();
            sync.EntityState = new NetworkEntityState(entityA, AuthorityType.Full, false, false, sync, string.Empty);
            Assert.That(EntitiesManager.UnityObjectToEntityId(go), Is.EqualTo(entityA));
            Assert.That(EntitiesManager.UnityObjectToEntityId(go.transform), Is.EqualTo(entityA));
            Assert.That(EntitiesManager.UnityObjectToEntityId((ICoherenceSync)sync), Is.EqualTo(entityA));
        }

        [Test]
        [Description("Verifies that a reference to an object on a child of the entity resolves to the owning entity.")]
        public void Test_EntityIDFromUnityType_OnChildGameObject_ResolvesToEntity()
        {
            var root = new GameObject("root");
            var sync = root.AddComponent<CoherenceSync>();
            sync.EntityState = new NetworkEntityState(entityA, AuthorityType.Full, false, false, sync, string.Empty);

            // Component lives on a child GameObject that has no CoherenceSync of its own.
            var child = new GameObject("child");
            child.transform.SetParent(root.transform);
            var childCollider = child.AddComponent<BoxCollider>();

            Assert.That(EntitiesManager.UnityObjectToEntityId((Component)childCollider), Is.EqualTo(entityA));
            Assert.That(EntitiesManager.UnityObjectToEntityId(child), Is.EqualTo(entityA));
            Assert.That(EntitiesManager.UnityObjectToEntityId(child.transform), Is.EqualTo(entityA));
        }

        [Test]
        [Description("Verifies that null is returned if the parameter is an invalid entity ID.")]
        public void Test_EntityIDToUnityTypesWorks_InvalidID()
        {
            Assert.That(entitiesManager.EntityIdToGameObject(Entity.InvalidRelative), Is.Null);
            Assert.That(entitiesManager.EntityIdToTransform(Entity.InvalidRelative), Is.Null);
            Assert.That(entitiesManager.EntityIdToRectTransform(Entity.InvalidRelative), Is.Null);
            Assert.That(entitiesManager.EntityIdToCoherenceSync(Entity.InvalidRelative), Is.Null);
        }

        [Test]
        [Description("Verifies that null is returned if entity parameter is not synced.")]
        public void Test_EntityIDToUnityTypesWorks_NotSynced()
        {
            // Unsynced entity
            Assert.That(entitiesManager.EntityIdToGameObject(entityA), Is.Null);
            Assert.That(entitiesManager.EntityIdToTransform(entityA), Is.Null);
            Assert.That(entitiesManager.EntityIdToRectTransform(entityA), Is.Null);
            Assert.That(entitiesManager.EntityIdToCoherenceSync(entityA), Is.Null);
        }

        [Test]
        [Description("Verifies that the proper non-null object is returned if entity parameter is synced.")]
        public void Test_EntityIDToUnityTypesWorks_Synced()
        {
            // Adding another synced object to make sure we get the right one when we
            // test the synced entity.
            var otherSync = CreateNetworkedEntity(entityA, Entity.InvalidRelative);
            _ = OnPrefabInstantiated(otherSync);

            // Synced entity
            using var builder = CreateNetworkedEntityWithBuilder(entityB, Entity.InvalidRelative);
            var go = new GameObject("test");
            var rectTransform = go.AddComponent<RectTransform>();
            _ = builder.Mock.Setup(sync => sync.gameObject).Returns(go);
            _ = builder.Mock.Setup(sync => sync.transform).Returns(go.transform);

            _ = OnPrefabInstantiated(builder.Sync);

            Assert.That(entitiesManager.EntityIdToGameObject(entityB), Is.EqualTo(go));
            Assert.That(entitiesManager.EntityIdToTransform(entityB), Is.EqualTo(go.transform));
            Assert.That(entitiesManager.EntityIdToRectTransform(entityB), Is.EqualTo(rectTransform));
            Assert.That(entitiesManager.EntityIdToCoherenceSync(entityB), Is.EqualTo(builder.Sync));
        }

        [Test]
        [Description("Verifies that after a unique entity is resolved with a remote entity," +
            "that the original entity state is removed from the mapper (even before the DuplicateDestroys comes)." +
            "This is important so the original entity state isn't in the mapper without an underlying sync instance, " +
            "which would be an illegal state.")]
        public void Test_UniqueResolved_ShouldRemoveFromMapper()
        {
            var uuid = "UNIQUE_ID";

            using var syncBuilder = new MockSyncBuilder().SetManualUUID(uuid).SetIsUnique();
            var local = syncBuilder.Build();
            _ = syncBuilder.Mock.Setup(s => s.InitializeReplacedUniqueObject(It.IsAny<SpawnInfo>())).Callback(() => entitiesManager.SyncNetworkEntityState(local));

            var assetID = $"MOCK ASSET : {entityA}";
            var config = (CoherenceSyncConfig)ScriptableObject.CreateInstance(typeof(CoherenceSyncConfig));
            config.Init(assetID);
            _ = syncBuilder.Mock.Setup(sync => sync.CoherenceSyncConfig).Returns(config);

            var (localState, _, _, _, _) = entitiesManager.SyncNetworkEntityState(local);

            _ = syncBuilder.Mock.Setup(s => s.EntityState).Returns(localState);

            _ = CreateNetworkedEntityWithBuilder(entityA, Entity.InvalidRelative, uuid: uuid);

            Assert.That(entitiesManager.GetNetworkEntityStateForEntity(localState.EntityID), Is.Null);
        }

        [Test]
        [Description("Verifies that when an entity is updated, that the connected entity changes are applied " +
            "after the component updates are applied.")]
        public void Test_OnEntityUpdated_AppliesConnectedEntityChanges()
        {
            using var builder = CreateNetworkedEntityWithBuilder(entityA, Entity.InvalidRelative);
            _ = entitiesManager.SyncNetworkEntityState(builder.Sync);

            var didUpdate = false;

            _ = builder.MockUpdater.Setup(u => u.ApplyComponentUpdates(It.IsAny<ComponentUpdates>()))
                .Callback<ComponentUpdates>(updates => didUpdate = true);
            _ = builder.MockUpdater.Setup(u => u.ApplyConnectedEntityChanges())
                .Callback(() => Assert.True(didUpdate));

            using var networkedEntityBuilder = MockNetworkedEntityBuilder.For(entityA);
            networkedEntityBuilder.Update(mockClient);

            builder.MockUpdater.Verify(u => u.ApplyComponentUpdates(It.IsAny<ComponentUpdates>()), Times.Once);
            builder.MockUpdater.Verify(u => u.ApplyConnectedEntityChanges(), Times.Once);
        }

        [TestCase(AuthorityType.None, false, false, false)]
        [TestCase(AuthorityType.None, false, false, true)]
        [TestCase(AuthorityType.None, false, true, false)]
        [TestCase(AuthorityType.None, false, true, true)]
        [TestCase(AuthorityType.None, true, false, false)]
        [TestCase(AuthorityType.None, true, false, true)]
        [TestCase(AuthorityType.None, true, true, false)]
        [TestCase(AuthorityType.None, true, true, true)]
        [TestCase(AuthorityType.Full, false, false, false)]
        [TestCase(AuthorityType.Full, false, false, true)]
        [TestCase(AuthorityType.Full, false, true, false)]
        [TestCase(AuthorityType.Full, false, true, true)]
        [TestCase(AuthorityType.Full, true, false, false)]
        [TestCase(AuthorityType.Full, true, false, true)]
        [TestCase(AuthorityType.Full, true, true, false)]
        [TestCase(AuthorityType.Full, true, true, true)]
        public void When_Client_Disconnects_Instantiator_Destroy_Is_Called_If_Sync_Is_Not_Destroyed_And_Does_Not_Control_State(AuthorityType authorityType, bool isOrphaned, bool networkInstantiated, bool isDestroyed)
        {
            MockClientConnected(true);
            var destroyCalled = false;
            using var instantiatorBuilder = new MockNetworkObjectInstantiatorBuilder()
                .SetDestroyCallback(_ => destroyCalled = true)
                .SetupSyncBuilder(x => x.SetIsDestroyed(isDestroyed));
            var (state, _, _, _, _) = entitiesManager.SyncNetworkEntityState(instantiatorBuilder.SyncBuilder.Sync);
            entitiesManager.networkEntities[state.EntityID] = new(state.EntityID, authorityType, isOrphaned, networkInstantiated, instantiatorBuilder.SyncBuilder.Sync, state.CoherenceUUID);

            mockClient.OnDisconnected();

            Assert.That(destroyCalled, Is.EqualTo(!isDestroyed && !authorityType.ControlsState()));
        }

        [Test]
        public void Test_InvokeCallbacks_Does_Not_Throw_If_Updaters_Removed_During_Iteration()
        {
            MockSyncBuilder entity1 = default;
            MockSyncBuilder entity2 = default;
            MockSyncBuilder entity3 = default;
            entity1 = InstantiateUpdatingEntity(() => StopUpdatingEntity(entity1));
            entity2 = InstantiateUpdatingEntity(() => StopUpdatingEntity(entity2));
            entity3 = InstantiateUpdatingEntity(() => StopUpdatingEntity(entity3));

            // The InvokeCallbacks method should not throw any exceptions despite the updater collection being modified during iteration.
            Assert.DoesNotThrow(() => entitiesManager.InvokeCallbacks(CoherenceSync.InterpolationLoop.Update));

            entity1.MockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.Exactly(1));
            entity2.MockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.Exactly(1));
            entity3.MockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.Exactly(1));

            entity1.Dispose();
            entity2.Dispose();
            entity3.Dispose();
        }

        [Test]
        public void Test_InvokeCallbacks_Does_Not_Throw_If_All_Updaters_Removed_During_Iteration()
        {
            var itemsIterated = 0;
            MockSyncBuilder entity1 = null;
            MockSyncBuilder entity2 = null;
            MockSyncBuilder entity3 = null;
            entity1 = InstantiateUpdatingEntity(RemoveAll);
            entity2 = InstantiateUpdatingEntity(RemoveAll);
            entity3 = InstantiateUpdatingEntity(RemoveAll);

            // The InvokeCallbacks method should not throw any exceptions despite the updater collection being modified during iteration.
            Assert.DoesNotThrow(() => entitiesManager.InvokeCallbacks(CoherenceSync.InterpolationLoop.Update));

            entity1.MockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.AtMostOnce);
            entity2.MockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.AtMostOnce);
            entity3.MockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.AtMostOnce);
            Assert.That(itemsIterated, Is.EqualTo(1));

            entity1.Dispose();
            entity2.Dispose();
            entity3.Dispose();

            void RemoveAll()
            {
                itemsIterated++;
                StopUpdatingEntity(entity1);
                StopUpdatingEntity(entity2);
                StopUpdatingEntity(entity3);
            }
        }

        [Test]
        public void Test_InvokeCallbacks_Does_Not_Throw_If_Updaters_Added_During_Iteration()
        {
            using var entity1 = InstantiateUpdatingEntity(AddUpdater);
            using var entity2 = InstantiateUpdatingEntity(AddUpdater);
            using var entity3 = InstantiateUpdatingEntity(AddUpdater);

            // The InvokeCallbacks method should not throw any exceptions despite the updater collection being modified during iteration.
            Assert.DoesNotThrow(() => entitiesManager.InvokeCallbacks(CoherenceSync.InterpolationLoop.Update));

            entity1.MockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.Exactly(1));
            entity2.MockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.Exactly(1));
            entity3.MockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.Exactly(1));

            void AddUpdater() => InstantiateUpdatingEntity(() => throw new("This should not get executed!"));
        }

        MockSyncBuilder InstantiateUpdatingEntity(Action onInvokeCallbacksExecuted)
        {
            var entity = new Entity((Entities.Index)(nextEntityIndex), 0, Entity.Relative);
            nextEntityIndex++;
            var builder = CreateNetworkedEntityWithBuilder(entity, Entity.InvalidRelative,
                setupSyncBuilder: builder => builder.SetAuthorityType(AuthorityType.None));
            _ = builder.Mock.Setup(sync => sync.InterpolationLocationConfig).Returns(CoherenceSync.InterpolationLoop.Update);
            _ = builder.MockUpdater.Setup(updater => updater.InvokeCallbacks()).Callback(onInvokeCallbacksExecuted);
            _ = OnPrefabInstantiated(builder.Sync);
            return builder;
        }

        void StopUpdatingEntity(MockSyncBuilder entity) => entitiesManager.UpdateInterpolationLoopConfig(entity.Sync, default);

        private MockSyncBuilder SetupUpdaterEntity(Entity entity, CoherenceSync.InterpolationLoop loop,
            bool hasStateAuthority = false, Action<MockSyncBuilder> setupSyncBuilder = null)
        {
            Action<MockSyncBuilder> setupSync = builder =>
            {
                if (!hasStateAuthority)
                {
                    builder.SetAuthorityType(AuthorityType.None);
                }

                setupSyncBuilder?.Invoke(builder);
            };
            var builder = CreateNetworkedEntityWithBuilder(entity, Entity.InvalidRelative,
                setupSyncBuilder: setupSync,
                hasStateAuthority: hasStateAuthority);
            _ = builder.Mock.Setup(sync => sync.InterpolationLocationConfig).Returns(loop);
            _ = builder.MockUpdater.Setup(updater => updater.SampleBindings(0));
            _ = builder.MockUpdater.Setup(updater => updater.InterpolateBindings());
            _ = builder.MockUpdater.Setup(updater => updater.InvokeCallbacks());
            _ = builder.MockUpdater.Setup(updater => updater.SendTag());
            _ = builder.MockUpdater.Setup(updater => updater.SendState(0, 0));
            _ = builder.MockUpdater.Setup(updater => updater.ProcessRemoteEntity());
            _ = builder.MockUpdater.Setup(updater => updater.SendInput());
            _ = OnPrefabInstantiated(builder.Sync);

            return builder;
        }

        private Mock<ICoherenceSyncUpdater> SetupUpdaterMock(Entity entity, CoherenceSync.InterpolationLoop loop, bool hasStateAuthority = false)
        {
            return SetupUpdaterEntity(entity, loop, hasStateAuthority).MockUpdater;
        }

        private void VerifyRemoteUpdaterMock(Mock<ICoherenceSyncUpdater> mockUpdater, int expectedTimes)
        {
            mockUpdater.Verify(updater => updater.InterpolateBindings(), Times.Exactly(expectedTimes));
            mockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.Exactly(expectedTimes));
            mockUpdater.Verify(updater => updater.ProcessRemoteEntity(), Times.Once);

            mockUpdater.Verify(updater => updater.SampleBindings(0), Times.Never);
            mockUpdater.Verify(updater => updater.SendState(0, 0), Times.Never);
        }

        private void VerifyStateAuthorityUpdaterMock(Mock<ICoherenceSyncUpdater> mockUpdater, int expectedTimes)
        {
            mockUpdater.Verify(updater => updater.SampleBindings(0), Times.Exactly(expectedTimes));
            mockUpdater.Verify(updater => updater.SendState(0, 0), Times.Once);

            mockUpdater.Verify(updater => updater.InterpolateBindings(), Times.Never);
            mockUpdater.Verify(updater => updater.InvokeCallbacks(), Times.Never);
            mockUpdater.Verify(updater => updater.ProcessRemoteEntity(), Times.Never);
        }

        private void AssertNetworkEntityCreated(Entity entity)
        {
            mockBridge.Verify(bridge =>
                bridge.OnNetworkEntityCreatedInvoke(It.Is<NetworkEntityState>(state =>
                    state.EntityID == entity
                )), Times.Once);
        }

        private struct PostNetworkSyncState
        {
            public NetworkEntityState state;
            public ComponentUpdates? updates;
            public uint? LOD;
            public bool disabled;
        }

        // Manually calling what would be called back in OnNetworkEntityCreated so that
        // the order of callbacks can be manipulated since it's possible to make the order
        // arbirtary among several entities.  This simulates the prefab being created
        // and enabled which triggers the CoherenceSync to sync the entity state.
        private PostNetworkSyncState OnPrefabInstantiated(ICoherenceSync sync)
        {
            // Can't legitimately call SyncNetworkEntityState unless
            // the prefab has been created and the OnNetworkEntityCreated callback
            // was invoked.  So ensure this has happened before attempting
            // to sync the entity state.
            AssertNetworkEntityCreated(sync.EntityState.EntityID);

            (NetworkEntityState state, ComponentUpdates? updates, uint? LOD, bool disabled, _) = entitiesManager.SyncNetworkEntityState(sync);

            return new PostNetworkSyncState()
            {
                state = state,
                updates = updates,
                LOD = LOD,
                disabled = disabled,
            };
        }

        private void MockClientConnected(bool isConnected)
        {
            _ = mockClient.Setup(client => client.IsConnected()).Returns(() => isConnected);
        }

        private ICoherenceSync CreateNetworkedEntity(Entity entityID, Entity parent, ClientID? clientID = null, Action<MockSyncBuilder> setupSyncBuilder = null)
        {
            var builder = CreateNetworkedEntityWithBuilder(entityID, parent, clientID, setupSyncBuilder: setupSyncBuilder);
            return builder.Sync;
        }

        private MockSyncBuilder CreateNetworkedEntityWithBuilder(Entity entityID, Entity parent,
            ClientID? clientID = null, string uuid = null, Action<MockSyncBuilder> setupSyncBuilder = null, bool hasStateAuthority = false)
        {
            var mockPrefab = MockPrefab.New();

            var assetID = $"MOCK ASSET : {entityID}";
            var config = (CoherenceSyncConfig)ScriptableObject.CreateInstance(typeof(CoherenceSyncConfig));
            config.Init(assetID);

            mockPrefab.sync.CoherenceSyncConfig = config;

            var networkedEntityBuilder = MockNetworkedEntityBuilder.For(entityID) // TODO: Dispose
                .Prefab(mockPrefab)
                .Parent(parent)
                .ClientID(clientID)
                .UUID(uuid)
                .SetupSyncBuilder(setupSyncBuilder);

            if (hasStateAuthority)
            {
                networkedEntityBuilder.SetHasStateAuthority();
            }

            onTearDown += networkedEntityBuilder.Dispose;

            return networkedEntityBuilder.CreateWithBuilder(mockClient);
        }

        private async Task WaitUntilConnectionsIsNotEmpty(double maxSecondsToWait)
        {
            var failAfter = EditorApplication.timeSinceStartup + maxSecondsToWait;

            while (connections.Count == 0)
            {
                await Task.Yield();

                if (EditorApplication.timeSinceStartup > failAfter)
                {
                    Assert.Fail($"Time spent waiting for {nameof(connections)} to become non-empty exceeded the maximum time limit of {maxSecondsToWait} seconds.\nconnections: {string.Join(", ", connections.Select(c => c.ClientId + "/" + c.EntityId))}");
                    return;
                }
            }
        }

        [Test]
        [Description("Verifies that when an entity is still in the mapper at DuplicateDestroy time, " +
            "the only entity that can be found in UniquenessManager for that uuid is the same entity.")]
        public void DuplicateDestroy_EntityInMapper_UniquenessManagerCanOnlyFindSameEntity()
        {
            // Arrange
            var uuid = "unique-foo";

            using var builder = CreateUniqueNetworkedEntity(entityA, uuid);
            _ = OnPrefabInstantiated(builder.Sync);

            // Act & Assert
            var stateInMapper = entitiesManager.GetNetworkEntityStateForEntity(entityA);
            Assert.That(stateInMapper, Is.Not.Null, "Entity should be in mapper");

            var uniqueObjectReplacement = uniquenessManager.TryGetUniqueObject(uuid);
            Assert.That(uniqueObjectReplacement, Is.Not.Null, "UniquenessManager should have an entry for this uuid");
            Assert.That(uniqueObjectReplacement.localObject, Is.SameAs(builder.Sync),
                "The only entity in UniquenessManager for this uuid should be the same entity that's in the mapper");
        }

        [Test]
        [Description("Verifies that if a remote entity with the same uuid arrives (via OnEntityCreated), " +
            "the local entity is removed from the mapper and so won't be found in the mapper when Destroy for the remote entity arrives.")]
        public void RemoteEntityArrives_LocalEntityRemovedFromMapper_BeforeDuplicateDestroy()
        {
            // Arrange - create local unique entity
            var uuid = "unique-foo";

            using var localResult = CreateUniqueNetworkedEntity(entityA, uuid);
            _ = OnPrefabInstantiated(localResult.Sync);

            // Verify local entity is in the mapper initially
            Assert.That(entitiesManager.GetNetworkEntityStateForEntity(entityA), Is.Not.Null,
                "Local entity should be in mapper initially");

            // Setup: when InitializeReplacedUniqueObject is called, sync the entity state
            // This simulates what happens when the local entity gets replaced by the remote one
            _ = localResult.Mock.Setup(s => s.InitializeReplacedUniqueObject(It.IsAny<SpawnInfo>()))
                .Callback(() => entitiesManager.SyncNetworkEntityState(localResult.Sync));

            // Act - simulate remote entity with same uuid arriving
            var remoteEntityUpdate = IncomingEntityUpdate.New();
            remoteEntityUpdate.Meta = new EntityWithMeta()
            {
                EntityId = entityB,
                HasMeta = true,
                HasStateAuthority = false, // Remote entity - we don't have authority
                HasInputAuthority = false,
                IsOrphan = false,
                LOD = 0,
                Operation = EntityOperation.Create,
                DestroyReason = DestroyReason.BadReason,
            };

            var remoteSpawnInfo = new SpawnInfo
            {
                assetId = $"MOCK ASSET : {entityA}", // Same asset
                uniqueId = uuid, // Same uuid - this triggers the uniqueness resolution
                connectedEntity = Entity.InvalidRelative,
            };
            MockSyncBuilder.AddInitialComponentsFromSpawnInfo(remoteEntityUpdate, remoteSpawnInfo);

            mockClient.Raise(client => client.OnEntityCreated += null, entityB, remoteEntityUpdate);

            // Assert - after remote entity arrives, local entity should be removed from mapper
            // (replaced by the remote entity's state)
            Assert.That(entitiesManager.GetNetworkEntityStateForEntity(entityA), Is.Null,
                "Local entity should be removed from mapper after remote entity with same uuid arrives");
        }

        [Test]
        [Description("Verifies that when receiving DuplicateDestroy for a unique entity that is registered " +
            "in the UniquenessManager, the entity is removed from the mapper.")]
        public void DuplicateDestroy_UniqueEntityInUniquenessManager_ShouldRemoveFromMapper()
        {
            // Arrange
            var uuid = "unique-foo";

            using var builder = CreateUniqueNetworkedEntity(entityA, uuid);
            _ = OnPrefabInstantiated(builder.Sync);

            // Verify entity is in the mapper before destroy
            Assert.That(entitiesManager.GetNetworkEntityStateForEntity(entityA), Is.Not.Null,
                "Entity should be in mapper before DuplicateDestroy");

            // Act - simulate receiving DuplicateDestroy from server
            mockClient.Raise(client => client.OnEntityDestroyed += null, entityA, DestroyReason.DuplicateDestroy);

            // Assert - entity should be removed from the mapper
            Assert.That(entitiesManager.GetNetworkEntityStateForEntity(entityA), Is.Null,
                "Entity should be removed from mapper after DuplicateDestroy");
        }

        [Test]
        [Description("Verifies that DuplicateDestroy with ReplacementStrategy.Destroy calls HandleNetworkedDestruction.")]
        public void DuplicateDestroy_WithDestroyStrategy_ShouldCallHandleNetworkedDestruction()
        {
            // Arrange
            var uuid = "unique-destroy-strategy";

            var handleNetworkedDestructionCalled = false;
            using var builder = CreateUniqueNetworkedEntity(entityA, uuid, setupMock: mock =>
            {
                _ = mock.Setup(sync => sync.ReplacementStrategy)
                    .Returns(CoherenceSync.UniqueObjectReplacementStrategy.Destroy);
                _ = mock.Setup(sync => sync.HandleNetworkedDestruction(false))
                    .Callback(() => handleNetworkedDestructionCalled = true);
            });
            _ = OnPrefabInstantiated(builder.Sync);

            // Act
            mockClient.Raise(client => client.OnEntityDestroyed += null, entityA, DestroyReason.DuplicateDestroy);

            // Assert
            Assert.That(handleNetworkedDestructionCalled, Is.True,
                "HandleNetworkedDestruction should be called for Destroy strategy");
            Assert.That(entitiesManager.GetNetworkEntityStateForEntity(entityA), Is.Null,
                "Entity should be removed from mapper");
        }

        [Test]
        [Description("Verifies that DuplicateDestroy where sync is null (already replaced) still removes from mapper.")]
        public void DuplicateDestroy_SyncAlreadyNull_ShouldRemoveFromMapper()
        {
            // Arrange
            var uuid = "unique-null-sync";

            using var builder = CreateUniqueNetworkedEntity(entityA, uuid);
            _ = OnPrefabInstantiated(builder.Sync);

            // Simulate sync being replaced/nulled before destroy arrives
            var state = entitiesManager.GetNetworkEntityStateForEntity(entityA);
            state.Sync = null;

            // Act
            mockClient.Raise(client => client.OnEntityDestroyed += null, entityA, DestroyReason.DuplicateDestroy);

            // Assert - entity should still be removed from the mapper
            Assert.That(entitiesManager.GetNetworkEntityStateForEntity(entityA), Is.Null,
                "Entity should be removed from mapper even when sync is null");
        }

        private MockSyncBuilder CreateUniqueNetworkedEntity(
            Entity entityID,
            string uuid,
            Action<Mock<ICoherenceSync>> setupMock = null)
        {
            var mockPrefab = MockPrefab.New();
            var assetID = $"MOCK ASSET : {entityID}";
            var config = (CoherenceSyncConfig)ScriptableObject.CreateInstance(typeof(CoherenceSyncConfig));
            config.Init(assetID);
            mockPrefab.sync.CoherenceSyncConfig = config;

            var spawnInfo = new SpawnInfo
            {
                assetId = assetID,
                connectedEntity = Entity.InvalidRelative,
                prefab = mockPrefab.sync,
                uniqueId = uuid
            };

            var update = IncomingEntityUpdate.New();
            update.Meta = new EntityWithMeta()
            {
                EntityId = entityID,
                HasMeta = true,
                HasStateAuthority = true,
                HasInputAuthority = false,
                IsOrphan = false,
                LOD = 0,
                Operation = EntityOperation.Create,
                DestroyReason = DestroyReason.BadReason,
            };

            var mockSyncBuilder = new MockSyncBuilder()
                .EntityID(entityID)
                .SetUpdate(update)
                .SetSpawnInfo(spawnInfo)
                .SetUUID(uuid)
                .SetManualUUID(uuid)
                .SetIsUnique(true)
                .SetIsChildFromSyncGroup(false);

            onTearDown += mockSyncBuilder.Dispose;

            // Setup default ReplacementStrategy and other required methods
            _ = mockSyncBuilder.Mock.Setup(sync => sync.ReplacementStrategy)
                .Returns(CoherenceSync.UniqueObjectReplacementStrategy.Destroy);
            _ = mockSyncBuilder.Mock.Setup(sync => sync.HandleNetworkedDestruction(It.IsAny<bool>()));

            // Allow additional mock setup
            setupMock?.Invoke(mockSyncBuilder.Mock);

            mockClient.Raise(client => client.OnEntityCreated += null, entityID, update);

            return mockSyncBuilder;
        }
    }
}

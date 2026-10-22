// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Bindings;
    using Coherence.Common;
    using Coherence.Common.Tests;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;
    using Entities;
    using Log;
    using Moq;
    using SimulationFrame;
    using UnityEngine;
    using static CoherenceSync;
    using Object = UnityEngine.Object;

    public sealed class MockSyncBuilder : IDisposable
    {
        private static readonly Entity DefaultID = new((Entities.Index)1, 0, Entity.Relative);

        private Entity entityID = DefaultID;
        private AuthorityType authorityType = AuthorityType.Full;
        private bool isOrphaned;
        private Func<bool> isDestroyed;
        private bool isNetworkInstantiated;
        private bool isUnique;
        private string uuid = string.Empty;
        private string manualUUID = string.Empty;
        private SimulationType simulationTypeConfig = SimulationType.ClientSide;
        private bool isChildFromSyncGroup;
        private CoherenceSyncBaked bakedScript;
        private InterpolationLoop interpolationLocationConfig = InterpolationLoop.Update;
        private bool hasInput;
        private string assetID = string.Empty;
        private IncomingEntityUpdate? update;
        private SpawnInfo? spawnInfo;
        private UnsyncedNetworkEntityPriority unsyncedEntityPriority = UnsyncedNetworkEntityPriority.AssetId;
        private Func<bool> adopt;
        private Func<bool> requestAuthority;
        private AuthorityType requestAuthorityType;
        private string name = "MockSync";
        private Action onHandleDisconnected;
        private Exception handleDisconnectedThrows;
        private Mock<ICoherenceSync> mock;
        private Mock<ICoherenceSyncUpdater> mockUpdater;
        private ICoherenceSyncUpdater updater;
        private MockNetworkObjectInstantiatorBuilder networkObjectInstantiatorBuilder;
        private bool buildExecuted;
        private ICoherenceSync result;
        private CoherenceSyncConfig config;
        private bool disposed;
        private List<Binding> bindings = new(0);
        private (bool didChangeParent, bool success)? connectedEntityChangedResult;
        private MockBridgeBuilder mockBridgeBuilder;
        private string coherenceTag = "";
        private ICoherenceBridge bridge;
        private CoherenceSync connectedEntity;

        public MockNetworkObjectInstantiatorBuilder NetworkObjectInstantiatorBuilder
            => networkObjectInstantiatorBuilder ??= (new MockNetworkObjectInstantiatorBuilder().SetSyncBuilder(this));

        public ICoherenceSync Sync => Build();

        public Mock<ICoherenceSync> Mock
        {
            get
            {
                if (mock is null)
                {
                    Build();
                }

                return mock;
            }
        }

        public Mock<ICoherenceSyncUpdater> MockUpdater
        {
            get
            {
                if (updater is null)
                {
                    Build();
                }

                return mockUpdater;
            }
        }

        public MockSyncBuilder Name(string name)
        {
            this.name = name;
            return this;
        }

        public MockSyncBuilder EntityID(Entity entityID)
        {
            this.entityID = entityID;
            return this;
        }

        public MockSyncBuilder SetAuthorityType(AuthorityType authorityType)
        {
            this.authorityType = authorityType;
            return this;
        }

        public MockSyncBuilder SetSimulationTypeConfig(SimulationType simulationTypeConfig)
        {
            this.simulationTypeConfig = simulationTypeConfig;
            return this;
        }

        public MockSyncBuilder SetIsOrphaned(bool isOrphaned = true)
        {
            this.isOrphaned = isOrphaned;
            return this;
        }

        public MockSyncBuilder SetIsDestroyed(bool isDestroyed) => SetIsDestroyed(() => isDestroyed);

        public MockSyncBuilder SetIsDestroyed(Func<bool> isDestroyed = null)
        {
            this.isDestroyed = isDestroyed ?? (() => true);
            return this;
        }

        public MockSyncBuilder SetHandleDisconnected(Action onHandleDisconnected)
        {
            this.onHandleDisconnected = onHandleDisconnected;
            return this;
        }

        public MockSyncBuilder SetHandleDisconnected(Exception handleDisconnectedThrows)
        {
            this.handleDisconnectedThrows = handleDisconnectedThrows;
            return this;
        }

        public MockSyncBuilder SetIsNetworkInstantiated(bool isNetworkInstantiated = true)
        {
            this.isNetworkInstantiated = isNetworkInstantiated;
            return this;
        }

        public MockSyncBuilder SetUUID(string uuid)
        {
            this.uuid = uuid;
            return this;
        }

        public MockSyncBuilder SetIsUnique(bool isUnique = true)
        {
            this.isUnique = isUnique;
            return this;
        }

        public MockSyncBuilder SetManualUUID(string manualUUID)
        {
            this.manualUUID = manualUUID;
            return this;
        }

        public MockSyncBuilder SetIsChildFromSyncGroup(bool isChildFromSyncGroup = true)
        {
            this.isChildFromSyncGroup = isChildFromSyncGroup;
            return this;
        }

        public MockSyncBuilder SetBakedScript(CoherenceSyncBaked bakedScript)
        {
            this.bakedScript = bakedScript;
            return this;
        }

        public MockSyncBuilder SetInterpolationLocationConfig(InterpolationLoop interpolationLocationConfig)
        {
            this.interpolationLocationConfig = interpolationLocationConfig;
            return this;
        }

        public MockSyncBuilder SetHasInput(bool hasInput = true)
        {
            this.hasInput = hasInput;
            return this;
        }

        public MockSyncBuilder SetAssetID(string ID)
        {
            assetID = ID;
            return this;
        }

        public MockSyncBuilder SetUpdate(IncomingEntityUpdate update)
        {
            this.update = update;
            return this;
        }

        public MockSyncBuilder SetSpawnInfo(SpawnInfo spawnInfo)
        {
            this.spawnInfo = spawnInfo;
            return this;
        }

        public MockSyncBuilder SetUnsyncedEntityPriority(UnsyncedNetworkEntityPriority unsyncedEntityPriority)
        {
            this.unsyncedEntityPriority = unsyncedEntityPriority;
            return this;
        }

        public MockSyncBuilder SetAdoptReturns(bool result) => SetAdoptReturns(() => result);

        public MockSyncBuilder SetAdoptReturns(Func<bool> adopt)
        {
            this.adopt = adopt;
            return this;
        }

        public MockSyncBuilder SetRequestAuthorityReturns(AuthorityType authorityType, bool result)
            => SetRequestAuthorityReturns(authorityType, () => result);

        public MockSyncBuilder SetRequestAuthorityReturns(AuthorityType authorityType, Func<bool> requestAuthority)
        {
            this.requestAuthority = requestAuthority;
            this.requestAuthorityType = authorityType;
            return this;
        }

        public MockSyncBuilder SetInstantiatorDestroy(Exception instantiatorDestroyThrows)
        {
            NetworkObjectInstantiatorBuilder.SetDestroyCallback(_ => throw instantiatorDestroyThrows);
            return this;
        }

        public MockSyncBuilder SetNetworkObjectInstantiatorBuilder(MockNetworkObjectInstantiatorBuilder builder)
        {
            networkObjectInstantiatorBuilder = builder;
            return this;
        }

        public MockSyncBuilder SetUpdater(ICoherenceSyncUpdater updater)
        {
            this.updater = updater;
            return this;
        }

        public MockSyncBuilder SetBindings(params Binding[] bindings)
        {
            this.bindings = bindings.ToList();
            return this;
        }

        public MockSyncBuilder SetBindings(List<Binding> bindings)
        {
            this.bindings = bindings;
            return this;
        }

        public MockSyncBuilder SetBridge(ICoherenceBridge bridge)
        {
            this.bridge = bridge;
            return this;
        }

        public MockSyncBuilder SetConnectedEntityChangedReturns(bool didChangeParent, bool success)
        {
            this.connectedEntityChangedResult = (didChangeParent, success);
            return this;
        }

        public MockSyncBuilder SetCoherenceTag(string coherenceTag)
        {
            this.coherenceTag = coherenceTag;
            return this;
        }

        public MockSyncBuilder SetConnectedEntity(CoherenceSync connectedEntity)
        {
            this.connectedEntity = connectedEntity;
            return this;
        }

        public ICoherenceSync Build()
        {
            if (buildExecuted)
            {
                return result ?? throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
            }

            buildExecuted = true;

            if (updater is null)
            {
                mockUpdater = new(MockBehavior.Strict);
                _ = mockUpdater.Setup(updater => updater.SampleAllBindings(0));
                _ = mockUpdater.Setup(updater => updater.GetComponentUpdates(It.IsAny<List<ComponentData>>(),
                    It.IsAny<double>(), It.IsAny<AbsoluteSimulationFrame>(), It.IsAny<bool>()));
                _ = mockUpdater.Setup(updater => updater.ApplyComponentDestroys(It.IsAny<ComponentDestroyMap>()));
                _ = mockUpdater.Setup(updater => updater.ApplyComponentUpdates(It.IsAny<ComponentUpdates>()));
                _ = mockUpdater.Setup(updater => updater.ApplyConnectedEntityChanges());
                updater = mockUpdater.Object;
            }

            mock = new(MockBehavior.Strict);
            config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            config.Init(spawnInfo?.assetId ?? assetID);
            config.Instantiator = NetworkObjectInstantiatorBuilder
                .Build();

            if (spawnInfo?.prefab is { } prefab && prefab.CoherenceSyncConfig)
            {
                prefab.CoherenceSyncConfig.Instantiator = NetworkObjectInstantiatorBuilder.Build();
            }

            var networkObjectProvider = new Mock<INetworkObjectProvider>(MockBehavior.Strict);
            _ = networkObjectProvider.Setup(provider => provider.LoadAsset(It.IsAny<string>(), It.IsAny<Action<ICoherenceSync>>()))
                .Callback((string _, Action<ICoherenceSync> onLoaded) => onLoaded(spawnInfo?.prefab));
            config.Provider = networkObjectProvider.Object;
            _ = CoherenceSyncConfigRegistry.Instance.Register(config);

            var entityState = new NetworkEntityState(entityID, authorityType, isOrphaned, isNetworkInstantiated, mock.Object, uuid);
            _ = mock.Setup(sync => sync.EntityState).Returns(entityState);
            _ = mock.Setup(sync => sync.HasStateAuthority).Returns(authorityType is AuthorityType.State or AuthorityType.Full);
            _ = mock.Setup(sync => sync.HasInputAuthority).Returns(authorityType is AuthorityType.Input or AuthorityType.Full);
            _ = mock.Setup(sync => sync.IsSynchronizedWithNetwork).Returns(true);
            _ = mock.Setup(sync => sync.CoherenceSyncConfig).Returns(config);
            _ = mock.Setup(sync => sync.ManualUniqueId).Returns(manualUUID);
            _ = mock.Setup(sync => sync.SimulationTypeConfig).Returns(simulationTypeConfig);
            _ = mock.Setup(sync => sync.IsUnique).Returns(isUnique);
            _ = mock.Setup(sync => sync.IsChildFromSyncGroup()).Returns(isChildFromSyncGroup);
            _ = mock.Setup(sync => sync.BakedScript).Returns(bakedScript);
            _ = mock.Setup(sync => sync.InterpolationLocationConfig).Returns(interpolationLocationConfig);
            _ = mock.Setup(sync => sync.HasInput).Returns(hasInput);
            _ = mock.Setup(sync => sync.Input).Returns((CoherenceInput)null);
            _ = mock.Setup(sync => sync.IsOrphaned).Returns(isOrphaned);
            _ = mock.Setup(sync => sync.IsDestroyed).Returns(isDestroyed ??= (() => false));
            _ = mock.Setup(sync => sync.Updater).Returns(()=> updater);
            _ = mock.Setup(sync => sync.UnsyncedEntityPriority).Returns(unsyncedEntityPriority);
            _ = mock.Setup(sync => sync.name).Returns(this.name);
            _ = mock.Setup(sync => sync.UsesLODsAtRuntime).Returns(false);
            _ = mock.Setup(sync => sync.SynchronizationChannel).Returns(Channel.Default.Name);
            _ = mock.Setup(sync => sync.coherencePosition).Returns(Vector3.zero);
            _ = mock.Setup(sync => sync.LifetimeTypeConfig).Returns(LifetimeType.SessionBased);
            _ = mock.Setup(sync => sync.PreserveChildren).Returns(false);
            _ = mock.Setup(sync => sync.IsGlobal).Returns(false);
            _ = mock.Setup(sync => sync.Bindings).Returns(bindings);
            _ = mock.Setup(sync => sync.CoherenceBridge).Returns(() => bridge ??= (mockBridgeBuilder ??= new()).Bridge);
            _ = mock.Setup(sync => sync.ApplyNodeBindings());
            _ = mock.Setup(sync => sync.RaiseOnConnectedEntityChanged());
            _ = mock.Setup(sync => sync.SendConnectedEntity(It.IsAny<double>()));
            _ = mock.Setup(sync => sync.CoherenceTag).Returns(() => coherenceTag);
            _ = mock.Setup(sync => sync.ConnectedEntity).Returns(() => connectedEntity);
            if (connectedEntityChangedResult.HasValue)
            {
                var didChangeParent = connectedEntityChangedResult.Value.didChangeParent;
                _ = mock.Setup(sync => sync.ConnectedEntityChanged(It.IsAny<Entity>(), out didChangeParent)).Returns(connectedEntityChangedResult.Value.success);
            }

            SetupHandleDisconnected();

            if (adopt is not null)
            {
                _ = mock.Setup(sync => sync.Adopt()).Returns(adopt);
            }

            if (requestAuthority is not null)
            {
                _ = mock.Setup(sync => sync.RequestAuthority(requestAuthorityType)).Returns(requestAuthority);
                _ = mock.Setup(sync => sync.RequestAuthority(requestAuthorityType, It.IsAny<Action<RequestAuthorityResult>>(), It.IsAny<CancellationToken>())).Callback(
                    (AuthorityType _, Action<RequestAuthorityResult> _, CancellationToken _) => requestAuthority());
                _ = mock.Setup(sync => sync.RequestAuthorityAsync(requestAuthorityType, It.IsAny<CancellationToken>())).Returns(() => requestAuthority() ?
                    System.Threading.Tasks.Task.FromResult(RequestAuthorityResult.Success) :
                    System.Threading.Tasks.Task.FromResult(new RequestAuthorityResult(RequestAuthorityResultType.EntityNotTransferableError, null, null)));
            }

            if (update != null && spawnInfo != null)
            {
                AddInitialComponentsFromSpawnInfo(update.Value, spawnInfo.Value);
            }

            result = mock.Object;
            return result;

            void SetupHandleDisconnected()
            {
                onHandleDisconnected ??= () => { };
                _ = mock.Setup(client => client.HandleDisconnected()).Callback(onHandleDisconnected);

                if (handleDisconnectedThrows is not null)
                {
                    _ = mock.Setup(client => client.HandleDisconnected()).Throws(handleDisconnectedThrows);
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            networkObjectInstantiatorBuilder?.Dispose();
            CoherenceSyncConfigRegistry.Instance.Deregister(config);
            if (config)
            {
                Object.DestroyImmediate(config);
            }
        }

        public static void AddInitialComponentsFromSpawnInfo(IncomingEntityUpdate update, SpawnInfo spawnInfo)
        {
            update.Components.UpdateComponent(WorldPositionComponent.Create(TestSchema.DataFactory, spawnInfo.position.ToCoreVector3(), 0));

            if (spawnInfo.rotation.HasValue)
            {
                update.Components.UpdateComponent(WorldOrientationComponent.Create(TestSchema.DataFactory, spawnInfo.rotation.Value.ToCoreQuaternion(), 0));
            }

            if (!string.IsNullOrEmpty(spawnInfo.assetId))
            {
                if (!CoherenceSyncConfigRegistry.Instance.TryGetFromAssetId(spawnInfo.assetId, out var config))
                {
                    throw new Exception($"No CoherenceSyncConfig found for AssetID '{spawnInfo.assetId}'");
                }

                update.Components.UpdateComponent(AssetIDComponent.Create(TestSchema.DataFactory, config.GetNetworkAssetId(), spawnInfo.isFromGroup));
            }

            if (spawnInfo.connectedEntity.IsValid)
            {
                update.Components.UpdateComponent(ConnectedEntityComponent.Create(TestSchema.DataFactory, spawnInfo.connectedEntity, Vector3d.zero, default, default));
            }

            if (spawnInfo.clientId.HasValue && spawnInfo.connectionType.HasValue)
            {
                update.Components.UpdateComponent(ConnectionComponent.Create(TestSchema.DataFactory, spawnInfo.clientId.Value, spawnInfo.connectionType.Value));
            }

            if (!string.IsNullOrEmpty(spawnInfo.uniqueId))
            {
                update.Components.UpdateComponent(UniqueIDComponent.Create(TestSchema.DataFactory, spawnInfo.uniqueId));
            }

            if (spawnInfo.scene.HasValue)
            {
                update.Components.UpdateComponent(ConnectionSceneComponent.Create(TestSchema.DataFactory, spawnInfo.scene.Value));
            }
        }
    }
}

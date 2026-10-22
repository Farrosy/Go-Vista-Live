// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;
    using System.Collections.Generic;
    using Coherence.Serializer;
    using Connection;
    using Core;
    using Entities;
    using Log;
    using ProtocolDef;
    using SimulationFrame;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Logger = Log.Logger;

    public interface IEntitiesManager
    {
        IEnumerable<NetworkEntityState> NetworkEntities { get; }
    }

    /// <summary>
    /// Handles network entity state, and links network entities to Unity objects.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="CoherenceSync"/> and <see cref="CoherenceBridge"/>.
    /// </remarks>
    public class EntitiesManager : IEntitiesManager
    {
        /// <summary>
        /// Set of known entities.
        /// </summary>
        /// <seealso cref="EntityCount"/>
        public IEnumerable<NetworkEntityState> NetworkEntities => networkEntities.Values;

        /// <summary>
        /// Number of known entities.
        /// </summary>
        /// <seealso cref="NetworkEntities"/>
        public int EntityCount => networkEntities.Count;

        /// <summary>
        /// The entity that represents the connection.
        /// </summary>
        public Entity ConnectionEntityID => connectionEntityID;

        /// <summary> A set of known non-toolkit entities.</summary>
        private HashSet<Entity> internalEntities = new();

        private IClient client;
        private readonly ICoherenceBridge bridge;
        private readonly IClientConnectionManager clientConnectionsManager;
        private readonly CoherenceInputManager inputManager;
        private readonly UniquenessManager uniquenessManager;

        private readonly Logger logger;

        /// <summary>A dictionary of the existing network entities and their related runtime state, identified by their Entity.</summary>
        internal readonly Dictionary<Entity, NetworkEntityState> networkEntities = new();

        /// <summary>A dictionary of the existing network entities that we haven't started synchronizing yet.</summary>
        internal readonly Dictionary<string, Queue<UnsyncedNetworkEntity>> unsyncedNetworkEntities = new();

        /// <summary>A dictionary of the existing unique network entities that we haven't started synchronizing yet.</summary>
        internal readonly Dictionary<string, UnsyncedNetworkEntity> unsyncedNetworkEntitiesByUniqueId = new();

        private readonly HashSet<Entity> ackedEntities = new(Entity.DefaultComparer);

        /// <summary>
        /// The key is for what parent entity the update is depending on.
        /// The value is for all updates that depend on a given key entity.
        /// </summary>
        private readonly Dictionary<Entity, List<IncomingEntityUpdate>> delayedUpdatesDependingOnParents = new();

        /// <summary>
        /// When an entity is meant to be instantiated after being delayed it is added here to slowly
        /// process so that there isn't a large chain suddenly processed that causes stack issues.
        /// </summary>
        private readonly Stack<Entity> delayedEntitiesToInstantiate = new();

        /// <summary>
        /// Store Entity Updates that couldn't be applied because the target Entity has not been instantiated yet.
        /// </summary>
        private readonly Dictionary<Entity, List<IncomingEntityUpdate>> delayedEntityUpdates = new();

        /// <summary>
        /// Store Entity commands that couldn't be applied because the target Entity has not been instantiated yet.
        /// </summary>
        private readonly Dictionary<Entity, List<CommandData>> delayedEntityCommands = new();

        /// <summary>A dictionary that keeps track of which point in the update loop an entity should process interpolation and callbacks.</summary>
        private readonly CoherenceLoopLookup stateAuthorityLoopLookup = new();
        private readonly CoherenceLoopLookup remoteAuthorityLoopLookup = new();
        private readonly Dictionary<Entity, ICoherenceSyncUpdater> inputEntities = new();
        private readonly HashSet<ICoherenceSyncUpdater> dirtyTagUpdaters = new();

        private Entity connectionEntityID;

        internal EntitiesManager()
        {
        }

        internal EntitiesManager(ICoherenceBridge bridge,
            IClientConnectionManager clientConnectionsManager,
            CoherenceInputManager inputManager,
            UniquenessManager uniquenessManager,
            Logger logger)
        {
            this.logger = logger.With<EntitiesManager>();

            this.bridge = bridge;
            this.clientConnectionsManager = clientConnectionsManager;
            this.inputManager = inputManager;
            this.uniquenessManager = uniquenessManager;

            SetClient(bridge.Client);
        }

        /// <summary>
        /// Gets a CoherenceSync for a given entity, from the known set of entities.
        /// </summary>
        /// <param name="id">A known existing entity.</param>
        /// <returns>The CoherenceSync associated with that entity. <see langword="null"/> if the entity is not found.</returns>
        /// <seealso cref="GetNetworkEntityStateForEntity"/>
        public ICoherenceSync GetCoherenceSyncForEntity(Entity id)
        {
            if (!TryGetNetworkEntityState(id, out var state))
            {
                logger.Trace($"We can't find a CoherenceSync for EntityID {id} in the mapper (we will try to set up any references to that entity later, if it is found.)");

                return null;
            }

            return state.Sync;
        }

        /// <summary>
        /// Gets the entity state for a given entity, from the known set of entities.
        /// </summary>
        /// <param name="id">A known existing entity.</param>
        /// <returns>The state associated with that entity. <see langword="null"/> if the entity is not found.</returns>
        /// <seealso cref="GetNetworkEntityStateForEntity"/>
        public NetworkEntityState GetNetworkEntityStateForEntity(Entity id)
        {
            if (!TryGetNetworkEntityState(id, out var state))
            {
                logger.Trace($"We can't find a CoherenceSync for EntityID {id} in the mapper (we will try to set up any references to that entity later, if it is found.)");

                return null;
            }

            return state;
        }

        internal void InterpolateBindings(CoherenceSync.InterpolationLoop interpolationLoop)
        {
            foreach (var updater in remoteAuthorityLoopLookup.Get(interpolationLoop))
            {
                updater.InterpolateBindings();
            }
        }

        internal void InvokeCallbacks(CoherenceSync.InterpolationLoop interpolationLoop)
        {
            foreach (var updater in remoteAuthorityLoopLookup.Get(interpolationLoop))
            {
                updater.InvokeCallbacks();
            }
        }

        internal void SampleBindings(CoherenceSync.InterpolationLoop interpolationLoop)
        {
            var time = bridge.NetworkTime.TimeAsDouble;
            foreach (var updater in stateAuthorityLoopLookup.Get(interpolationLoop))
            {
                updater.SampleBindings(time);
            }
        }

        internal void SyncAndSend(double time, AbsoluteSimulationFrame simulationFrame)
        {
            foreach (var updater in dirtyTagUpdaters)
            {
                updater.SendTag();
            }

            dirtyTagUpdaters.Clear();

            foreach (var updater in stateAuthorityLoopLookup.GetAllUpdaters())
            {
                updater.SendState(time, simulationFrame);
            }

            foreach (var updater in remoteAuthorityLoopLookup.GetAllUpdaters())
            {
                updater.ProcessRemoteEntity();
            }

            foreach (var updater in inputEntities.Values)
            {
                updater.SendInput();
            }
        }

        /// <inheritdoc cref="UnityObjectToEntityId(CoherenceSync)"/>
        public static Entity UnityObjectToEntityId(GameObject from) => UnityObjectToEntityId(GetOwningCoherenceSync(from));

        /// <inheritdoc cref="UnityObjectToEntityId(CoherenceSync)"/>
        public static Entity UnityObjectToEntityId(Component from) => UnityObjectToEntityId(GetOwningCoherenceSync(from));

        /// <summary>
        /// Gets the entity associated with a <see cref="CoherenceSync"/>.
        /// </summary>
        public Entity UnityObjectToEntityId(Transform from) => UnityObjectToEntityId(GetOwningCoherenceSync(from));

        /// <summary>
        /// Gets the entity associated with a <see cref="CoherenceSync"/>.
        /// </summary>
        public static Entity UnityObjectToEntityId(ICoherenceSync from) => from?.EntityState?.EntityID ?? Entity.InvalidRelative;

        internal static ICoherenceSync GetOwningCoherenceSync(GameObject from) => from ? from.GetComponentInParent<ICoherenceSync>(true) : null;

        internal static ICoherenceSync GetOwningCoherenceSync(Component from) => from ? from.GetComponentInParent<ICoherenceSync>(true) : null;

        /// <summary>
        /// Get the <see cref="GameObject"/> associated with an <see cref="Entity"/>.
        /// </summary>
        public GameObject EntityIdToGameObject(Entity from)
        {
            if (from == Entity.InvalidRelative)
            {
                return null;
            }

            var toSync = GetCoherenceSyncForEntity(from);

            return toSync?.gameObject;
        }

        /// <summary>
        /// Get the <see cref="Transform"/> associated with an <see cref="Entity"/>.
        /// </summary>
        public Transform EntityIdToTransform(Entity from)
        {
            if (from == Entity.InvalidRelative)
            {
                return null;
            }

            var toSync = GetCoherenceSyncForEntity(from);

            return toSync?.transform;
        }

        /// <summary>
        /// Get the <see cref="RectTransform"/> associated with an <see cref="Entity"/>.
        /// </summary>
        public RectTransform EntityIdToRectTransform(Entity from)
        {
            if (from == Entity.InvalidRelative)
            {
                return null;
            }

            var toSync = GetCoherenceSyncForEntity(from);

            return toSync?.transform as RectTransform;
        }

        /// <summary>
        /// Get the <see cref="ICoherenceSync"/> associated with an <see cref="Entity"/>.
        /// </summary>
        public ICoherenceSync EntityIdToCoherenceSync(Entity from)
        {
            if (from == Entity.InvalidRelative)
            {
                return null;
            }

            return GetCoherenceSyncForEntity(from);
        }

        /// <summary>
        /// Set the active Unity scene based on the associated <see cref="CoherenceBridge"/>.
        /// </summary>
        /// <remarks>
        /// Scene is set via <see cref="SceneManager.SetActiveScene"/>.
        /// If <see cref="CoherenceBridge.InstantiationScene"/> is set and is loaded, that scene will be set as active.
        /// Otherwise, the active scene won't be changed.
        /// </remarks>
        public Scene? SetActiveScene()
        {
            Scene? lastActiveScene = null;

            var targetScene = bridge.InstantiationScene;

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene != targetScene && targetScene is { isLoaded: true })
            {
                lastActiveScene = activeScene;
                SceneManager.SetActiveScene(targetScene.Value);
            }

            return lastActiveScene;
        }

        internal bool TryGetNetworkEntityState(Entity id, out NetworkEntityState networkEntityState)
        {
            return networkEntities.TryGetValue(id, out networkEntityState);
        }

        /// <summary>
        /// Enumerates the known network entities.
        /// </summary>
        /// <seealso cref="networkEntities"/>
        public Dictionary<Entity, NetworkEntityState>.Enumerator GetEnumerator()
        {
            return networkEntities.GetEnumerator();
        }

        internal virtual (NetworkEntityState State, ComponentUpdates? Data, uint? Lod, bool IsServerObjectOnClient, ChannelID? creationChannel) SyncNetworkEntityState(ICoherenceSync sync)
        {
            if (!client.IsConnected())
            {
                return (null, null, null, false, null);
            }

            var assetId = sync.CoherenceSyncConfig?.ID ?? string.Empty;
            var uniqueId = sync.ManualUniqueId;
            if (TryClaimUnsyncedNetworkEntity(assetId, uniqueId, out var unsyncedNetworkEntity))
            {
                if (uniquenessManager.RegisterUniqueCoherenceSyncAndDestroyIfDuplicate(sync, unsyncedNetworkEntity.EntityState.CoherenceUUID))
                {
                    unsyncedNetworkEntity.Dispose();

                    return (null, null, null, false, null);
                }

                // This happens when a unique local sync is resolved to
                // actually be remote. The sync now needs to switch state from locally created
                // to remotely created. And the old state needs to be removed from the mapper,
                // even though the duplicate destroy will do this as well, but if we don't
                // do it now, the mapper will be in an inconsistent state for a moment.
                if (sync.EntityState != null)
                {
                    sync.EntityState.Sync = null;
                    RemoveEntityFromMapper(sync.EntityState);
                }

                unsyncedNetworkEntity.EntityState.Sync = sync;
                AddNetworkEntityStateToMapper(unsyncedNetworkEntity.EntityState, sync);

                InstantiateDelayedEntitiesDependingOnThisParent(unsyncedNetworkEntity.EntityState.EntityID);

                if (IsRemoteServerObjectOnHostClient(sync, unsyncedNetworkEntity.EntityState))
                {
                    unsyncedNetworkEntity.Dispose();
                    return (unsyncedNetworkEntity.EntityState, null, unsyncedNetworkEntity.LOD, true, null);
                }

                return (unsyncedNetworkEntity.EntityState, unsyncedNetworkEntity.Updates.ReleaseOwnershipOfComponents(), unsyncedNetworkEntity.LOD, false, unsyncedNetworkEntity.CreationChannelID);
            }

            if (DisableServerObjectIfClient(sync))
            {
                return (null, null, null, true, null);
            }

            var uuid = string.Empty;

            if (sync.IsUnique)
            {
                uuid = sync.ManualUniqueId;

                if (uniquenessManager.RegisterUniqueCoherenceSyncAndDestroyIfDuplicate(sync, uuid))
                {
                    return (null, null, null, false, null);
                }
            }

            var entityState = CreateEntity(sync, uuid);
            if (entityState == null)
            {
                return (null, null, null, false, null);
            }

            AddNetworkEntityStateToMapper(entityState, sync);

            return (entityState, null, null, false, null);
        }

        internal void ApplyDelayedUpdates(Entity id)
        {
            if (delayedEntityUpdates.TryGetValue(id, out var updates))
            {
                foreach (var update in updates)
                {
                    UpdateNetworkedEntity(id, update.ReleaseOwnershipOfComponents());
                }
            }

            delayedEntityUpdates.Remove(id);
        }

        internal bool AddDelayedCommand(CommandData command, Entity id)
        {
            command.TakeOwnership("EntitiesManager.AddDelayedCommand");

            if (networkEntities.ContainsKey(id) || internalEntities.Contains(id))
            {
                return false;
            }

            logger.Debug("Received command for an unknown Toolkit entity.\n" +
                "Storing the Update to be applied when the Entity is instantiated.",
                ("EntityID", id), ("Type", command.CommandID));

            if (!delayedEntityCommands.TryGetValue(id, out var commands))
            {
                commands = new List<CommandData>();
                delayedEntityCommands.Add(id, commands);
            }

            commands.Add(command);

            return true;
        }

        internal void ApplyDelayedCommands(Entity id)
        {
            if (!TryGetNetworkEntityState(id, out var state))
            {
                logger.Error(Error.ToolkitEntitiesManagerDelayedCommands, ("id", id));
                return;
            }

            if (delayedEntityCommands.TryGetValue(id, out var commands))
            {
                var sync = state.Sync;

                foreach (var command in commands)
                {
                    sync.ReceiveCommand(command.ReleaseOwnership());
                }
            }

            delayedEntityCommands.Remove(id);
        }

        private bool DisableServerObjectIfClient(ICoherenceSync sync)
        {
            if (sync.SimulationTypeConfig != CoherenceSync.SimulationType.ServerSide)
            {
                return false;
            }

            return !bridge.IsSimulatorOrHost;
        }

        private bool IsRemoteServerObjectOnHostClient(ICoherenceSync sync, NetworkEntityState entityState) =>
            sync.SimulationTypeConfig == CoherenceSync.SimulationType.ServerSide
            && bridge.ConnectionType != ConnectionType.Simulator
            && bridge.IsSimulatorOrHost
            && !entityState.HasStateAuthority;

        internal void DestroyAuthorityNetworkEntityState(NetworkEntityState state)
        {
            if (state == null)
            {
                return;
            }

            RemoveEntityFromMapper(state);

            if (state.HasStateAuthority)
            {
                client.DestroyEntity(state.EntityID);
            }
        }

        internal void UpdateInterpolationLoopConfig(ICoherenceSync sync, CoherenceSync.InterpolationLoop newLocation)
        {
            var lookup = GetLoopLookup(sync.HasStateAuthority);
            lookup.RemoveCompletely(sync.EntityState.EntityID);
            lookup.Add(sync.EntityState.EntityID, sync.Updater, newLocation);
        }

        private CoherenceLoopLookup GetLoopLookup(bool hasStateAuthority) =>
            hasStateAuthority
                ? stateAuthorityLoopLookup
                : remoteAuthorityLoopLookup;

        internal bool ContainsEntity(Entity id)
        {
            return networkEntities.ContainsKey(id) || internalEntities.Contains(id);
        }

        internal void Update()
        {
            if (delayedEntitiesToInstantiate.TryPop(out var parentEntity))
            {
                var updatesDependingOnThisEntity = delayedUpdatesDependingOnParents[parentEntity];

                _ = delayedUpdatesDependingOnParents.Remove(parentEntity);

                foreach (var delayedUpdate in updatesDependingOnThisEntity)
                {
                    CreateNetworkedEntity(delayedUpdate.Meta.EntityId, delayedUpdate.ReleaseOwnershipOfComponents(), out _);
                }
            }
        }

        private void SetClient(IClient client)
        {
            this.client = client;
            client.OnEntityCreated += (id, update) => CreateNetworkedEntity(id, update, out _);
            client.OnEntityUpdated += UpdateNetworkedEntity;
            client.OnEntityDestroyed += DestroyNetworkedEntity;
            client.OnDisconnected += HandleDisconnected;
            client.OnEntityAcked += HandleEntityAcked;
        }

        internal bool IsEntityAcked(Entity entity) => ackedEntities.Contains(entity);

        private void HandleEntityAcked(Entity entity) => ackedEntities.Add(entity);

        private bool TryClaimUnsyncedNetworkEntity(string assetId, string uniqueId, out UnsyncedNetworkEntity unsyncedNetworkEntity)
        {
            if (!string.IsNullOrEmpty(uniqueId) && unsyncedNetworkEntitiesByUniqueId.Remove(uniqueId, out unsyncedNetworkEntity))
            {
                return true;
            }

            if (unsyncedNetworkEntities.TryGetValue(assetId, out var queue) && queue.Count > 0)
            {
                unsyncedNetworkEntity = queue.Dequeue();
                return true;
            }

            unsyncedNetworkEntity = default;
            return false;
        }

        private NetworkEntityState CreateEntity(ICoherenceSync sync, string uuid)
        {
            var isNestedPrefab = sync.IsChildFromSyncGroup() && !string.IsNullOrEmpty(uuid);
            var components = CreateInitialComponents(sync, uuid, isNestedPrefab);

            if (sync.BakedScript != null && sync.UsesLODsAtRuntime)
            {
                if (client.SchemaDefinition.TryGetEntityArchetypeIDByName(sync.ArchetypeName, out var archetypeID))
                {
                    components.Add(ArchetypeComponent.Create(client.SchemaDataFactory, (int)archetypeID));
                }
                else
                {
                    logger.Warning(Warning.ToolkitBakedSyncCreateEntityMissingArchetype,
                        $"Unable to find archetype {sync.ArchetypeName} in dictionary. Please, bake manually (coherence > Bake)");
                }
            }

            // Not sampling here because this causes multiple samples in the buffer
            // and the replicated clients could then use an interpolated value
            // for the initial value.  Leaving this comment here since the effects of
            // this interpolation are hard to detect and make the test
            // Sync_InitialStateWhenSpawnedViaCommand flaky.
            // This change presumes the initial sample sent with the create is handled
            // in the LateUpdate sample which should always happen for new entities.

            if (!Channel.TryGetByName(sync.SynchronizationChannel, out var channel))
            {
                logger.Error(Error.ToolkitEntitiesManagerUnknownChannel,
                    $"Failed to create entity `{sync.name}` because its synchronization channel is set to " +
                    $"unknown channel `{sync.SynchronizationChannel}`. The entity will not be synchronized.");

                return null;
            }

            var serializeEntity = client.CreateEntity(components.ToArray(), false, channel.ID);

            logger.Trace($"Created new entity locally for asset {sync.CoherenceSyncConfig.ID} and uuid {uuid}");

            return new NetworkEntityState(serializeEntity, AuthorityType.Full, false, false, sync, uuid);
        }

        private List<ComponentData> CreateInitialComponents(ICoherenceSync sync, string uuid, bool isFromGroup)
        {
            var components = new List<ComponentData>
            {
                WorldPositionComponent.Create(client.SchemaDataFactory,
                    sync.coherencePosition.ToCoreVector3(), bridge.NetworkTime.ClientSimulationFrame),
                AssetIDComponent.Create(client.SchemaDataFactory,
                    sync.CoherenceSyncConfig.GetNetworkAssetId(), isFromGroup)
            };

            if (!string.IsNullOrEmpty(uuid))
            {
                components.Add(UniqueIDComponent.Create(client.SchemaDataFactory, uuid));
            }

            if (sync.LifetimeTypeConfig != CoherenceSync.LifetimeType.SessionBased)
            {
                components.Add(PersistenceComponent.Create(client.SchemaDataFactory));
            }

            if (sync.PreserveChildren)
            {
                components.Add(PreserveChildrenComponent.Create(client.SchemaDataFactory));
            }

            if (sync.IsGlobal)
            {
                components.Add(GlobalComponent.Create(client.SchemaDataFactory));
            }

            return components;
        }

        private void HandleDisconnected(ConnectionCloseReason obj)
        {
            var entitiesToBeDestroyed = new List<NetworkEntityState>(networkEntities.Values);

            foreach (var entity in entitiesToBeDestroyed)
            {
                try
                {
                    if (entity.Sync is not { } sync)
                    {
                        continue;
                    }

                    if (entity.AuthorityType.Value.ControlsState())
                    {
                        sync.HandleDisconnected();
                    }
                    else if (!sync.IsDestroyed)
                    {
                        sync.CoherenceSyncConfig.Instantiator.Destroy(sync);
                    }
                }
                catch (Exception e)
                {
                    logger.Error(Error.ToolkitEntitiesManagerHandleDisconnected,
                        $"An exception occurred in {nameof(HandleDisconnected)} " +
                        $"with entity {entity.Sync} ({entity.CoherenceUUID})" +
                        $"with {nameof(AuthorityType)} {entity.AuthorityType.Value}.\n{e}");
                }
            }

            networkEntities.Clear();

            // Reset delayed updates so they are not leaked
            foreach (var (_, updates) in delayedEntityUpdates)
            {
                foreach (var update in updates)
                {
                    update.Dispose();
                }
            }
            delayedEntityUpdates.Clear();

            // Dispose of delayed commands so they are not leaked
            foreach (var (_, commands) in delayedEntityCommands)
            {
                foreach (var command in commands)
                {
                    command.Dispose();
                }
            }
            delayedEntityCommands.Clear();
            stateAuthorityLoopLookup.Clear();
            remoteAuthorityLoopLookup.Clear();
            inputEntities.Clear();
            dirtyTagUpdaters.Clear();
            internalEntities.Clear();
            ackedEntities.Clear();

            // Reset unsynced entities so their updates are not leaked
            foreach (var (_, queue) in unsyncedNetworkEntities)
            {
                while (queue.Count > 0)
                {
                    var unsyncedEntity = queue.Dequeue();
                    unsyncedEntity.Updates.Dispose();
                }
            }
            unsyncedNetworkEntities.Clear();

            foreach (var (_, unsyncedEntity) in unsyncedNetworkEntitiesByUniqueId)
            {
                unsyncedEntity.Updates.Dispose();
            }
            unsyncedNetworkEntitiesByUniqueId.Clear();

            connectionEntityID = Entity.InvalidRelative;
        }

        private void CreateNetworkedEntity(Entity entityID, IncomingEntityUpdate entityUpdate, out bool shouldSpawn)
        {
            entityUpdate.TakeOwnershipOfComponents("EntitiesManager.CreateNetworkedEntity");

            var (spawn, info) = GetSpawnInfo(entityUpdate);
            info.entityId = entityID;
            shouldSpawn = spawn;

            if (!shouldSpawn)
            {
                internalEntities.Add(entityID);
                logger.Debug("Create for non-toolkit entity",
                    ("EntityID", entityID),
                    ("Components", entityUpdate.Components));
                entityUpdate.Dispose();
                return;
            }

            bool isConnectionEntity = info.clientId.HasValue && info.connectionType.HasValue && info.scene.HasValue;
            if (isConnectionEntity)
            {
                internalEntities.Add(info.entityId);

                var isLocalClient = entityUpdate.Meta.HasStateAuthority;
                if (isLocalClient)
                {
                    connectionEntityID = info.entityId;
                }

                if (!bridge.EnableClientConnections)
                {
                    entityUpdate.Dispose();
                    return;
                }

                clientConnectionsManager.GetPrefab(bridge.ClientID, info.connectionType.Value, (sync) =>
                {
                    if (sync != null)
                    {
                        info.assetId = sync.CoherenceSyncConfig.ID;
                        InstantiateNetworkedEntity(sync, info, entityUpdate.ReleaseOwnershipOfComponents());
                    }
                    else
                    {
                        var connection = new CoherenceClientConnection(
                            bridge,
                            info.entityId,
                            info.clientId.Value,
                            info.connectionType.Value,
                            entityUpdate.Meta.HasStateAuthority,
                            info.scene.Value);

                        clientConnectionsManager.Add(connection);
                        entityUpdate.Dispose();
                    }
                });
            }
            else
            {
                var registry = CoherenceSyncConfigRegistry.Instance;
                if (registry.TryGetFromAssetId(info.assetId, out var config))
                {
                    config.Provider.LoadAsset(info.assetId,
                        sync => InstantiateNetworkedEntity(sync, info, entityUpdate.ReleaseOwnershipOfComponents()));
                }
            }
        }

        private (bool shouldSpawn, SpawnInfo info) GetSpawnInfo(IncomingEntityUpdate entityUpdate)
        {
            var info = new SpawnInfo();
            var gotPosition = false;
            var gotUnityAsset = false;

            var comps = entityUpdate.Components.Updates.Store;

            if (comps.TryGetValue(WorldPositionComponent.ComponentID, out var worldPos))
            {
                var worldPositionComponent = WorldPositionComponent.From(worldPos);
                info.position = worldPositionComponent.Value.ToUnityVector3();
                gotPosition = true;
            }

            if (comps.TryGetValue(WorldOrientationComponent.ComponentID, out var worldRot))
            {
                var worldOrientationComponent = WorldOrientationComponent.From(worldRot);
                info.rotation = worldOrientationComponent.Value.ToUnityQuaternion();
            }

            if (comps.TryGetValue(AssetIDComponent.ComponentID, out var assetID))
            {
                var assetIDComponent = AssetIDComponent.From(assetID);

                if (CoherenceSyncConfigRegistry.Instance.GetFromNetworkId(assetIDComponent.Value, out var config))
                {
                    info.assetId = config.ID; // actual asset ID
                    info.isFromGroup = assetIDComponent.IsFromGroup;
                    gotUnityAsset = true;
                }
                else
                {
                    logger.Error(Error.ToolkitSpawnInfoMissingConfigSync,
                        $"Can't find Sync Config for Network Asset ID: {CoherenceSyncConfig.NetworkAssetIdToString(assetIDComponent.Value)}. " +
                        "Using the Network Prefabs window please verify that prefab with this ID is registered. " +
                        "This might indicate outdated client.");
                }
            }

            if (comps.TryGetValue(ConnectedEntityComponent.ComponentID, out var connectedEntity))
            {
                var connectedEntityComponent = ConnectedEntityComponent.From(connectedEntity);
                info.connectedEntity = connectedEntityComponent.Parent;
            }

            if (comps.TryGetValue(ConnectionComponent.ComponentID, out var connection))
            {
                var connectionComponent = ConnectionComponent.From(connection);
                info.clientId = connectionComponent.ID;
                info.connectionType = connectionComponent.Type;
            }

            if (comps.TryGetValue(UniqueIDComponent.ComponentID, out var uniqueID))
            {
                var uniqueIDComponent = UniqueIDComponent.From(uniqueID);
                info.uniqueId = uniqueIDComponent.UUID;
            }

            if (comps.TryGetValue(ConnectionSceneComponent.ComponentID, out var connectionScene))
            {
                var connectionSceneComponent = ConnectionSceneComponent.From(connectionScene);
                info.scene = connectionSceneComponent.Value;
            }

            var shouldSpawn = (gotPosition && gotUnityAsset) || info.clientId.HasValue;

            return (shouldSpawn, info);
        }

        private void UpdateNetworkedEntity(Entity id, IncomingEntityUpdate entityUpdate)
        {
            entityUpdate.TakeOwnershipOfComponents("EntitiesManager.UpdateNetworkedEntity");

            _ = clientConnectionsManager.TryUpdateConnectionEntity(id, entityUpdate);

            if (TryGetNetworkEntityState(id, out var state))
            {
                var sync = state.Sync;

                if (sync.Updater == null)
                {
                    // The entity is in the middle of destruction (this can happen when a parent
                    // of a PrefabSyncGroup is destroyed, which triggers a premature local destruction
                    // of children, without clearing the network entity state).
                    entityUpdate.Dispose();
                    return;
                }

                state.IsOrphaned = entityUpdate.Meta.IsOrphan;
                sync.Updater.ApplyComponentDestroys(entityUpdate.Components.Destroys.ReleaseOwnership());

                var updates = entityUpdate.Components.Updates;
                RemoveUnhandledComponents(updates);
                entityUpdate.Components.Updates = updates;  //replace with updated updates.
                sync.Updater.ApplyComponentUpdates(updates.ReleaseOwnershipOfComponents());

                sync.Updater.ApplyConnectedEntityChanges();

                if (sync.UsesLODsAtRuntime)
                {
                    sync.SetObservedLodLevel((int)entityUpdate.Meta.LOD);
                }
            }
            else if (!internalEntities.Contains(id))
            {
                logger.Debug("Received update for an unknown Toolkit entity. Storing the Update to be applied when the Entity is instantiated.", ("EntityID", id), ("Comps", entityUpdate));

                if (!delayedEntityUpdates.TryGetValue(id, out var updates))
                {
                    updates = new List<IncomingEntityUpdate>(1);
                    delayedEntityUpdates.Add(id, updates);
                }

                updates.Add(entityUpdate);
            }
            else
            {
                // Make sure to reset the update to avoid leaking components
                entityUpdate.Dispose();
            }
        }

        private void DestroyNetworkedEntity(Entity entityID, DestroyReason destroyReason)
        {
            if (entityID == Entity.InvalidRelative)
            {
                logger.Warning(Warning.ToolkitEntitiesManagerRemoteEntityInvalid,
                    $"We can't delete an entity with ID 'default', this call to '{nameof(DestroyNetworkedEntity)}' will be ignored.",
                    ("context", this));
                return;
            }

            clientConnectionsManager.Remove(entityID);

            if (internalEntities.Remove(entityID))
            {
                logger.Debug("Destroy for non-toolkit entity", ("entity", entityID), ("reason", destroyReason));
            }

            if (!TryGetNetworkEntityState(entityID, out var state))
            {
                logger.Trace($"Unable to delete entity [{entityID}] with reason: {destroyReason} because it is not mapped.", ("context", this));

                return;
            }

            var sync = state.Sync;

            if (destroyReason == DestroyReason.MaxEntitiesReached)
            {
                logger.Warning(Warning.ToolkitEntitiesManagerMaxEntities,
                    $"Max entity count exceeded. Entity '{sync.name}'[{entityID}] will be destroyed.",
                    ("context", this));
            }
            else if (destroyReason == DestroyReason.MaxQueriesReached)
            {
                logger.Warning(Warning.ToolkitEntitiesManagerMaxQueries,
                    $"Max query count exceeded. Entity `{sync.name}`[{entityID}] with the query will be destroyed. " +
                    $"See: {DocumentationLinks.GetDocsUrl(DocumentationKeys.MaxQueryCount)}",
                    ("context", this));
            }
            else if (destroyReason == DestroyReason.UnauthorizedCreate)
            {
                logger.Warning(Warning.ToolkitEntitiesManagerUnauthorizedCreate,
                    $"This client is unauthorized to create entities because HostAuthority.CreateEntities feature is enabled. " +
                    $"Entity '{sync.name}' [{entityID}] will be destroyed",
                    ("context", this));
            }
            else if (destroyReason == DestroyReason.DuplicateDestroy)
            {
                // Note: sync can be null if the entity was already replaced by the duplicate create
                // before the destroy arrived.
                if (sync == null)
                {
                    RemoveEntityFromMapper(state);
                    return;
                }

                // If the duplicate is a child of a sync group then don't disable or delete
                // since that is handled by the parent, just do the replacement.
                if (!sync.IsChildFromSyncGroup())
                {
                    if (sync.ReplacementStrategy == CoherenceSync.UniqueObjectReplacementStrategy.Destroy)
                    {
                        DestroyLocalObject(state, destroyReason);
                    }
                    else
                    {
                        // if we don't destroy, we have to disable until the create for the
                        // official version arrives.  Leaving it active can just lead to
                        // an unsynced instance disconnected from the network.
                        sync.SetActive(false);
                    }
                }

                RemoveEntityFromMapper(state);
                return;
            }

            DestroyLocalObject(state, destroyReason);
            RemoveEntityFromMapper(state);
        }

        private void DestroyLocalObject(NetworkEntityState state, DestroyReason destroyReason)
        {
            if (state.Sync.IsChildFromSyncGroup())
            {
                // If its part of a group it will be destroyed by the parent
                return;
            }

            bridge.OnNetworkEntityDestroyedInvoke(state, destroyReason);

            state.Sync.HandleNetworkedDestruction(false);
        }

        private void AddNetworkEntityStateToMapper(NetworkEntityState state, ICoherenceSync sync)
        {
            var id = state.EntityID;

            if (id == Entity.InvalidRelative)
            {
                logger.Warning(Warning.ToolkitEntitiesManagerMapperInvalid,
                    $"We can't add the CoherenceSync '{state.Sync}' to the mapper, because it has the EntityID {Entity.InvalidRelative}.",
                    ("context", state.Sync));
                return;
            }

            if (networkEntities.ContainsKey(id))
            {
                logger.Warning(Warning.ToolkitEntitiesManagerMapperDuplicate,
                    $"We can't add CoherenceSync '{state.Sync}' to the mapper, because the EntityID {id} already exists.",
                    ("context", state.Sync));
                return;
            }

            networkEntities[id] = state;

            // Unsubscribe before applying a pending authority change, so its value update
            // can't run HandleEntityAuthorityChanged (which would add the entity to the loop lookup
            // that is added to explicitly further down, causing a double add).
            // The unsubscribe is also needed for the unique entity swap case, where the new state is
            // assigned to an existing CoherenceSync that may still be subscribed.
            state.Sync.OnAuthorityChanged -= HandleEntityAuthorityChanged;

            // Apply any authority change that arrived before this entity was instantiated (e.g.
            // while its instantiation was deferred waiting for a parent), now that it is known.
            bridge.AuthorityManager?.ApplyPendingAuthorityChange(state);

            state.Sync.OnAuthorityChanged += HandleEntityAuthorityChanged;

            if (sync is CoherenceSync cs)
            {
                cs.OnCoherenceTagDirtied += HandleTagDirtied;
            }

            // Ensure the initial tag is sent for state-authority entities.
            // The tag may have been serialized or set before subscription.
            if (state.HasStateAuthority)
            {
                dirtyTagUpdaters.Add(state.Sync.Updater);
            }

            GetLoopLookup(state.HasStateAuthority).Add(state.EntityID, state.Sync.Updater, state.Sync.InterpolationLocationConfig);

            if (EntityRequiresSendingInput(sync))
            {
                inputEntities[id] = sync.Updater;
            }

            if (state.Sync.HasInput)
            {
                inputManager.AddInput(state.Sync.Input);
            }
        }

        private void HandleTagDirtied(ICoherenceSync sync)
        {
            if (sync.Updater != null)
            {
                dirtyTagUpdaters.Add(sync.Updater);
            }
        }

        private void HandleEntityAuthorityChanged(AuthorityChangedEventArgs args)
        {
            var sync = args.Sync;
            var entityID = sync.EntityState.EntityID;

            GetLoopLookup(args.OldAuthority.ControlsState()).RemoveCompletely(entityID);
            GetLoopLookup(args.NewAuthority.ControlsState()).Add(entityID, sync.Updater, sync.InterpolationLocationConfig);

            if (sync.HasInput)
            {
                var lostInputAuthority = args.OldAuthority.ControlsInput() && !args.NewAuthority.ControlsInput();
                var gainedInputAuthority = !args.OldAuthority.ControlsInput() && args.NewAuthority.ControlsInput();

                if (lostInputAuthority)
                {
                    inputEntities.Remove(entityID);
                }
                else if (gainedInputAuthority && EntityRequiresSendingInput(sync))
                {
                    inputEntities[entityID] = sync.Updater;
                }
            }
        }

        private static bool EntityRequiresSendingInput(ICoherenceSync sync)
        {
            return sync.HasInputAuthority && sync.HasInput && !sync.Input.UseFixedSimulationFrames;
        }

        internal void RemoveEntityFromMapper(NetworkEntityState state)
        {
            if (state == null)
            {
                return;
            }

            var entityID = state.EntityID;

            networkEntities.Remove(entityID);
            ackedEntities.Remove(entityID);
            delayedEntityUpdates.Remove(entityID);
            delayedEntityCommands.Remove(entityID);

            stateAuthorityLoopLookup.RemoveCompletely(entityID);
            remoteAuthorityLoopLookup.RemoveCompletely(entityID);
            inputEntities.Remove(entityID);

            if (state.Sync != null)
            {
                if (state.Sync.HasInput)
                {
                    inputManager.RemoveInput(state.Sync.Input);
                }

                state.Sync.OnAuthorityChanged -= HandleEntityAuthorityChanged;

                if (state.Sync is CoherenceSync cs)
                {
                    cs.OnCoherenceTagDirtied -= HandleTagDirtied;
                    dirtyTagUpdaters.Remove(state.Sync.Updater);
                }
            }
        }

        private void InstantiateNetworkedEntity(ICoherenceSync syncPrefab, SpawnInfo info, IncomingEntityUpdate entityUpdate)
        {
            entityUpdate.TakeOwnershipOfComponents("EntitiesManager.InstantiateNetworkedEntity");

            Scene? lastActiveScene = null;
            try
            {
                lastActiveScene = SetActiveScene();
                NetworkEntityState entityState = null;

                if (info.isFromGroup)
                {
                    entityState = CreateUnsynchronizedNetworkEntity(info, entityUpdate.ReleaseOwnershipOfComponents(), syncPrefab.UnsyncedEntityPriority);

                    if (!networkEntities.ContainsKey(info.connectedEntity))
                    {
                        return;
                    }
                }
                else if (info.connectedEntity.IsValid && !networkEntities.ContainsKey(info.connectedEntity))
                {
                    DelayInstantiationDependingOnParent(info.connectedEntity, entityUpdate.ReleaseOwnershipOfComponents());
                    return;
                }

                if (!info.isFromGroup)
                {
                    entityState = CreateUnsynchronizedNetworkEntity(info, entityUpdate.ReleaseOwnershipOfComponents(), syncPrefab.UnsyncedEntityPriority);
                }

                if (uniquenessManager.FindUniqueObjectForNewRemoteNetworkEntity(info))
                {
                    PostNetworkEntityCreationActions(entityState);
                    return;
                }

                info.ComponentUpdates = entityUpdate.Components.Updates;
                InstantiateCoherenceSync(syncPrefab, info, entityState);
            }
            catch (Exception e)
            {
                logger.Error(Error.ToolkitEntitiesManagerInstantiationException,
                    $"The instantiation of the prefab '{syncPrefab}' failed: {e}",
                    ("context", syncPrefab));
            }
            finally
            {
                if (lastActiveScene.HasValue)
                {
                    SceneManager.SetActiveScene(lastActiveScene.Value);
                }
            }
        }

        private void DelayInstantiationDependingOnParent(Entity parentEntity, IncomingEntityUpdate entityUpdate)
        {
            entityUpdate.TakeOwnershipOfComponents("EntitiesManager.DelayInstantiationDependingOnParent");

            if (!delayedUpdatesDependingOnParents.ContainsKey(parentEntity))
            {
                delayedUpdatesDependingOnParents[parentEntity] = new List<IncomingEntityUpdate> { entityUpdate };
            }
            else
            {
                delayedUpdatesDependingOnParents[parentEntity].Add(entityUpdate);
            }
        }

        private void InstantiateCoherenceSync(ICoherenceSync syncPrefab, SpawnInfo info, NetworkEntityState state)
        {
            CoherenceBridgeStore.instantiatingBridge = bridge;
            try
            {
                if (!info.isFromGroup)
                {
                    info.bridge = bridge;
                    info.prefab = syncPrefab;
                    info.rotation ??= (syncPrefab as MonoBehaviour)?.transform.rotation ?? Quaternion.identity;

                    var config = syncPrefab.CoherenceSyncConfig;
                    config.Instantiator.Instantiate(info);
                }

                PostNetworkEntityCreationActions(state);
            }
            // User-created Instantiators could throw any exceptions
            catch (Exception exception)
            {
                logger.Warning(Warning.ToolkitSyncInstantiatorException,
                    syncPrefab.IsDestroyed ? $"Trying to instantiate a networked prefab that is missing.\n{exception}"
                    : !syncPrefab.CoherenceSyncConfig ? $"An exception occurred when trying to instantiate '{syncPrefab.name}': The networked prefab has no CoherenceSyncConfig.\n{exception}"
                    : syncPrefab.CoherenceSyncConfig.Instantiator is null ? $"An exception occurred when trying to instantiate '{syncPrefab.name}': The networked prefab's CoherenceSyncConfig has no Instantiator.\n{exception}"
                    : $"An exception occurred when trying to instantiate '{syncPrefab.name}' via {syncPrefab.CoherenceSyncConfig.Instantiator.GetType().Name}.\n{exception}",
                    ("context", syncPrefab.IsDestroyed ? syncPrefab : syncPrefab.gameObject));
            }
            finally
            {
                CoherenceBridgeStore.instantiatingBridge = null;
            }
        }

        private NetworkEntityState CreateUnsynchronizedNetworkEntity(SpawnInfo info,
            IncomingEntityUpdate entityUpdate, CoherenceSync.UnsyncedNetworkEntityPriority unsyncedNetworkEntityPriority)
        {
            entityUpdate.TakeOwnershipOfComponents("EntitiesManager.CreateUnsynchronizedNetworkEntity");

            var networkEntity =
                new NetworkEntityState(info.entityId, entityUpdate.Meta.Authority(), entityUpdate.Meta.IsOrphan, true, null, info.uniqueId);

            var isConnectionEntity = info.clientId.HasValue && info.connectionType.HasValue && info.scene.HasValue;
            var connection = isConnectionEntity ? new CoherenceClientConnection(bridge, networkEntity, info.entityId,
                info.clientId.Value, info.connectionType.Value, info.scene.Value) : null;
            networkEntity.ClientConnection = connection;

            RemoveUnhandledComponents(entityUpdate.Components.Updates);

            var unsyncedNetworkEntity = new UnsyncedNetworkEntity()
            {
                EntityState = networkEntity,
                Updates = entityUpdate.Components.Updates,
                LOD = entityUpdate.Meta.LOD,
                UniqueUUID = info.uniqueId,
                CreationChannelID = entityUpdate.ChannelID,
            };

            // Component destroys are not expected or used for creates, so we can just dispose of them
            entityUpdate.Components.Destroys.Dispose();

            if ((unsyncedNetworkEntityPriority == CoherenceSync.UnsyncedNetworkEntityPriority.AssetId || string.IsNullOrEmpty(info.uniqueId))
                && !info.isFromGroup)
            {
                if (!unsyncedNetworkEntities.TryGetValue(info.assetId, out Queue<UnsyncedNetworkEntity> queue))
                {
                    queue = new Queue<UnsyncedNetworkEntity>();
                    unsyncedNetworkEntities.Add(info.assetId, queue);
                }

                queue.Enqueue(unsyncedNetworkEntity);
            }
            else
            {
                // Replacing an entry orphans what it was holding. The components are rented, and
                // this dictionary owns them until something claims the entity, so the displaced
                // entry has to be disposed rather than dropped. A group member that leaves and
                // re-enters a client's view arrives again under the same unique id, which is how
                // an entry comes to be replaced at all.
                if (unsyncedNetworkEntitiesByUniqueId.Remove(info.uniqueId, out var displaced))
                {
                    displaced.Updates.Dispose();
                }

                unsyncedNetworkEntitiesByUniqueId[info.uniqueId] = unsyncedNetworkEntity;
            }

            return networkEntity;
        }

        private void PostNetworkEntityCreationActions(NetworkEntityState state)
        {
            try
            {
                if (state.ClientConnection != null)
                {
                    clientConnectionsManager.Add(state.ClientConnection);
                }

                bridge.OnNetworkEntityCreatedInvoke(state);
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.ToolkitEntitiesManagerPostNetworkCreateException,
                    $"{nameof(bridge.OnNetworkEntityCreatedInvoke)} exception in handler: {handlerException}");
            }
        }

        private void InstantiateDelayedEntitiesDependingOnThisParent(Entity parentEntity)
        {
            if (!delayedUpdatesDependingOnParents.ContainsKey(parentEntity))
            {
                return;
            }

            delayedEntitiesToInstantiate.Push(parentEntity);
        }

        private static void RemoveUnhandledComponents(ComponentUpdates componentUpdates)
        {
            // The GenericPrefabReference/Id aren't bindings
            componentUpdates.Remove(AssetIDComponent.ComponentID);
        }
    }
}

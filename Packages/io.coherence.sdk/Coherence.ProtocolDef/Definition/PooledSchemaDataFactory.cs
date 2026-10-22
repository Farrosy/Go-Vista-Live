// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Coherence;
    using Coherence.Common.Pooling;
    using Coherence.Connection;
    using Coherence.Debugging;
    using Coherence.Entities;
    using Coherence.SimulationFrame;

    public class PooledSchemaDataFactory : ISchemaDataFactory
    {
        private ProtocolDefinition schemaDefinition;
        private ManagedTypesTables managedTypesTables;

        private bool poolInputs;

        private DataPoolBucket[] dataPoolBuckets;
        private Pool<AbsoluteSimulationFrame[]> simFramesPool;
        private Pool<ComponentMap> componentMapPool;
        private Pool<ComponentDestroyMap> componentDestroyMapPool;

        private Dictionary<byte[], RentInfo> activeComponents = new();
        private Dictionary<byte[], RentInfo> activeCommands = new();
        private Dictionary<byte[], RentInfo> activeInputs = new();
        private Dictionary<ComponentMap, RentInfo> activeComponentMaps = new();
        private Dictionary<ComponentDestroyMap, RentInfo> activeComponentDestroyMaps = new();

        // Cached delegates — each instance method passed as a method group allocates a new
        // delegate object to capture 'this'. Storing them as fields means the object is
        // created once per factory instance rather than once per Create* call.
        private readonly OnComponentDataDisposed cachedOnComponentDataDisposed;
        private readonly OnCommandDataDisposed cachedOnCommandDataDisposed;
        private readonly OnInputDataDisposed cachedOnInputDataDisposed;
        private readonly OnComponentMapDisposed cachedOnComponentMapDisposed;
        private readonly OnComponentDestroyMapDisposed cachedOnComponentDestroyMapDisposed;
        private readonly OnOwnershipChanged cachedOnComponentOwnershipChanged;
        private readonly OnOwnershipChanged cachedOnCommandOwnershipChanged;
        private readonly OnOwnershipChanged cachedOnInputOwnershipChanged;
        private readonly OnOwnershipChanged cachedOnComponentMapOwnershipChanged;
        private readonly OnOwnershipChanged cachedOnComponentDestroyMapOwnershipChanged;

        public PooledSchemaDataFactory(ProtocolDefinition schemaDefinition, bool threadSafe, bool poolInputs)
        {
            this.schemaDefinition = schemaDefinition;
            this.managedTypesTables = new ManagedTypesTables();
            this.poolInputs = poolInputs;

            this.cachedOnComponentDataDisposed = OnComponentDataDisposed;
            this.cachedOnCommandDataDisposed = OnCommandDataDisposed;
            this.cachedOnInputDataDisposed = OnInputDataDisposed;
            this.cachedOnComponentMapDisposed = OnComponentMapDisposed;
            this.cachedOnComponentDestroyMapDisposed = OnComponentDestroyMapDisposed;
            this.cachedOnComponentOwnershipChanged = OnComponentOwnershipChanged;
            this.cachedOnCommandOwnershipChanged = OnCommandOwnershipChanged;
            this.cachedOnInputOwnershipChanged = OnInputOwnershipChanged;
            this.cachedOnComponentMapOwnershipChanged = OnComponentMapOwnershipChanged;
            this.cachedOnComponentDestroyMapOwnershipChanged = OnComponentDestroyMapOwnershipChanged;

            this.dataPoolBuckets = InitDataPoolBuckets(schemaDefinition, threadSafe);

            this.simFramesPool = Pool<AbsoluteSimulationFrame[]>.Builder(_ => new AbsoluteSimulationFrame[ProtocolComponentDefinition.MaxFields])
                .WithReturnAction(simFrames => Array.Clear(simFrames, 0, simFrames.Length))
                .Prefill(64)
                .Concurrent(threadSafe)
                .Build();

            this.componentMapPool = Pool<ComponentMap>.Builder(_ => new ComponentMap(capacity: 8, cachedOnComponentMapDisposed, cachedOnComponentMapOwnershipChanged))
                .WithReturnAction(map => map.Clear())
                .Prefill(64)
                .Concurrent(threadSafe)
                .Build();

            this.componentDestroyMapPool = Pool<ComponentDestroyMap>.Builder(_ => new ComponentDestroyMap(capacity: 8, cachedOnComponentDestroyMapDisposed, cachedOnComponentDestroyMapOwnershipChanged))
                .WithReturnAction(map => map.Clear())
                .Prefill(64)
                .Concurrent(threadSafe)
                .Build();
        }

        public ComponentData CreateComponent(uint componentID)
        {
            var componentDef = schemaDefinition.GetComponentDefinition(componentID);

            var data = GetDataPool(componentDef.DataSize).Rent();
            var simFrames = simFramesPool.Rent();
            AppendRentInfo(activeComponents, data, componentID);

            return new ComponentData(componentDef, managedTypesTables, data, simFrames, cachedOnComponentDataDisposed, cachedOnComponentOwnershipChanged);
        }

        public CommandData CreateCommand(uint commandID, Entity entity, MessageTarget messageTarget,
            bool usesMeta = default, ClientID senderClientID = default, long frame = default)
        {
            var commandDef = schemaDefinition.GetCommandDefinition(commandID);

            var data = GetDataPool(commandDef.DataSize).Rent();
            AppendRentInfo(activeCommands, data, commandID);

            return new CommandData(commandDef, managedTypesTables, data, entity, messageTarget, usesMeta, senderClientID, frame, cachedOnCommandDataDisposed, cachedOnCommandOwnershipChanged);
        }

        public InputData CreateInput(uint inputID, Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame)
        {
            var inputDef = schemaDefinition.GetInputDefinition(inputID);

            byte[] data;
            if (poolInputs)
            {
                data = GetDataPool(inputDef.DataSize).Rent();
                AppendRentInfo(activeInputs, data, inputID);
            }
            else
            {
                data = new byte[inputDef.DataSize];
            }

            return new InputData(inputDef, managedTypesTables, data, entity, messageTarget, frame, cachedOnInputDataDisposed, cachedOnInputOwnershipChanged);
        }

        public ComponentMap CreateComponentMap()
        {
            var map = componentMapPool.Rent();
            AppendRentInfo(activeComponentMaps, map, 0);

            return map;
        }

        public ComponentDestroyMap CreateComponentDestroyMap()
        {
            var map = componentDestroyMapPool.Rent();
            AppendRentInfo(activeComponentDestroyMaps, map, 0);

            return map;
        }

        [Conditional(DbgAssert.ASSERTIONS_ENABLED)]
        public void AssertNoLeaks()
        {
            foreach (var (_, rentInfo) in activeComponents)
            {
                var definition = schemaDefinition.GetComponentDefinition(rentInfo.DefinitionID);
                DbgAssert.ThatFmt(false,
                    "Component {0} was rented but never disposed. There is a leak!\nOwnership records: {1}",
                    definition, rentInfo.GetOwnershipRecords());
            }

            foreach (var (_, rentInfo) in activeCommands)
            {
                var definition = schemaDefinition.GetCommandDefinition(rentInfo.DefinitionID);
                DbgAssert.ThatFmt(false,
                    "Command {0} was rented but never disposed. There is a leak!\nOwnership records: {1}",
                    definition, rentInfo.GetOwnershipRecords());
            }

            if (poolInputs)
            {
                foreach (var (_, rentInfo) in activeInputs)
                {
                    var definition = schemaDefinition.GetInputDefinition(rentInfo.DefinitionID);
                    DbgAssert.ThatFmt(false,
                        "Input {0} was rented but never disposed. There is a leak!\nOwnership records: {1}",
                        definition, rentInfo.GetOwnershipRecords());
                }
            }

            foreach (var (_, rentInfo) in activeComponentMaps)
            {
                DbgAssert.ThatFmt(false,
                    "Component map was rented but never disposed. There is a leak!\nOwnership records: {0}",
                    rentInfo.GetOwnershipRecords());
            }

            foreach (var (_, rentInfo) in activeComponentDestroyMaps)
            {
                DbgAssert.ThatFmt(false,
                    "Component destroy map was rented but never disposed. There is a leak!\nOwnership records: {0}",
                    rentInfo.GetOwnershipRecords());
            }
        }

        private void OnComponentDataDisposed(ComponentData componentData, byte[] data, AbsoluteSimulationFrame[] simFrames)
        {
            GetDataPool(data.Length).Return(data);
            simFramesPool.Return(simFrames);

            RemoveRentInfo(activeComponents, data);
        }

        private void OnCommandDataDisposed(CommandData commandData, byte[] data)
        {
            GetDataPool(data.Length).Return(data);

            RemoveRentInfo(activeCommands, data);
        }

        private void OnInputDataDisposed(InputData inputData, byte[] data)
        {
            if (poolInputs)
            {
                GetDataPool(data.Length).Return(data);
                RemoveRentInfo(activeInputs, data);
            }
        }

        private void OnComponentMapDisposed(ComponentMap componentMap)
        {
            componentMapPool.Return(componentMap);

            RemoveRentInfo(activeComponentMaps, componentMap);
        }

        private void OnComponentDestroyMapDisposed(ComponentDestroyMap componentDestroyMap)
        {
            componentDestroyMapPool.Return(componentDestroyMap);

            RemoveRentInfo(activeComponentDestroyMaps, componentDestroyMap);
        }

        private void OnComponentOwnershipChanged(object data, string ownerIdentifier)
        {
            ChangeOwnershipRentInfo(activeComponents, "component", (byte[])data, ownerIdentifier);
        }

        private void OnCommandOwnershipChanged(object data, string ownerIdentifier)
        {
            ChangeOwnershipRentInfo(activeCommands, "command", (byte[])data, ownerIdentifier);
        }

        private void OnInputOwnershipChanged(object data, string ownerIdentifier)
        {
            if (poolInputs)
            {
                ChangeOwnershipRentInfo(activeInputs, "input", (byte[])data, ownerIdentifier);
            }
        }

        private void OnComponentMapOwnershipChanged(object data, string ownerIdentifier)
        {
            ChangeOwnershipRentInfo(activeComponentMaps, "component map", (ComponentMap)data, ownerIdentifier);
        }

        private void OnComponentDestroyMapOwnershipChanged(object data, string ownerIdentifier)
        {
            ChangeOwnershipRentInfo(activeComponentDestroyMaps, "component destroy map", (ComponentDestroyMap)data, ownerIdentifier);
        }

        private static DataPoolBucket[] InitDataPoolBuckets(ProtocolDefinition schemaDefinition, bool threadSafe)
        {
            var maxDataSize = schemaDefinition.GetMaxDataSize() ?? 0;

            var dataPoolBuckets = new List<DataPoolBucket>();

            void AddDataPoolBucket(int dataSize)
            {
                DbgAssert.That(dataPoolBuckets.Count == 0 || dataSize > dataPoolBuckets.Last().DataSize, "Data pool buckets must be added in ascending order of data size.");

                dataPoolBuckets.Add(new DataPoolBucket(
                    dataSize,
                    Pool<byte[]>.Builder(_ => new byte[dataSize])
                        .WithReturnAction(data => Array.Clear(data, 0, data.Length))
                        .Prefill(64)
                        .Concurrent(threadSafe)
                        .Build()));
            }

            AddDataPoolBucket(0); // for zero-size data such as Global, GlobalQuery, Persistence and PreserveChildren components
            AddDataPoolBucket(8);
            AddDataPoolBucket(32);
            AddDataPoolBucket(64);

            if (maxDataSize > dataPoolBuckets.Last().DataSize)
            {
                AddDataPoolBucket(maxDataSize);
            }

            return dataPoolBuckets.ToArray();
        }

        private Pool<byte[]> GetDataPool(int dataSize)
        {
            foreach (var bucket in dataPoolBuckets)
            {
                if (bucket.DataSize >= dataSize)
                {
                    return bucket.Pool;
                }
            }

            throw new Exception($"Couldn't find a data pool big enough for data size {dataSize}");
        }

        [Conditional(DbgAssert.ASSERTIONS_ENABLED)]
        private void ChangeOwnershipRentInfo<T>(Dictionary<T, RentInfo> dict, string type, T obj, string ownerIdentifier)
        {
            if (!dict.TryGetValue(obj, out var rentInfo))
            {
                DbgAssert.ThatFmt(false, "Ownership changed for a object that is not a known active {0}.", type);
                return;
            }

            if (rentInfo.GetCurrentOwner() == null && ownerIdentifier == null)
            {
                DbgAssert.ThatFmt(false, "Cannot release ownership of a {0} that has no current owner.", type);
            }

            if (rentInfo.GetCurrentOwner() != null && ownerIdentifier != null)
            {
                DbgAssert.ThatFmt(false, "Cannot change ownership of a {0} from '{1}' owner to '{2}' without releasing the first owner.", type, rentInfo.GetCurrentOwner(), ownerIdentifier);
            }

            rentInfo.SetCurrentOwner(ownerIdentifier);
            if (ownerIdentifier != null)
            {
                rentInfo.AddOwnershipRecord(ownerIdentifier);
            }

            dict[obj] = rentInfo;
        }

        [Conditional(DbgAssert.ASSERTIONS_ENABLED)]
        private void AppendRentInfo<T>(Dictionary<T, RentInfo> dict, T obj, uint id)
        {
            DbgAssert.ThatFmt(!dict.ContainsKey(obj),
                "Object rented for id {0} is already in active dictionary.", id);

            dict[obj] = new RentInfo(id);
        }

        [Conditional(DbgAssert.ASSERTIONS_ENABLED)]
        private void RemoveRentInfo<T>(Dictionary<T, RentInfo> dict, object obj)
        {
            DbgAssert.That(dict.Remove((T)obj),
                "Returned object is not found in the active dictionary.");
        }

        private readonly struct DataPoolBucket
        {
            public readonly int DataSize;
            public readonly Pool<byte[]> Pool;

            public DataPoolBucket(int dataSize, Pool<byte[]> pool)
            {
                DataSize = dataSize;
                Pool = pool;
            }
        }

        private struct RentInfo
        {
            public readonly uint DefinitionID;

            private string currentOwner;
            private List<string> ownershipRecords;

            public RentInfo(uint definitionID)
            {
                DefinitionID = definitionID;
                currentOwner = null;
                ownershipRecords = null;
            }

            public string GetCurrentOwner()
            {
                return currentOwner;
            }

            public void SetCurrentOwner(string ownerIdentifier)
            {
                currentOwner = ownerIdentifier;
            }

            public void AddOwnershipRecord(string ownerIdentifier)
            {
                ownershipRecords ??= new List<string>(8);
                ownershipRecords.Add(ownerIdentifier);
            }

            public string GetOwnershipRecords()
            {
                if (ownershipRecords == null || ownershipRecords.Count == 0)
                {
                    return "<no ownership changes recorded>";
                }

                var history = string.Join(" > ", ownershipRecords);
                return $"{history} (current owner: {(currentOwner ?? "<none>")})";
            }
        }
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Debugging;
    using Entities;
    using Schema;
    using SimulationFrame;
    using Unsafe;

    public struct ComponentData : IFieldDataCollection
    {
        private ProtocolComponentDefinition definition;
        private byte[] data;
        private AbsoluteSimulationFrame[] simFrames;
        private ManagedTypesTables managedTypesTables;
        private OnComponentDataDisposed onDisposedCallback;
        private OnOwnershipChanged onOwnershipChangedCallback;

        public uint FieldsMask { get; set; }
        public uint StoppedMask { get; set; }

        public bool IsInitialized => definition != null;
        public ProtocolComponentDefinition Definition => definition;
        public uint ComponentID => definition.ComponentID;
        public string ComponentName => definition.Name;
        public int FieldCount => definition.Fields.Count;
        public IReadOnlyList<ProtocolFieldDefinition> FieldDefinitions => definition.Fields;
        public int ComponentOrder => definition.Attributes.ComponentOrder;
        public bool IsSendOrdered => definition.Attributes.SendOrdered;
        public bool HasRefFields => definition.RefFieldsCount > 0;
        public int RefFieldsCount => definition.RefFieldsCount;
        public int PriorityLevel => definition.PriorityLevel;
        public bool HasBaseComponent => definition.Attributes.HasBaseComponent;
        public uint BaseComponentID => definition.Attributes.BaseComponentID;

        public FieldIterator<ComponentData> Fields => new(this);
        public FieldIterator<ComponentData> EntityFields => new(this, FieldIterator<ComponentData>.EntityFieldFilter);

        internal ComponentData(
            ProtocolComponentDefinition definition,
            ManagedTypesTables managedTypesTables,
            byte[] data,
            AbsoluteSimulationFrame[] simFrames,
            OnComponentDataDisposed onDisposedCallback = null,
            OnOwnershipChanged onOwnershipChangedCallback = null)
        {
            this.definition = definition;
            this.data = data;
            this.simFrames = simFrames;
            this.managedTypesTables = managedTypesTables;
            this.FieldsMask = 0;
            this.StoppedMask = 0;
            this.onDisposedCallback = onDisposedCallback;
            this.onOwnershipChangedCallback = onOwnershipChangedCallback;
        }

        public FieldData GetField(int fieldIndex)
        {
            AssertInitialized();

            var fieldDefinition = GetFieldDefinition(fieldIndex);
            var fieldData = GetFieldDataSpan(fieldIndex);
            var fieldSimFrames = GetFieldSimFramesSpan(fieldIndex);

            return new FieldData(fieldDefinition, managedTypesTables, fieldData, fieldSimFrames, fieldIndex);
        }

        public bool IsFieldSet(int fieldIndex)
        {
            AssertInitialized();

            var fieldMask = 1u << fieldIndex;
            return (FieldsMask & fieldMask) != 0;
        }

        public ComponentData SetFieldSet(int fieldIndex, bool isSet)
        {
            AssertInitialized();

            var fieldMask = 1u << fieldIndex;
            if (isSet)
            {
                FieldsMask |= fieldMask;
            }
            else
            {
                FieldsMask &= ~fieldMask;
            }

            return this;
        }

        public void ClearFieldsMask(uint mask)
        {
            AssertInitialized();
            FieldsMask &= ~mask;
        }

        public void ClearStoppedMask(uint mask)
        {
            AssertInitialized();
            StoppedMask &= ~mask;
        }

        public void Update(ComponentData other)
        {
            AssertInitialized();

            FieldsMask |= other.FieldsMask;
            StoppedMask &= ~other.FieldsMask;
            StoppedMask |= other.StoppedMask;

            if (other.FieldsMask == 0)
            {
                return;
            }

            foreach (var field in Fields)
            {
                if ((other.FieldsMask & field.Mask) == 0)
                {
                    continue;
                }

                field.CopyFrom(other.GetField(field.Index));
            }
        }

        public void SetComponentDefinition(ProtocolComponentDefinition newDefinition)
        {
            AssertInitialized();

            var isBase = this.definition.Attributes.HasBaseComponent &&
                         this.definition.Attributes.BaseComponentID == newDefinition.ComponentID;

            var isDerived = newDefinition.Attributes.HasBaseComponent &&
                            newDefinition.Attributes.BaseComponentID == this.definition.ComponentID;

            if (!isBase && !isDerived)
            {
                throw new ArgumentException($"New component definition {newDefinition.Name}({newDefinition.ComponentID}) " +
                    $"is not derived from nor a base to the current definition {definition.Name}({definition.ComponentID})");
            }

            definition = newDefinition;
        }

        public ComponentData Clone(ISchemaDataFactory schemaDataFactory)
        {
            AssertInitialized();

            var newComponent = schemaDataFactory.CreateComponent(ComponentID);
            newComponent.FieldsMask = FieldsMask;
            newComponent.StoppedMask = StoppedMask;

            foreach (var field in Fields)
            {
                newComponent.GetField(field.Index).CopyFrom(field);
            }

            return newComponent;
        }

        public uint InitialFieldsMask()
        {
            AssertInitialized();

            if (FieldCount == ProtocolComponentDefinition.MaxFields)
            {
                return uint.MaxValue;
            }

            return (1u << FieldCount) - 1;
        }

        public AbsoluteSimulationFrame? GetMinSimulationFrame()
        {
            AssertInitialized();

            var hasMin = false;
            var min = new AbsoluteSimulationFrame();

            foreach (var field in Fields)
            {
                if (!field.HasSimFrames)
                {
                    continue;
                }

                if ((FieldsMask & field.Mask) == 0)
                {
                    continue;
                }

                var simFrame = field.GetSimFrame();
                if (!hasMin || simFrame < min)
                {
                    min = simFrame;
                    hasMin = true;
                }
            }

            return hasMin ? min : null;
        }

        public long GetMemorySize()
        {
            if (!IsInitialized)
            {
                return 0;
            }

            long size = data.Length +
                       simFrames.Length * Utils.SizeOf<AbsoluteSimulationFrame>();

            foreach (var field in Fields)
            {
                switch (field.Type)
                {
                    case SchemaType.String:
                        var str = field.GetString();
                        if (str != null)
                        {
                            size += str.Length * sizeof(char);
                        }
                        break;
                    case SchemaType.Bytes:
                        var bytes = field.GetBytes();
                        if (bytes != null)
                        {
                            size += bytes.Length;
                        }
                        break;
                }
            }

            return size;
        }

        public IEntityMapper.Error MapToAbsolute(IEntityMapper mapper)
        {
            AssertInitialized();

            foreach (var field in EntityFields)
            {
                var err = mapper.MapToAbsoluteEntity(field.GetEntity(), false, out var absoluteEntity);
                if (err != IEntityMapper.Error.None)
                {
                    return err;
                }

                field.SetEntity(absoluteEntity);
            }

            return IEntityMapper.Error.None;
        }

        public IEntityMapper.Error MapToRelative(IEntityMapper mapper)
        {
            AssertInitialized();

            // We assume that the inConnection held changes with unresolved references, so the 'createEntityIfNotFound=true' is
            // there only because there's a chance that the parent creation change will be processed after this one
            // meaning there's no mapping for the parent yet. This wouldn't be necessary if mapping creation would happen
            // in the clientWorld via create/destroy requests while here we would only check whether mapping exists or not.
            foreach (var entityField in EntityFields)
            {
                var err = mapper.MapToRelativeEntity(entityField.GetEntity(), true, out var relativeEntity);
                if (err != IEntityMapper.Error.None)
                {
                    return err;
                }
                entityField.SetEntity(relativeEntity);
            }

            return IEntityMapper.Error.None;
        }

        public uint ReplaceReferences(Entity fromEntity, Entity toEntity)
        {
            AssertInitialized();

            uint refsMask = 0;

            foreach (var entityField in EntityFields)
            {
                if (entityField.GetEntity() == fromEntity)
                {
                    entityField.SetEntity(toEntity);
                    refsMask |= entityField.Mask;
                }
            }

            return refsMask;
        }

        public void ClearReferences(ISet<Entity> clearEntities)
        {
            AssertInitialized();

            foreach (var entityField in EntityFields)
            {
                if (clearEntities.Contains(entityField.GetEntity()))
                {
                    entityField.SetEntity(Entity.InvalidRelative);
                }
            }
        }

        public bool IsWorldPositionComponent()
        {
            AssertInitialized();

            return ComponentID == WorldPositionComponent.ComponentID ||
                (definition.Attributes.HasBaseComponent && definition.Attributes.BaseComponentID == WorldPositionComponent.ComponentID);
        }

        private Span<byte> GetFieldDataSpan(int fieldIndex)
        {
            var fieldDefinition = GetFieldDefinition(fieldIndex);
            return new Span<byte>(data, definition.FieldOffsets[fieldIndex], fieldDefinition.Size);
        }

        private Span<AbsoluteSimulationFrame> GetFieldSimFramesSpan(int fieldIndex)
        {
            var fieldDefinition = GetFieldDefinition(fieldIndex);
            if (!fieldDefinition.HasSimFrames)
            {
                return Span<AbsoluteSimulationFrame>.Empty;
            }

            return new Span<AbsoluteSimulationFrame>(simFrames, fieldIndex, 1);
        }

        private ProtocolFieldDefinition GetFieldDefinition(int fieldIndex)
        {
            DbgAssert.ThatFmt(fieldIndex >= 0 && fieldIndex < definition.Fields.Count, "Field index {0} out of range", fieldIndex);
            return definition.Fields[fieldIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertInitialized()
        {
            DbgAssert.That(IsInitialized, "ComponentData is not initialized");
        }

        public override string ToString()
        {
            if (!IsInitialized)
            {
                return "Uninitialized ComponentData";
            }

            var sb = new StringBuilder();

            sb.Append(definition.Name);

            sb.Append("(");

            sb.Append("T: ");
            sb.Append(definition.ComponentID);

            sb.Append(", M: ");
            sb.Append(Convert.ToString(FieldsMask, 2).PadLeft(FieldCount, '0'));

            sb.Append(", S: ");
            sb.Append(Convert.ToString(StoppedMask, 2).PadLeft(FieldCount, '0'));


            sb.Append(")");


            sb.Append("[");

            for (var i = 0; i < FieldCount; i++)
            {
                GetField(i).ToString(sb);

                if (i < FieldCount - 1)
                {
                    sb.Append(", ");
                }
            }

            sb.Append("]");

            return sb.ToString();
        }

        public void TakeOwnership(string ownerIdentifier)
        {
            AssertInitialized();
            onOwnershipChangedCallback?.Invoke(data, ownerIdentifier);
        }

        public ComponentData ReleaseOwnership()
        {
            AssertInitialized();
            onOwnershipChangedCallback?.Invoke(data, null);

            return this;
        }

        public void Dispose()
        {
            if (!IsInitialized)
            {
                DbgAssert.That(false, "Trying to dispose an uninitialized or already disposed ComponentData");
                return;
            }

            foreach (var field in Fields)
            {
                if (field.Type == SchemaType.String)
                {
                    field.SetString(null);
                }
                else if (field.Type == SchemaType.Bytes)
                {
                    field.SetBytes(null);
                }
            }

            onDisposedCallback?.Invoke(this, data, simFrames);

            // Make it uninitialized again so it can't be used after disposal
            definition = null;
            data = null;
            simFrames = null;
        }
    }

    public static class ComponentDataEx
    {
        public static string ToStringEx(this IReadOnlyList<ComponentData> comps)
        {
            if (comps == null || comps.Count == 0)
            {
                return "None";
            }

            return string.Join<ComponentData>(", ", comps);
        }

        public static void DisposeAll(this IReadOnlyList<ComponentData> comps)
        {
            if (comps == null)
            {
                return;
            }

            for (var i = 0; i < comps.Count; i++)
            {
                comps[i].Dispose();
            }
        }

        public static void TakeOwnershipAll(this IReadOnlyList<ComponentData> comps, string ownerIdentifier)
        {
            if (comps == null)
            {
                return;
            }

            for (var i = 0; i < comps.Count; i++)
            {
                comps[i].TakeOwnership(ownerIdentifier);
            }
        }

        public static IReadOnlyList<ComponentData> ReleaseOwnershipAll(this IReadOnlyList<ComponentData> comps)
        {
            if (comps == null)
            {
                return comps;
            }

            for (var i = 0; i < comps.Count; i++)
            {
                _ = comps[i].ReleaseOwnership();
            }

            return comps;
        }

        public static bool IsQueryComponent(uint componentID)
        {
            return componentID == GlobalQueryComponent.ComponentID ||
                   componentID == TagQueryComponent.ComponentID ||
                   componentID == WorldPositionQueryComponent.ComponentID;
        }
    }

    public class ComponentDataComparer
    {
        public static readonly Comparison<KeyValuePair<uint, ComponentData>> Cached = (x, y) =>
        {
            var order = x.Value.ComponentOrder.CompareTo(y.Value.ComponentOrder);
            return order != 0 ? order : x.Value.ComponentID.CompareTo(y.Value.ComponentID);
        };
    }
}

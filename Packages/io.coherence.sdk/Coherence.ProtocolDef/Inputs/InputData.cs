// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Coherence.Debugging;
    using Coherence.Entities;
    using Coherence.Log;
    using Coherence.Schema;
    using Coherence.SimulationFrame;

    public struct InputData : IDisposable, IMessageData<InputData>
    {
        private ProtocolInputDefinition definition;
        private byte[] data;
        private ManagedTypesTables managedTypesTables;
        private OnInputDataDisposed onDisposedCallback;
        private OnOwnershipChanged onOwnershipChangedCallback;

        public Entity Entity { get; set; }
        public MessageTarget MessageTarget { get; set; }
        public AbsoluteSimulationFrame Frame { get; set; }

        public uint SenderParticipant { get; set; }
        public ChannelID ChannelID { get; set; }

        public bool IsInitialized => definition != null;
        public ProtocolInputDefinition Definition => definition;
        public uint InputID => definition.InputID;
        public uint TypeID => InputID;
        public int FieldCount => definition.Fields.Count;

        public FieldIterator<InputData> Fields => new(this);
        public FieldIterator<InputData> EntityFields => new(this, FieldIterator<InputData>.EntityFieldFilter);

        internal InputData(ProtocolInputDefinition definition, ManagedTypesTables managedTypesTables, byte[] data,
            Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame,
            OnInputDataDisposed onDisposedCallback = null, OnOwnershipChanged onOwnershipChangedCallback = null)
        {
            this.definition = definition;
            this.data = data;
            this.managedTypesTables = managedTypesTables;
            this.Entity = entity;
            this.MessageTarget = messageTarget;
            this.Frame = frame;
            this.SenderParticipant = default;
            this.ChannelID = ChannelID.Default;
            this.onDisposedCallback = onDisposedCallback;
            this.onOwnershipChangedCallback = onOwnershipChangedCallback;
        }

        public FieldData GetField(int fieldIndex)
        {
            AssertInitialized();

            var fieldDefinition = GetFieldDefinition(fieldIndex);
            var fieldData = GetFieldDataSpan(fieldIndex);

            return new FieldData(fieldDefinition, managedTypesTables, fieldData, default, fieldIndex);
        }

        public InputData Clone(ISchemaDataFactory schemaDataFactory)
        {
            AssertInitialized();

            var newInput = schemaDataFactory.CreateInput(InputID, Entity, MessageTarget, Frame);
            newInput.SenderParticipant = SenderParticipant;
            newInput.ChannelID = ChannelID;

            foreach (var field in Fields)
            {
                var newField = newInput.GetField(field.Index);

                newField.CopyFrom(field);
            }

            return newInput;
        }

        public void NullEntityRefs(Entity entity)
        {
            AssertInitialized();
            MessageHelpers.NullEntityRefs(this, entity);
        }

        public IEntityMapper.Error MapToAbsolute(IEntityMapper mapper, Logger logger)
        {
            AssertInitialized();
            return MessageHelpers.MapToAbsolute(ref this, mapper, logger);
        }

        public IEntityMapper.Error MapToRelative(IEntityMapper mapper, Logger logger)
        {
            AssertInitialized();
            return MessageHelpers.MapToRelative(ref this, mapper, logger);
        }

        private Span<byte> GetFieldDataSpan(int fieldIndex)
        {
            var fieldDefinition = GetFieldDefinition(fieldIndex);
            return new Span<byte>(data, definition.FieldOffsets[fieldIndex], fieldDefinition.Size);
        }

        private ProtocolFieldDefinition GetFieldDefinition(int fieldIndex)
        {
            DbgAssert.ThatFmt(fieldIndex >= 0 && fieldIndex < definition.Fields.Count, "Field index {0} out of range", fieldIndex);
            return definition.Fields[fieldIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertInitialized()
        {
            DbgAssert.That(IsInitialized, "InputData is not initialized");
        }

        public override string ToString()
        {
            if (!IsInitialized)
            {
                return "Uninitialized InputData";
            }

            var sb = new StringBuilder();

            sb.Append(definition.Name);

            sb.Append("(");

            sb.Append("T: ");
            sb.Append(definition.InputID);

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

        public InputData ReleaseOwnership()
        {
            AssertInitialized();
            onOwnershipChangedCallback?.Invoke(data, null);

            return this;
        }

        public void Dispose()
        {
            if (!IsInitialized)
            {
                DbgAssert.That(false, "Trying to dispose an uninitialized or already disposed InputData");

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

            onDisposedCallback?.Invoke(this, data);

            // Make it uninitialized again so it can't be used after disposal
            definition = null;
            data = null;
        }
    }
}

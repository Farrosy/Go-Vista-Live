// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Coherence.Connection;
    using Coherence.Debugging;
    using Coherence.Entities;
    using Coherence.Log;
    using Coherence.Schema;

    public struct CommandData : IDisposable, IMessageData<CommandData>
    {
        private ProtocolCommandDefinition definition;
        private byte[] data;
        private ManagedTypesTables managedTypesTables;
        private OnCommandDataDisposed onDisposedCallback;
        private OnOwnershipChanged onOwnershipChangedCallback;

        public Entity Entity { get; set; }
        public MessageTarget MessageTarget { get; set; }
        public bool UsesMeta { get; set; }
        public ClientID SenderClientID { get; set; }
        public long Frame { get; set; }

        public uint SenderParticipant { get; set; }
        public ChannelID ChannelID { get; set; }

        public bool IsInitialized => definition != null;
        public ProtocolCommandDefinition Definition => definition;
        public uint CommandID => definition.CommandID;
        public uint TypeID => CommandID;
        public int FieldCount => definition.Fields.Count;

        public FieldIterator<CommandData> Fields => new(this);
        public FieldIterator<CommandData> EntityFields => new(this, FieldIterator<CommandData>.EntityFieldFilter);

        internal CommandData(ProtocolCommandDefinition definition, ManagedTypesTables managedTypesTables, byte[] data,
            Entity entity, MessageTarget messageTarget,
            bool usesMeta = default, ClientID senderClientID = default, long frame = default,
            OnCommandDataDisposed onDisposedCallback = null, OnOwnershipChanged onOwnershipChangedCallback = null)
        {
            this.definition = definition;
            this.data = data;
            this.managedTypesTables = managedTypesTables;
            this.Entity = entity;
            this.MessageTarget = messageTarget;
            this.UsesMeta = usesMeta;
            this.SenderClientID = senderClientID;
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

        public CommandData Clone(ISchemaDataFactory schemaDataFactory)
        {
            AssertInitialized();

            var newCommand = schemaDataFactory.CreateCommand(CommandID, Entity, MessageTarget, UsesMeta, SenderClientID, Frame);
            newCommand.SenderParticipant = SenderParticipant;
            newCommand.ChannelID = ChannelID;

            foreach (var field in Fields)
            {
                var newField = newCommand.GetField(field.Index);

                newField.CopyFrom(field);
            }

            return newCommand;
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
            DbgAssert.That(IsInitialized, "CommandData is not initialized");
        }

        public override string ToString()
        {
            if (!IsInitialized)
            {
                return "Uninitialized CommandData";
            }

            var sb = new StringBuilder();

            sb.Append(definition.Name);

            sb.Append("(");

            sb.Append("T: ");
            sb.Append(definition.CommandID);

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

        public CommandData ReleaseOwnership()
        {
            AssertInitialized();
            onOwnershipChangedCallback?.Invoke(data, null);

            return this;
        }

        public void Dispose()
        {
            if (!IsInitialized)
            {
                DbgAssert.That(false, "Trying to dispose an uninitialized or already disposed CommandData");

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

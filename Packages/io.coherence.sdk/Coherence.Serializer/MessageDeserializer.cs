namespace Coherence.Serializer
{
    using System.Collections.Generic;
    using System.IO;
    using Coherence.Brook;
    using Coherence.Connection;
    using Coherence.Log;
    using Coherence.ProtocolDef;

    public interface IMessageDeserializer
    {
        void ReadOrderedCommands(IInBitStream bitStream, List<(MessageID, CommandData)> deserializedMessages);
        void ReadCommands(IInBitStream bitStream, List<CommandData> buffer);
        void ReadInputs(IInBitStream bitStream, List<InputData> buffer);
    }

    public class MessageDeserializer : IMessageDeserializer
    {
        private readonly ISchemaDataFactory schemaDataFactory;
        private readonly Logger logger;

        public MessageDeserializer(ISchemaDataFactory schemaDataFactory, Logger logger)
        {
            this.schemaDataFactory = schemaDataFactory;
            this.logger = logger.With<MessageDeserializer>();
        }

        public void ReadOrderedCommands(IInBitStream bitStream, List<(MessageID, CommandData)> deserializedMessages)
        {
            deserializedMessages.Clear();

            ushort lastMessageID = 0;
            try
            {
                while (DeserializeCommands.DeserializeCommand(bitStream, out var messageType))
                {
                    if (messageType != MessageType.Command)
                    {
                        throw new InvalidDataException($"Unexpected message type '{messageType}'");
                    }

                    var id = ReadNextMessageID(bitStream, lastMessageID);
                    var command = ReadCommand(bitStream);
                    command.TakeOwnership("MessageDeserializer.ReadOrderedCommands");

                    deserializedMessages.Add((id, command.ReleaseOwnership()));
                    lastMessageID = id.Value;
                }
            }
            catch
            {
                foreach (var (_, command) in deserializedMessages)
                {
                    command.Dispose();
                }

                throw;
            }
        }

        public void ReadCommands(IInBitStream bitStream, List<CommandData> buffer)
        {
            buffer.Clear();

            var numMessages = bitStream.ReadUint8();

            try
            {
                for (var i = 0; i < numMessages; i++)
                {
                    var command = ReadCommand(bitStream);
                    command.TakeOwnership("MessageDeserializer.ReadCommands");

                    buffer.Add(command.ReleaseOwnership());
                }
            }
            catch
            {
                foreach (var cmd in buffer)
                {
                    if (cmd.IsInitialized)
                    {
                        cmd.Dispose();
                    }
                }

                throw;
            }
        }

        public CommandData ReadCommand(IInBitStream bitStream)
        {
            var entityID = DeserializerTools.DeserializeEntity(bitStream);
            var messageTarget = DeserializeMessageTarget(bitStream);
            var commandID = DeserializerTools.DeserializeComponentTypeID(bitStream);
            var meta = DeserializeCommandMeta(bitStream);

            var senderID = meta.HasValue ? meta.Value.SenderID : 0;
            var frame = meta.HasValue ? meta.Value.Frame : 0;
            var command = schemaDataFactory.CreateCommand(commandID, entityID, messageTarget,
                meta.HasValue, new ClientID(senderID), frame);
            command.TakeOwnership("MessageDeserializer.ReadCommand");

            try
            {
                var inBitStream = new InProtocolBitStream(bitStream);

                foreach (var field in command.Fields)
                {
                    FieldSerializer.DeserializeFieldData(inBitStream, field);
                }

                return command.ReleaseOwnership();
            }
            catch
            {
                command.Dispose();
                throw;
            }
        }

        public void ReadInputs(IInBitStream bitStream, List<InputData> buffer)
        {
            buffer.Clear();

            var numMessages = bitStream.ReadUint8();

            try
            {
                for (var i = 0; i < numMessages; i++)
                {
                    var input = ReadInput(bitStream);
                    input.TakeOwnership("MessageDeserializer.ReadInputs");

                    buffer.Add(input.ReleaseOwnership());
                }
            }
            catch
            {
                foreach (var input in buffer)
                {
                    if (input.IsInitialized)
                    {
                        input.Dispose();
                    }
                }

                throw;
            }
        }

        public InputData ReadInput(IInBitStream bitStream)
        {
            var entityID = DeserializerTools.DeserializeEntity(bitStream);
            var messageTarget = DeserializeMessageTarget(bitStream);
            var componentType = DeserializerTools.DeserializeComponentTypeID(bitStream);

            var inBitStream = new InProtocolBitStream(bitStream);
            var frame = (long)bitStream.ReadUint64();

            var input = schemaDataFactory.CreateInput(componentType, entityID, messageTarget, frame);
            input.TakeOwnership("MessageDeserializer.ReadInput");

            try
            {
                foreach (var field in input.Fields)
                {
                    FieldSerializer.DeserializeFieldData(inBitStream, field);
                }

                return input.ReleaseOwnership();
            }
            catch
            {
                input.Dispose();
                throw;
            }
        }

        // ReadNextMessageID reads and uses VarInt decoding to get the next message id in the sequence based in the last read value.
        private MessageID ReadNextMessageID(IInBitStream bitstream, ushort lastID)
        {
            var delta = DeserializerTools.ReadShortVarInt(bitstream);
            var sign = bitstream.ReadBits(1);
            var index = (uint)(lastID + (delta * (sign == 1 ? 1 : -1)));

            return new MessageID((ushort)index);
        }

        private RawCommandMeta? DeserializeCommandMeta(IInBitStream inBitStream)
        {
            uint senderID = 0;
            long frame = 0;

            var hasMeta = inBitStream.ReadBits(1) == 1;
            if (!hasMeta)
            {
                return null;
            }

            var isSenderIDPresent = inBitStream.ReadBits(1) == 1;
            if (isSenderIDPresent)
            {
                senderID = inBitStream.ReadUint32();
            }

            frame = (long)inBitStream.ReadUint64();

            return new RawCommandMeta
            {
                SenderID = senderID,
                Frame = frame
            };
        }

        private MessageTarget DeserializeMessageTarget(IInBitStream inBitStream)
        {
            return (MessageTarget)inBitStream.ReadBits(2);
        }
    }
}

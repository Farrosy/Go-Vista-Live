// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;

    public struct UShortCommand
    {
        public const uint CommandID = 12;

        public CommandData Data { get; private set; }

        public ushort Value
        {
            get => Data.GetField(0).GetUInt16();
            set => Data.GetField(0).SetUInt16(value);
        }

        private UShortCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static UShortCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget,
            ushort value)
        {
            var commandData = schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
            var command = From(commandData);
            command.Value = value;

            return commandData;
        }
    }
}

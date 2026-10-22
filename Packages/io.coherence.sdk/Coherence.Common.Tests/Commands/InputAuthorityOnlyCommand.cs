// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;

    public struct InputAuthorityOnlyCommand
    {
        public const uint CommandID = 14;

        public CommandData Data { get; private set; }

        private InputAuthorityOnlyCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static InputAuthorityOnlyCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget)
            => schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;

    public struct StateAuthorityOnlyCommand
    {
        public const uint CommandID = 13;

        public CommandData Data { get; private set; }

        private StateAuthorityOnlyCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static StateAuthorityOnlyCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget)
            => schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
    }
}

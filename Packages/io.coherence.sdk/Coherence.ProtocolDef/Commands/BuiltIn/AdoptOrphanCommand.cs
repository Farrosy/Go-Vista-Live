// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Entities;

    public struct AdoptOrphanCommand
    {
        public const uint CommandID = 3;

        public CommandData Data { get; private set; }

        private AdoptOrphanCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static AdoptOrphanCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget)
            => schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
    }
}

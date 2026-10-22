// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Entities;

    public struct QuerySyncedCommand
    {
        public const uint CommandID = 2;

        public CommandData Data { get; private set; }

        public bool LiveQuerySynced
        {
            get => Data.GetField(0).GetBool();
            set => Data.GetField(0).SetBool(value);
        }

        public bool GlobalQuerySynced
        {
            get => Data.GetField(1).GetBool();
            set => Data.GetField(1).SetBool(value);
        }

        private QuerySyncedCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static QuerySyncedCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget,
            bool liveQuerySynced, bool globalQuerySynced)
        {
            var commandData = schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
            var command = From(commandData);
            command.LiveQuerySynced = liveQuerySynced;
            command.GlobalQuerySynced = globalQuerySynced;

            return commandData;
        }
    }
}

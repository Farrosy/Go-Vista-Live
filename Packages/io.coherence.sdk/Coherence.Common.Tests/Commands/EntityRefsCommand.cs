// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;

    public struct EntityRefsCommand
    {
        public const uint CommandID = 10;

        public CommandData Data { get; private set; }

        public Entity EntityRef1
        {
            get => Data.GetField(0).GetEntity();
            set => Data.GetField(0).SetEntity(value);
        }

        public Entity EntityRef2
        {
            get => Data.GetField(1).GetEntity();
            set => Data.GetField(1).SetEntity(value);
        }

        private EntityRefsCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static EntityRefsCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget,
            Entity entityRef1, Entity entityRef2)
        {
            var commandData = schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
            var command = From(commandData);
            command.EntityRef1 = entityRef1;
            command.EntityRef2 = entityRef2;

            return commandData;
        }
    }
}

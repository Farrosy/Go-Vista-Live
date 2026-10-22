// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Entities;

    public struct SceneIndexChangedCommand
    {
        public const uint CommandID = 5;

        public CommandData Data { get; private set; }

        public int SceneIndex
        {
            get => Data.GetField(0).GetInt();
            set => Data.GetField(0).SetInt(value);
        }

        private SceneIndexChangedCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static SceneIndexChangedCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget,
            int sceneIndex)
        {
            var commandData = schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
            var command = From(commandData);
            command.SceneIndex = sceneIndex;

            return commandData;
        }
    }
}

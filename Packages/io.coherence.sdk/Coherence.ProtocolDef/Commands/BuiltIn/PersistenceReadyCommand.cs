// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct PersistenceReadyCommand
    {
        public const uint CommandID = 4;

        public CommandData Data { get; private set; }

        private PersistenceReadyCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static PersistenceReadyCommand From(CommandData commandData) => new(commandData);
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Connection;
    using Coherence.Entities;

    public struct AuthorityRequestCommand
    {
        public const uint CommandID = 0;

        public CommandData Data { get; private set; }

        public ClientID Requester
        {
            get => (ClientID)Data.GetField(0).GetUInt();
            set => Data.GetField(0).SetUInt((uint)value);
        }

        public AuthorityType AuthorityType
        {
            get => (AuthorityType)Data.GetField(1).GetInt();
            set => Data.GetField(1).SetInt((int)value);
        }

        private AuthorityRequestCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static AuthorityRequestCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget,
            ClientID requester, AuthorityType authorityType)
        {
            var commandData = schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
            var command = From(commandData);
            command.Requester = requester;
            command.AuthorityType = authorityType;

            return commandData;
        }
    }
}

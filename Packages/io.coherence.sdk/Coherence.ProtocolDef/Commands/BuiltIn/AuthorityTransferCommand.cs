// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Connection;
    using Coherence.Entities;

    public struct AuthorityTransferCommand
    {
        public const uint CommandID = 1;

        public CommandData Data { get; private set; }

        public ClientID NewAuthority
        {
            get => (ClientID)Data.GetField(0).GetUInt();
            set => Data.GetField(0).SetUInt((uint)value);
        }

        public bool Accepted
        {
            get => Data.GetField(1).GetBool();
            set => Data.GetField(1).SetBool(value);
        }

        public AuthorityType AuthorityType
        {
            get => (AuthorityType)Data.GetField(2).GetInt();
            set => Data.GetField(2).SetInt((int)value);
        }

        private AuthorityTransferCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static AuthorityTransferCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget,
            ClientID newAuthority, bool accepted, AuthorityType authorityType)
        {
            var commandData = schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
            var command = From(commandData);
            command.NewAuthority = newAuthority;
            command.Accepted = accepted;
            command.AuthorityType = authorityType;

            return commandData;
        }
    }
}

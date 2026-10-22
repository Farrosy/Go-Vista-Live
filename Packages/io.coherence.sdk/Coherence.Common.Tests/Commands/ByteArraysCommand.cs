// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;

    public struct ByteArraysCommand
    {
        public const uint CommandID = 11;

        public CommandData Data { get; private set; }

        public byte[] Bytes1
        {
            get => Data.GetField(0).GetBytes();
            set => Data.GetField(0).SetBytes(value);
        }

        public byte[] Bytes2
        {
            get => Data.GetField(1).GetBytes();
            set => Data.GetField(1).SetBytes(value);
        }

        public byte[] Bytes3
        {
            get => Data.GetField(2).GetBytes();
            set => Data.GetField(2).SetBytes(value);
        }

        public byte[] Bytes4
        {
            get => Data.GetField(3).GetBytes();
            set => Data.GetField(3).SetBytes(value);
        }

        public byte[] Bytes5
        {
            get => Data.GetField(4).GetBytes();
            set => Data.GetField(4).SetBytes(value);
        }

        public byte[] Bytes6
        {
            get => Data.GetField(5).GetBytes();
            set => Data.GetField(5).SetBytes(value);

        }

        private ByteArraysCommand(CommandData commandData)
        {
            if (commandData.CommandID != CommandID)
            {
                throw new System.InvalidOperationException($"Invalid command ID: {commandData.CommandID}. Expected: {CommandID}");
            }

            this.Data = commandData;
        }

        public static ByteArraysCommand From(CommandData commandData) => new(commandData);

        public static CommandData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget,
            byte[] bytes1, byte[] bytes2, byte[] bytes3, byte[] bytes4, byte[] bytes5, byte[] bytes6)
        {
            var commandData = schemaDataFactory.CreateCommand(CommandID, entity, messageTarget);
            var command = From(commandData);
            command.Bytes1 = bytes1;
            command.Bytes2 = bytes2;
            command.Bytes3 = bytes3;
            command.Bytes4 = bytes4;
            command.Bytes5 = bytes5;
            command.Bytes6 = bytes6;

            return commandData;
        }
    }
}

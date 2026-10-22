// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.SimulationFrame;

    public struct ByteArraysInput
    {
        public const uint InputID = 9;

        public InputData Data { get; private set; }

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

        private ByteArraysInput(InputData inputData)
        {
            if (inputData.InputID != InputID)
            {
                throw new System.InvalidOperationException($"Invalid input ID: {inputData.InputID}. Expected: {InputID}");
            }

            this.Data = inputData;
        }

        public static ByteArraysInput From(InputData inputData) => new(inputData);

        public static InputData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame,
            byte[] bytes1, byte[] bytes2, byte[] bytes3, byte[] bytes4, byte[] bytes5, byte[] bytes6)
        {
            var inputData = schemaDataFactory.CreateInput(InputID, entity, messageTarget, frame);
            var input = From(inputData);
            input.Bytes1 = bytes1;
            input.Bytes2 = bytes2;
            input.Bytes3 = bytes3;
            input.Bytes4 = bytes4;
            input.Bytes5 = bytes5;
            input.Bytes6 = bytes6;

            return inputData;
        }
    }
}

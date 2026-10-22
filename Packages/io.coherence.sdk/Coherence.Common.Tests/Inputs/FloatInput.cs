// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.SimulationFrame;

    public struct FloatInput
    {
        public const uint InputID = 2;

        public InputData Data { get; private set; }

        public float FloatField
        {
            get => Data.GetField(0).GetFloat();
            set => Data.GetField(0).SetFloat(value);
        }

        private FloatInput(InputData inputData)
        {
            if (inputData.InputID != InputID)
            {
                throw new System.InvalidOperationException($"Invalid input ID: {inputData.InputID}. Expected: {InputID}");
            }

            this.Data = inputData;
        }

        public static FloatInput From(InputData inputData) => new(inputData);

        public static InputData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame,
            float floatField)
        {
            var inputData = schemaDataFactory.CreateInput(InputID, entity, messageTarget, frame);
            var input = From(inputData);
            input.FloatField = floatField;

            return inputData;
        }
    }
}

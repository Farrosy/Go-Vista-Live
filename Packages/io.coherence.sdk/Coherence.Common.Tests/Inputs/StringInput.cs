// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.SimulationFrame;

    public struct StringInput
    {
        public const uint InputID = 6;

        public InputData Data { get; private set; }

        public string StringField
        {
            get => Data.GetField(0).GetString();
            set => Data.GetField(0).SetString(value);
        }

        private StringInput(InputData inputData)
        {
            if (inputData.InputID != InputID)
            {
                throw new System.InvalidOperationException($"Invalid input ID: {inputData.InputID}. Expected: {InputID}");
            }

            this.Data = inputData;
        }

        public static StringInput From(InputData inputData) => new(inputData);

        public static InputData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame,
            string stringField)
        {
            var inputData = schemaDataFactory.CreateInput(InputID, entity, messageTarget, frame);
            var input = From(inputData);
            input.StringField = stringField;

            return inputData;
        }
    }
}

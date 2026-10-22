// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using System.Numerics;
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.SimulationFrame;

    public struct MultiInput
    {
        public const uint InputID = 7;

        public InputData Data { get; private set; }

        public int IntField
        {
            get => Data.GetField(0).GetInt();
            set => Data.GetField(0).SetInt(value);
        }

        public float FloatField
        {
            get => Data.GetField(1).GetFloat();
            set => Data.GetField(1).SetFloat(value);
        }

        public Vector2 AxisField
        {
            get => Data.GetField(2).GetVector2();
            set => Data.GetField(2).SetVector2(value);
        }

        public string StringField
        {
            get => Data.GetField(3).GetString();
            set => Data.GetField(3).SetString(value);
        }

        private MultiInput(InputData inputData)
        {
            if (inputData.InputID != InputID)
            {
                throw new System.InvalidOperationException($"Invalid input ID: {inputData.InputID}. Expected: {InputID}");
            }

            this.Data = inputData;
        }

        public static MultiInput From(InputData inputData) => new(inputData);

        public static InputData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame,
            int intField, float floatField, Vector2 axisField, string stringField)
        {
            var inputData = schemaDataFactory.CreateInput(InputID, entity, messageTarget, frame);
            var input = From(inputData);
            input.IntField = intField;
            input.FloatField = floatField;
            input.AxisField = axisField;
            input.StringField = stringField;

            return inputData;
        }
    }
}

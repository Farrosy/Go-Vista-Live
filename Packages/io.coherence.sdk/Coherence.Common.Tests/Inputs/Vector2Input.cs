// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using System.Numerics;
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.SimulationFrame;

    public struct Vector2Input
    {
        public const uint InputID = 4;

        public InputData Data { get; private set; }

        public Vector2 Vector2Field
        {
            get => Data.GetField(0).GetVector2();
            set => Data.GetField(0).SetVector2(value);
        }

        private Vector2Input(InputData inputData)
        {
            if (inputData.InputID != InputID)
            {
                throw new System.InvalidOperationException($"Invalid input ID: {inputData.InputID}. Expected: {InputID}");
            }

            this.Data = inputData;
        }

        public static Vector2Input From(InputData inputData) => new(inputData);

        public static InputData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame,
            Vector2 vector2Field)
        {
            var inputData = schemaDataFactory.CreateInput(InputID, entity, messageTarget, frame);
            var input = From(inputData);
            input.Vector2Field = vector2Field;

            return inputData;
        }
    }
}

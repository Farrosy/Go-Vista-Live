// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.SimulationFrame;

    public struct Vector3Input
    {
        public const uint InputID = 5;

        public InputData Data { get; private set; }

        public Vector3d Vector3Field
        {
            get => Data.GetField(0).GetVector3();
            set => Data.GetField(0).SetVector3(value);
        }

        private Vector3Input(InputData inputData)
        {
            if (inputData.InputID != InputID)
            {
                throw new System.InvalidOperationException($"Invalid input ID: {inputData.InputID}. Expected: {InputID}");
            }

            this.Data = inputData;
        }

        public static Vector3Input From(InputData inputData) => new(inputData);

        public static InputData Create(ISchemaDataFactory schemaDataFactory, Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame,
            Vector3d vector3Field)
        {
            var inputData = schemaDataFactory.CreateInput(InputID, entity, messageTarget, frame);
            var input = From(inputData);
            input.Vector3Field = vector3Field;

            return inputData;
        }
    }
}

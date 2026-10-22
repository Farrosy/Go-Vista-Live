// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.ProtocolDef;
    using Coherence.SimulationFrame;

    public struct SimFramesComponent
    {
        public const uint ComponentID = 23;

        public ComponentData Data { get; private set; }

        public int SimFrameIntValue
        {
            get => Data.GetField(0).GetInt();
            set => Data.GetField(0).SetInt(value);
        }

        public bool IsSimFrameIntValueSet
        {
            get => Data.IsFieldSet(0);
            set => Data = Data.SetFieldSet(0, value);
        }

        public AbsoluteSimulationFrame SimFrameIntValueSimFrame
        {
            get => Data.GetField(0).GetSimFrame();
            set => Data.GetField(0).SetSimFrame(value);
        }

        public float FloatValue
        {
            get => Data.GetField(1).GetFloat();
            set => Data.GetField(1).SetFloat(value);
        }

        public bool IsFloatValueSet
        {
            get => Data.IsFieldSet(1);
            set => Data = Data.SetFieldSet(1, value);
        }

        public float SimFrameFloatValue
        {
            get => Data.GetField(2).GetFloat();
            set => Data.GetField(2).SetFloat(value);
        }

        public bool IsSimFrameFloatValueSet
        {
            get => Data.IsFieldSet(2);
            set => Data = Data.SetFieldSet(2, value);
        }

        public AbsoluteSimulationFrame SimFrameFloatValueSimFrame
        {
            get => Data.GetField(2).GetSimFrame();
            set => Data.GetField(2).SetSimFrame(value);
        }

        private SimFramesComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static SimFramesComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, int simFrameIntValue, AbsoluteSimulationFrame simFrameIntValueSimFrame,
            float floatValue, float simFrameFloatValue, AbsoluteSimulationFrame simFrameFloatValueSimFrame)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.SimFrameIntValue = simFrameIntValue;
            component.SimFrameIntValueSimFrame = simFrameIntValueSimFrame;
            component.FloatValue = floatValue;
            component.SimFrameFloatValue = simFrameFloatValue;
            component.SimFrameFloatValueSimFrame = simFrameFloatValueSimFrame;

            return componentData;
        }
    }
}

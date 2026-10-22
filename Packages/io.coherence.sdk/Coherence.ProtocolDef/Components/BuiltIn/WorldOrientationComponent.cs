// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System.Numerics;
    using Coherence.SimulationFrame;

    public struct WorldOrientationComponent
    {
        public const uint ComponentID = 1;

        public ComponentData Data { get; private set; }

        public Quaternion Value
        {
            get => Data.GetField(0).GetQuaternion();
            set => Data.GetField(0).SetQuaternion(value);
        }

        public AbsoluteSimulationFrame ValueSimFrame
        {
            get => Data.GetField(0).GetSimFrame();
            set => Data.GetField(0).SetSimFrame(value);
        }

        private WorldOrientationComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static WorldOrientationComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, Quaternion value, AbsoluteSimulationFrame valueSimFrame)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Value = value;
            component.ValueSimFrame = valueSimFrame;

            return componentData;
        }
    }
}

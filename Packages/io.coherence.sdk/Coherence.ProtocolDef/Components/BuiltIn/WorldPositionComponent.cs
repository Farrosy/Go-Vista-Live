// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System.Numerics;
    using Coherence.Common;
    using Coherence.SimulationFrame;

    public struct WorldPositionComponent
    {
        public const uint ComponentID = 0;

        public ComponentData Data { get; private set; }

        public Vector3d Value
        {
            get => Data.GetField(0).GetVector3();
            set => Data.GetField(0).SetVector3(value);
        }

        public bool IsValueSet => Data.IsFieldSet(0);

        public AbsoluteSimulationFrame ValueSimFrame
        {
            get => Data.GetField(0).GetSimFrame();
            set => Data.GetField(0).SetSimFrame(value);
        }

        private WorldPositionComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static WorldPositionComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, Vector3d value, AbsoluteSimulationFrame valueSimFrame)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Value = value;
            component.ValueSimFrame = valueSimFrame;

            return componentData;
        }

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, Vector3 value, AbsoluteSimulationFrame valueSimFrame)
        {
            return Create(schemaDataFactory, value.ToVector3d(), valueSimFrame);
        }
    }
}

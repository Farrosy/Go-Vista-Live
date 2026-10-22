// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using Coherence.ProtocolDef;
    using UnityEngine;

    public struct GenericScaleComponent
    {
        public const uint ComponentID = 17;

        public ComponentData Data { get; private set; }

        public Vector3 Value
        {
            get => Data.GetField(0).GetVector3().ToUnityVector3();
            set => Data.GetField(0).SetVector3(value.ToVector3d());
        }

        private GenericScaleComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static GenericScaleComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, Vector3 value)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Value = value;

            return componentData;
        }
    }
}

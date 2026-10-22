// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.ProtocolDef;

    public struct FloatComponent
    {
        public const uint ComponentID = 19;

        public ComponentData Data { get; private set; }

        public float Number
        {
            get => Data.GetField(0).GetFloat();
            set => Data.GetField(0).SetFloat(value);
        }

        private FloatComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static FloatComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, float number)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Number = number;

            return componentData;
        }
    }
}

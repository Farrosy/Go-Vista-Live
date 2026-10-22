// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.ProtocolDef;

    public struct IntComponent
    {
        public const uint ComponentID = 18;

        public ComponentData Data { get; private set; }

        public int Number
        {
            get => Data.GetField(0).GetInt();
            set => Data.GetField(0).SetInt(value);
        }

        private IntComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static IntComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, int number)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Number = number;

            return componentData;
        }
    }
}

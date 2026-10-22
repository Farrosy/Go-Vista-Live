// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.ProtocolDef;

    public struct Ordered2Component
    {
        public const uint ComponentID = 21;

        public ComponentData Data { get; private set; }

        private Ordered2Component(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static Ordered2Component From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory) => schemaDataFactory.CreateComponent(ComponentID);
    }
}

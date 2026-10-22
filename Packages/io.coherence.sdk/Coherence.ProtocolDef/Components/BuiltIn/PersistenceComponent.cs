// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct PersistenceComponent
    {
        public const uint ComponentID = 5;

        public ComponentData Data { get; private set; }

        private PersistenceComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static PersistenceComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory) => schemaDataFactory.CreateComponent(ComponentID);
    }
}

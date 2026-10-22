// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct PreserveChildrenComponent
    {
        public const uint ComponentID = 14;

        public ComponentData Data { get; private set; }

        private PreserveChildrenComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static PreserveChildrenComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory) => schemaDataFactory.CreateComponent(ComponentID);
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct ArchetypeComponent
    {
        public const uint ComponentID = 4;

        public ComponentData Data { get; private set; }

        public int Index
        {
            get => Data.GetField(0).GetInt();
            set => Data.GetField(0).SetInt(value);
        }

        public bool IsIndexSet => Data.IsFieldSet(0);

        private ArchetypeComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static ArchetypeComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, int index)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Index = index;

            return componentData;
        }
    }
}

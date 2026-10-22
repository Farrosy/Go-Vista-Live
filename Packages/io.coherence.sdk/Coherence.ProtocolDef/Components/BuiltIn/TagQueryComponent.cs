// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct TagQueryComponent
    {
        public const uint ComponentID = 13;

        public ComponentData Data { get; private set; }

        public string Tag
        {
            get => Data.GetField(0).GetString();
            set => Data.GetField(0).SetString(value);
        }

        public bool IsTagSet => Data.IsFieldSet(0);

        private TagQueryComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static TagQueryComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, string tag)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Tag = tag;

            return componentData;
        }
    }
}

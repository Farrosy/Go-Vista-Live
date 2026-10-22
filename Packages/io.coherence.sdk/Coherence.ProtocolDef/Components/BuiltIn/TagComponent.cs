// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct TagComponent
    {
        public const uint ComponentID = 12;

        public ComponentData Data { get; private set; }

        public string Tag
        {
            get => Data.GetField(0).GetString();
            set => Data.GetField(0).SetString(value);
        }

        public bool IsTagSet => Data.IsFieldSet(0);

        private TagComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static TagComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory factory, string tag)
        {
            var componentData = factory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Tag = tag;

            return componentData;
        }
    }
}

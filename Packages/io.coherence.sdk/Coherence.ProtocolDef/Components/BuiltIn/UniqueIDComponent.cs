// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct UniqueIDComponent
    {
        public const uint ComponentID = 7;

        public ComponentData Data { get; private set; }

        public string UUID
        {
            get => Data.GetField(0).GetString();
            set => Data.GetField(0).SetString(value);
        }

        public bool IsUUIDSet => Data.IsFieldSet(0);

        private UniqueIDComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static UniqueIDComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, string uuid)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.UUID = uuid;

            return componentData;
        }
    }
}

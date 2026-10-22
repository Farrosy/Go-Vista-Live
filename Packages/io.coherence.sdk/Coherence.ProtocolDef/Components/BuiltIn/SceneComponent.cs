// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct SceneComponent
    {
        public const uint ComponentID = 15;

        public ComponentData Data { get; private set; }

        public uint Value
        {
            get => Data.GetField(0).GetUInt();
            set => Data.GetField(0).SetUInt(value);
        }

        private SceneComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static SceneComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, uint value)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);

            component.Value = value;

            return componentData;
        }
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using Coherence.ProtocolDef;

    public struct AssetIDComponent
    {
        public const uint ComponentID = 16;

        public ComponentData Data { get; private set; }

        public int Value
        {
            get => Data.GetField(0).GetInt();
            set => Data.GetField(0).SetInt(value);
        }

        public bool IsFromGroup
        {
            get => Data.GetField(1).GetBool();
            set => Data.GetField(1).SetBool(value);
        }

        private AssetIDComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static AssetIDComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, int value, bool isFromGroup)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Value = value;
            component.IsFromGroup = isFromGroup;

            return componentData;
        }
    }
}

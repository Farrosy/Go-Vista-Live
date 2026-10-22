// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct ConnectionSceneComponent
    {
        public const uint ComponentID = 9;

        public ComponentData Data { get; private set; }

        public uint Value
        {
            get => Data.GetField(0).GetUInt();
            set => Data.GetField(0).SetUInt(value);
        }

        public bool IsValueSet => Data.IsFieldSet(0);

        private ConnectionSceneComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static ConnectionSceneComponent From(ComponentData componentData) => new(componentData);

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

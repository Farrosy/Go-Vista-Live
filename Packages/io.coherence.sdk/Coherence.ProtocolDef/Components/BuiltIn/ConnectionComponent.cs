// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Connection;

    public struct ConnectionComponent
    {
        public const uint ComponentID = 8;

        public ComponentData Data { get; private set; }

        public ClientID ID
        {
            get => (ClientID)Data.GetField(0).GetUInt();
            set => Data.GetField(0).SetUInt((uint)value);
        }

        public ConnectionType Type
        {
            get => (ConnectionType)Data.GetField(1).GetInt();
            set => Data.GetField(1).SetInt((int)value);
        }

        private ConnectionComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static ConnectionComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, ClientID id, ConnectionType type)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);

            component.ID = id;
            component.Type = type;

            return componentData;
        }
    }
}

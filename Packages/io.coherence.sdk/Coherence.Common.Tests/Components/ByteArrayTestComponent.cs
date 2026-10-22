// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.ProtocolDef;

    public struct ByteArrayTestComponent
    {
        public const uint ComponentID = 17;

        public ComponentData Data { get; private set; }

        public byte[] Bytes
        {
            get => Data.GetField(0).GetBytes();
            set => Data.GetField(0).SetBytes(value);
        }

        private ByteArrayTestComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static ByteArrayTestComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, byte[] bytes)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Bytes = bytes;

            return componentData;
        }
    }
}

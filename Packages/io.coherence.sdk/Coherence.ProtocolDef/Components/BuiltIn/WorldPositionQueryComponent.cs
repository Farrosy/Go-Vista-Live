// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System.Numerics;
    using Coherence.Common;

    public struct WorldPositionQueryComponent
    {
        public const uint ComponentID = 3;

        public ComponentData Data { get; private set; }

        public Vector3d Position
        {
            get => Data.GetField(0).GetVector3();
            set => Data.GetField(0).SetVector3(value);
        }

        public bool IsPositionSet => Data.IsFieldSet(0);

        public float Radius
        {
            get => Data.GetField(1).GetFloat();
            set => Data.GetField(1).SetFloat(value);
        }

        public bool IsRadiusSet => Data.IsFieldSet(1);

        public float Buffer
        {
            get => Data.GetField(2).GetFloat();
            set => Data.GetField(2).SetFloat(value);
        }

        public bool IsBufferSet => Data.IsFieldSet(2);

        private WorldPositionQueryComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static WorldPositionQueryComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, Vector3d position, float radius, float buffer)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Position = position;
            component.Radius = radius;
            component.Buffer = buffer;

            return componentData;
        }

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, Vector3 position, float radius, float buffer)
        {
            return Create(schemaDataFactory, position.ToVector3d(), radius, buffer);
        }
    }
}

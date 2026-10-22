// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System.Numerics;
    using Coherence.Common;
    using Coherence.Entities;

    public struct ConnectedEntityComponent
    {
        public const uint ComponentID = 6;

        public ComponentData Data { get; private set; }

        public Entity Parent
        {
            get => Data.GetField(0).GetEntity();
            set => Data.GetField(0).SetEntity(value);
        }

        public bool IsParentSet => Data.IsFieldSet(0);

        public Vector3d Position
        {
            get => Data.GetField(1).GetVector3();
            set => Data.GetField(1).SetVector3(value);
        }

        public Quaternion Rotation
        {
            get => Data.GetField(2).GetQuaternion();
            set => Data.GetField(2).SetQuaternion(value);
        }

        public Vector3d Scale
        {
            get => Data.GetField(3).GetVector3();
            set => Data.GetField(3).SetVector3(value);
        }

        private ConnectedEntityComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static ConnectedEntityComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, Entity parent, Vector3d position, Quaternion rotation, Vector3d scale)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.Parent = parent;
            component.Position = position;
            component.Rotation = rotation;
            component.Scale = scale;

            return componentData;
        }

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, Entity parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return Create(schemaDataFactory, parent, position.ToVector3d(), rotation, scale.ToVector3d());
        }
    }
}

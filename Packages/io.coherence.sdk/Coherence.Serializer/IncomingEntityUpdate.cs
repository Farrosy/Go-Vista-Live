// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;

    public struct IncomingEntityUpdate
    {
        public EntityWithMeta Meta;
        public DeltaComponents Components;
        public ChannelID ChannelID;

        public Entity Entity => Meta.EntityId;
        public bool IsCreate => Meta.Operation == EntityOperation.Create;
        public bool IsDestroy => Meta.Operation == EntityOperation.Destroy;

        public static IncomingEntityUpdate New(int capacity = 0)
        {
            return new IncomingEntityUpdate
            {
                Components = DeltaComponents.New(capacity),
            };
        }

        public static IncomingEntityUpdate New(ISchemaDataFactory schemaDataFactory)
        {
            return new IncomingEntityUpdate
            {
                Components = DeltaComponents.New(schemaDataFactory),
            };
        }

        public void Merge(in IncomingEntityUpdate other)
        {
            other.TakeOwnershipOfComponents("IncomingEntityUpdate.Merge");

            var operation = Meta.Operation.Merge(other.Meta.Operation);

            Meta = other.Meta;
            Meta.Operation = operation;
            Components.Merge(other.Components.ReleaseOwnershipOfComponents());
        }

        public void TakeOwnershipOfComponents(string ownerIdentifier) => Components.TakeOwnershipOfComponents(ownerIdentifier);

        public IncomingEntityUpdate ReleaseOwnershipOfComponents()
        {
            _ = Components.ReleaseOwnershipOfComponents();
            return this;
        }

        public void Dispose()
        {
            Meta = default;
            Components.Dispose();
            Components = default;
        }

        public override string ToString()
        {
            return $"{nameof(Meta)}: [{Meta}], {nameof(Components)}: [{Components}]";
        }
    }
}

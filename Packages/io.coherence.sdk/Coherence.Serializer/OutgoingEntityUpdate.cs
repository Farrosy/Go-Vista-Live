// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using System.Collections.Concurrent;
    using Coherence.Debugging;
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using SimulationFrame;

    public struct OutgoingEntityUpdate
    {
        private static readonly ConcurrentBag<OutgoingEntityUpdate> Pool = new();

        public EntityOperation Operation;
        public long Priority;
        public DeltaComponents Components;
        private bool isInitialized;

        public bool IsDestroy => Operation == EntityOperation.Destroy;
        public bool IsCreate => Operation == EntityOperation.Create;
        public bool IsUpdate => Operation == EntityOperation.Update;

        public bool HasExistenceOperation =>
            Operation == EntityOperation.Create || Operation == EntityOperation.Destroy;

        public static OutgoingEntityUpdate New()
        {
            if (Pool.TryTake(out var instance))
            {
                instance.isInitialized = true;
                return instance;
            }

            return new OutgoingEntityUpdate
            {
                Operation = EntityOperation.Unknown,
                Priority = 0,
                Components = DeltaComponents.New(),
                isInitialized = true,
            };
        }

        public OutgoingEntityUpdate Clone(ISchemaDataFactory schemaDataFactory)
        {
            var clone = New();

            clone.Operation = Operation;
            clone.Priority = Priority;
            clone.Components.CloneFrom(Components, schemaDataFactory);

            return clone;
        }

        public void Return()
        {
            DbgAssert.That(isInitialized, "Returning an OutgoingEntityUpdate that is not initialized. It was probably returned twice!");

            Reset();
            Pool.Add(this);
            isInitialized = false;
        }

        private void Reset()
        {
            Operation = EntityOperation.Unknown;
            Priority = 0;
            Components.Reset();
        }

        /// <summary>
        /// Remove all own updated fields that are present in update
        /// This since we don't need to resend fields that have been changed afterwards.
        /// If current or newer entity update is a Destroy operator, return a Destroy.
        /// </summary>
        /// <param name="update">A newer entity update</param>
        public void Subtract(OutgoingEntityUpdate update, ProtocolDefinition schemaDefinition)
        {
            if (IsDestroy)
            {
                return;
            }

            if (update.IsDestroy)
            {
                Operation = update.Operation;
                return;
            }

            foreach (var destructs in update.Components.Destroys)
            {
                // remove updates for components that are removed
                if (Components.Updates.Store.Remove(destructs, out var comp))
                {
                    comp.Dispose();
                }

                // remove destructs that already exist
                Components.Destroys.Remove(destructs);
            }

            foreach (var (componentId, change) in update.Components.Updates.Store)
            {
                // remove updates for which updates already exists
                Components.Updates.ClearMask(change);

                // also remove all the removes in the changed state where the existing has an update to the
                // component since the component update overrides the remove.
                Components.Destroys.Remove(componentId);
            }

            if (!Components.ContainsOrderedComponent(schemaDefinition))
            {
                Components.OrderedUpdateTime = null;
            }
        }

        /// <summary>
        /// Add updated components to own collection.
        /// If an update exists in both, the update will overwrite the owned changes.
        /// If current or newer entity update is a Destroy operator, return a Destroy.
        /// </summary>
        /// <param name="update">A newer entity update</param>
        public void Add(OutgoingEntityUpdate update)
        {
            update.TakeOwnershipOfComponents("OutgoingEntityUpdate.Add");

            // Destroy always takes precedence
            if (IsDestroy)
            {
                update.Return();
                return;
            }

            if (update.IsDestroy)
            {
                Operation = update.Operation;
                update.Return();
                return;
            }

            Components.Merge(update.Components.ReleaseOwnershipOfComponents());

            // Dropped create overwrites update
            if (update.IsCreate)
            {
                Operation = update.Operation;
            }

            // We have the ownership of the update, so we can return it to the pool
            update.Return();
        }

        public void TakeOwnershipOfComponents(string context) => Components.TakeOwnershipOfComponents(context);

        public OutgoingEntityUpdate ReleaseOwnershipOfComponents()
        {
            _ = Components.ReleaseOwnershipOfComponents();

            return this;
        }

        public new string ToString()
        {
            return $"Op: {Operation} Prio: {Priority} Comps: {Components.ToString()}";
        }
    }
}

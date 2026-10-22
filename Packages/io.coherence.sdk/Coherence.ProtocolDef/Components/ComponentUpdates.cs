// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Collections.Generic;
    using Common;

    public struct ComponentUpdates : IDisposable
    {
        public ComponentMap Store { get; private set; }
        public int Count => Store.Count;
        public Vector3d FloatingOrigin;

        public static ComponentUpdates New()
        {
            return new ComponentUpdates()
            {
                Store = new ComponentMap()
            };
        }

        public static ComponentUpdates New(ISchemaDataFactory schemaDataFactory)
        {
            return new ComponentUpdates
            {
                Store = schemaDataFactory.CreateComponentMap()
            };
        }

        public static ComponentUpdates New(int capacity)
        {
            return new ComponentUpdates
            {
                Store = new ComponentMap(capacity),
            };
        }

        public static ComponentUpdates New(IDictionary<uint, ComponentData> components)
        {
            return new ComponentUpdates
            {
                Store = new ComponentMap(components),
            };
        }

        public static ComponentUpdates New(IReadOnlyList<ComponentData> data)
        {
            return new ComponentUpdates
            {
                Store = new ComponentMap(data),
            };
        }

        public static ComponentUpdates New(params ComponentData[] data)
        {
            return New((IReadOnlyList<ComponentData>)data);
        }

        public ComponentUpdates Clone(ISchemaDataFactory schemaDataFactory)
        {
            return new ComponentUpdates
            {
                Store = Store.Clone(schemaDataFactory),
                FloatingOrigin = FloatingOrigin,
            };
        }

        public void ClearMask(ComponentData comp)
        {
            var componentType = comp.ComponentID;
            if (!Store.TryGetValue(componentType, out var currentComp))
            {
                return;
            }

            currentComp.ClearFieldsMask(comp.FieldsMask);
            if (currentComp.FieldsMask == 0)
            {
                // Component has no fields to send. This happens when the same component with newer data
                // is already in flight so it's safe to simply remove it - no reason to send empty component.
                if (Store.Remove(componentType, out var removedComp))
                {
                    removedComp.Dispose();
                }
            }
            else
            {
                // also clear the stopped mask for removed fields
                // since there is a new update which means it is no
                // longer stopped in this update, but the existing update
                // might have a stop which should merge correctly.
                // This uses the comp.FieldsMask to know which fields have
                // new values in them to clear the stopped mask.
                currentComp.ClearStoppedMask(comp.FieldsMask);

                Store[componentType] = currentComp;
            }
        }

        public void Update(ComponentData comp)
        {
            comp.TakeOwnership("ComponentUpdates.Update");

            Store.Update(comp.ReleaseOwnership());
        }

        public void Reset() => Store?.Reset();
        public void Dispose()
        {
            Store.Dispose();
            Store = null;
        }

        public void Remove(uint componentType) => Store.Remove(componentType);
        public void Remove(IEnumerable<uint> componentTypes) => Store.Remove(componentTypes);
        public bool ContainsOrderedComponent() => Store.ContainsOrderedComponent();


        public void TakeOwnershipOfComponents(string ownerIdentifier) => Store.TakeOwnership(ownerIdentifier);
        public ComponentUpdates ReleaseOwnershipOfComponents()
        {
            _ = Store.ReleaseOwnership();
            return this;
        }

        public override string ToString() => $"Store: [{Store}], FO: {FloatingOrigin}";
    }
}

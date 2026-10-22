// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Coherence.Common;

    public struct DeltaComponents
    {
        public ComponentUpdates Updates;
        public ComponentDestroyMap Destroys;
        public int Count => Updates.Count + Destroys.Count;
        internal bool IsInitialized => Destroys != null;

        /// <summary>
        /// The time at which the last ordered component was updated at.
        /// Or null if the components were acked.
        /// </summary>
        public DateTime? OrderedUpdateTime;

        public static DeltaComponents New(int capacity = 16)
        {
            var deltaComponents = new DeltaComponents();
            deltaComponents.EnsureInitialized(capacity);
            return deltaComponents;
        }

        public static DeltaComponents New(ISchemaDataFactory schemaDataFactory)
        {
            var deltaComponents = new DeltaComponents();
            deltaComponents.EnsureInitialized(schemaDataFactory);
            return deltaComponents;
        }

        internal void EnsureInitialized(int capacity = 16)
        {
            if (IsInitialized)
            {
                return;
            }

            Updates = ComponentUpdates.New(capacity);
            Destroys = new ComponentDestroyMap(capacity / 4);
        }

        internal void EnsureInitialized(ISchemaDataFactory schemaDataFactory)
        {
            if (IsInitialized)
            {
                return;
            }

            Updates = ComponentUpdates.New(schemaDataFactory);
            Destroys = schemaDataFactory.CreateComponentDestroyMap();
        }

        public void CloneFrom(DeltaComponents other, ISchemaDataFactory schemaDataFactory)
        {
            foreach (var (compID, comp) in other.Updates.Store)
            {
                var clonedComp = comp.Clone(schemaDataFactory);
                clonedComp.TakeOwnership("DeltaComponents.CloneFrom");
                Updates.Store.Add(compID, clonedComp);
            }

            Destroys.UnionWith(other.Destroys);

            OrderedUpdateTime = MergeOrderedUpdateTime(OrderedUpdateTime, other.OrderedUpdateTime);
        }

        public DeltaComponents Clone(ISchemaDataFactory schemaDataFactory)
        {
            var clone = new DeltaComponents
            {
                Updates = Updates.Clone(schemaDataFactory),
                Destroys = Destroys.Clone(schemaDataFactory),
                OrderedUpdateTime = OrderedUpdateTime,
            };

            return clone;
        }

        public void UpdateComponent(ComponentData comp)
        {
            comp.TakeOwnership("DeltaComponents.UpdateComponent");

            Destroys.Remove(comp.ComponentID);
            Updates.Update(comp.ReleaseOwnership());
        }

        public void UpdateComponents(ComponentUpdates componentUpdates)
        {
            componentUpdates.TakeOwnershipOfComponents("DeltaComponents.UpdateComponents");

            var componentChanges = componentUpdates.Store.Sorted;
            for (var i = 0; i < componentChanges.Count; i++)
            {
                UpdateComponent(componentChanges[i].Value.ReleaseOwnership());
            }

            Updates.FloatingOrigin = MergeFloatingOrigin(Updates, componentUpdates);

            componentUpdates.Store.Clear();
        }

        public void RemoveComponent(uint comp)
        {
            Destroys.Add(comp);
            Updates.Remove(comp);
        }

        public void RemoveComponents(IEnumerable<uint> components)
        {
            foreach (var comp in components)
            {
                RemoveComponent(comp);
            }
        }

        public void Merge(DeltaComponents other)
        {
            other.TakeOwnershipOfComponents("DeltaComponents.Merge");

            foreach (var destructs in other.Destroys)
            {
                RemoveComponent(destructs);
            }

            foreach (var (_, comp) in other.Updates.Store)
            {
                UpdateComponent(comp.ReleaseOwnership());
            }

            OrderedUpdateTime = MergeOrderedUpdateTime(OrderedUpdateTime, other.OrderedUpdateTime);
            Updates.FloatingOrigin = MergeFloatingOrigin(Updates, other.Updates);

            other.Updates.Store.Clear();
            other.Dispose();
        }

        /// <summary>
        /// Returns true if any components (updated or removed) is marked as ordered.
        /// </summary>
        public bool ContainsOrderedComponent(ProtocolDefinition schemaDefinition)
        {
            foreach (var destructs in Destroys)
            {
                if (schemaDefinition.GetComponentDefinition(destructs).Attributes.SendOrdered)
                {
                    return true;
                }
            }

            return Updates.ContainsOrderedComponent();
        }

        public bool HasUnackedOrderedComponents()
        {
            return OrderedUpdateTime != null;
        }

        public void Reset()
        {
            Updates.Reset();
            Destroys.Clear();
            OrderedUpdateTime = null;
        }

        public void Dispose()
        {
            Updates.Dispose();
            Destroys.Dispose();
            OrderedUpdateTime = null;
        }

        public void TakeOwnershipOfComponents(string ownerIdentifier)
        {
            Updates.TakeOwnershipOfComponents(ownerIdentifier);
            Destroys.TakeOwnership(ownerIdentifier);
        }

        public DeltaComponents ReleaseOwnershipOfComponents()
        {
            _ = Updates.ReleaseOwnershipOfComponents();
            _ = Destroys.ReleaseOwnership();

            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(256);
            if (Updates.Count > 0)
            {
                sb.Append("Updates: ");
                sb.Append(Updates);
            }

            if (Destroys.Count > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append("Destroys: ");
                sb.Append(string.Join(", ", Destroys));
            }

            if (sb.Length == 0)
            {
                sb.Append("Empty");
            }

            if (OrderedUpdateTime != null)
            {
                sb.Append(" OrderedUpdateTime: ");
                sb.Append(OrderedUpdateTime);
            }

            return sb.ToString();
        }

        public static DateTime? MergeOrderedUpdateTime(DateTime? first, DateTime? second)
        {
            if (first == null)
            {
                return second;
            }

            if (second == null)
            {
                return first;
            }

            if (first.Value > second.Value)
            {
                return first;
            }

            return second;
        }

        public static Vector3d MergeFloatingOrigin(ComponentUpdates current, ComponentUpdates newer)
        {
            if (newer.Store.ContainsWorldPositionComponent())
            {
                return newer.FloatingOrigin;
            }

            return current.FloatingOrigin;
        }
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System.Collections.Generic;
    using System.Text;

    public class ComponentMap : SortedValueMap<uint, ComponentData>
    {
        private OnComponentMapDisposed onDisposedCallback;
        private OnOwnershipChanged onOwnershipChangedCallback;

        public ComponentMap(
            OnComponentMapDisposed onDisposedCallback = null,
            OnOwnershipChanged onOwnershipChangedCallback = null)
            : base(ComponentDataComparer.Cached)
        {
            this.onDisposedCallback = onDisposedCallback;
            this.onOwnershipChangedCallback = onOwnershipChangedCallback;
        }

        public ComponentMap(
            int capacity,
            OnComponentMapDisposed onDisposedCallback = null,
            OnOwnershipChanged onOwnershipChangedCallback = null)
            : base(ComponentDataComparer.Cached, capacity)
        {
            this.onDisposedCallback = onDisposedCallback;
            this.onOwnershipChangedCallback = onOwnershipChangedCallback;
        }

        public ComponentMap(
            IDictionary<uint, ComponentData> components,
            OnComponentMapDisposed onDisposedCallback = null,
            OnOwnershipChanged onOwnershipChangedCallback = null)
            : base(ComponentDataComparer.Cached, components)
        {
            this.onDisposedCallback = onDisposedCallback;
            this.onOwnershipChangedCallback = onOwnershipChangedCallback;
        }

        public ComponentMap(
            IReadOnlyList<ComponentData> comps,
            OnComponentMapDisposed onDisposedCallback = null,
            OnOwnershipChanged onOwnershipChangedCallback = null)
            : this(comps.Count, onDisposedCallback, onOwnershipChangedCallback)
        {
            for (var i = 0; i < comps.Count; i++)
            {
                this[comps[i].ComponentID] = comps[i];
            }
        }

        public ComponentMap(params ComponentData[] comps) : this((IReadOnlyList<ComponentData>)comps) { }

        public ComponentMap(
            ComponentData singleComponent,
            OnComponentMapDisposed onDisposedCallback = null,
            OnOwnershipChanged onOwnershipChangedCallback = null)
            : this(1, onDisposedCallback, onOwnershipChangedCallback)
        {
            this[singleComponent.ComponentID] = singleComponent;
        }

        public ComponentMap Clone(ISchemaDataFactory schemaDataFactory, string initialOwnerIdentifier = null)
        {
            var clone = schemaDataFactory.CreateComponentMap();

            foreach (var (componentType, compData) in this)
            {
                clone[componentType] = compData.Clone(schemaDataFactory);
            }

            if (initialOwnerIdentifier != null)
            {
                clone.TakeOwnership(initialOwnerIdentifier);
            }

            return clone;
        }

        public void Update(ComponentData comp)
        {
            comp.TakeOwnership("ComponentMap.Update");

            var componentType = comp.ComponentID;
            if (this.TryGetValue(componentType, out var existingChange))
            {
                existingChange.Update(comp);
                this[componentType] = existingChange;

                // Dispose the given comp since its data has been copied into an existing component,
                // and the caller gave us the ownership of the comp.
                comp.Dispose();
            }
            else
            {
                this.Add(componentType, comp);
            }
        }

        public void Update(ComponentMap others, out int newQueriesCount)
        {
            others.TakeOwnership("ComponentMap.Update");

            newQueriesCount = 0;

            foreach (var (componentType, comp) in others)
            {
                if (ComponentDataEx.IsQueryComponent(componentType) && !this.ContainsKey(componentType))
                {
                    newQueriesCount++;
                }

                Update(comp.ReleaseOwnership());
            }

            others.Clear(); // all component's ownerships have been transferred to this map
            others.Dispose();
        }

        public void Remove(uint componentType)
        {
            if (Remove(componentType, out var comp))
            {
                comp.Dispose();
            }
        }

        public void Remove(IEnumerable<uint> componentTypes)
        {
            foreach (var componentType in componentTypes)
            {
                Remove(componentType);
            }
        }

        public bool ContainsOrderedComponent()
        {
            foreach (var (_, comp) in this)
            {
                if (comp.IsSendOrdered)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ContainsWorldPositionComponent()
        {
            foreach (var (_, comp) in this)
            {
                if (comp.IsWorldPositionComponent())
                {
                    return true;
                }
            }

            return false;
        }

        public long GetMemorySize()
        {
            long size = 0;
            foreach (var (_, comp) in this)
            {
                size += comp.GetMemorySize();
            }

            return size;
        }

        public void TakeOwnership(string ownerIdentifier)
        {
            foreach (var (_, comp) in this)
            {
                comp.TakeOwnership(ownerIdentifier);
            }

            onOwnershipChangedCallback?.Invoke(this, ownerIdentifier);
        }

        public ComponentMap ReleaseOwnership()
        {
            foreach (var (_, comp) in this)
            {
                _ = comp.ReleaseOwnership();
            }

            onOwnershipChangedCallback?.Invoke(this, null);

            return this;
        }

        /// <summary>
        /// Disposes of all components in the map and clears the map.
        /// </summary>
        public void Reset()
        {
            foreach (var (_, comp) in this)
            {
                comp.Dispose();
            }

            this.Clear();
        }

        /// <summary>
        /// Calls <see cref="Reset"/> and invokes the disposed callback if it is set.
        /// </summary>
        public void Dispose()
        {
            Reset();
            onDisposedCallback?.Invoke(this);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var (_, comp) in this)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(comp.ToString());
            }

            return sb.ToString();
        }
    }
}

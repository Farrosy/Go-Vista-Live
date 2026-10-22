// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Collections.Generic;

    public class ComponentDestroyMap : HashSet<uint>, IDisposable
    {
        private OnComponentDestroyMapDisposed onDisposedCallback;
        private OnOwnershipChanged onOwnershipChangedCallback;

        public ComponentDestroyMap(
            OnComponentDestroyMapDisposed onDisposedCallback = null,
            OnOwnershipChanged onOwnershipChangedCallback = null)
        {
            this.onDisposedCallback = onDisposedCallback;
            this.onOwnershipChangedCallback = onOwnershipChangedCallback;
        }

        public ComponentDestroyMap(
            int capacity,
            OnComponentDestroyMapDisposed onDisposedCallback = null,
            OnOwnershipChanged onOwnershipChangedCallback = null) : base(capacity)
        {
            this.onDisposedCallback = onDisposedCallback;
            this.onOwnershipChangedCallback = onOwnershipChangedCallback;
        }

        public ComponentDestroyMap Clone(ISchemaDataFactory schemaDataFactory)
        {
            var clone = schemaDataFactory.CreateComponentDestroyMap();

            foreach (var componentID in this)
            {
                _ = clone.Add(componentID);
            }

            return clone;
        }

        public void Add(ComponentDestroyMap other)
        {
            if (other == null)
            {
                return;
            }

            foreach (var componentID in other)
            {
                _ = this.Add(componentID);
            }
        }

        public void Dispose()
        {
            onDisposedCallback?.Invoke(this);
        }

        public void TakeOwnership(string ownerIdentifier)
        {
            onOwnershipChangedCallback?.Invoke(this, ownerIdentifier);
        }

        public ComponentDestroyMap ReleaseOwnership()
        {
            onOwnershipChangedCallback?.Invoke(this, null);

            return this;
        }
    }
}

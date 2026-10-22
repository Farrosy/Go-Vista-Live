// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;
    using System.Collections.Generic;
    using Entities;
    using Utils;

    /// <summary>
    /// Caches ICoherenceSyncUpdater instances grouped by CoherenceLoopConfig to provide quick access to all networked entities at each point in the update loop.
    /// Keeps three separate collections for Update, FixedUpdate and LateUpdate which should be slightly faster than a dictionary lookup.
    /// </summary>
    internal class CoherenceLoopLookup
    {
        private readonly KeyedValues<Entity, ICoherenceSyncUpdater> updateLookup = new();
        private readonly KeyedValues<Entity, ICoherenceSyncUpdater> lateUpdateLookup = new();
        private readonly KeyedValues<Entity, ICoherenceSyncUpdater> fixedUpdateLookup = new();
        private readonly Dictionary<Entity, ICoherenceSyncUpdater> allUpdaters = new();

        public KeyedValues<Entity, ICoherenceSyncUpdater> Get(CoherenceSync.InterpolationLoop loop) => loop switch
        {
            CoherenceSync.InterpolationLoop.Update => updateLookup,
            CoherenceSync.InterpolationLoop.LateUpdate => lateUpdateLookup,
            CoherenceSync.InterpolationLoop.FixedUpdate => fixedUpdateLookup,
            var _ => throw new ArgumentOutOfRangeException(nameof(loop), loop, null)
        };

        public Dictionary<Entity, ICoherenceSyncUpdater>.ValueCollection GetAllUpdaters() => allUpdaters.Values;

        public void Add(Entity id, ICoherenceSyncUpdater updater, CoherenceSync.InterpolationLoop interpolationLocation)
        {
            if (updater == null)
            {
                return;
            }

            allUpdaters.Add(id, updater);

            if (interpolationLocation.HasFlag(CoherenceSync.InterpolationLoop.Update))
            {
                updateLookup.Add(id, updater);
            }

            if (interpolationLocation.HasFlag(CoherenceSync.InterpolationLoop.LateUpdate))
            {
                lateUpdateLookup.Add(id, updater);
            }

            if (interpolationLocation.HasFlag(CoherenceSync.InterpolationLoop.FixedUpdate))
            {
                fixedUpdateLookup.Add(id, updater);
            }
        }

        public void RemoveCompletely(Entity id)
        {
            Remove(id, (CoherenceSync.InterpolationLoop)0xff);
            allUpdaters.Remove(id);
        }

        public void Remove(Entity id, CoherenceSync.InterpolationLoop interpolationLocation)
        {
            if (interpolationLocation.HasFlag(CoherenceSync.InterpolationLoop.Update))
            {
                updateLookup.Remove(id);
            }

            if (interpolationLocation.HasFlag(CoherenceSync.InterpolationLoop.LateUpdate))
            {
                lateUpdateLookup.Remove(id);
            }

            if (interpolationLocation.HasFlag(CoherenceSync.InterpolationLoop.FixedUpdate))
            {
                fixedUpdateLookup.Remove(id);
            }
        }

        public void Clear()
        {
            updateLookup.Clear();
            fixedUpdateLookup.Clear();
            lateUpdateLookup.Clear();
            allUpdaters.Clear();
        }
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using System.Collections.Generic;
    using Coherence.Serializer;
    using Entities;

    internal struct RefsInfo
    {
        private static readonly List<Entity> EmptyRefs = new();

        public Entity Referer;
        public IReadOnlyList<Entity> ReferencedEntities => referencedEntities;
        private readonly List<Entity> referencedEntities;

        public bool HasAnyRefs => ReferencedEntities?.Count > 0;

        public RefsInfo(in IncomingEntityUpdate update)
        {
            Referer = update.Entity;
            referencedEntities = null;

            var values = update.Components.Updates.Store.Sorted;
            for (var i = 0; i < values.Count; i++)
            {
                var (_, comp) = values[i];
                if (comp.HasRefFields)
                {
                    referencedEntities ??= new List<Entity>(comp.RefFieldsCount);

                    foreach (var entityField in comp.EntityFields)
                    {
                        referencedEntities.Add(entityField.GetEntity());
                    }
                }
            }

            referencedEntities ??= EmptyRefs;
        }

        public RefsInfo(in Entity referer, List<Entity> referencedEntities)
        {
            Referer = referer;
            this.referencedEntities = referencedEntities;
        }
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using Log;
    using UnityEngine;
    using Entities;
    using Coherence.ProtocolDef;

    /// <summary>
    /// Filters what entities are replicated to the client based on tags.
    /// </summary>
    /// <remarks>
    /// This query does not take position of entities into account,
    /// use <see cref="Coherence.Toolkit.CoherenceLiveQuery"/> for that.
    /// </remarks>
    [AddComponentMenu("coherence/Queries/Coherence Tag Query")]
    [DefaultExecutionOrder(ScriptExecutionOrder.CoherenceQuery)]
    [NonBindable]
    [HelpURL("https://docs.coherence.io/v/2.3/manual/components/coherence-tag-query")]
    [CoherenceDocumentation(DocumentationKeys.TagQuery)]
    public sealed class CoherenceTagQuery : CoherenceQuery
    {
        // for components, we don't expose direct creation of instances - add as component instead
        private CoherenceTagQuery()
        {
        }

        /// <summary>
        /// The string to use as the filter.
        /// </summary>
        /// <remarks>
        /// Only entities that use this tag will be received.
        ///
        /// Not associated with GameObject's <see cref="UnityEngine.GameObject.tag"/>.
        /// </remarks>
        [CoherenceTag]
        public string coherenceTag;

        private string lastTag;
        private bool tagIsSet;

        protected override bool NeedsUpdate => coherenceTag != lastTag;

        protected override void UpdateQuery(bool queryActive = true)
        {
            var missingTag = string.IsNullOrEmpty(coherenceTag);

            if (missingTag || !queryActive)
            {
                if (EntityID != Entity.InvalidRelative && tagIsSet)
                {
                    Client.RemoveComponents(EntityID, new uint[] { TagQueryComponent.ComponentID });
                    tagIsSet = false;
                }
            }
            else
            {
                Client.UpdateComponents(EntityID, new ComponentData[]
                {
                    TagQueryComponent.Create(Client.SchemaDataFactory, coherenceTag.Replace(",", "|"))
                });
                tagIsSet = true;
            }

            lastTag = coherenceTag;
        }
    }
}

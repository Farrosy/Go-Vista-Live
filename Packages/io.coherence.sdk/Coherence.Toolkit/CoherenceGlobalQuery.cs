// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using UnityEngine;
    using Entities;
    using Coherence.ProtocolDef;

    /// <summary>
    /// Filters what entities are replicated to the client based being global.
    /// </summary>
    /// <remarks>
    /// This query does not take position of entities into account,
    /// use <see cref="Coherence.Toolkit.CoherenceLiveQuery"/> for that.
    /// </remarks>
    [AddComponentMenu("coherence/Queries/Coherence Global Query")]
    [DefaultExecutionOrder(ScriptExecutionOrder.CoherenceQuery)]
    [NonBindable]
    [HelpURL("https://docs.coherence.io/v/2.3/manual/components/coherenceglobalquery")]
    [CoherenceDocumentation(DocumentationKeys.GlobalQuery)]
    public sealed class CoherenceGlobalQuery : CoherenceQuery
    {
        private bool queryIsAdded;

        // for components, we don't expose direct creation of instances - add as component instead
        private CoherenceGlobalQuery()
        {
        }

        protected override bool NeedsUpdate => false;

        protected override void UpdateQuery(bool queryActive = true)
        {
            if (queryActive)
            {
                Client.UpdateComponents(EntityID, new ComponentData[]
                {
                    GlobalQueryComponent.Create(Client.SchemaDataFactory)
                });
                queryIsAdded = true;
            }
            else
            {
                if (EntityID == Entity.InvalidRelative || !queryIsAdded)
                {
                    return;
                }

                Client.RemoveComponents(EntityID, new uint[]
                {
                    GlobalQueryComponent.ComponentID
                });
                queryIsAdded = false;
            }
        }
    }
}

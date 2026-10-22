// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using Coherence.Toolkit;
    using UnityEngine;

    public class ObjectReferenceAnalyzer : CoherenceSyncConfigAnalyzer
    {
        public override bool ValidateConfig(CoherenceSyncConfig config, EntryInfo info)
        {
            if (!config.EditorTarget)
            {
                // Only NullObjectAnalyzer should return false when EditorTarget is null
                // to avoid ConfigsAnalyzerHandler.GetCompoundedErrorMessage returning
                // multiple warnings about the same issue.
                return true;
            }

            if (config.EditorTarget is GameObject gameObject)
            {
                return gameObject.TryGetComponent(out ICoherenceSync sync) && sync.CoherenceSyncConfig == config;
            }
            else
            {
                return config.EditorTarget is ICoherenceSync sync && sync.CoherenceSyncConfig == config;
            }
        }

        protected override string GetErrorMessageInternal(CoherenceSyncConfig config, EntryInfo info)
            => $"{config.name} has no CoherenceSync component or is not referenced in the CoherenceSync owner. Check the inspector and target Object for more information.";
    }
}

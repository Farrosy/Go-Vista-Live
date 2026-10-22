// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using Coherence.Toolkit;

    public class NullObjectAnalyzer : CoherenceSyncConfigAnalyzer
    {
        public override bool ValidateConfig(CoherenceSyncConfig config, EntryInfo info) => config.EditorTarget;

        protected override string GetErrorMessageInternal(CoherenceSyncConfig config, EntryInfo info) => $"A {nameof(CoherenceSyncConfig)} is null or its missing a Unity asset.";
    }
}

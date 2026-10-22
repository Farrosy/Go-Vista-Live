// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Diagnostics.CodeAnalysis;
    using Coherence.Toolkit;

    public abstract class CoherenceSyncConfigAnalyzer
    {
        public string GetErrorMessage([DisallowNull] CoherenceSyncConfig config, EntryInfo info)
            => !ValidateConfig(config, info) ? GetErrorMessageInternal(config, info) : string.Empty;

        public abstract bool ValidateConfig([DisallowNull] CoherenceSyncConfig config, EntryInfo info);
        protected abstract string GetErrorMessageInternal([DisallowNull] CoherenceSyncConfig config, EntryInfo info);
    }
}


// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using Coherence.Toolkit;

    public class ObjectProviderAnalyzer : CoherenceSyncConfigAnalyzer
    {
        public override bool ValidateConfig(CoherenceSyncConfig config, EntryInfo info) => config.Provider is not null && config.Provider.Validate(config);

        protected override string GetErrorMessageInternal(CoherenceSyncConfig config, EntryInfo info)
            => config.Provider is null
                ? $"{config.name} has null Object Provider. Check the inspector for more information."
                : $"{config.name} has problems in the serialized data of {config.Provider.GetType().Name}. Check the inspector for more information.";
    }
}

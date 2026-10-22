// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using Coherence.Toolkit;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;

    public class ConfigsAnalyzerHandler
    {
        private List<CoherenceSyncConfigAnalyzer> analyzers = new();

        private EntryInfo entriesInfo;
        private Dictionary<CoherenceSyncConfig, EntryInfo> entriesDictionary;

        public EntryInfo AllEntriesInfo => entriesInfo;
        internal List<CoherenceSyncConfigAnalyzer> Analyzers => analyzers;

        public ConfigsAnalyzerHandler()
        {
            var types = typeof(CoherenceSyncConfigAnalyzer).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(CoherenceSyncConfigAnalyzer))).ToList();

            foreach (var type in types)
            {
                analyzers.Add(Activator.CreateInstance(type) as CoherenceSyncConfigAnalyzer);
            }

            RefreshConfigsInfo();
        }

        public void RefreshConfigsInfo()
        {
            entriesInfo = CoherenceSyncConfigUtils.GetNetworkedObjectsInfo(out entriesDictionary);
        }

        public bool TryGetInfo([MaybeNull] CoherenceSyncConfig config, out EntryInfo info)
        {
            if (!config)
            {
                info = default;
                return false;
            }

            return entriesDictionary.TryGetValue(config, out info);
        }

        public bool Validate([MaybeNull] CoherenceSyncConfig config)
        {
            if (!config)
            {
                return false;
            }

            var result = true;
            entriesDictionary.TryGetValue(config, out var info);
            foreach (var analyzer in analyzers)
            {
                result &= analyzer.ValidateConfig(config, info);
            }

            return result;
        }

        public string GetCompoundedErrorMessage([MaybeNull] CoherenceSyncConfig config)
        {
            if (!config)
            {
                return $"A {nameof(CoherenceSyncConfig)} reference is missing. Check the CoherenceSync inspector and Configure window for more details.";
            }

            entriesDictionary.TryGetValue(config, out var info);

            List<string> errors = null;
            foreach (var analyzer in analyzers)
            {
                if (analyzer.GetErrorMessage(config, info) is { Length: > 0 } error)
                {
                    (errors ??= new()).Add(error);
                }
            }

            if (errors is not { Count: > 0 })
            {
                return "";
            }

            var errorMessage = new StringBuilder();
            for (var i = 0; i < errors.Count; i++)
            {
                var error = errors[i];

                errorMessage.AppendLine(error);

                if (i != errors.Count - 1)
                {
                    errorMessage.AppendLine(string.Empty);
                }
            }

            return errorMessage.ToString();
        }
    }
}

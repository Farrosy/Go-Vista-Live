// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using Newtonsoft.Json;

    [Serializable]
    public class Settings
    {
        internal const string defaultLogFilePath = "Logs/player_logs.txt";
        internal const LogLevel defaultFileLogLevel = LogLevel.Debug;

        internal event Action OnApplied;
        internal static Func<Settings> Provider;

#if UNITY_5_3_OR_NEWER
        [JsonProperty("editorLoglevel")]
        public LogLevel EditorLogLevel = LogLevel.Info;
#endif

        [JsonProperty("loglevel")]
        public LogLevel LogLevel = LogLevel.Info;

        [JsonProperty("filtermode")]
        public Log.FilterMode FilterMode = Log.FilterMode.Include;

        [JsonProperty("logStackTrace")]
        public bool LogStackTrace;

        [JsonProperty("addTimestamp")]
        public bool AddTimestamp;

        [JsonProperty("watermark")]
        public string Watermark = "(coherence)";

        [JsonProperty("addSourceType")]
        public bool AddSourceType;

        [JsonProperty("sourcefilters")]
        public string SourceFilters = string.Empty;

        [JsonProperty("logToFile")]
        public bool LogToFile;

        [JsonProperty("logFilePath", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(defaultLogFilePath)]
        public string LogFilePath = defaultLogFilePath;

#if UNITY_5_3_OR_NEWER
        [UnityEngine.HideInInspector]
        public bool MigratedToSerializedObject;
#endif

        [JsonProperty("fileLogLevel", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(defaultFileLogLevel)]
        public LogLevel FileLogLevel = defaultFileLogLevel;

        [NonSerialized]
        private string[] processedSourceFilters;

        [NonSerialized]
        private int lastSourceFilterHash;

        public LogLevel GetLowestLogLevel()
        {
            if (LogToFile && FileLogLevel < LogLevel)
            {
                return FileLogLevel;
            }

            return LogLevel;
        }

        public void Apply()
        {
            OnApplied?.Invoke();
        }

        internal string[] GetSourceFilter()
        {
            if (string.IsNullOrEmpty(SourceFilters))
            {
                return null;
            }

            ProcessSourceFilters();

            return processedSourceFilters;
        }

        private void ProcessSourceFilters()
        {
            var currentHash = SourceFilters.GetHashCode();
            if (lastSourceFilterHash == currentHash)
            {
                return;
            }
            lastSourceFilterHash = currentHash;

            processedSourceFilters = SourceFilters.Split(',').Select(s => s.Trim()).ToArray();
        }
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log.Targets
{
    using System;

    [Serializable]
    public struct LogTargetConfig
    {
        public LogTarget Target;
        public LogFormat Format;
        public LogLevel LogLevel;
        public string FilePath;

        public override string ToString()
        {
            var result = $"{Target.ToString().ToLowerInvariant()}:{Format.ToString().ToLowerInvariant()}:{LogLevel.ToString().ToLowerInvariant()}";

            if (Target == LogTarget.File)
            {
                if (string.IsNullOrEmpty(FilePath))
                {
                    throw new Exception("Missing FilePath for file log target");
                }

                result += $":\"{FilePath}\"";
            }

            return result;
        }

        public static bool TryParse(string str, out LogTargetConfig config, out string error)
        {
            config = default;

            var parts = str.Split(':', count: 4);
            if (parts.Length < 3 || parts.Length > 4)
            {
                error = $"Invalid log target format: {str}. Wrong number of parts.";
                return false;
            }

            if (!TryParseEnum<LogTarget>(parts[0], out error, out var logTarget))
            {
                return false;
            }

            string filePath = null;

            if (logTarget == LogTarget.File)
            {
                if (parts.Length != 4)
                {
                    error = $"File log target requires a file path: {str}";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(parts[3]))
                {
                    error = $"File path cannot be empty for file log target: {str}";
                    return false;
                }

                filePath = parts[3];
            }
            else if (logTarget == LogTarget.Console && parts.Length != 3)
            {
                error = $"Console log target should not have a file path: {str}";
                return false;
            }

            if (!TryParseEnum<LogFormat>(parts[1], out error, out var logFormat))
            {
                return false;
            }

            if (!TryParseEnum<LogLevel>(parts[2], out error, out var logLevel))
            {
                return false;
            }

            config = new LogTargetConfig()
            {
                Target = logTarget,
                Format = logFormat,
                LogLevel = logLevel,
                FilePath = filePath
            };

            return true;

            static bool TryParseEnum<TEnum>(string valueStr, out string error, out TEnum value) where TEnum : struct, Enum
            {
                if (!Enum.TryParse<TEnum>(valueStr, ignoreCase: true, out value))
                {
                    error = $"Invalid value '{valueStr}'. Available options are {GetEnumOptionsFormatted<TEnum>()}";

                    return false;
                }

                error = string.Empty;
                return true;
            }

            static string GetEnumOptionsFormatted<TEnum>() where TEnum : struct, Enum => string.Join("|", Enum.GetNames(typeof(TEnum)));
        }
    }

    public enum LogTarget
    {
        Console,
        File
    }

    public enum LogFormat
    {
        Plain,
        Colored,
        JSON
    }

    public interface ILogTarget : IDisposable
    {
        LogLevel Level { get; set; }

        void Log(LogLevel level, string message, (string key, object value)[] args, Logger logger);
    }
}

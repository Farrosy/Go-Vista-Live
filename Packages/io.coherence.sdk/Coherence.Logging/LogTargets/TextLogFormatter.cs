// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log.Targets
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class TextLogFormatter : ILogFormatter
    {
        private const string TimestampColor = "\x1B[90m";
        private const string WatermarkColor = "\x1B[90m";
        private const string SourceColor = "\x1B[90m";
        private const string ArgsColor = "\x1B[94m";

        private const string TraceColor = "\x1B[95m";
        private const string DebugColor = "\x1B[96m";
        private const string InfoColor = "\x1B[32m";
        private const string WarningColor = "\x1B[33m";
        private const string ErrorColor = "\x1B[91m";

        private const string ResetColor = "\x1B[0m";

        private bool colored;

        public TextLogFormatter(bool colored = false)
        {
            this.colored = colored;
        }

        public static StringBuilder AppendLevel(StringBuilder logBuilder, LogLevel level, bool noTrailingSpace = false, bool colored = false)
        {
            if (colored)
            {
                logBuilder.Append(GetLevelColor(level));
            }

            _ = logBuilder.Append(GetLevelString(level));

            if (colored)
            {
                logBuilder.Append(ResetColor);
            }

            if (noTrailingSpace)
            {
                return logBuilder;
            }

            return logBuilder.Append(" ");
        }

        public static StringBuilder AppendPrefix(StringBuilder logBuilder, bool useWatermark, Type source = null, bool colored = false)
        {
            if (useWatermark)
            {
                if (colored)
                {
                    logBuilder.Append(WatermarkColor);
                }

                logBuilder.Append("(coherence)");
                logBuilder.Append(" ");

                if (colored)
                {
                    logBuilder.Append(ResetColor);
                }
            }

            AppendSource(logBuilder, source, colored);

            return logBuilder;
        }

        public static StringBuilder AppendSource(StringBuilder logBuilder, Type source, bool colored = false)
        {
            const int LENGTH = 17;
            const int BRACKET_ADJUSTED_LENGTH = LENGTH - 2; // account for angle brackets
            const string ELLIPSIS = "...";

            if (source == null)
            {
                return logBuilder.Append(' ', LENGTH + 1);
            }

            if (colored)
            {
                logBuilder.Append(SourceColor);
            }

            _ = logBuilder.Append("<");

            if (source.Name.Length > BRACKET_ADJUSTED_LENGTH)
            {
                _ = logBuilder.Append(source.Name, 0, BRACKET_ADJUSTED_LENGTH - ELLIPSIS.Length)
                    .Append(ELLIPSIS)
                    .Append(">");
            }
            else
            {
                _ = logBuilder.Append(source.Name)
                    .Append(">")
                    .Append(' ', BRACKET_ADJUSTED_LENGTH - source.Name.Length);
            }

            _ = logBuilder.Append(" ");

            if (colored)
            {
                logBuilder.Append(ResetColor);
            }

            return logBuilder;
        }

        public static StringBuilder AppendTimestamp(StringBuilder logBuilder, bool noTrailingSpace = false, bool colored = false)
        {
            if (colored)
            {
                logBuilder.Append(TimestampColor);
            }

            logBuilder.Append(DateTime.Now.ToString("HH:mm:ss.fff"));

            if (colored)
            {
                logBuilder.Append(ResetColor);
            }

            if (noTrailingSpace)
            {
                return logBuilder;
            }

            return logBuilder.Append(" ");
        }

        public static StringBuilder AppendArgs(StringBuilder logBuilder, ICollection<(string key, object value)> args, bool useTab = true, bool colored = false)
            => AppendArgs(logBuilder, args, useTab ? '\t' : ' ', colored);

        public static StringBuilder AppendArgs(StringBuilder logBuilder, ICollection<(string key, object value)> args, char delimiter, bool colored = false)
        {
            if (args.Count <= 0)
            {
                return logBuilder;
            }

            logBuilder.Append(delimiter);

            foreach ((string key, object value) in args)
            {
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (colored)
                {
                    logBuilder.Append(ArgsColor)
                        .Append(key)
                        .Append("=")
                        .Append(ResetColor)
                        .Append(value);
                }
                else
                {
                    logBuilder.Append(key)
                        .Append("=")
                        .Append(value);
                }

                logBuilder.Append(" ");
            }

            logBuilder.Remove(logBuilder.Length - 1, 1);

            return logBuilder;
        }

        public string Format(LogLevel level, string message, (string key, object value)[] args, Type source, bool includeWatermark)
        {
            var logBuilder = new StringBuilder();

            _ = AppendTimestamp(logBuilder, colored: colored);
            _ = AppendLevel(logBuilder, level, colored: colored);
            _ = AppendPrefix(logBuilder, includeWatermark, source, colored: colored)
                .Append(message);
            _ = AppendArgs(logBuilder, new ArraySegment<(string key, object value)>(args), colored: colored);

            return logBuilder.ToString();
        }

        private static string GetLevelColor(LogLevel level) => level switch
        {
            LogLevel.Trace => TraceColor,
            LogLevel.Debug => DebugColor,
            LogLevel.Info => InfoColor,
            LogLevel.Warning => WarningColor,
            LogLevel.Error => ErrorColor,
            _ => throw new ArgumentException("Unknown log level", nameof(level)),
        };

        private static string GetLevelString(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            _ => throw new ArgumentException("Unknown log level", nameof(level)),
        };
    }
}

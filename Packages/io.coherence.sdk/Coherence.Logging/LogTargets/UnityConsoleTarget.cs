// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER

namespace Coherence.Log.Targets
{
    using System;
    using System.Text;
    using System.Threading;
    using UnityEngine;
    using Logger = Coherence.Log.Logger;
    using Object = UnityEngine.Object;

    public class UnityConsoleTarget : ILogTarget
    {
        public LogLevel Level { get; set; }

        /// <summary>
        /// Should stack traces be included in Trace, Debug and Info level logs?
        /// </summary>
        /// <remarks>
        /// <para>
        /// This can be configured in Project Settings > coherence > Unity Logs > Include Stack Trace.
        /// </para>
        /// <para>
        /// Stack traces will always be included for Warning and Error level logs, regardless of this setting.
        /// </para>
        /// </remarks>
        public bool LogStackTrace
        {
            get => logOptions == LogOption.None;
            set => logOptions = value ? LogOption.None : LogOption.NoStacktrace;
        }

        private LogOption logOptions;

        /// <summary>
        /// Should a timestamp prefix be added to each logged message?
        /// </summary>
        /// <remarks>
        /// This can be configured in Project Settings > coherence > Unity Logs > Add Timestamp.
        /// </remarks>
        /// <example>
        /// 14:23:15.123 My message.
        /// </example>
        public bool AddTimestamp { get; set; }

        /// <summary>
        /// A custom watermark to add to each logged message.
        /// </summary>
        /// <remarks>
        /// This can be configured in Project Settings > coherence > Unity Logs > Watermark.
        /// </remarks>
        /// <example>
        /// [coherence] My message.
        /// </example>
        public string Watermark { get; set; } = "";

        /// <summary>
        /// Should the source type be included in each logged message?
        /// </summary>
        /// <remarks>
        /// This can be configured in Project Settings > coherence > Unity Logs > Add Source Type.
        /// </remarks>
        /// <example>
        /// MyType: My message.
        /// </example>
        public bool AddSourceType { get; set; }

        [ThreadStatic]
        private static StringBuilder stringBuilderCache;

        public void Log(LogLevel level, string message, (string key, object value)[] args, Logger logger)
        {
            var logBuilder = stringBuilderCache ??= new StringBuilder();
            logBuilder.Clear();

            if (AddTimestamp)
            {
                TextLogFormatter.AppendTimestamp(logBuilder);
            }

            if (logger.UseWatermark && Watermark.Length > 0)
            {
                logBuilder.Append(Watermark);
                logBuilder.Append(" ");
            }

            if (AddSourceType && logger.Source != null)
            {
                logBuilder.Append(logger.Source.Name);
                logBuilder.Append(": ");
            }

            logBuilder.Append(message);

            Object context = null;
            if (logger is UnityLogger unityLogger)
            {
                context = unityLogger.GetUnityLogContext();
            }

            if (args.Length > 0 && args[0].value is Object unityObject)
            {
                context = unityObject;
                TextLogFormatter.AppendArgs(logBuilder,
                    new ArraySegment<(string key, object value)>(args, 1, args.Length - 1), '\n');
            }
            else
            {
                TextLogFormatter.AppendArgs(logBuilder, args, '\n');
            }

            var result = logBuilder.ToString();
            switch (level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.LogFormat(LogType.Log, logOptions, context, "{0}", result);
                    break;
                case LogLevel.Warning:
                    if (context)
                    {
                        Debug.LogWarning(result, context);
                    }
                    else
                    {
                        Debug.LogWarning(result);
                    }

                    break;
                case LogLevel.Error:
                    if (context)
                    {
                        Debug.LogError(result, context);
                    }
                    else
                    {
                        Debug.LogError(result);
                    }

                    break;
                default: throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }

        public void Dispose()
        {
        }
    }
}

#endif

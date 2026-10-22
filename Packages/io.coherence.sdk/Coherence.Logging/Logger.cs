// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Targets;
#if UNITY_2022_2_OR_NEWER
    using UnityEngine;
#endif

    public class Logger : IDisposable
    {
        public const string IDArg = "logId";
        public const string AlertArg = "logAlert";

        public bool UseWatermark { get; set; } = true;

        public delegate void LogDelegate(LogLevel level, bool filtered, string log, Type source,
            (string key, object value)[] args);

        public static event LogDelegate OnLog;

        protected readonly List<ILogTarget> baseLogTargets;
        protected readonly List<ILogTarget> customLogTargets;

        public object Context
        {
            get => context ?? WithLogger?.Context;
            set => context = value;
        }

        internal Type Source { get; }
        protected Logger WithLogger { get; set; }

        protected readonly List<(string key, object value)> prefixArgs = new();

        private object context;

        public Logger(
            Type source = null,
            object context = null,
            List<ILogTarget> baseLogTargets = null,
            IEnumerable<ILogTarget> customLogTargets = null)
        {
            Source = source;
            Context = context;

            this.baseLogTargets = baseLogTargets;
            this.customLogTargets = customLogTargets != null
                ? new List<ILogTarget>(customLogTargets)
                : new List<ILogTarget>();
        }

        public void AddLogTarget(ILogTarget logTarget)
        {
            customLogTargets.Add(logTarget);
        }

        public IEnumerable<ILogTarget> GetLogTargets()
        {
            if (baseLogTargets != null)
            {
                foreach (var target in baseLogTargets)
                {
                    yield return target;
                }
            }

            if (customLogTargets != null)
            {
                foreach (var target in customLogTargets)
                {
                    yield return target;
                }
            }
        }

#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        protected virtual bool BuildAndPrintLog(LogLevel level, string log, params (string key, object value)[] args)
        {
            return LogToTargets(baseLogTargets, level, log, args)
                   || LogToTargets(customLogTargets, level, log, args);
        }

        private bool LogToTargets(IEnumerable<ILogTarget> targets, LogLevel level, string log, params (string key, object value)[] args)
        {
            if (targets == null)
            {
                return false;
            }

            var didLog = false;

            foreach (var logTarget in targets)
            {
                if (level < logTarget.Level)
                {
                    continue;
                }

                logTarget.Log(level, log, args, this);

                didLog = true;
            }

            return didLog;
        }

        public virtual Logger With<TSource>()
        {
            return With(typeof(TSource));
        }

        public virtual Logger With(Type source)
        {
            var newLogger = new Logger(source, null, baseLogTargets, customLogTargets);
            newLogger.WithLogger = this;
            newLogger.Context = Context;
            newLogger.UseWatermark = UseWatermark;

            return newLogger;
        }

        public virtual Logger WithArgs(params (string key, object value)[] args)
        {
            foreach (var kv in args)
            {
                var existingArgIndex = prefixArgs.FindIndex(arg => arg.key == kv.key);
                if (existingArgIndex >= 0)
                {
                    prefixArgs[existingArgIndex] = kv;
                }
                else
                {
                    prefixArgs.Add(kv);
                }
            }

            return this;
        }

        public Logger NoWatermark()
        {
            UseWatermark = false;

            return this;
        }


#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        [Conditional(LogConditionals.Trace)]
        public virtual void Trace(string log, params (string key, object value)[] args)
        {
            LogImpl(LogLevel.Trace, log, args);
        }

#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        [Conditional(LogConditionals.Debug)]
        public virtual void Debug(string log, params (string key, object value)[] args)
        {
            LogImpl(LogLevel.Debug, log, args);
        }


#if COHERENCE_DISABLE_LOG_INFO
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        public virtual void Info(string log, params (string key, object value)[] args)
        {
            LogImpl(LogLevel.Info, log, args);
        }

#if COHERENCE_DISABLE_LOG_WARNING
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        [Obsolete("Log warnings by ID now")]
        public virtual void Warning(string log, params (string key, object value)[] args)
        {
            LogImpl(LogLevel.Warning, log, args);
        }

#if COHERENCE_DISABLE_LOG_WARNING
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        public virtual void Warning(Warning id, params (string key, object value)[] args)
        {
            var log = id.GetText();

            args = AppendLogID(args, id);

            LogImpl(LogLevel.Warning, log, args);
        }

#if COHERENCE_DISABLE_LOG_WARNING
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        public virtual void Warning(Warning id, string msg, params (string key, object value)[] args)
        {
            args = AppendLogID(args, id);

            LogImpl(LogLevel.Warning, msg, args);
        }

#if COHERENCE_DISABLE_LOG_ERROR
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        [Obsolete("Log errors by ID now")]
        public virtual void Error(string log, params (string key, object value)[] args)
        {
            LogImpl(LogLevel.Error, log, args);
        }

#if COHERENCE_DISABLE_LOG_ERROR
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        public virtual void Error(Error id, params (string key, object value)[] args)
        {
            var log = id.GetText();

            args = AppendLogID(args, id);

            LogImpl(LogLevel.Error, log, args);
        }

#if COHERENCE_DISABLE_LOG_ERROR
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        public virtual void Error(Error id, string msg, params (string key, object value)[] args)
        {
            args = AppendLogID(args, id);

            LogImpl(LogLevel.Error, msg, args);
        }
#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        [Obsolete("Use specific methods per level now.")]
        public void Log(LogLevel level, string log, params (string key, object value)[] args)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }

#if UNITY_2022_2_OR_NEWER
        [HideInCallstack]
#endif
        protected virtual void LogImpl(LogLevel level, string log, params (string key, object value)[] args)
        {
            if (Source != null)
            {
                var sourceString = Source.Name;
                var mode = Coherence.Log.Log.SourceFilterMode;
                var sourceFilter = Coherence.Log.Log.SourceFilters;
                if (sourceFilter != null && sourceFilter.Length > 0)
                {
                    switch (mode)
                    {
                        case Coherence.Log.Log.FilterMode.Include
                            when !sourceFilter.Contains(sourceString):
                        case Coherence.Log.Log.FilterMode.Exclude
                            when sourceFilter.Contains(sourceString):
                            return;
                    }
                }
            }

            args = GatherPrefixArgs(args);
            var didLog = BuildAndPrintLog(level, log, args);

            OnLog?.Invoke(level, !didLog, log, Source, args);
        }

        protected virtual (string key, object value)[] GatherPrefixArgs(params (string key, object value)[] args)
        {
            if (WithLogger != null)
            {
                args = WithLogger.GatherPrefixArgs(args);
            }

            if (prefixArgs != null)
            {
                var originalSize = args.Length;
                Array.Resize(ref args, args.Length + prefixArgs.Count);
                prefixArgs.CopyTo(args, originalSize);
            }

            return args;
        }

        protected (string key, object value)[] AppendLogID((string key, object value)[] args, object id)
        {
            // possible ID was already added.
            for (int i = 0; i < args.Count(); i++)
            {
                var arg = args[i];
                if (arg.key == IDArg)
                {
                    return args;
                }
            }

            // This could be cached and also probably not boxing ID would be smart.
            return args.Concat(new (string key, object value)[] { (IDArg, id) }).ToArray();
        }

        public void Dispose()
        {
            foreach (var target in customLogTargets)
            {
                target.Dispose();
            }

            customLogTargets.Clear();
        }

        internal static void SafeLog(string message)
        {
            try
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.Log($"(coherence) {message}");
#else
                System.Console.WriteLine(message);
#endif
            }
            catch
            {
                // Not doing anything to avoid infinite recursion
            }
        }

        internal static void SafeWarningLog(string message)
        {
            try
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.LogWarning($"(coherence) {message}");
#else
                System.Console.Error.WriteLine(message);
#endif
            }
            catch
            {
                // Not doing anything to avoid infinite recursion
            }
        }

        internal static void SafeErrorLog(string message)
        {
            try
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.LogError($"(coherence) {message}");
#else
                System.Console.Error.WriteLine(message);
#endif
            }
            catch
            {
                // Not doing anything to avoid infinite recursion
            }
        }
    }
}

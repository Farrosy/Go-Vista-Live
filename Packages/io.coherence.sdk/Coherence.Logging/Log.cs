// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log
{
    using Targets;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    public static class Log
    {
        public enum FilterMode
        {
            Include,
            Exclude
        }

        internal static FilterMode SourceFilterMode => GetSettings().FilterMode;
        internal static string[] SourceFilters => GetSettings().GetSourceFilter();

        private static readonly object lockObject = new();
        private static bool didParseCLIArgs;
        private static bool didCheckForLevelDefines;

        private static ILogTarget consoleTarget;
        private static FileTarget fileTarget;
        private static Settings settings;
        private static List<ILogTarget> baseTargets = new();

#if UNITY_5_3_OR_NEWER
        private static Func<Type, List<ILogTarget>, Logger> LoggerSource = (source, baseTargets) => new UnityLogger(source, baseTargets);
#else
        private static Func<Type, List<ILogTarget>, Logger> LoggerSource = (source, baseTargets) => new Logger(source, null, baseTargets);
#endif

        public static Settings GetSettings()
        {
            if (settings != null)
            {
                return settings;
            }

            Init();
            return settings;
        }

        internal static void SetSettings(Settings setSettings)
        {
            if (settings != null)
            {
                settings.OnApplied -= OnSettingsApplied;
            }

            settings = setSettings ?? throw new ArgumentNullException(nameof(setSettings));
            settings.OnApplied += OnSettingsApplied;
            settings.Apply();
        }

        private static void Init()
        {
            lock (lockObject)
            {
                InitSettings();
                ParseCLIArgs();
                InitConsoleTarget();
                InitFileTarget();
                CheckForLevelDefines();
            }
        }

        private static void InitSettings()
        {
            if (settings != null)
            {
                return;
            }

#if UNITY_5_3_OR_NEWER
            if (Settings.Provider == null)
            {
                UnityEngine.Debug.LogWarning("(coherence) Logger was initialized before its settings were loaded. " +
                    "This may cause issues with logging. Please report this to the coherence development team, " +
                    "including the stack trace of this warning and the SDK version in use.");
                settings = new Settings();
            }
            else
            {
                settings = Settings.Provider();
            }
#else
            settings = new Settings()
            {
                // It will be filtered based on preproc symbols anyway
                LogLevel = LogLevel.Trace,
            };
#endif

            settings.OnApplied += OnSettingsApplied;
        }

        private static void OnSettingsApplied()
        {
            InitConsoleTarget();
            InitFileTarget();
        }

        private static bool CanLogToFile()
        {
#if UNITY_5_3_OR_NEWER
#if UNITY_EDITOR || UNITY_STANDALONE
            // Logging to file allowed only in editor or standalone builds
            return true;
#else
            return false;
#endif
#else
            return true;
#endif
        }

        private static void InitFileTarget()
        {
            if (!CanLogToFile())
            {
                return;
            }

            if (settings.LogToFile)
            {
                if (fileTarget == null)
                {
                    fileTarget = new FileTarget(settings.LogFilePath, new JsonLogFormatter());
                    baseTargets.Add(fileTarget);
#if UNITY_EDITOR
                    UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeFileTarget;
                    UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeFileTarget;
#endif
                }
                else if (fileTarget.FilePath != settings.LogFilePath)
                {
                    fileTarget.Dispose();
                    baseTargets.Remove(fileTarget);

                    fileTarget = new FileTarget(settings.LogFilePath, new JsonLogFormatter());
                    baseTargets.Add(fileTarget);
                }

                fileTarget.Level = settings.FileLogLevel;
            }
            else if (fileTarget != null)
            {
                DisposeFileTarget();
            }

            static void DisposeFileTarget()
            {
                if (fileTarget is not null)
                {
                    baseTargets.Remove(fileTarget);
                    fileTarget.Dispose();
                    fileTarget = null;
                }

#if UNITY_EDITOR
                UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeFileTarget;
#endif
            }
        }

        private static void InitConsoleTarget()
        {
            if (consoleTarget == null)
            {
#if UNITY_5_3_OR_NEWER
                consoleTarget = new UnityConsoleTarget();
#else
                consoleTarget = new ConsoleTarget(new TextLogFormatter());
#endif
                baseTargets.Add(consoleTarget);
            }

            consoleTarget.Level = settings.LogLevel;

#if UNITY_5_3_OR_NEWER
            if (consoleTarget is UnityConsoleTarget unityConsole)
            {
                unityConsole.LogStackTrace = settings.LogStackTrace;
                unityConsole.AddTimestamp = settings.AddTimestamp;
                unityConsole.Watermark = settings.Watermark ?? "";
                unityConsole.AddSourceType = settings.AddSourceType;
            }
#endif
        }

        private static void ParseCLIArgs()
        {
            if (didParseCLIArgs)
            {
                return;
            }

            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--coherence-log-file-path")
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new Exception("Missing value of --coherence-log-file-path argument.");
                    }

                    var filePath = args[i + 1];

                    settings.LogToFile = true;
                    settings.LogFilePath = filePath;
                }

                if (args[i] == "--coherence-file-log-level")
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new Exception("Missing value of --coherence-file-log-level argument.");
                    }

                    if (!Enum.TryParse(args[i + 1], ignoreCase: true, out LogLevel level))
                    {
                        throw new ArgumentException($"Value {args[i + 1]} is not a valid enum value.");
                    }

                    settings.FileLogLevel = level;
                }
            }

            didParseCLIArgs = true;
        }

        private static void CheckForLevelDefines()
        {
            if (didCheckForLevelDefines)
            {
                return;
            }

            didCheckForLevelDefines = true;

            var lowestLogLevel = settings.GetLowestLogLevel();

            if (lowestLogLevel <= LogLevel.Trace)
            {
                CheckForTraceDefine();
            }

            if (lowestLogLevel <= LogLevel.Debug)
            {
                CheckForDebugDefine();
            }
        }

        private static void CheckForTraceDefine()
        {
            var hasTraceDefine = false;
            HasTraceDefine(ref hasTraceDefine);

            if (!hasTraceDefine)
            {
                GetLogger(typeof(Log)).Warning(Warning.MissingTraceDefine);
            }
        }

        private static void CheckForDebugDefine()
        {
            var hasDebugDefine = false;
            HasDebugDefine(ref hasDebugDefine);

            if (!hasDebugDefine)
            {
                GetLogger(typeof(Log)).Warning(Warning.MissingDebugDefine);
            }
        }

        [Conditional(LogConditionals.Trace)]
        private static void HasTraceDefine(ref bool has)
        {
            has = true;
        }

        [Conditional(LogConditionals.Debug)]
        private static void HasDebugDefine(ref bool has)
        {
            has = true;
        }

        // Logger context is used to differentiate between different instances
        // of loggers that log the same Source Types.  For example, Unity scene
        // objects that use a logger set the context of the logger to be the scene
        // they are in.
        public static Logger GetLogger<TSource>(object context = null)
        {
            return GetLogger(typeof(TSource), context);
        }

        public static Logger GetLogger(Type source, object context = null)
        {
            Init();

            var logger = LoggerSource(source, baseTargets);
            logger.Context = context;

            return logger;
        }

        public static LazyLogger GetLazyLogger<TSource>() => new(() => GetLogger<TSource>());
        public static LazyLogger GetLazyLogger(Type source) => new(() => GetLogger(source));

        public static Logger GetFromConfigs(IEnumerable<LogTargetConfig> configs)
        {
            var targets = new List<ILogTarget>();

            foreach (var config in configs)
            {
                ILogFormatter formatter = config.Format switch
                {
                    LogFormat.Plain => new TextLogFormatter(colored: false),
                    LogFormat.Colored => new TextLogFormatter(colored: true),
                    LogFormat.JSON => new JsonLogFormatter(),
                    _ => throw new ArgumentException($"Unsupported log format {config.Format}")
                };

                switch (config.Target)
                {
                    case LogTarget.Console:
                        targets.Add(new ConsoleTarget(formatter)
                        {
                            Level = config.LogLevel,
                        });
                        break;
                    case LogTarget.File:
                        targets.Add(new FileTarget(config.FilePath, formatter)
                        {
                            Level = config.LogLevel
                        });
                        break;
                    default:
                        throw new ArgumentException($"Unsupported log target {config.Target}");
                }
            }

            return LoggerSource(null, targets);
        }
    }
}

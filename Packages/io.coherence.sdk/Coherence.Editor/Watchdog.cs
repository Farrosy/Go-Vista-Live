// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Log;
    using UnityEditor;
    using UnityEditor.Compilation;
    using UnityEditor.PackageManager;

    [InitializeOnLoad]
    internal class Watchdog
    {
        private const string watchdogDialogKey = "Coherence.Watchdog.Dialog";

        private static Dictionary<IWatchdogTrigger, List<CompilerMessage>> diagnostics = new();
        private static List<IWatchdogTrigger> triggers = new();

        private const string LogEntriesInternalAPI = "UnityEditor.LogEntries, UnityEditor.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        private const string LogEntryInternalAPI = "UnityEditor.LogEntry, UnityEditor.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        private static readonly Regex compilerMessageRegex = new(
            @"(?<filename>[^:]*)\((?<line>\d+),(?<column>\d+)\):\s*(?<type>warning|error)\s*(?<message>.*)",
            RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        private static object editorCompilationSingletonInstance;
        private static MethodInfo getCompileMessagesMethod;

        private static readonly LazyLogger logger = Log.GetLazyLogger<Watchdog>();

        public static bool HasDiagnostics => diagnostics.Count > 0;

        static Watchdog()
        {
            if (CloneMode.Enabled)
            {
                return;
            }

            LoadTriggers();
            LoadCompilerMessagesFromConsole();

            if (EditorUtility.scriptCompilationFailed)
            {
                Trigger();
            }

            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            Events.registeringPackages += OnRegisteringPackages;
            EditorApplication.delayCall += OnLoad;
        }

        private static void LoadTriggers()
        {
            triggers.Clear();

            var types = TypeCache.GetTypesWithAttribute<WatchdogTriggerAttribute>();
            foreach (var type in types)
            {
                if (Activator.CreateInstance(type) is not IWatchdogTrigger trigger)
                {
                    continue;
                }

                triggers.Add(trigger);
            }
        }

        private static void OnLoad()
        {
            foreach (var trigger in triggers)
            {
                if (trigger.ListenForRecompile && GetTriggerListenForRecompile(trigger))
                {
                    SetTriggerListenForRecompile(trigger, false);
                    if (diagnostics.TryGetValue(trigger, out var compilerMessages))
                    {
                        trigger.OnRecompile(compilerMessages);
                    }
                    else
                    {
                        trigger.OnRecompile(Array.Empty<CompilerMessage>());
                    }
                }
            }
        }

        private static bool GetTriggerListenForRecompile(IWatchdogTrigger trigger)
            => SessionState.GetBool(trigger.GetType().FullName, false);

        private static void SetTriggerListenForRecompile(IWatchdogTrigger trigger, bool state)
            => SessionState.SetBool(trigger.GetType().FullName, state);

        private static void LoadCompilerMessagesFromConsole()
        {
            if (!EditorUtility.scriptCompilationFailed)
            {
                return;
            }

            try
            {
                var logEntriesType = Type.GetType(LogEntriesInternalAPI);
                var logEntryType = Type.GetType(LogEntryInternalAPI);

                var startGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", BindingFlags.Public | BindingFlags.Static);
                var logEntriesCount = (int)startGettingEntriesMethod.Invoke(null, null);
                var getEntryInternal = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Public | BindingFlags.Static);
                var log = Activator.CreateInstance(logEntryType);

                var compilerMessages = new CompilerMessage[logEntriesCount];
                for (var i = 0; i < logEntriesCount; i++)
                {
                    var result = (bool)getEntryInternal.Invoke(null, new[] { i, log });
                    if (!result)
                    {
                        continue;
                    }

                    var messageFieldInfo = logEntryType.GetField("message");
                    if (messageFieldInfo.GetValue(log) is not string messageString)
                    {
                        continue;
                    }

                    var match = compilerMessageRegex.Match(messageString);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var compilerMessage = new CompilerMessage
                    {
                        type = (CompilerMessageType)Enum.Parse(typeof(CompilerMessageType), match.Groups["type"].Value, true),
                        message = match.Groups["message"].Value,
                        column = int.Parse(match.Groups["column"].Value),
                        file = match.Groups["filename"].Value,
                        line = int.Parse(match.Groups["line"].Value),
                    };
                    compilerMessages[i] = compilerMessage;
                }

                LoadCompilerMessages(compilerMessages);

                var endMethod = logEntriesType.GetMethod("EndGettingEntries", BindingFlags.Public | BindingFlags.Static);
                endMethod.Invoke(null, null);
            }
            catch (Exception e)
            {
                logger.Debug($"Failed to get compiler messages via reflection: {e.Message} \n\n {e.StackTrace}");
            }
        }

        private static void OnRegisteringPackages(PackageRegistrationEventArgs args)
        {
            foreach (var package in args.changedFrom)
            {
                if (!package.name.Equals(Paths.packageId))
                {
                    continue;
                }

                CodeGenSelector.Clear(warn: false);
            }
        }

        private static void OnAssemblyCompilationFinished(string dllPath, CompilerMessage[] compilerMessages)
        {
            LoadCompilerMessages(compilerMessages);
            Trigger();
        }

        private static void LoadCompilerMessages(CompilerMessage[] compilerMessages)
        {
            diagnostics.Clear();

            foreach (var trigger in triggers)
            {
                foreach (var compilerMessage in compilerMessages)
                {
                    if (!trigger.Validate(compilerMessage))
                    {
                        continue;
                    }

                    if (!diagnostics.ContainsKey(trigger))
                    {
                        diagnostics[trigger] = new List<CompilerMessage>();
                    }

                    diagnostics[trigger].Add(compilerMessage);
                }
            }
        }

        private static void Trigger()
        {
            if (diagnostics.Count > 0)
            {
                var message = diagnostics
                    .Select(d => $"Issue: {d.Key.Issue}\nFix: {d.Key.Fix}")
                    .Aggregate((a, b) => $"{a}\n\n{b}");
                message += "\n\nThis is a destructive operation. Make sure to backup any important changes before proceeding.";

                if (!EditorUtility.DisplayDialog("coherence Watchdog", message, "Proceed", "Cancel",
                        DialogOptOutDecisionType.ForThisSession, watchdogDialogKey))
                {
                    return;
                }
            }

            // to avoid infinite compile error loop
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;

            var compilationRequested = false;
            foreach (var (trigger, compilerMessages) in diagnostics)
            {
                if (trigger.ListenForRecompile)
                {
                    SetTriggerListenForRecompile(trigger, true);
                }

                compilationRequested |= trigger.PerformFix(compilerMessages);
            }

            if (compilationRequested)
            {
                CompilationPipeline.RequestScriptCompilation();
            }
        }
    }
}

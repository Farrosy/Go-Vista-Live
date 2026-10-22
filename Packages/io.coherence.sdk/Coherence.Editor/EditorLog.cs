// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Text;
    using System.IO;
    using UnityEngine;
    using UnityEditor;
    using Log;
    using Logger = Log.Logger;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    [InitializeOnLoad]
    internal static class FileLog
    {
        private const string activeKey = "Coherence.FileLog.Active";
        private static readonly object threadlock = new();
        private static FileStream fileStream;

        static FileLog()
        {
#if !COHERENCE_DISABLE_FILE_LOG

            Logger.OnLog += OnLog;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            try
            {
                _ = Directory.CreateDirectory(Paths.AbsoluteCoherenceLogFilesPath);
            }
            catch (Exception e)
            {
                Logger.OnLog -= OnLog;
                Debug.LogWarning("Failed to create log directory at " +
                                 $"'{Paths.AbsoluteCoherenceLogFilesPath}': {e.Message}");
                return;
            }

            if (!SessionState.GetBool(activeKey, false))
            {
                SessionState.SetBool(activeKey, true);

                try
                {
                    File.Delete(Paths.AbsolutePreviousCoherenceLogFilePath);
                }
                catch (IOException)
                {
                    // Ignore if we can't delete the previous log file (e.g., file in use or not found).
                }
                catch (UnauthorizedAccessException)
                {
                    Logger.OnLog -= OnLog;
                    Debug.LogWarning($"Cannot write to log file at '{Paths.AbsolutePreviousCoherenceLogFilePath}': " +
                                     "Access is denied. Please check your file permissions for the folder. Or," +
                                     " disable file logging in Coherence settings using the " +
                                     "COHERENCE_DISABLE_FILE_LOG define symbol.");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                try
                {
                    File.Move(Paths.AbsoluteCurrentCoherenceLogFilePath, Paths.AbsolutePreviousCoherenceLogFilePath);
                }
                catch (FileNotFoundException)
                {
                    // Ignore if there is no current log file to move.
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            EnsureFileStreamOpen();
#endif
        }

        /// <summary>
        /// Opens <see cref="fileStream"/> if it isn't already open. Called both from the static constructor and
        /// lazily from <see cref="OnLog"/>, since domain reloads are not guaranteed to re-run the static
        /// constructor before the next log call (observed on some Unity versions), which would otherwise leave
        /// logging silently disabled for the rest of the session after <see cref="OnBeforeAssemblyReload"/> disposes it.
        /// </summary>
        private static void EnsureFileStreamOpen()
        {
            if (fileStream != null)
            {
                return;
            }

            try
            {
                fileStream = new FileStream(
                    Paths.AbsoluteCurrentCoherenceLogFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    bufferSize: 1);
                fileStream.Seek(0, SeekOrigin.End);
            }
            catch (Exception e)
            {
                Logger.OnLog -= OnLog;
                Debug.LogException(e);
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            Logger.OnLog -= OnLog;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            lock (threadlock)
            {
                fileStream?.Dispose();
                fileStream = null;
            }
        }

        internal static void SetLength(long length)
        {
            lock (threadlock)
            {
                if (fileStream == null)
                {
                    return;
                }

                fileStream.SetLength(length);

                // Dispose rather than keep using this instance: its internally tracked position/length can go
                // stale relative to the just-truncated file on some Unity versions, causing the next
                // Seek(SeekOrigin.End) in OnLog to land at the wrong offset instead of the true end of file.
                // EnsureFileStreamOpen will lazily reopen a fresh, correctly synced handle on the next log call.
                fileStream.Dispose();
                fileStream = null;
            }
        }

        internal static void OnLog(LogLevel logLevel, bool filtered, string message, Type source,
            ICollection<(string key, object value)> args)
        {
            if (filtered)
            {
                return;
            }

            var editorLogLevel = Log.GetSettings().EditorLogLevel;

            if (logLevel < editorLogLevel)
            {
                return;
            }

            try
            {
                var log =
                    $"{DateTime.UtcNow:u} - {source?.Name ?? "NoSource"} - {logLevel.ToString().ToUpperInvariant()} - {message ?? "No message"}";
                if (args != null && args.Count > 0)
                {
                    var argsDict = new Dictionary<string, string>();
                    foreach (var (key, value) in args)
                    {
                        argsDict[key] = value?.ToString();
                    }

                    var json = Utils.CoherenceJson.SerializeObject(
                        argsDict,
                        Formatting.None);

                    log += $" - {json}";
                }

                log += "\n";

                lock (threadlock)
                {
                    EnsureFileStreamOpen();

                    if (fileStream == null)
                    {
                        return;
                    }

                    fileStream.Seek(0, SeekOrigin.End);
                    var bytes = Encoding.UTF8.GetBytes(log);
                    fileStream.Write(bytes, 0, bytes.Length);
                    fileStream.Flush(true);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}

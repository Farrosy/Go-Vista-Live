// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log.Targets
{
    using System;
    using System.IO;
    using Logger = Logger;

    public class FileTarget : ILogTarget
    {
        public LogLevel Level { get; set; }
        public string FilePath { get; }

        private static readonly object threadlock = new();

        private bool disposed;
        private FileStream file;
        private StreamWriter writer;
        private bool initialized;
        private ILogFormatter formatter;

        public FileTarget(string filePath, ILogFormatter formatter)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            this.formatter = formatter;
        }

        ~FileTarget()
        {
            if (disposed)
            {
                return;
            }

            Dispose();
            Logger.SafeWarningLog($"Finalizer ~FileTarget executed without Dispose having been called. This is a potential resource leak.\nFilePath: {FilePath}");
        }

        public void Log(LogLevel level, string message, (string key, object value)[] args, Logger logger)
        {
            if (disposed)
            {
                return;
            }

            var result = formatter.Format(level, message, args, logger.Source, logger.UseWatermark);

            lock (threadlock)
            {
                if (disposed)
                {
                    return;
                }

                EnsureInitialized();
                writer?.Write(result);
            }
        }

        public void Dispose()
        {
            lock (threadlock)
            {
                if (disposed)
                {
                    return;
                }

                try
                {
                    writer?.Flush();
                    writer?.Dispose();
                    file?.Dispose();
                }
                catch (Exception e)
                {
                    Logger.SafeErrorLog($"Exception in {nameof(FileTarget)} disposal for path {FilePath}: {e.Message}");
                }

                writer = null;
                file = null;
                disposed = true;
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            GC.SuppressFinalize(this);

            try
            {
                // Make sure that the target directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

                // If file exists and is not empty, truncate it; otherwise, create a new file
                var fileInfo = new FileInfo(FilePath);
                var mode = (fileInfo.Exists && fileInfo.Length > 0) ? FileMode.Truncate : FileMode.OpenOrCreate;

                file = new FileStream(FilePath, mode, FileAccess.Write, FileShare.Read);
                writer = new StreamWriter(file);
                writer.AutoFlush = true;
            }
            catch (Exception e)
            {
                Logger.SafeErrorLog($"Failed to initialize {nameof(FileTarget)} logging for path {FilePath}: {e.Message}");
                Dispose();
            }
        }
    }
}

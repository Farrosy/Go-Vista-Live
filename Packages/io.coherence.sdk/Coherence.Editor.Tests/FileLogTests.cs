// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using Log;
    using NUnit.Framework;
    using Logger = Log.Logger;

    /// <summary>
    /// Edit mode tests for <see cref="FileLog"/>.
    /// </summary>
    public sealed class FileLogTests
    {
        private long initialFileLength;
        private LogLevel savedEditorLogLevel;

        [SetUp]
        public void SetUp()
        {
            initialFileLength = File.Exists(Paths.AbsoluteCurrentCoherenceLogFilePath)
                ? new FileInfo(Paths.AbsoluteCurrentCoherenceLogFilePath).Length
                : 0;
            savedEditorLogLevel = Log.GetSettings().EditorLogLevel;
        }

        [TearDown]
        public void TearDown()
        {
            Log.GetSettings().EditorLogLevel = savedEditorLogLevel;

            FileLog.SetLength(initialFileLength);
        }

        [Test]
        [Description("FileLog.OnLog is subscribed to Logger.OnLog so that log events reach the file.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void FileLog_IsSubscribedToLoggerOnLog()
        {
            var field = typeof(Logger).GetField(nameof(Logger.OnLog), BindingFlags.Static | BindingFlags.NonPublic);
            var del = field?.GetValue(null) as MulticastDelegate;
            var handlers = del?.GetInvocationList() ?? Array.Empty<Delegate>();

            Assert.That(handlers, Has.Some.Matches<Delegate>(d =>
                d.Method.Name == nameof(FileLog.OnLog) &&
                d.Method.DeclaringType == typeof(FileLog)));
        }

        [Test]
        [Description("A filtered log event is silently ignored and produces no file output.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void OnLog_WhenFiltered_DoesNotWriteToFile()
        {
            const string marker = "FILELOG_TEST_FILTERED_MARKER";

            FireOnLog(LogLevel.Info, filtered: true, marker, source: null);

            Assert.That(ReadNewContent(), Does.Not.Contain(marker));
        }

        [Test]
        [Description("A log event whose level is below the configured EditorLogLevel threshold is not written to file.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void OnLog_BelowEditorLogLevel_DoesNotWriteToFile()
        {
            const string marker = "FILELOG_TEST_BELOW_LEVEL_MARKER";
            Log.GetSettings().EditorLogLevel = LogLevel.Error;

            FireOnLog(LogLevel.Info, filtered: false, marker, source: null);

            Assert.That(ReadNewContent(), Does.Not.Contain(marker));
        }

        [Test]
        [Description("A log event at the configured EditorLogLevel threshold is written to file.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void OnLog_AtEditorLogLevel_WritesToFile()
        {
            const string marker = "FILELOG_TEST_AT_LEVEL_MARKER";
            Log.GetSettings().EditorLogLevel = LogLevel.Info;

            FireOnLog(LogLevel.Info, filtered: false, marker, source: null);

            Assert.That(ReadNewContent(), Does.Contain(marker));
        }

        [Test]
        [Description("A null source type causes the source segment of the log line to read 'NoSource'.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void OnLog_NullSource_WritesNoSourcePlaceholder()
        {
            FireOnLog(LogLevel.Info, filtered: false, "FILELOG_TEST_NULL_SOURCE", source: null);

            Assert.That(ReadNewContent(), Does.Contain("NoSource"));
        }

        [Test]
        [Description("A non-null source type writes the type's simple name to the log line.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void OnLog_WithSource_WritesSourceTypeName()
        {
            FireOnLog(LogLevel.Info, filtered: false, "FILELOG_TEST_SOURCE_TYPE", source: typeof(FileLogTests));

            Assert.That(ReadNewContent(), Does.Contain(nameof(FileLogTests)));
        }

        [Test]
        [Description("A null message causes the message segment of the log line to read 'No message'.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void OnLog_NullMessage_WritesNoMessagePlaceholder()
        {
            FireOnLog(LogLevel.Info, filtered: false, message: null, source: typeof(FileLogTests));

            Assert.That(ReadNewContent(), Does.Contain("No message"));
        }

        [Test]
        [Description("Key-value args are serialized as a JSON object and appended to the log line.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void OnLog_WithArgs_AppendsArgsAsJson()
        {
            var args = new (string key, object value)[] { ("testKey", "testValue") };

            FireOnLog(LogLevel.Info, filtered: false, "FILELOG_TEST_WITH_ARGS", source: null, args);

            var content = ReadNewContent();
            Assert.That(content, Does.Contain("testKey"));
            Assert.That(content, Does.Contain("testValue"));
        }

        [Test]
        [Description("The log level segment of each written entry is uppercased.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void OnLog_LogLevelSegment_IsUppercase()
        {
            FireOnLog(LogLevel.Warning, filtered: false, "FILELOG_TEST_UPPERCASE_LEVEL", source: null);

            Assert.That(ReadNewContent(), Does.Contain("WARNING"));
        }

        [Test]
        [Description("A log event written after the internal FileStream has been disposed and nulled out " +
                      "(simulating a domain reload that disposes it without the static constructor reopening it " +
                      "before the next log call) still reaches the file, since OnLog reopens the stream lazily.")]
#if COHERENCE_DISABLE_FILE_LOG
        [Ignore("File logging is disabled via COHERENCE_DISABLE_FILE_LOG define symbol.")]
#endif
        public void OnLog_AfterFileStreamDisposedAndNulled_ReopensAndWritesToFile()
        {
            const string marker = "FILELOG_TEST_REOPEN_AFTER_NULL_MARKER";
            var field = typeof(FileLog).GetField("fileStream", BindingFlags.Static | BindingFlags.NonPublic);

            (field?.GetValue(null) as IDisposable)?.Dispose();
            field?.SetValue(null, null);

            FireOnLog(LogLevel.Info, filtered: false, marker, source: null);

            Assert.That(ReadNewContent(), Does.Contain(marker));
        }

        private static void FireOnLog(LogLevel level, bool filtered, string message, Type source,
            (string key, object value)[] args = null)
        {
            ICollection<(string key, object value)> argsToPass = args ?? Array.Empty<(string, object)>();
            FileLog.OnLog(level, filtered, message, source, argsToPass);
        }

        private string ReadNewContent()
        {
            // A freshly-opened read handle can briefly observe a stale length for a file another handle
            // just wrote and flushed on the same process, so retry for a bounded window before giving up.
            var deadline = DateTime.UtcNow.AddMilliseconds(500);
            string content;

            do
            {
                content = ReadFileTail();
            }
            while (content.Length == 0 && DateTime.UtcNow < deadline);

            return content;
        }

        private string ReadFileTail()
        {
            if (!File.Exists(Paths.AbsoluteCurrentCoherenceLogFilePath))
            {
                return string.Empty;
            }

            using var fs = new FileStream(Paths.AbsoluteCurrentCoherenceLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(initialFileLength, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.
namespace Coherence.Editor.Tests
{
    using System.IO;
    using Coherence.Tests;
    using NUnit.Framework;

    /// <summary>
    /// Unit tests for <see cref="Paths"/>, focused on resolving Unity's editor log directory
    /// from <see cref="UnityEngine.Application.consoleLogPath"/>.
    /// </summary>
    public sealed class PathsTests : CoherenceTest
    {
        [TestCase(null)]
        [TestCase("")]
        public void ResolveEditorLogDirectory_Returns_Empty_When_No_ConsoleLogPath(string consoleLogPath)
        {
            // No console log path is available e.g. when logging is disabled via '-nolog'.
            // The result must be a valid (empty) string rather than throwing, since this runs
            // inside the static constructor of Paths.
            var directory = Paths.ResolveEditorLogDirectory(consoleLogPath);
            Assert.That(directory, Is.Empty);
        }

        [Test]
        public void ResolveEditorLogDirectory_Returns_Containing_Directory()
        {
            var consoleLogPath = "/some/dir/Editor.log";
            var directory = Paths.ResolveEditorLogDirectory(consoleLogPath);
            Assert.That(directory, Is.EqualTo(Path.GetDirectoryName(consoleLogPath)));
        }

        [Test]
        public void ResolveEditorLogDirectory_Follows_Custom_LogFile_Directory()
        {
            // When Unity is launched with '-logFile <custom>', consoleLogPath points at that custom
            // file; the resolved directory must follow it rather than a hardcoded default location.
            var consoleLogPath = "/custom/path/build.log";
            var directory = Paths.ResolveEditorLogDirectory(consoleLogPath);
            Assert.That(directory, Is.EqualTo(Path.GetDirectoryName(consoleLogPath)));
        }

        [Test]
        public void CurrentEditorLogFileAbsolutePath_Does_Not_Throw_And_Is_Not_Null()
        {
            // Accessing the static member must not throw a TypeInitializationException regardless
            // of the current console log path.
            Assert.That(Paths.CurrentEditorLogFileAbsolutePath, Is.Not.Null);
        }
    }
}

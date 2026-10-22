// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tests.Editor
{
    using System;
    using System.Text.RegularExpressions;
    using Coherence.CodeGen;
    using Coherence.Editor;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    [TestFixture]
    public class CodeGenSelectorTests
    {
        [Test]
        [Description("No args → all CodeGenData fields remain at their defaults")]
        public void ApplyCliArgsOverrides_EmptyArgs_ChangesNothing()
        {
            var data = new CodeGenData();

            CodeGenSelector.ApplyCliArgsOverrides(data, allowSchemaOverrides: false, args: Array.Empty<string>());

            Assert.That(data.NoUnityReferences, Is.False);
            Assert.That(data.OutputDirectory, Is.Null);
            Assert.That(data.SchemaDefinition, Is.Null);
        }

        [Test]
        [Description("--no-unity-refs sets NoUnityReferences to true")]
        public void ApplyCliArgsOverrides_NoUnityRefs_SetsFlag()
            => ApplyAndAssert(
                new[] { "--no-unity-refs" },
                data => Assert.That(data.NoUnityReferences, Is.True));

        [Test]
        [Description("--output-dir <path> sets OutputDirectory to the given path")]
        public void ApplyCliArgsOverrides_OutputDir_SetsOutputDirectory()
            => ApplyAndAssert(
                new[] { "--output-dir", "path/to/output" },
                data => Assert.That(data.OutputDirectory, Is.EqualTo("path/to/output")));

        [Test]
        [Description("--output-dir consumes the next token so it is not re-processed as a flag")]
        public void ApplyCliArgsOverrides_OutputDir_SkipsNextArg()
        {
            var data = new CodeGenData();

            CodeGenSelector.ApplyCliArgsOverrides(data, allowSchemaOverrides: false,
                args: new[] { "--output-dir", "some/path", "--no-unity-refs" });

            Assert.That(data.OutputDirectory, Is.EqualTo("some/path"));
            Assert.That(data.NoUnityReferences, Is.True);
        }

        [Test]
        [Description("--output-dir with no following token throws ArgumentException")]
        public void ApplyCliArgsOverrides_OutputDirMissingValue_ThrowsArgumentException()
            => Assert.Throws<ArgumentException>(() =>
                CodeGenSelector.ApplyCliArgsOverrides(new CodeGenData(), allowSchemaOverrides: false,
                    args: new[] { "--output-dir" }));

        [Test]
        [Description("--schema with no following token throws ArgumentException")]
        public void ApplyCliArgsOverrides_SchemaMissingValue_ThrowsArgumentException()
            => Assert.Throws<ArgumentException>(() =>
                CodeGenSelector.ApplyCliArgsOverrides(new CodeGenData(), allowSchemaOverrides: false,
                    args: new[] { "--schema" }));

        [Test]
        [Description("--schema when allowSchemaOverrides is false logs an error and leaves SchemaDefinition unchanged")]
        public void ApplyCliArgsOverrides_SchemaWhenOverrideForbidden_DoesNotChangeSchemaDefinition()
        {
            var data = new CodeGenData();

            LogAssert.Expect(LogType.Error, new Regex(".*Trying to override the Schemas.*"));
            CodeGenSelector.ApplyCliArgsOverrides(data, allowSchemaOverrides: false,
                args: new[] { "--schema", "some/path.schema" });

            Assert.That(data.SchemaDefinition, Is.Null);
        }

        [Test]
        [Description("Unrecognised args are silently ignored and leave CodeGenData unchanged")]
        public void ApplyCliArgsOverrides_UnknownArgs_AreIgnored()
        {
            var data = new CodeGenData();

            CodeGenSelector.ApplyCliArgsOverrides(data, allowSchemaOverrides: false,
                args: new[] { "--unknown-flag", "some-value", "--another-unknown" });

            Assert.That(data.NoUnityReferences, Is.False);
            Assert.That(data.OutputDirectory, Is.Null);
        }

        [Test]
        [Description("Multiple recognised flags in one args array are all applied")]
        public void ApplyCliArgsOverrides_MultipleArgs_AllApplied()
        {
            var data = new CodeGenData();

            CodeGenSelector.ApplyCliArgsOverrides(data, allowSchemaOverrides: false,
                args: new[] { "--no-unity-refs", "--output-dir", "build/output" });

            Assert.That(data.NoUnityReferences, Is.True);
            Assert.That(data.OutputDirectory, Is.EqualTo("build/output"));
        }

        private static void ApplyAndAssert(string[] args, Action<CodeGenData> assertion)
        {
            var data = new CodeGenData();
            CodeGenSelector.ApplyCliArgsOverrides(data, allowSchemaOverrides: false, args: args);
            assertion(data);
        }
    }
}

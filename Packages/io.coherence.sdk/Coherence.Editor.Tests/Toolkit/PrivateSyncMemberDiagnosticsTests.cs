// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests.Toolkit
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Coherence.Editor.Toolkit;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public class PrivateSyncMemberDiagnosticsTests : CoherenceTest
    {
        private static readonly BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        [Test]
        [Description("A non-public field on a MonoBehaviour logs a warning")]
        public void NonPublic_Field_On_MonoBehaviour_Logs_Warning()
        {
            var field = typeof(MonoBehaviourFixture).GetField(nameof(MonoBehaviourFixture.nonPublicField), NonPublicInstance);

            LogAssert.Expect(LogType.Warning, new Regex($@"Field MonoBehaviourFixture\.{nameof(MonoBehaviourFixture.nonPublicField)} is marked with \[Sync\].*not public"));

            PrivateSyncMemberDiagnostics.LogPrivateMemberWarnings(new[] { field }, Enumerable.Empty<MethodInfo>());
        }

        [Test]
        [Description("A public field on a MonoBehaviour does not log a warning")]
        public void Public_Field_On_MonoBehaviour_Does_Not_Log_Warning()
        {
            var field = typeof(MonoBehaviourFixture).GetField(nameof(MonoBehaviourFixture.publicField), PublicInstance);

            PrivateSyncMemberDiagnostics.LogPrivateMemberWarnings(new[] { field }, Enumerable.Empty<MethodInfo>());

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Description("A non-public field on a non-MonoBehaviour does not log a warning")]
        public void NonPublic_Field_On_Non_MonoBehaviour_Does_Not_Log_Warning()
        {
            var field = typeof(NonMonoBehaviourFixture).GetField(nameof(NonMonoBehaviourFixture.nonPublicField), NonPublicInstance);

            PrivateSyncMemberDiagnostics.LogPrivateMemberWarnings(new[] { field }, Enumerable.Empty<MethodInfo>());

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Description("A non-public method on a MonoBehaviour logs a warning")]
        public void NonPublic_Method_On_MonoBehaviour_Logs_Warning()
        {
            var method = typeof(MonoBehaviourFixture).GetMethod(nameof(MonoBehaviourFixture.NonPublicMethod), NonPublicInstance);

            LogAssert.Expect(LogType.Warning, new Regex($@"Method MonoBehaviourFixture\.{nameof(MonoBehaviourFixture.NonPublicMethod)} is marked with \[Command\].*not public"));

            PrivateSyncMemberDiagnostics.LogPrivateMemberWarnings(Enumerable.Empty<FieldInfo>(), new[] { method });
        }

        [Test]
        [Description("A public method on a MonoBehaviour does not log a warning")]
        public void Public_Method_On_MonoBehaviour_Does_Not_Log_Warning()
        {
            var method = typeof(MonoBehaviourFixture).GetMethod(nameof(MonoBehaviourFixture.PublicMethod), PublicInstance);

            PrivateSyncMemberDiagnostics.LogPrivateMemberWarnings(Enumerable.Empty<FieldInfo>(), new[] { method });

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Description("A non-public method on a non-MonoBehaviour does not log a warning")]
        public void NonPublic_Method_On_Non_MonoBehaviour_Does_Not_Log_Warning()
        {
            var method = typeof(NonMonoBehaviourFixture).GetMethod(nameof(NonMonoBehaviourFixture.NonPublicMethod), NonPublicInstance);

            PrivateSyncMemberDiagnostics.LogPrivateMemberWarnings(Enumerable.Empty<FieldInfo>(), new[] { method });

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Description("Empty collections do not log any warnings")]
        public void Empty_Collections_Do_Not_Log_Warnings()
        {
            PrivateSyncMemberDiagnostics.LogPrivateMemberWarnings(Enumerable.Empty<FieldInfo>(), Enumerable.Empty<MethodInfo>());

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Description("Multiple non-public fields on a MonoBehaviour each log a warning")]
        public void Multiple_NonPublic_Fields_Log_Warning_For_Each()
        {
            var fields = new[]
            {
                typeof(MonoBehaviourFixture).GetField(nameof(MonoBehaviourFixture.nonPublicField), NonPublicInstance),
                typeof(MonoBehaviourFixture).GetField(nameof(MonoBehaviourFixture.anotherNonPublicField), NonPublicInstance),
            };

            LogAssert.Expect(LogType.Warning, new Regex($@"Field MonoBehaviourFixture\.{nameof(MonoBehaviourFixture.nonPublicField)}"));
            LogAssert.Expect(LogType.Warning, new Regex($@"Field MonoBehaviourFixture\.{nameof(MonoBehaviourFixture.anotherNonPublicField)}"));

            PrivateSyncMemberDiagnostics.LogPrivateMemberWarnings(fields, Enumerable.Empty<MethodInfo>());
        }

        // Fixture types. Fields and methods intentionally do not carry [Sync]/[Command] to avoid
        // triggering PrivateSyncMemberDiagnostics during domain reload and polluting other tests.
        // Members are internal (not private) so nameof() can resolve them from the test scope.
        private class MonoBehaviourFixture : MonoBehaviour
        {
#pragma warning disable CS0649
            internal float nonPublicField;
            internal float anotherNonPublicField;
            public float publicField;
#pragma warning restore CS0649

            internal void NonPublicMethod() { }
            public void PublicMethod() { }
        }

        private class NonMonoBehaviourFixture
        {
#pragma warning disable CS0649
            internal float nonPublicField;
#pragma warning restore CS0649

            internal void NonPublicMethod() { }
        }
    }
}

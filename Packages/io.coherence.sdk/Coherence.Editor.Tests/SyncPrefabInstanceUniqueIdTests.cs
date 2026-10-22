// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using Coherence.Tests;
    using Coherence.Toolkit;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Edit mode unit tests for <see cref="SyncPrefabInstanceUniqueId.SyncInstanceUpdated"/>.
    /// </summary>
    public sealed class SyncPrefabInstanceUniqueIdTests : CoherenceTest
    {
        private GameObject gameObject;
        private CoherenceSync sync;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            gameObject = new GameObject(nameof(SyncPrefabInstanceUniqueIdTests));
            sync = gameObject.AddComponent<CoherenceSync>();
            sync.uniquenessType = CoherenceSync.UniquenessType.NoDuplicates;
        }

        [TearDown]
        public override void TearDown()
        {
            Object.DestroyImmediate(gameObject);
            base.TearDown();
        }

        [Test]
        [Description("Passing a null instance should not throw.")]
        public void SyncInstanceUpdated_NullInstance_DoesNotThrow() =>
            Assert.DoesNotThrow(() => SyncPrefabInstanceUniqueId.SyncInstanceUpdated(null, "some-id"));

        [Test]
        [Description("A sync with AllowDuplicates uniqueness is not unique, so the scene UUID should not be set.")]
        public void SyncInstanceUpdated_AllowDuplicates_DoesNotSetId()
        {
            sync.uniquenessType = CoherenceSync.UniquenessType.AllowDuplicates;

            SyncPrefabInstanceUniqueId.SyncInstanceUpdated(sync, "test-id");

            Assert.That(GetScenePrefabInstanceUUID(), Is.Empty);
        }

        [Test]
        [Description("When the incoming id matches ManualUniqueId the scene UUID is already up to date and should not be overwritten.")]
        public void SyncInstanceUpdated_MatchingManualUniqueId_DoesNotSetId()
        {
            sync.ManualUniqueId = "test-id";

            SyncPrefabInstanceUniqueId.SyncInstanceUpdated(sync, "test-id");

            Assert.That(GetScenePrefabInstanceUUID(), Is.Empty);
        }

        [Test]
        [Description("When the scene UUID is empty it should be assigned the incoming id.")]
        public void SyncInstanceUpdated_EmptySceneUUID_SetsId()
        {
            SyncPrefabInstanceUniqueId.SyncInstanceUpdated(sync, "test-id");

            Assert.That(GetScenePrefabInstanceUUID(), Is.EqualTo("test-id"));
        }

        [Test]
        [Description("An existing scene UUID should not be overwritten when forceUpdate is false.")]
        public void SyncInstanceUpdated_NonEmptySceneUUID_PreservesIdWhenNotForced()
        {
            SetScenePrefabInstanceUUID("existing-uuid");

            SyncPrefabInstanceUniqueId.SyncInstanceUpdated(sync, "new-id");

            Assert.That(GetScenePrefabInstanceUUID(), Is.EqualTo("existing-uuid"));
        }

        [Test]
        [Description("An existing scene UUID should be overwritten when forceUpdate is true.")]
        public void SyncInstanceUpdated_NonEmptySceneUUID_OverridesIdWhenForced()
        {
            SetScenePrefabInstanceUUID("existing-uuid");

            SyncPrefabInstanceUniqueId.SyncInstanceUpdated(sync, "new-id", forceUpdate: true);

            Assert.That(GetScenePrefabInstanceUUID(), Is.EqualTo("new-id"));
        }

        private string GetScenePrefabInstanceUUID()
        {
            using var so = new SerializedObject(sync);
            so.Update();
            return so.FindProperty(CoherenceSync.Property.scenePrefabInstanceUUID).stringValue;
        }

        private void SetScenePrefabInstanceUUID(string value)
        {
            using var so = new SerializedObject(sync);
            so.FindProperty(CoherenceSync.Property.scenePrefabInstanceUUID).stringValue = value;
            so.ApplyModifiedProperties();
        }
    }
}

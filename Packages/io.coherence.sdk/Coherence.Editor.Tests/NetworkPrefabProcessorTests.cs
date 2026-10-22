namespace Coherence.Editor.Tests
{
    using System.Collections.Generic;
    using Coherence.Log;
    using Coherence.Tests;
    using Coherence.Toolkit;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Edit mode unit tests for <see cref="NetworkPrefabProcessor"/>.
    /// </summary>
    public class NetworkPrefabProcessorTests : CoherenceTest
    {
        [Test]
        [Description("TryUpdatePrefab should update the bakedScriptType serialized property when its current value differs from the expected baked script full type name.")]
        public void TryUpdatePrefab_Updates_bakedScriptType()
        {
#if COHERENCE_DONT_UPDATE_PREFABS
            Assert.Ignore("Can't run test because COHERENCE_DONT_UPDATE_PREFABS is defined.");
#endif
            var sync = CreateSync();
            var callbackWasInvoked = false;
            NetworkPrefabProcessor.TryUpdatePrefab(sync);
            var serializedObject = new SerializedObject(sync);
            using var bakedScriptTypeProperty = serializedObject.FindProperty(CoherenceSync.Property.bakedScriptType);
            const string oldValue = "x";
            bakedScriptTypeProperty.stringValue = oldValue;
            serializedObject.ApplyModifiedProperties();

            var wasUpdated = NetworkPrefabProcessor.TryUpdatePrefab(sync, () => callbackWasInvoked = true);

            Assert.That(wasUpdated, Is.True);
            Assert.That(callbackWasInvoked, Is.True);
            serializedObject.Update();
            Assert.That(bakedScriptTypeProperty.stringValue, Is.Not.EqualTo(oldValue));
        }

        [Test]
        [Description("TryUpdatePrefab should clear the scenePrefabInstanceUUID serialized property when it has a non-empty value.")]
        public void TryUpdatePrefab_Updates_scenePrefabInstanceUUID()
        {
#if COHERENCE_DONT_UPDATE_PREFABS
            Assert.Ignore("Can't run test because COHERENCE_DONT_UPDATE_PREFABS is defined.");
#endif
            var sync = CreateSync();
            var callbackWasInvoked = false;
            NetworkPrefabProcessor.TryUpdatePrefab(sync);
            var serializedObject = new SerializedObject(sync);
            using var scenePrefabInstanceUUIDProperty = serializedObject.FindProperty(CoherenceSync.Property.scenePrefabInstanceUUID);
            const string oldValue = "x";
            scenePrefabInstanceUUIDProperty.stringValue = oldValue;
            serializedObject.ApplyModifiedProperties();

            var wasUpdated = NetworkPrefabProcessor.TryUpdatePrefab(sync, () => callbackWasInvoked = true);

            Assert.That(wasUpdated, Is.True);
            Assert.That(callbackWasInvoked, Is.True);
            serializedObject.Update();
            Assert.That(scenePrefabInstanceUUIDProperty.stringValue, Is.Not.EqualTo(oldValue));
        }

        [Test]
        [Description("TryUpdatePrefab should return false and not invoke the callback on subsequent calls when no properties need updating.")]
        public void TryUpdatePrefab_Does_Nothing_On_Repeat_Executions()
        {
            #if COHERENCE_DONT_UPDATE_PREFABS
            Assert.Ignore("Can't run test because COHERENCE_DONT_UPDATE_PREFABS is defined.");
            #endif

            var sync = CreateSync();
            var wasCalled = false;

            NetworkPrefabProcessor.TryUpdatePrefab(sync);
            NetworkPrefabProcessor.TryUpdatePrefab(sync, () => wasCalled = true);
            NetworkPrefabProcessor.TryUpdatePrefab(sync, () => wasCalled = true);

            Assert.That(wasCalled, Is.False);
        }

        [Test]
        [Description("TryUpdatePrefab should return false without throwing when the sync argument is null.")]
        public void TryUpdatePrefab_Returns_False_For_Null_Sync() =>
            Assert.That(NetworkPrefabProcessor.TryUpdatePrefab(null), Is.False);

        [Test]
        [Description("TryUpdatePrefab should return false without throwing when the sync argument refers to a destroyed UnityEngine.Object.")]
        public void TryUpdatePrefab_Returns_False_For_Destroyed_Sync()
        {
            var sync = CreateSync();
            Object.DestroyImmediate(sync);

            Assert.That(NetworkPrefabProcessor.TryUpdatePrefab(sync), Is.False);
        }

        [Test]
        [Description("UpdateNetworkPrefabs should return false and log an error when the prefab list is null.")]
        public void UpdateNetworkPrefabs_Returns_False_For_Null_List()
        {
#if COHERENCE_DONT_UPDATE_PREFABS
            Assert.Ignore("Can't run test because COHERENCE_DONT_UPDATE_PREFABS is defined.");
#endif
            var cloneModeWas = CloneMode.Enabled;
            CloneMode.Enabled = false;
            var loggerWas = NetworkPrefabProcessor.Logger;
            NetworkPrefabProcessor.Logger.Logger = logger;
            var editorStatusWas = NetworkPrefabProcessor.EditorStatusProvider;
            NetworkPrefabProcessor.EditorStatusProvider = new NotCompilingEditorStatus();

            bool result;
            try
            {
                result = NetworkPrefabProcessor.UpdateNetworkPrefabs(null, new List<CoherenceSync>());
            }
            finally
            {
                NetworkPrefabProcessor.EditorStatusProvider = editorStatusWas;
                NetworkPrefabProcessor.Logger = loggerWas;
                CloneMode.Enabled = cloneModeWas;
            }

            Assert.That(result, Is.False);
            Assert.That(logger.GetCountForErrorID(Error.EditorNetworkPrefabProcessorUpdateNullPrefab), Is.EqualTo(1));
        }

        [Test]
        [Description("UpdateNetworkPrefabs should return false when the prefab list is empty.")]
        public void UpdateNetworkPrefabs_Returns_False_For_Empty_List() =>
            Assert.That(NetworkPrefabProcessor.UpdateNetworkPrefabs(new List<CoherenceSync>(), new List<CoherenceSync>()), Is.False);

        [Test]
        [Description("UpdateNetworkPrefabs should return false and skip null elements in the prefab list without throwing.")]
        public void UpdateNetworkPrefabs_Returns_False_For_List_With_Null_Elements() =>
            Assert.That(NetworkPrefabProcessor.UpdateNetworkPrefabs(new List<CoherenceSync> { null }, new List<CoherenceSync>()), Is.False);

        private static CoherenceSync CreateSync() => new GameObject(nameof(CoherenceSync)).AddComponent<CoherenceSync>();

        private sealed class NotCompilingEditorStatus : EditorStatus
        {
            public override bool IsCompiling => false;
        }
    }
}

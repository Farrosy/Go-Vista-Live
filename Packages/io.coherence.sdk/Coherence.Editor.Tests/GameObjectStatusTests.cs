// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System.Collections.Generic;
    using Coherence.Tests;
    using Coherence.Toolkit;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    /// <summary>
    /// Edit mode unit tests for <see cref="GameObjectStatus"/>.
    /// </summary>
    public sealed class GameObjectStatusTests : CoherenceTest
    {
        private readonly List<string> createdAssetPaths = new();
        private readonly List<GameObject> createdSceneObjects = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in createdSceneObjects)
            {
                if (go)
                {
                    Object.DestroyImmediate(go);
                }
            }

            createdSceneObjects.Clear();

            for (var i = createdAssetPaths.Count - 1; i >= 0; i--)
            {
                AssetDatabase.DeleteAsset(createdAssetPaths[i]);
            }

            createdAssetPaths.Clear();

            base.TearDown();
        }

        [Test]
        [Description("A root scene object not part of any prefab should report as a non-prefab hierarchy root.")]
        public void SceneRootObject_ReportsAsNonPrefabHierarchyRoot()
        {
            var go = CreateSceneObject("SceneRoot");

            var status = new GameObjectStatus(go);

            Assert.That(status.IsRootOfNonPrefabHierarchy, Is.True);
            Assert.That(status.IsAsset, Is.False);
            Assert.That(status.IsVariant, Is.False);
            Assert.That(status.IsInstanceInScene, Is.False);
            Assert.That(status.IsInPrefabStage, Is.False);
        }

        [Test]
        [Description("A child of a plain scene object should not be flagged as any kind of hierarchy root.")]
        public void SceneChildObject_IsNotHierarchyRoot()
        {
            var parent = CreateSceneObject("Parent");
            var child = CreateSceneObject("Child");
            child.transform.SetParent(parent.transform);

            var status = new GameObjectStatus(child);

            Assert.That(status.IsRootOfNonPrefabHierarchy, Is.False);
            Assert.That(status.PrefabInstanceStatus, Is.EqualTo(PrefabInstanceStatus.NotAPrefab));
        }

        [Test]
        [Description("The root of a saved prefab asset should be identified as an asset and an asset hierarchy root.")]
        public void PrefabAssetRoot_IsAssetAndAssetHierarchyRoot()
        {
            var prefab = CreatePrefabAsset("TestBase");

            var status = new GameObjectStatus(prefab);

            Assert.That(status.IsAsset, Is.True);
            Assert.That(status.IsRootOfAssetHierarchy, Is.True);
            Assert.That(status.IsVariantAsset, Is.False);
            Assert.That(status.IsInstanceInScene, Is.False);
        }

        [Test]
        [Description("A connected prefab instance root in a scene should be identified as an instance hierarchy root.")]
        public void PrefabInstanceRoot_IsInstanceHierarchyRoot()
        {
            var prefab = CreatePrefabAsset("TestBase");
            var instance = InstantiatePrefab(prefab);

            var status = new GameObjectStatus(instance);

            Assert.That(status.IsInstanceInScene, Is.True);
            Assert.That(status.IsRootOfInstanceHierarchy, Is.True);
            Assert.That(status.PrefabInstanceStatus, Is.EqualTo(PrefabInstanceStatus.Connected));
            Assert.That(status.IsNearestPrefabInstanceRoot, Is.True);
            Assert.That(status.IsAsset, Is.False);
        }

        [Test]
        [Description("The root of a variant prefab asset should be flagged as both a variant asset and an asset hierarchy root.")]
        public void VariantPrefabAssetRoot_IsVariantAssetAndAssetHierarchyRoot()
        {
            var basePrefab = CreatePrefabAsset("TestBase");
            var variantPrefab = CreateVariantPrefabAsset(basePrefab, "TestVariant");

            var status = new GameObjectStatus(variantPrefab);

            Assert.That(status.IsVariantAsset, Is.True);
            Assert.That(status.IsVariant, Is.True);
            Assert.That(status.IsAsset, Is.True);
            Assert.That(status.IsRootOfAssetHierarchy, Is.True);
        }

        [Test]
        [Description("An instance of a variant prefab in a scene should be flagged as a variant instance and as connected to a variant prefab.")]
        public void VariantPrefabInstanceRoot_IsVariantInstanceInScene()
        {
            var basePrefab = CreatePrefabAsset("TestBase");
            var variantPrefab = CreateVariantPrefabAsset(basePrefab, "TestVariant");
            var instance = InstantiatePrefab(variantPrefab);

            var status = new GameObjectStatus(instance);

            Assert.That(status.IsVariantInstance, Is.True);
            Assert.That(status.IsVariant, Is.True);
            Assert.That(status.IsInstanceInScene, Is.True);
            Assert.That(status.IsVariantAsset, Is.True);
        }

        [Test]
        [Description("The root of a prefab opened in the prefab stage should report as being in the stage and as the stage hierarchy root.")]
        public void PrefabStageRoot_IsInPrefabStageAndStageHierarchyRoot()
        {
            var prefab = CreatePrefabAsset("TestBase");
            var stage = PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(prefab));
            try
            {
                var root = stage.prefabContentsRoot;
                var status = new GameObjectStatus(root);

                Assert.That(status.IsInPrefabStage, Is.True);
                Assert.That(status.IsRootOfPrefabStageHierarchy, Is.True);
                Assert.That(status.IsAsset, Is.True);
            }
            finally
            {
                StageUtility.GoToMainStage();
            }
        }

        [Test]
        [Description("A variant prefab stage root with CoherenceSync added as an override relative to the base prefab should report SyncIsAddedOverride=true and IsVariantStage=true, so the Configure window allows editing.")]
        public void VariantPrefabStageRoot_WithCoherenceSyncOverride_AllowsConfigureWindowEditing()
        {
            var basePrefab = CreatePrefabAsset("TestBase");
            var variantPrefab = CreateVariantPrefabWithCoherenceSync(basePrefab, "TestVariant");

            var stage = PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(variantPrefab));
            try
            {
                var root = stage.prefabContentsRoot;
                var status = new GameObjectStatus(root);

                // Both must be true for the pre-fix condition to have incorrectly blocked editing.
                // IsVariantStage=true is what makes the condition false and allows editing.
                Assert.That(status.SyncIsAddedOverride, Is.True, "CoherenceSync is an added override relative to the base prefab.");
                Assert.That(status.IsVariantStage, Is.True, "The prefab stage belongs to a variant prefab.");
            }
            finally
            {
                StageUtility.GoToMainStage();
            }
        }

        [Test]
        [Description("A variant prefab asset root with CoherenceSync saved as an override should have IsVariantAsset=true. This is what the fix relies on to allow Configure window editing when the prefab is selected as an asset outside the prefab stage.")]
        public void VariantPrefabAssetRoot_WithCoherenceSyncOverride_HasIsVariantAsset()
        {
            var basePrefab = CreatePrefabAsset("TestBase");
            var variantPrefab = CreateVariantPrefabWithCoherenceSync(basePrefab, "TestVariant");
            var status = new GameObjectStatus(variantPrefab);

            Assert.That(status.IsVariantAsset, Is.True);
            Assert.That(status.IsVariantStage, Is.False, "We are not in a prefab stage.");
            // Note: whether SyncIsAddedOverride is true for a prefab asset (vs. a prefab stage
            // root) depends on Unity's IsAddedComponentOverride behaviour for asset objects.
            // The fix guards against the blocking condition regardless: IsVariantAsset=true means
            // SyncIsAddedOverride && !IsVariantStage && !IsVariantAsset is always false.
        }

        private GameObject CreateSceneObject(string name)
        {
            var go = new GameObject(name);
            createdSceneObjects.Add(go);
            return go;
        }

        private GameObject CreatePrefabAsset(string name)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath($"Assets/{name}.prefab");
            createdAssetPaths.Add(path);
            var go = new GameObject(name);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private GameObject CreateVariantPrefabAsset(GameObject basePrefab, string name)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath($"Assets/{name}.prefab");
            createdAssetPaths.Add(path);
            var instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            var variantPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(instance, path, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(instance);
            return variantPrefab;
        }

        private GameObject InstantiatePrefab(GameObject prefab)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            createdSceneObjects.Add(instance);
            return instance;
        }

        /// <summary>
        /// Creates a variant prefab with <see cref="CoherenceSync"/> added as a component override
        /// (i.e. present in the variant but not in the base prefab).
        /// </summary>
        private GameObject CreateVariantPrefabWithCoherenceSync(GameObject basePrefab, string name)
        {
            var variantPrefab = CreateVariantPrefabAsset(basePrefab, name);
            var variantPath = AssetDatabase.GetAssetPath(variantPrefab);

            // Instantiate the variant, add CoherenceSync as an instance override, then apply
            // it specifically to the variant asset — not to the base prefab.
            var instance = PrefabUtility.InstantiatePrefab(variantPrefab) as GameObject;
            var sync = instance.AddComponent<CoherenceSync>();
            PrefabUtility.ApplyAddedComponent(sync, variantPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(instance);

            return AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
        }
    }
}

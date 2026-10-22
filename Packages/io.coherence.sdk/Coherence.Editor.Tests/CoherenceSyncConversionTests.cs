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
    /// Edit mode tests for <see cref="CoherenceSyncUtils.TryConvertToCoherenceSyncPrefab"/>.
    /// </summary>
    public sealed class CoherenceSyncConversionTests : CoherenceTest
    {
        private const string BaseName = nameof(CoherenceSyncConversionTests);

        private string basePrefabPath;
        private GameObject basePrefab;
        private GameObject instance;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            basePrefabPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/{BaseName}_Base.prefab");
            var go = new GameObject(BaseName);
            basePrefab = PrefabUtility.SaveAsPrefabAsset(go, basePrefabPath);
            Object.DestroyImmediate(go);
        }

        [TearDown]
        public override void TearDown()
        {
            if (instance)
            {
                Undo.ClearUndo(instance);
                Object.DestroyImmediate(instance);
                instance = null;
            }

            if (basePrefab)
            {
                AssetDatabase.DeleteAsset(basePrefabPath);
                basePrefab = null;
            }

            base.TearDown();
        }

        [Test]
        [Description("When CoherenceSync is added to a plain (non-prefab) scene object and TryConvert is called, a new prefab asset is created at the given path.")]
        public void TryConvert_OnPlainSceneObject_CreatesNewPrefab()
        {
            // TryConvertToCoherenceSyncPrefab re-calls GenerateUniqueAssetPath internally, so the
            // file may land at a slightly different path than newPath. Use result for assertions
            // and cleanup rather than newPath.
            var go = new GameObject(BaseName + "_Plain");
            var newPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/{BaseName}_New.prefab");
            GameObject result = null;

            try
            {
                var success = CoherenceSyncUtils.TryConvertToCoherenceSyncPrefab(
                    go, newPath, out result, InteractionMode.AutomatedAction);

                Assert.That(success, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.TryGetComponent<CoherenceSync>(out _), Is.True);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(result)), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(result ? AssetDatabase.GetAssetPath(result) : newPath);
            }
        }

        [Test]
        [Description("When CoherenceSync is added to a prefab instance as a component override, TryConvert applies it to the existing prefab asset rather than creating a new one.")]
        public void TryConvert_OnPrefabInstanceWithOverride_AppliesComponentToExistingPrefab()
        {
            instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;

            var sync = Undo.AddComponent<CoherenceSync>(instance);
            Assert.That(PrefabUtility.IsAddedComponentOverride(sync), Is.True,
                "Precondition: CoherenceSync must be an added component override on the instance.");

            var success = CoherenceSyncUtils.TryConvertToCoherenceSyncPrefab(
                instance, basePrefabPath, out var result, InteractionMode.AutomatedAction);

            Assert.That(success, Is.True);

            basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);

            Assert.That(basePrefab.TryGetComponent<CoherenceSync>(out _), Is.True,
                "CoherenceSync should now be part of the prefab asset, not just the instance.");
            Assert.That(AssetDatabase.GetAssetPath(result), Is.EqualTo(basePrefabPath),
                "The result should be the original prefab asset, not a newly created file.");
        }

        [Test]
        [Description("When TryConvert is called on a scene instance of a non-modifiable imported model (OBJ/FBX), it creates a prefab variant of the source model rather than trying to apply the override back to the read-only asset.")]
        public void TryConvert_OnModelPrefabInstance_CreatesPrefabVariant()
        {
            const string CubeObjGuid = "0eb79a4c01af54ccc98af5abaa494224";
            var modelPath = AssetDatabase.GUIDToAssetPath(CubeObjGuid);
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Assert.That(modelPrefab, Is.Not.Null, $"Could not load Cube.obj model prefab at '{modelPath}'.");

            instance = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            Assert.That(PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(instance), Is.False,
                "Precondition: model prefab instances cannot have overrides applied back to the source asset.");

            var variantPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/{BaseName}_CubeVariant.prefab");
            GameObject result = null;

            try
            {
                var success = CoherenceSyncUtils.TryConvertToCoherenceSyncPrefab(
                    instance, variantPath, out result, InteractionMode.AutomatedAction);

                Assert.That(success, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.TryGetComponent<CoherenceSync>(out _), Is.True,
                    "The resulting prefab should have CoherenceSync.");
                Assert.That(PrefabUtility.IsPartOfVariantPrefab(result), Is.True,
                    "The result should be a prefab variant of the model, not a flat prefab.");
            }
            finally
            {
                if (result)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(result));
                }
            }
        }
    }
}

// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System.Collections.Generic;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEditor.SceneManagement;

    public class SceneUtilsTests : CoherenceTest
    {
        private const string BaseScene =
            Editor.Paths.packageRootPath + "/Coherence.Toolkit.Tests/SceneUtilsTests.unity";

        private Scene targetScene;
        private Scene otherScene;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            targetScene = EditorSceneManager.OpenScene(BaseScene);
            otherScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            _ = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        private static GameObject CreateInScene(Scene scene, bool active = true)
        {
            SceneManager.SetActiveScene(scene);
            var go = new GameObject();
            go.SetActive(active);
            return go;
        }

        [Test]
        [Description("Returns only components belonging to the target scene.")]
        public void FindInScene_Component_Returns_Components_From_Target_Scene()
        {
            var go = CreateInScene(targetScene);
            _ = CreateInScene(otherScene);

#if UNITY_6000_4_OR_NEWER
            var results = SceneUtils.FindInScene<Transform>(targetScene, FindObjectsInactive.Exclude);
#elif UNITY_2023_1_OR_NEWER
            var results = SceneUtils.FindInScene<Transform>(targetScene, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var results = SceneUtils.FindInScene<Transform>(targetScene);
#endif

            Assert.That(results, Has.Length.EqualTo(1));
            Assert.That(results[0].gameObject, Is.EqualTo(go));
        }

        [Test]
        [Description("Excludes inactive components by default.")]
        public void FindInScene_Component_Excludes_Inactive_By_Default()
        {
            _ = CreateInScene(targetScene, active: false);

#if UNITY_6000_4_OR_NEWER
            var results = SceneUtils.FindInScene<Transform>(targetScene, FindObjectsInactive.Exclude);
#elif UNITY_2023_1_OR_NEWER
            var results = SceneUtils.FindInScene<Transform>(targetScene, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var results = SceneUtils.FindInScene<Transform>(targetScene);
#endif

            Assert.That(results, Is.Empty);
        }

        [Test]
        [Description("Includes inactive components when includeInactive is true.")]
        public void FindInScene_Component_Includes_Inactive_When_Requested()
        {
            _ = CreateInScene(targetScene, active: false);

#if UNITY_6000_4_OR_NEWER
            var results = SceneUtils.FindInScene<Transform>(targetScene, FindObjectsInactive.Include);
#elif UNITY_2023_1_OR_NEWER
            var results = SceneUtils.FindInScene<Transform>(targetScene, FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var results = SceneUtils.FindInScene<Transform>(targetScene, includeInactive: true);
#endif

            Assert.That(results, Has.Length.EqualTo(1));
        }

        [Test]
        [Description("Returns only GameObjects belonging to the target scene.")]
        public void FindInScene_GameObject_Returns_GameObjects_From_Target_Scene()
        {
            var go = CreateInScene(targetScene);
            _ = CreateInScene(otherScene);

#if UNITY_6000_4_OR_NEWER
            var results = SceneUtils.FindInScene<Transform>(targetScene, FindObjectsInactive.Exclude);
#elif UNITY_2023_1_OR_NEWER
            var results = SceneUtils.FindInScene<Transform>(targetScene, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var results = SceneUtils.FindInScene<Transform>(targetScene);
#endif

            Assert.That(results, Has.Length.EqualTo(1));
            Assert.That(results[0].gameObject, Is.EqualTo(go));
        }

        [Test]
        [Description("Populates the list with components belonging to the target scene.")]
        public void FindInScene_Component_List_Returns_Components_From_Target_Scene()
        {
            var go = CreateInScene(targetScene);
            _ = CreateInScene(otherScene);

            var result = new List<Transform>();
#if UNITY_6000_4_OR_NEWER
            SceneUtils.FindInScene(targetScene, result, FindObjectsInactive.Exclude);
#elif UNITY_2023_1_OR_NEWER
            SceneUtils.FindInScene(targetScene, result, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            SceneUtils.FindInScene(targetScene, result);
#endif

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].gameObject, Is.EqualTo(go));
        }

        [Test]
        [Description("Clears the list before populating with components from the target scene.")]
        public void FindInScene_Component_List_Clears_Before_Populating()
        {
            _ = CreateInScene(targetScene);
            var staleEntry = CreateInScene(otherScene).GetComponent<Transform>();

            var result = new List<Transform> { staleEntry };
#if UNITY_6000_4_OR_NEWER
            SceneUtils.FindInScene(targetScene, result, FindObjectsInactive.Exclude);
#elif UNITY_2023_1_OR_NEWER
            SceneUtils.FindInScene(targetScene, result, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            SceneUtils.FindInScene(targetScene, result);
#endif

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].gameObject.scene, Is.EqualTo(targetScene));
        }

        [Test]
        [Description("Populates the list with GameObjects belonging to the target scene.")]
        public void FindInScene_GameObject_List_Returns_GameObjects_From_Target_Scene()
        {
            var go = CreateInScene(targetScene);
            _ = CreateInScene(otherScene);

            var result = new List<GameObject>();
#if UNITY_6000_4_OR_NEWER
            SceneUtils.FindInScene(targetScene, result, FindObjectsInactive.Exclude);
#elif UNITY_2023_1_OR_NEWER
            SceneUtils.FindInScene(targetScene, result, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            SceneUtils.FindInScene(targetScene, result);
#endif

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(go));
        }
    }
}

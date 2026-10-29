using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class LobbyReturnCanvasBuilder {
    private const string ScenePath = "Assets/Scenes/Multiplayer/LobbyReturnScene.unity";
    private const string PrefabPath = "Assets/Prefab/LobbyReturnCanvas.prefab";
    private const string MainScenePath = "Assets/Scenes/MainMultiplayerScene.unity";

    static LobbyReturnCanvasBuilder() {
        EditorApplication.delayCall += EnsureAssets;
    }

    private static void EnsureAssets() {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            prefab = CreateCanvasPrefab();
        if (prefab == null)
            return;
        EnsurePrefabVisibilityController();
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        EnsureBackgroundInMainScene(prefab);
        if (!SceneHasLobbyController(ScenePath))
            CreateFunctionalLobbyScene(prefab);

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        var updatedScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(buildScenes);
        if (!updatedScenes.Exists(item => item.path == ScenePath)) {
            updatedScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = updatedScenes.ToArray();
        }
    }

    private static GameObject CreateCanvasPrefab() {
        GameObject canvasObject = new GameObject("LobbyReturnCanvas", typeof(RectTransform), typeof(Canvas),
                                                 typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject("Lobby Background Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
            AssetDatabase.CreateFolder("Assets", "Prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
        Object.DestroyImmediate(canvasObject);
        return prefab;
    }

    private static void EnsurePrefabVisibilityController() {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (contents == null)
            return;
        Canvas canvas = contents.GetComponent<Canvas>();
        Transform imageTransform = contents.transform.Find("Lobby Background Image");
        Image image = imageTransform != null ? imageTransform.GetComponent<Image>() : null;
        LobbyBackgroundCanvas controller = contents.GetComponent<LobbyBackgroundCanvas>();
        if (controller == null)
            controller = contents.AddComponent<LobbyBackgroundCanvas>();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("targetCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("backgroundImage").objectReferenceValue = image;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
    }

    private static bool SceneHasLobbyController(string path) {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        if (!scene.IsValid() || !scene.isLoaded)
            return false;
        bool found = false;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.GetComponentInChildren<LobyUISceneController>(true) != null) {
                found = true;
                break;
            }
        }
        if (openedHere)
            EditorSceneManager.CloseScene(scene, true);
        return found;
    }

    private static void CreateFunctionalLobbyScene(GameObject prefab) {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null) {
            Debug.LogError("MainMultiplayerScene tidak ditemukan; LobbyReturnScene belum bisa dibuat.");
            return;
        }
        Scene source = SceneManager.GetSceneByPath(MainScenePath);
        bool openedHere = !source.IsValid() || !source.isLoaded;
        if (openedHere)
            source = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        if (!source.IsValid() || !source.isLoaded)
            return;

        GameObject imageCanvas = null;
        bool alreadyHasCanvas = false;
        foreach (GameObject root in source.GetRootGameObjects()) {
            if (root.name == prefab.name) {
                alreadyHasCanvas = true;
                break;
            }
        }
        if (!alreadyHasCanvas)
            imageCanvas = PrefabUtility.InstantiatePrefab(prefab, source) as GameObject;
        EditorSceneManager.SaveScene(source, ScenePath, true);
        if (imageCanvas != null)
            Object.DestroyImmediate(imageCanvas);
        if (openedHere)
            EditorSceneManager.CloseScene(source, true);
    }

    private static void EnsureBackgroundInMainScene(GameObject prefab) {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null) {
            Debug.LogError("MainMultiplayerScene tidak ditemukan; background awal belum bisa dipasang.");
            return;
        }
        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == prefab.name) {
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
                return;
            }
        }

        PrefabUtility.InstantiatePrefab(prefab, scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedHere)
            EditorSceneManager.CloseScene(scene, true);
    }
}
#endif

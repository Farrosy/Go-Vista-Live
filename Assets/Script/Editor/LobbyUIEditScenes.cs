using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Alur edit UI lobby, satu scene untuk satu canvas (seperti LoginUIScene):
///  - LobyUIScene.unity      -> Assets/Prefab/LobbyCanvas.prefab
///  - RoomWaitingScene.unity -> Assets/Prefab/RoomWaitingCanvas.prefab
///
/// Kedua scene berisi INSTANCE prefab. Edit canvas di scene itu, lalu Ctrl+S: perubahan otomatis
/// di-Apply ke prefab, sehingga MainMultiplayerScene & LobbyReturnScene ikut berubah. Bisa juga edit
/// langsung di Prefab Mode (klik dua kali prefab-nya).
///
/// Jangan edit canvas ini langsung di MainMultiplayerScene: override di sana menutupi perubahan prefab.
/// </summary>
[InitializeOnLoad]
public static class LobbyUIEditScenes {
    private const string LobbyScenePath = "Assets/Scenes/Multiplayer/LobyUIScene.unity";
    private const string WaitingScenePath = "Assets/Scenes/Multiplayer/RoomWaitingScene.unity";
    private const string LobbyPrefabPath = "Assets/Prefab/LobbyCanvas.prefab";
    private const string WaitingPrefabPath = "Assets/Prefab/RoomWaitingCanvas.prefab";
    private const string WaitingCanvasName = "Room Waiting Canvas";

    static LobbyUIEditScenes() {
        EditorSceneManager.sceneSaving -= ApplyEditsToPrefab;
        EditorSceneManager.sceneSaving += ApplyEditsToPrefab;
        EditorApplication.delayCall += MigrateEditScenes;
    }

    // ------------------------------------------------------------ Menu

    [MenuItem("Go Vista/Lobby UI/Edit LobbyCanvas (LobyUIScene)")]
    private static void OpenLobbyScene() => OpenScene(LobbyScenePath);

    [MenuItem("Go Vista/Lobby UI/Edit RoomWaitingCanvas (RoomWaitingScene)")]
    private static void OpenWaitingScene() => OpenScene(WaitingScenePath);

    [MenuItem("Go Vista/Lobby UI/Reset LobbyCanvas ke default")]
    private static void ResetLobby() => ConfirmReset(LobbyPrefabPath, waitingCanvas: false);

    [MenuItem("Go Vista/Lobby UI/Reset RoomWaitingCanvas ke default")]
    private static void ResetWaiting() => ConfirmReset(WaitingPrefabPath, waitingCanvas: true);

    private static void OpenScene(string path) {
        if (File.Exists(path) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    private static void ConfirmReset(string prefabPath, bool waitingCanvas) {
        if (!EditorUtility.DisplayDialog("Reset ke layout default",
                $"Isi {prefabPath} akan dibangun ulang dari kode (LobyUISceneController). " +
                "SEMUA editan manual di prefab ini akan hilang.", "Reset", "Batal"))
            return;
        LobyUISceneController.ResetCanvasPrefabToDefault(waitingCanvas);
    }

    // ------------------------------------------------------------ Ctrl+S -> Apply ke prefab

    private static void ApplyEditsToPrefab(Scene scene, string path) {
        if (path == LobbyScenePath) Apply(scene, LobbyPrefabPath);
        else if (path == WaitingScenePath) Apply(scene, WaitingPrefabPath);
    }

    private static void Apply(Scene scene, string prefabPath) {
        GameObject instance = FindInstance(scene, prefabPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (instance == null || prefab == null)
            return;

        // Status aktif root tidak ikut di-apply: RoomWaitingCanvas di prefab harus nonaktif (diaktifkan
        // kode saat masuk room), tapi di scene edit dibiarkan aktif supaya terlihat.
        bool wasActive = instance.activeSelf;
        instance.SetActive(prefab.activeSelf);
        try {
            if (!PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false))
                return;
            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            Debug.Log($"[Lobby UI] Perubahan di {scene.name} diterapkan ke {prefabPath}.");
        } finally {
            instance.SetActive(wasActive);
        }
    }

    private static GameObject FindInstance(Scene scene, string prefabPath) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(root) &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == prefabPath)
                return root;
        }
        return null;
    }

    // ------------------------------------------------------------ Migrasi sekali jalan

    // Dulu LobyUIScene berisi 2 canvas, dan RoomWaitingScene berisi salinan canvas (bukan instance) yang
    // menimpa prefab tiap kali script di-reload. Rapikan jadi 1 scene = 1 instance prefab.
    private static void MigrateEditScenes() {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        string waitingGuid = AssetDatabase.AssetPathToGUID(WaitingPrefabPath);
        if (string.IsNullOrEmpty(waitingGuid))
            return;

        if (NeedsMigration(LobbyScenePath, scene => FindInstance(scene, WaitingPrefabPath) != null,
                           text => text.Contains(waitingGuid)))
            EditScene(LobbyScenePath, RemoveWaitingCanvasFromLobbyScene);

        if (NeedsMigration(WaitingScenePath, scene => FindInstance(scene, WaitingPrefabPath) == null,
                           text => !text.Contains(waitingGuid)))
            EditScene(WaitingScenePath, UseWaitingPrefabInstance);
    }

    private static bool NeedsMigration(string path, System.Func<Scene, bool> loadedCheck, System.Func<string, bool> fileCheck) {
        if (!File.Exists(path)) return false;
        Scene scene = SceneManager.GetSceneByPath(path);
        return scene.IsValid() && scene.isLoaded ? loadedCheck(scene) : fileCheck(File.ReadAllText(path));
    }

    private static void EditScene(string path, System.Func<Scene, string> edit) {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (!wasOpen) {
            try {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            } catch (System.Exception e) {
                Debug.LogWarning($"[Lobby UI] Tidak bisa membuka {path}: {e.Message}");
                return;
            }
        }

        string result = edit(scene);
        string sceneName = scene.name;
        if (wasOpen) {
            // Scene sedang dibuka user: jangan auto-save (bisa ikut menyimpan perubahan lain milik user).
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Lobby UI] {sceneName}: {result}. Silakan Ctrl+S.");
        } else {
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[Lobby UI] {sceneName}: {result} (disimpan).");
        }
    }

    private static string RemoveWaitingCanvasFromLobbyScene(Scene scene) {
        GameObject waiting = FindInstance(scene, WaitingPrefabPath);
        if (waiting != null) Object.DestroyImmediate(waiting);
        return "RoomWaitingCanvas dipindah ke RoomWaitingScene (dihapus dari scene ini)";
    }

    private static string UseWaitingPrefabInstance(Scene scene) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == WaitingCanvasName && !PrefabUtility.IsPartOfPrefabInstance(root))
                Object.DestroyImmediate(root);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaitingPrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = WaitingCanvasName;
        instance.SetActive(true);
        return "salinan canvas diganti instance prefab RoomWaitingCanvas";
    }
}

using Normal.Realtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Memasang proximity voice (sekali jalan, aman diulang):
///  - Prefab avatar : AudioSource 3D, RealtimeAvatarVoice, ProximityVoice.
///  - MainMultiplayerScene : "Voice Listener" (AudioListener dipindah dari Main Camera) dan "Voice UI".
/// Menu: Go Vista > Voice > Pasang Proximity Voice.
public static class VoiceSetup {
    private const string ScenePath = "Assets/Scenes/MainMultiplayerScene.unity";
    private const string FontPath = "Assets/Fonts/Fredoka-SemiBold SDF.asset";
    private const string ListenerObjectName = "Voice Listener";
    private const string UiObjectName = "Voice UI";
    private static readonly string[] AvatarPrefabs = {
        "Assets/Resources/Player.prefab",
        "Assets/Resources/Player Female.prefab",
    };

    [MenuItem("Go Vista/Voice/Pasang Proximity Voice")]
    public static void Install() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        foreach (string path in AvatarPrefabs)
            ConfigureAvatar(path);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) {
            Debug.LogError($"[Voice] Scene tidak ditemukan: {ScenePath}");
            return;
        }
        if (!InstallListener(scene))
            return;
        InstallUi(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Voice] Proximity voice terpasang di prefab avatar dan MainMultiplayerScene.");
    }

    private static void ConfigureAvatar(string path) {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try {
            ConfigureAudioSource(GetOrAdd<AudioSource>(root));
            GetOrAdd<RealtimeAvatarVoice>(root);
            GetOrAdd<ProximityVoice>(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// Linear, karena hanya kurva ini yang benar-benar diam di maxDistance. Logarithmic berhenti mengecil
    /// di sana, jadi pemain yang sangat jauh tetap terdengar pelan.
    private static void ConfigureAudioSource(AudioSource source) {
        source.playOnAwake = false;
        source.spatialize = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = VoiceRules.FullVolumeDistance;
        source.maxDistance = VoiceRules.SilentDistance;
    }

    private static bool InstallListener(Scene scene) {
        Camera mainCamera = FindMainCamera(scene);
        if (mainCamera == null) {
            Debug.LogError("[Voice] Main Camera tidak ditemukan di MainMultiplayerScene.");
            return false;
        }
        AudioListener cameraListener = mainCamera.GetComponent<AudioListener>();
        if (cameraListener != null)
            Object.DestroyImmediate(cameraListener);

        GameObject listener = GetOrCreateRoot(scene, ListenerObjectName);
        GetOrAdd<AudioListener>(listener);
        var rig = new SerializedObject(GetOrAdd<VoiceListenerRig>(listener));
        rig.FindProperty("cameraTransform").objectReferenceValue = mainCamera.transform;
        rig.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static void InstallUi(Scene scene) {
        GameObject ui = GetOrCreateRoot(scene, UiObjectName);
        var button = new SerializedObject(GetOrAdd<VoiceMicButton>(ui));
        button.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        button.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Camera FindMainCamera(Scene scene) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) {
                if (camera.CompareTag("MainCamera"))
                    return camera;
            }
        }
        return null;
    }

    private static GameObject GetOrCreateRoot(Scene scene, string name) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == name)
                return root;
        }
        var created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : target.AddComponent<T>();
    }
}

using System.Collections.Generic;
using System.Linq;
using ITHappy;
using Normal.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TrafficDemoSetup
{
    private const string DemoScenePath = "Assets/Download_Assets/Houidisoft technology/Simple water/Demo/Demo 1.unity";
    private const string PlayerPrefabPath = "Assets/Resources/Player.prefab";
    private const string CarFolder = "Assets/Download_Assets/ithappy/Megacity/Traffic/Prefabs/Cars/Cars";
    private const float SpawnYaw = 180f;

    private static readonly Vector3 SpawnPosition = new Vector3(159.078674f, 24f, 98f);

    private static readonly string[] CarNames =
    {
        "car_001", "car_003", "car_008", "car_012", "car_016",
        "car_021", "car_025", "car_033", "cargo_car_001"
    };

    [MenuItem("Go Vista/Setup Demo Traffic")]
    public static void Setup()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != DemoScenePath)
        {
            EditorUtility.DisplayDialog("Setup Demo Traffic", "Open Demo 1.unity first.", "OK");
            return;
        }

        if (EditorSceneManager.IsPreviewScene(active))
        {
            EditorUtility.DisplayDialog("Setup Demo Traffic", "Exit prefab mode first.", "OK");
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Setup Demo Traffic", "Exit Play mode first.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Setup Demo Traffic");
        int undoGroup = Undo.GetCurrentGroup();

        TrafficNetwork network = EnsureNetwork(active);
        GameObject player = EnsurePlayer(active);
        EnsureCamera(active, player);
        EnsureInputHub(player);

        if (network != null)
        {
            var serialized = new SerializedObject(network);
            serialized.FindProperty("trafficCenter").objectReferenceValue = player != null ? player.transform : null;
            serialized.FindProperty("fallbackCenter").vector3Value = SpawnPosition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(active);
        Undo.CollapseUndoOperations(undoGroup);

        string report = TrafficNetwork.BuildAndDescribe(2f, 1f, 0.9f, 45f, SpawnPosition);
        Debug.Log("[Traffic Demo] Setup complete. Save the scene and press Play.\n" + report);
        EditorUtility.DisplayDialog("Setup Demo Traffic",
            "Traffic + player are set up in Demo 1.\nSave the scene (Ctrl+S), then press Play.", "OK");
    }

    [MenuItem("Go Vista/Validate Traffic Routes")]
    public static void Validate()
    {
        string report = TrafficNetwork.BuildAndDescribe(2f, 1f, 0.9f, 45f, SpawnPosition);
        Debug.Log("[Traffic Demo] Route validation:\n" + report);
    }

    private static TrafficNetwork EnsureNetwork(Scene scene)
    {
        TrafficNetwork network = UnityEngine.Object.FindObjectsByType<TrafficNetwork>()
            .FirstOrDefault(item => item.gameObject.scene == scene);

        if (network == null)
        {
            var go = new GameObject("Traffic");
            Undo.RegisterCreatedObjectUndo(go, "Create Traffic");
            network = Undo.AddComponent<TrafficNetwork>(go);
        }

        Transform carRoot = network.transform.Find("Cars");
        if (carRoot == null)
        {
            var cars = new GameObject("Cars");
            Undo.RegisterCreatedObjectUndo(cars, "Create Cars");
            cars.transform.SetParent(network.transform, false);
            carRoot = cars.transform;
        }

        var serialized = new SerializedObject(network);
        serialized.FindProperty("carRoot").objectReferenceValue = carRoot;
        serialized.FindProperty("fallbackCenter").vector3Value = SpawnPosition;
        serialized.FindProperty("maxCars").intValue = 14;
        serialized.FindProperty("carSpacing").floatValue = 45f;
        serialized.FindProperty("trafficRadius").floatValue = 250f;
        serialized.FindProperty("speedRange").vector2Value = new Vector2(6f, 9f);
        serialized.FindProperty("acceleration").floatValue = 25f;
        serialized.FindProperty("brake").floatValue = 35f;
        serialized.FindProperty("lookAhead").floatValue = 12f;
        serialized.FindProperty("stopDistance").floatValue = 4f;
        serialized.FindProperty("mergeDistance").floatValue = 1f;
        serialized.FindProperty("minTangentDot").floatValue = 0.9f;
        serialized.FindProperty("minRouteLength").floatValue = 45f;
        serialized.FindProperty("sampleStep").floatValue = 2f;

        List<GameObject> prefabs = LoadCarPrefabs();
        SerializedProperty carPrefabs = serialized.FindProperty("carPrefabs");
        carPrefabs.arraySize = prefabs.Count;
        for (int i = 0; i < prefabs.Count; i++)
            carPrefabs.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return network;
    }

    private static GameObject EnsurePlayer(Scene scene)
    {
        GameObject player = FindRoot(scene, "Player");

        if (player == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Traffic Demo] Missing player prefab: {PlayerPrefabPath}");
                return null;
            }

            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(player, "Create Player");
            player.name = "Player";
        }

        player.transform.SetPositionAndRotation(SpawnPosition, Quaternion.Euler(0f, SpawnYaw, 0f));

        RemoveComponent<PlayerAnimSync>(player);
        RemoveComponent<RealtimeTransform>(player);
        RemoveComponent<RealtimeView>(player);

        if (player.GetComponent<DemoKeyboardInput>() == null)
            Undo.AddComponent<DemoKeyboardInput>(player);

        return player;
    }

    private static void EnsureCamera(Scene scene, GameObject player)
    {
        FreeCamera freeCamera = UnityEngine.Object.FindObjectsByType<FreeCamera>()
            .FirstOrDefault(item => item.gameObject.scene == scene);

        Camera camera = freeCamera != null
            ? freeCamera.GetComponent<Camera>()
            : UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)
                .FirstOrDefault(item => item.gameObject.scene == scene && item.CompareTag("MainCamera"));

        if (camera == null)
        {
            Debug.LogError("[Traffic Demo] No camera found in Demo 1.");
            return;
        }

        if (freeCamera != null)
        {
            Undo.RecordObject(freeCamera, "Disable Free Camera");
            freeCamera.enabled = false;
        }

        CameraOrbit orbit = camera.GetComponent<CameraOrbit>();
        if (orbit == null) orbit = Undo.AddComponent<CameraOrbit>(camera.gameObject);

        var orbitSerialized = new SerializedObject(orbit);
        orbitSerialized.FindProperty("target").objectReferenceValue = player != null ? player.transform : null;
        orbitSerialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(camera, "Setup Player Camera");
        camera.enabled = true;
        camera.gameObject.tag = "MainCamera";
        camera.transform.rotation = Quaternion.Euler(15f, SpawnYaw, 0f);

        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener == null) listener = Undo.AddComponent<AudioListener>(camera.gameObject);
        Undo.RecordObject(listener, "Enable Player AudioListener");
        listener.enabled = true;

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera other = cameras[i];
            if (other == camera || other.gameObject.scene != scene) continue;
            if (other.cameraType == CameraType.Reflection) continue;

            Undo.RecordObject(other, "Disable Showcase Camera");
            other.enabled = false;
            if (other.CompareTag("MainCamera")) other.gameObject.tag = "Untagged";

            AudioListener otherListener = other.GetComponent<AudioListener>();
            if (otherListener != null)
            {
                Undo.RecordObject(otherListener, "Disable Duplicate AudioListener");
                otherListener.enabled = false;
            }
        }
    }

    private static void EnsureInputHub(GameObject player)
    {
        PlayerInputHub hub = UnityEngine.Object.FindAnyObjectByType<PlayerInputHub>();
        if (hub == null)
        {
            var go = new GameObject("InputHub");
            Undo.RegisterCreatedObjectUndo(go, "Create InputHub");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            hub = Undo.AddComponent<PlayerInputHub>(go);
        }

        var serialized = new SerializedObject(hub);
        SerializedProperty keyboard = serialized.FindProperty("keyboardFallback");
        if (keyboard != null)
        {
            keyboard.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        if (player != null && player.GetComponent<DemoKeyboardInput>() == null)
            Undo.AddComponent<DemoKeyboardInput>(player);
    }

    private static List<GameObject> LoadCarPrefabs()
    {
        var result = new List<GameObject>();
        for (int i = 0; i < CarNames.Length; i++)
        {
            string path = $"{CarFolder}/{CarNames[i]}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) result.Add(prefab);
            else Debug.LogWarning($"[Traffic Demo] Missing car prefab: {path}");
        }
        return result;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == name) return roots[i];
        return null;
    }

    private static void RemoveComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component != null) Undo.DestroyObjectImmediate(component);
    }
}

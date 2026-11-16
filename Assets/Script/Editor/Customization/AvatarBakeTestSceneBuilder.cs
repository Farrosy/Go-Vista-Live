using System.IO;
using Bozo.ModularCharacters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tahap 0 (tes bake): membuat scene Assets/Scenes/Customization/AvatarBakeTest.unity berisi kamera,
/// cahaya, lantai, dan AvatarBakeTester + BakeStatsOverlay. Tekan Play lalu pakai tombol Spawn di layar.
/// Aman dijalankan ulang: scene lama ditimpa. Folder vendor BoZo tidak diubah.
/// </summary>
public static class AvatarBakeTestSceneBuilder {
    public const string ScenePath = "Assets/Scenes/Customization/AvatarBakeTest.unity";
    private const string BasePrefabPath =
        "Assets/Download_Assets/BoZo_StylizedModularCharacters/Prefabs/BSMC_CharacterBase.prefab";
    private const string MenuPath = "Go Vista/Customization/Build Avatar Bake Test Scene";
    private static readonly Vector3 TesterPosition = new Vector3(-1.4f, 0f, 0f);

    [MenuItem(MenuPath)]
    public static void BuildFromMenu() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// Tanpa dialog simpan: scene yang sedang terbuka diganti tanpa disimpan.
    public static void Build() {
        var basePrefab = AssetDatabase.LoadAssetAtPath<OutfitSystem>(BasePrefabPath);
        if (basePrefab == null) {
            Debug.LogError($"[BakeTest] Prefab dasar tidak ditemukan: {BasePrefabPath}");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        CreateLight();
        CreateFloor();
        if (!CreateTester(basePrefab)) return;

        EditorSceneManager.MarkSceneDirty(scene);
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        // Buka ulang dari disk: scene baru yang hanya "Save As" kehilangan referensi prefab saat masuk Play.
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log($"[BakeTest] Scene dibuat: {ScenePath}. Tekan Play, lalu Spawn 1/5/10.");
    }

    private static void CreateCamera() {
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, -6.5f), Quaternion.Euler(8f, 0f, 0f));
        Camera cam = camera.GetComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x14, 0x1A, 0x33, 0xFF);
    }

    private static void CreateLight() {
        var light = new GameObject("Directional Light", typeof(Light));
        light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        Light l = light.GetComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.2f;
        l.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
    }

    private static void CreateFloor() {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(2f, 1f, 2f);
    }

    private static bool CreateTester(OutfitSystem basePrefab) {
        var root = new GameObject("Avatar Bake Tester");
        root.transform.position = TesterPosition; // geser ke kiri, panel angka ada di kanan layar
        var tester = root.AddComponent<AvatarBakeTester>();
        root.AddComponent<BakeStatsOverlay>();

        var serialized = new SerializedObject(tester);
        serialized.FindProperty("characterBasePrefab").objectReferenceValue = basePrefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized.Update();
        if (serialized.FindProperty("characterBasePrefab").objectReferenceValue != null) return true;

        Debug.LogError("[BakeTest] Gagal mengisi Character Base Prefab. Scene tidak disimpan.");
        return false;
    }
}

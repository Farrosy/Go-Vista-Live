using System.Collections.Generic;
using MTAssets.EasyMinimapSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Bake ulang peta radar (GoVistaMinimap) dari WorldScene dengan Minimap Scanner milik Easy Minimap System.
/// Jalankan lagi setiap kali tata letak kota berubah. Hasil scan disimpan di
/// Assets/Plugins/MT Assets/_AssetsData/Scans (scan lama dihapus otomatis oleh scanner).
public static class MinimapScanTool {
    private const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    private const string ScannerName = "Minimap Scanner (City)";
    // Pojok kiri bawah area scan; scan 500 x 500 m mencakup seluruh kota, marina, dan pantai.
    private static readonly Vector3 ScannerCorner = new Vector3(25f, 0f, 0f);
    private const float ScanHeight = 400f; // di atas gedung tertinggi
    // Tidak ikut di peta: maskot dan beacon Tur Kota, NPC diam, kamera sisa di WorldScene.
    private static readonly string[] IgnoredRoots = { "City Tour", "Main Camera", "NPC_Kota", "NPC_Kota 2", "NpcDevSpawner (DEV)" };

    // Shader air menghitung kedalaman dengan rumus kamera perspektif, jadi di kamera scan (ortografis)
    // seluruh permukaan dianggap busa dan tampil putih. Selama scan, air diganti warna datar.
    private const string WaterShader = "Custom/SimpleWaterURP";
    private const float WaterShallowMix = 0.3f;

    [MenuItem("Go Vista/Minimap/Scan ulang peta kota (WorldScene)")]
    public static void RescanWorld() {
        Scene scene = SceneManager.GetSceneByPath(WorldScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);

        MinimapScanner scanner = EnsureScanner(scene);
        var swapped = new List<(Renderer renderer, Material original)>();
        try {
            SwapWaterToFlatColor(scene, swapped);
            scanner.DoScanInThisAreaOfComponentAndShowOnMinimap();
        } finally {
            foreach ((Renderer renderer, Material original) in swapped) {
                Object.DestroyImmediate(renderer.sharedMaterial);
                renderer.sharedMaterial = original;
            }
        }

        EditorUtility.SetDirty(scanner);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"[Minimap] Peta kota selesai di-scan: {AssetDatabase.GetAssetPath(scanner.textureResultOfScan)}");
    }

    private static MinimapScanner EnsureScanner(Scene scene) {
        GameObject root = FindRoot(scene, ScannerName);
        if (root == null) {
            root = new GameObject(ScannerName);
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        root.transform.position = ScannerCorner;
        if (!root.TryGetComponent(out MinimapScanner scanner)) scanner = root.AddComponent<MinimapScanner>();
        scanner.scanArea = MinimapScanner.ScanArea.Units500;
        scanner.scanResolution = MinimapScanner.ScanResolution.Pixels2048x2048;
        scanner.scanHeight = ScanHeight;
        scanner.gameObjectsToIgnore.Clear();
        foreach (string name in IgnoredRoots) {
            GameObject ignored = FindRoot(scene, name);
            if (ignored != null) scanner.gameObjectsToIgnore.Add(ignored);
        }
        return scanner;
    }

    private static void SwapWaterToFlatColor(Scene scene, List<(Renderer, Material)> swapped) {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) {
            Material original = renderer.sharedMaterial;
            if (original == null || original.shader == null || original.shader.name != WaterShader) continue;
            Color deep = original.GetColor("_DeepColor");
            Color shallow = original.GetColor("_ShallowColor");
            Color flat = Color.Lerp(deep, shallow, WaterShallowMix);
            flat.a = 1f;
            var temp = new Material(unlit) { name = "Minimap Scan Water (temp)", hideFlags = HideFlags.DontSave };
            temp.SetColor("_BaseColor", flat);
            swapped.Add((renderer, original));
            renderer.sharedMaterial = temp;
        }
    }

    // ------------------------------------------------------------ Ikon tempat penting

    private const string LandmarksRootName = "Minimap Landmarks";
    private const string IconRoot = "Assets/Art/Minimap/";
    private const float LandmarkPixels = 28f;
    private const int LandmarkOrder = 2; // di bawah pemain lain (4) dan panah lokal (5)

    /// Gedung tujuan Tur Kota (QuestData city-tour). Tag = teks dalam kurung di nama objek gedung,
    /// sama seperti BuildingTag di CityTourContent. Ikon diletakkan di tengah gedung (bukan di pintu).
    private static readonly (string Tag, string Icon)[] Landmarks = {
        ("Central Plaza", "radar_icon_plaza"),
        ("Banking Center", "radar_icon_bank"),
        ("Dupoin", "radar_icon_broker_candles"),
        ("Inovation Tower", "radar_icon_innovation"),
        ("Trading Academy", "radar_icon_academy"),
        ("KVB", "radar_icon_broker_chart"),
        ("Vista Palace", "radar_icon_palace"),
    };

    /// Membuat/memperbarui "Minimap Landmarks" di WorldScene: satu objek kosong per gedung dengan MinimapBlip.
    /// Aman dijalankan ulang; posisi dihitung lagi dari batas gedung.
    [MenuItem("Go Vista/Minimap/Pasang ikon tempat penting (WorldScene)")]
    public static void PlaceLandmarks() {
        Scene scene = SceneManager.GetSceneByPath(WorldScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);

        GameObject root = FindRoot(scene, LandmarksRootName);
        if (root == null) {
            root = new GameObject(LandmarksRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        foreach ((string tag, string icon) in Landmarks) {
            Transform building = FindTagged(scene, tag);
            if (building == null) {
                Debug.LogError($"[Minimap] Gedung '({tag})' tidak ditemukan di WorldScene.");
                continue;
            }
            Transform marker = root.transform.Find(tag);
            if (marker == null) {
                marker = new GameObject(tag).transform;
                marker.SetParent(root.transform, false);
            }
            Vector3 center = RendererCenter(building);
            marker.position = new Vector3(center.x, building.position.y, center.z);

            MinimapBlip blip = marker.GetComponent<MinimapBlip>();
            if (blip == null) blip = marker.gameObject.AddComponent<MinimapBlip>();
            var serializedBlip = new SerializedObject(blip);
            serializedBlip.FindProperty("pixelSize").floatValue = LandmarkPixels;
            serializedBlip.FindProperty("keepUpright").boolValue = true;
            serializedBlip.FindProperty("clampToEdge").boolValue = false;
            serializedBlip.ApplyModifiedPropertiesWithoutUndo();

            MinimapItem item = blip.Item;
            item.itemSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconRoot + icon + ".png");
            if (item.itemSprite == null) Debug.LogError($"[Minimap] Ikon tidak ditemukan: {icon}");
            item.spriteColor = Color.white;
            item.orderInLayer = LandmarkOrder;
            item.raycastTarget = false;
            EditorUtility.SetDirty(item);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"[Minimap] {Landmarks.Length} ikon tempat penting dipasang.");
    }

    private static Transform FindTagged(Scene scene, string tag) {
        string suffix = "(" + tag + ")";
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith(suffix)) return t;
        return null;
    }

    private static Vector3 RendererCenter(Transform building) {
        Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return building.position;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return bounds.center;
    }

    private static GameObject FindRoot(Scene scene, string name) {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }
}

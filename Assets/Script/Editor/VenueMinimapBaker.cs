using System.Collections.Generic;
using System.IO;
using MTAssets.EasyMinimapSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Bake peta venue KPOP Arena (Demo_DanceClub_01) dan KPOP Community (Demo_RooftopBar_01).
/// Gaya sama dengan scan pulau: render top-down asli, dipotong 2,6 m di atas lantai supaya atap, plafon,
/// dan balkon tidak ikut (memory: venue-map-decisions). Hasilnya dipasang ke MinimapScanner di scene venue,
/// jadi radar (GoVistaMinimap) dan peta penuh (GoVistaFullMap) otomatis tampil di sana.
///  - Bagian dalam ruangan yang tidak ada geometrinya (blok kosong) diisi warna lantai polos.
///  - Di luar ruangan diisi warna latar venue.
/// Data area (VenueMapData) hanya diisi kalau belum ada, supaya hasil edit di Inspector tidak tertimpa.
public static class VenueMinimapBaker {
    private const string OutputFolder = "Assets/Art/Minimap/Generated/";
    private const string ScannerName = "Venue Minimap Scanner";
    private const string RoomRootName = "Scene";

    private const int Resolution = 1024;                       // 17 px/m: cukup untuk zoom maksimal peta penuh
    private const MinimapScanner.ScanArea ScanArea = MinimapScanner.ScanArea.Units60;
    private const MinimapScanner.ScanResolution ScanResolution = MinimapScanner.ScanResolution.Pixels1024x1024;
    private const float ScanMeters = 60f;
    private const float CutAboveFloor = 2.6f;                  // di bawah balkon (3 m) dan plafon
    private const float Brightness = 1.25f;                    // venue gelap; sama dengan mockup

    private static readonly Color32 BlankFloor = new Color32(72, 55, 85, 255);
    private static readonly Color32 Background = new Color32(13, 10, 22, 255);
    private static readonly Color KeyColor = new Color(1f, 0f, 1f); // hanya untuk membedakan "tidak ada geometri"

    // Root yang tidak ikut difoto: kamera demo, atap, kota latar, lighting versi BIRP.
    private static readonly string[] HiddenRoots = { "Main Camera", "BackgroundCity", "Lighting (BIRP)" };

    [MenuItem("Go Vista/Minimap/Bake peta venue (KPOP Arena + Community)")]
    public static void BakeAll() => BakeAll(resetAreas: false);

    /// Menimpa data area di scene dengan VenueMapDefaults (hasil edit di Inspector hilang), lalu bake.
    [MenuItem("Go Vista/Minimap/Bake peta venue + isi ulang data area")]
    public static void BakeAllResetAreas() => BakeAll(resetAreas: true);

    private static void BakeAll(bool resetAreas) {
        foreach (VenueMapDefaults.Venue venue in VenueMapDefaults.All) Bake(venue, resetAreas);
        AssetDatabase.SaveAssets();
        Debug.Log("[Peta venue] Selesai. Gambar ada di " + OutputFolder);
    }

    private static void Bake(VenueMapDefaults.Venue venue, bool resetAreas) {
        Scene scene = SceneManager.GetSceneByPath(venue.ScenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(venue.ScenePath, OpenSceneMode.Additive);
        try {
            VenueMapData data = EnsureScanner(scene, venue, resetAreas, out MinimapScanner scanner);
            Rect room = RoomBounds(scene);
            Texture2D texture = SaveTexture(Render(scene, data.FloorY, room), venue.ScenePath);
            AssignScan(scanner, texture);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Peta venue] {venue.DisplayName}: ruangan {room.width:0.#} × {room.height:0.#} m → {AssetDatabase.GetAssetPath(texture)}");
        } finally {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ------------------------------------------------------------ Scene

    private static VenueMapData EnsureScanner(Scene scene, VenueMapDefaults.Venue venue, bool resetAreas,
        out MinimapScanner scanner) {
        GameObject holder = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == ScannerName) holder = root;
        if (holder == null) {
            holder = new GameObject(ScannerName);
            SceneManager.MoveGameObjectToScene(holder, scene);
        }
        // Sudut kiri bawah area scan (MinimapScanner memakai posisi objek sebagai sudut).
        holder.transform.SetPositionAndRotation(new Vector3(-ScanMeters / 2f, 0f, -ScanMeters / 2f), Quaternion.identity);

        if (!holder.TryGetComponent(out scanner)) scanner = holder.AddComponent<MinimapScanner>();
        scanner.scanArea = ScanArea;
        scanner.scanResolution = ScanResolution;

        if (!holder.TryGetComponent(out VenueMapData data)) data = holder.AddComponent<VenueMapData>();
        if (resetAreas || data.Areas.Count == 0)
            data.EditorSetup(venue.DisplayName, Background, venue.FloorY, venue.UpperLift, venue.Areas(), venue.UpperOutline());
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(scanner);
        return data;
    }

    private static Rect RoomBounds(Scene scene) {
        Bounds bounds = default;
        bool any = false;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name != RoomRootName) continue;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) {
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
        }
        if (!any) {
            Debug.LogWarning($"[Peta venue] Root \"{RoomRootName}\" tidak ditemukan di {scene.name}; seluruh area dianggap ruangan.");
            return new Rect(-ScanMeters / 2f, -ScanMeters / 2f, ScanMeters, ScanMeters);
        }
        return Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
    }

    // ------------------------------------------------------------ Render

    private static Color32[] Render(Scene scene, float floorY, Rect room) {
        var restore = new Dictionary<GameObject, bool>();
        Scene previousActive = SceneManager.GetActiveScene();
        GameObject cameraObject = null;
        var target = new RenderTexture(Resolution, Resolution, 24);
        try {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects()) {
                    restore[root] = root.activeSelf;
                    root.SetActive(root.scene == scene && IsPhotographed(root.name));
                }
            SceneManager.SetActiveScene(scene); // lighting dan ambient milik venue

            // Dibuat setelah root lain dimatikan, jadi kamera ini tidak ikut dimatikan.
            cameraObject = new GameObject("Venue Map Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ScanMeters / 2f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, floorY + CutAboveFloor, 0f), Quaternion.Euler(90f, 0f, 0f));
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = KeyColor;
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var readback = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
            readback.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            Color32[] pixels = readback.GetPixels32();
            Object.DestroyImmediate(readback);
            return Finish(pixels, room);
        } finally {
            RenderTexture.active = null;
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
            foreach (KeyValuePair<GameObject, bool> pair in restore)
                if (pair.Key != null) pair.Key.SetActive(pair.Value);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }

    private static bool IsPhotographed(string rootName) =>
        rootName != ScannerName && !rootName.StartsWith("Roof") && System.Array.IndexOf(HiddenRoots, rootName) < 0;

    /// Terangkan, isi blok kosong di dalam ruangan dengan lantai polos, dan luar ruangan dengan latar.
    /// Pixel (0,0) = sudut kiri bawah scan, selalu di luar ruangan, jadi warnanya = warna "tidak ada geometri".
    private static Color32[] Finish(Color32[] pixels, Rect room) {
        Color32 key = pixels[0];
        float metersPerPixel = ScanMeters / Resolution;
        for (int y = 0; y < Resolution; y++) {
            float worldZ = -ScanMeters / 2f + (y + 0.5f) * metersPerPixel;
            for (int x = 0; x < Resolution; x++) {
                float worldX = -ScanMeters / 2f + (x + 0.5f) * metersPerPixel;
                int i = y * Resolution + x;
                if (!room.Contains(new Vector2(worldX, worldZ))) pixels[i] = Background;
                else if (Same(pixels[i], key)) pixels[i] = BlankFloor;
                else pixels[i] = Brighten(pixels[i]);
            }
        }
        return pixels;
    }

    private static bool Same(Color32 a, Color32 b) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) <= 6;

    private static Color32 Brighten(Color32 c) => new Color32(
        (byte)Mathf.Min(255, c.r * Brightness), (byte)Mathf.Min(255, c.g * Brightness), (byte)Mathf.Min(255, c.b * Brightness), 255);

    // ------------------------------------------------------------ Aset

    private static Texture2D SaveTexture(Color32[] pixels, string scenePath) {
        Directory.CreateDirectory(OutputFolder);
        string path = OutputFolder + "Venue_" + Path.GetFileNameWithoutExtension(scenePath) + ".png";
        var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = Resolution;
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void AssignScan(MinimapScanner scanner, Texture2D texture) {
        var serialized = new SerializedObject(scanner);
        SerializedProperty property = serialized.FindProperty("_textureResultOfScan");
        if (property == null) {
            scanner.textureResultOfScan = texture;
        } else {
            property.objectReferenceValue = texture;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

using System.Collections.Generic;
using System.IO;
using MTAssets.EasyMinimapSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Membuat MinimapShoreData (jarak ke pantai) dari hasil scan peta kota, untuk ombak animasi di peta penuh.
///  - Air = piksel scan yang warnanya sama dengan laut DAN tersambung ke laut lepas (kolam/danau di kota tidak ikut).
///  - Di luar area scan dianggap laut, jadi pulau yang terpotong tepi scan tetap punya ombak di potongannya.
///  - Hasil: tekstur satu kanal (0 = darat, 1 = MaxDistance meter atau lebih) + warna laut + posisi dunia.
/// Dijalankan otomatis oleh "Scan ulang peta kota"; menu di bawah untuk bake ulang tanpa scan ulang.
public static class MinimapShoreBaker {
    private const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    private const string OutputFolder = "Assets/Art/Minimap/Generated";
    private const string DataPath = OutputFolder + "/MinimapShore.asset";
    private const string FieldPath = OutputFolder + "/minimap_shore_distance.png";

    private const int Resolution = 1024;
    private const float MarginMeters = 40f;   // laut tambahan di luar area scan, cukup untuk cincin ombak terluar
    private const float MaxDistance = 28f;
    private const int WaterTolerance = 30;    // jarak warna RGB (0-255) yang masih dianggap air

    [MenuItem("Go Vista/Minimap/Bake garis pantai peta penuh (WorldScene)")]
    public static void BakeFromWorldScene() {
        Scene scene = SceneManager.GetSceneByPath(WorldScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);
        try {
            MinimapScanner scanner = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (scanner == null) scanner = root.GetComponentInChildren<MinimapScanner>(true);
            if (scanner == null) Debug.LogError("[Minimap] Tidak ada Minimap Scanner di WorldScene. Scan peta dulu.");
            else Bake(scanner);
        } finally {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    public static void Bake(MinimapScanner scanner) {
        Texture2D scanAsset = scanner.textureResultOfScan;
        if (scanAsset == null) {
            Debug.LogError("[Minimap] Minimap Scanner belum punya hasil scan.");
            return;
        }
        // Baca PNG aslinya (tanpa kompresi) supaya warna air persis dan tidak perlu mengubah import scan.
        var scan = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        scan.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(scanAsset)));
        Color32[] scanPixels = scan.GetPixels32();
        int scanW = scan.width, scanH = scan.height;
        Object.DestroyImmediate(scan);

        Vector3 corner = scanner.transform.position;
        float scanSize = ScanAreaMeters(scanner.scanArea);
        Color32 water = MostCommonBorderColor(scanPixels, scanW, scanH);

        float worldSize = scanSize + 2f * MarginMeters;
        var worldMin = new Vector2(corner.x - MarginMeters, corner.z - MarginMeters);
        float metersPerPixel = worldSize / Resolution;

        // 1. Calon air: warna laut, atau di luar area scan.
        var candidate = new bool[Resolution * Resolution];
        for (int y = 0; y < Resolution; y++)
        for (int x = 0; x < Resolution; x++) {
            float u = ((x + 0.5f) * metersPerPixel - MarginMeters) / scanSize;
            float v = ((y + 0.5f) * metersPerPixel - MarginMeters) / scanSize;
            bool outside = u < 0f || v < 0f || u >= 1f || v >= 1f;
            candidate[y * Resolution + x] = outside ||
                IsWater(scanPixels[(int)(v * scanH) * scanW + (int)(u * scanW)], water);
        }

        // 2. Laut = calon air yang tersambung ke tepi (laut lepas). Sisanya darat.
        bool[] ocean = FloodFromBorder(candidate);

        // 3. Jarak Euclid ke darat terdekat.
        float[] distance = DistanceToLand(ocean);

        var pixels = new Color32[distance.Length];
        for (int i = 0; i < distance.Length; i++) {
            byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(distance[i] * metersPerPixel / MaxDistance) * 255f);
            pixels[i] = new Color32(value, value, value, 255);
        }

        Directory.CreateDirectory(OutputFolder);
        var field = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
        field.SetPixels32(pixels);
        File.WriteAllBytes(FieldPath, field.EncodeToPNG());
        Object.DestroyImmediate(field);
        AssetDatabase.ImportAsset(FieldPath);
        ConfigureFieldImport();

        var data = AssetDatabase.LoadAssetAtPath<MinimapShoreData>(DataPath);
        if (data == null) {
            data = ScriptableObject.CreateInstance<MinimapShoreData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }
        data.distanceField = AssetDatabase.LoadAssetAtPath<Texture2D>(FieldPath);
        data.sourceScan = scanAsset;
        data.waterColor = water;
        data.worldMin = worldMin;
        data.worldSize = worldSize;
        data.maxDistance = MaxDistance;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Minimap] Garis pantai di-bake: {DataPath} (warna laut #{ColorUtility.ToHtmlStringRGB(water)}).");
    }

    private static void ConfigureFieldImport() {
        var importer = (TextureImporter)AssetImporter.GetAtPath(FieldPath);
        importer.textureType = TextureImporterType.SingleChannel;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
        importer.SetTextureSettings(settings);
        importer.sRGBTexture = false;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = Resolution;
        importer.SaveAndReimport();
    }

    private static float ScanAreaMeters(MinimapScanner.ScanArea area) =>
        float.TryParse(area.ToString().Replace("Units", ""), out float meters) ? meters : 500f;

    private static bool IsWater(Color32 c, Color32 water) {
        int dr = c.r - water.r, dg = c.g - water.g, db = c.b - water.b;
        return dr * dr + dg * dg + db * db <= WaterTolerance * WaterTolerance;
    }

    /// Warna terbanyak di tepi scan (tepi scan selalu laut). Dikelompokkan per 4 level supaya noise kompresi tidak memecah.
    private static Color32 MostCommonBorderColor(Color32[] pixels, int w, int h) {
        var buckets = new Dictionary<int, (int count, long r, long g, long b)>();
        void Add(Color32 c) {
            int key = (c.r >> 2) << 12 | (c.g >> 2) << 6 | (c.b >> 2);
            buckets.TryGetValue(key, out var e);
            buckets[key] = (e.count + 1, e.r + c.r, e.g + c.g, e.b + c.b);
        }
        for (int x = 0; x < w; x++) { Add(pixels[x]); Add(pixels[(h - 1) * w + x]); }
        for (int y = 0; y < h; y++) { Add(pixels[y * w]); Add(pixels[y * w + w - 1]); }
        (int count, long r, long g, long b) best = default;
        foreach (var e in buckets.Values) if (e.count > best.count) best = e;
        return new Color32((byte)(best.r / best.count), (byte)(best.g / best.count), (byte)(best.b / best.count), 255);
    }

    private static bool[] FloodFromBorder(bool[] candidate) {
        const int n = Resolution;
        var ocean = new bool[n * n];
        var queue = new Queue<int>();
        void Push(int i) {
            if (!candidate[i] || ocean[i]) return;
            ocean[i] = true;
            queue.Enqueue(i);
        }
        for (int i = 0; i < n; i++) { Push(i); Push((n - 1) * n + i); Push(i * n); Push(i * n + n - 1); }
        while (queue.Count > 0) {
            int i = queue.Dequeue();
            int x = i % n, y = i / n;
            if (x > 0) Push(i - 1);
            if (x < n - 1) Push(i + 1);
            if (y > 0) Push(i - n);
            if (y < n - 1) Push(i + n);
        }
        return ocean;
    }

    /// Jarak Euclid tepat (Felzenszwalb & Huttenlocher), dalam piksel. Darat = 0. Pakai double: 1e20 + q^2 hilang di float.
    private static float[] DistanceToLand(bool[] ocean) {
        const int n = Resolution;
        const double inf = 1e20;
        var grid = new double[n * n];
        for (int i = 0; i < grid.Length; i++) grid[i] = ocean[i] ? inf : 0.0;

        var f = new double[n];
        var d = new double[n];
        var v = new int[n];
        var z = new double[n + 1];
        for (int x = 0; x < n; x++) {
            for (int y = 0; y < n; y++) f[y] = grid[y * n + x];
            Transform1D(f, d, v, z, n);
            for (int y = 0; y < n; y++) grid[y * n + x] = d[y];
        }
        var result = new float[n * n];
        for (int y = 0; y < n; y++) {
            System.Array.Copy(grid, y * n, f, 0, n);
            Transform1D(f, d, v, z, n);
            for (int x = 0; x < n; x++) result[y * n + x] = (float)System.Math.Sqrt(d[x]);
        }
        return result;
    }

    private static void Transform1D(double[] f, double[] d, int[] v, double[] z, int n) {
        int k = 0;
        v[0] = 0;
        z[0] = double.NegativeInfinity;
        z[1] = double.PositiveInfinity;
        for (int q = 1; q < n; q++) {
            double s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
            while (s <= z[k]) {
                k--;
                s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
            }
            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = double.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++) {
            while (z[k + 1] < q) k++;
            double dq = q - v[k];
            d[q] = dq * dq + f[v[k]];
        }
    }
}

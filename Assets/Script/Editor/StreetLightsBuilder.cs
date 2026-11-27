using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lampu jalan untuk siklus siang-malam. Menu: Go Vista > Kota > Pasang Lampu Jalan.
///
/// 1. Menaruh tiang lamppost_001 baru (sama seperti contohlampu) di trotoar jalan lurus yang belum punya
///    tiang: selang-seling kiri/kanan ~30 m, jalan pinggir kota hanya sisi dalam. Semua tiang baru ada di
///    bawah root "Street Lights (Generated)"; menjalankan menu lagi menghapus root itu lalu membangun ulang.
/// 2. Memasang Light + StreetLamp di SEMUA tiang lampu (yang lama juga): lamppost_001 dapat Spot Light
///    menghadap ke bawah, lampu taman bola lamp_post_001 dapat Point Light pendek.
///    DayNightCycle yang menyalakannya saat senja.
/// 3. Memastikan ada objek "Day Night Cycle" di WorldScene.
///
/// Untuk menambah/memindah tiang: ubah daftar Spots (posisi trotoar + nama tile jalan di sebelahnya).
/// </summary>
public static class StreetLightsBuilder {
    private const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    private const string RootName = "Street Lights (Generated)";
    private const string CycleName = "Day Night Cycle";
    private const string LightChildName = "Lamp Light";
    private const string LamppostPath = "Assets/Download_Assets/ithappy/Megacity/Meshes/Landscape/Roads/lamppost_001.fbx";
    private const string ParkLampPath = "Assets/Download_Assets/ithappy/Megacity/Prefabs/Props/lamp_post_001.prefab";
    private const string StreetMesh = "lamppost_001";
    private const string ParkMesh = "lamp_post_001";
    private const string ExampleLampName = "contohlampu";
    private const string SkyShaderName = "GoVista/SkyboxDayNight";

    // Warna lampu jalan: putih hangat (sodium modern), cocok dengan target senja.
    private static readonly Color LampColor = new Color(1f, 0.8f, 0.55f);

    private readonly struct Spot {
        public readonly float X, Z;
        public readonly string Road; // tile Road_009 di sebelahnya; lengan lampu diarahkan ke tengah jalan ini
        public Spot(float x, float z, string road) { X = x; Z = z; Road = road; }
    }

    private static readonly Spot[] Spots = {
        new Spot(114.1f, 223.9f, "Road_009 (136)"),
        new Spot(174.1f, 234.3f, "Road_009 (102)"),
        new Spot(204.1f, 223.9f, "Road_009 (86)"),
        new Spot(417.0f, 223.6f, "Road_009 (167)"),
        new Spot(448.3f, 223.6f, "Road_009 (165)"),
        new Spot(298.9f, 213.8f, "Road_009 (146)"),
        new Spot(288.5f, 243.8f, "Road_009 (149)"),
        new Spot(298.9f, 274.1f, "Road_009 (131)"),
        new Spot(298.9f, 304.3f, "Road_009 (133)"),
        new Spot(174.1f, 313.8f, "Road_009 (103)"),
        new Spot(204.1f, 313.8f, "Road_009 (084)"),
        new Spot(234.1f, 313.8f, "Road_009 (106)"),
        new Spot(264.3f, 313.8f, "Road_009 (121)"),
        new Spot(324.7f, 313.8f, "Road_009 (122)"),
        new Spot(149.3f, 259.1f, "Road_009 (145)"),
        new Spot(149.3f, 304.0f, "Road_009 (135)"),
        new Spot(104.3f, 124.1f, "Road_009 (101)"),
        new Spot(104.3f, 169.1f, "Road_009 (085)"),
        new Spot(104.3f, 214.1f, "Road_009 (153)"),
        new Spot(129.1f, 99.3f, "Road_009 (119)"),
        new Spot(159.1f, 99.3f, "Road_009 (140)"),
        new Spot(189.1f, 99.3f, "Road_009 (091)"),
        new Spot(279.1f, 99.3f, "Road_009 (112)"),
        new Spot(323.8f, 99.3f, "Road_009 (95)"),
        new Spot(114.1f, 159.3f, "Road_009 (105)"),
        new Spot(159.1f, 159.3f, "Road_009 (79)"),
        new Spot(189.1f, 148.9f, "Road_009 (93)"),
        new Spot(308.5f, 189.0f, "Road_009 (88)"),
        new Spot(254.3f, 109.1f, "Road_009 (126)"),
        new Spot(348.7f, 109.1f, "Road_009 (98)"),
        new Spot(359.1f, 153.8f, "Road_009 (97)"),
        new Spot(359.1f, 198.8f, "Road_009 (117)"),
        new Spot(359.1f, 258.8f, "Road_009 (123)"),
        new Spot(348.7f, 304.3f, "Road_009 (115)"),
        new Spot(159.2f, 283.8f, "Road_009 (120)"),
        new Spot(196.7f, 283.8f, "Road_009 (076)"),
        new Spot(219.2f, 294.2f, "Road_009 (100)"),
        new Spot(323.9f, 294.2f, "Road_009 (96)"),
        new Spot(385.2f, 283.8f, "Road_009 (179)"),
        new Spot(417.0f, 283.8f, "Road_009 (176)"),
        new Spot(308.8f, 129.3f, "Road_009 (134)"),
        new Spot(339.0f, 118.9f, "Road_009 (144)"),
        new Spot(224.3f, 198.8f, "Road_009 (75)"),
        new Spot(427.3f, 257.6f, "Road_009 (177)"),
        // Tahap 2 (celah gelap): tikungan, pertigaan, jalan zebra cross; tiap trotoar <= ~16 m dari tiang.
        new Spot(213.88f, 124.12f, "Road_011(Clone) (03)"),
        new Spot(213.88f, 168.82f, "Road_010 (33)"),
        new Spot(144.08f, 223.88f, "Road_011(Clone) (17)"),
        new Spot(338.53f, 178.62f, "Road_009 (87)"),
        new Spot(284.01f, 138.82f, "Road_010 (19)"),
        new Spot(348.69f, 228.80f, "Road_011(Clone) (6)"),
        new Spot(284.01f, 168.82f, "Road_010 (31)"),
        new Spot(278.89f, 294.17f, "Road_019(Clone) (3)"),
        new Spot(94.58f, 233.58f, "road_013 (4)"),
        new Spot(223.58f, 233.58f, "road_013 (2)"),
        new Spot(432.92f, 178.60f, "Road_009 (159)"),
        new Spot(464.72f, 178.60f, "Road_009 (161)"),
        new Spot(278.83f, 118.92f, "Road_011(Clone) (13)"),
        new Spot(138.86f, 288.96f, "Road_011(Clone) (1)"),
        new Spot(224.28f, 109.08f, "Road_010 (28)"),
        new Spot(144.08f, 148.88f, "Road_009 (130)"),
        new Spot(138.88f, 244.08f, "Road_009 (118)"),
        new Spot(93.88f, 109.08f, "Road_009 (73)"),
        new Spot(93.88f, 154.08f, "Road_011(Clone) (16)"),
        new Spot(93.88f, 184.08f, "Road_009 (150)"),
        new Spot(273.64f, 153.82f, "Road_011(Clone) (19)"),
        new Spot(224.28f, 139.08f, "Road_010 (24)"),
        new Spot(234.08f, 283.78f, "Road_010 (30)"),
        new Spot(338.76f, 283.79f, "Road_009 (94)"),
        new Spot(348.70f, 139.06f, "Road_009 (129)"),
        new Spot(385.23f, 234.00f, "Road_009 (169)"),
    };

    // Lampu taman bola (lamp_post_001) di rumput sepanjang jalan setapak taman kolam (park_road_007),
    // di kedua sisi jembatan, menjauh dari bangku.
    // Parkiran depan dealer Flamingo (bukan tile jalan): tiang di garis pembatas tengah tiap kolom slot,
    // lengan menghadap jalur tengah (x ~129). (x, z, arah lengan / yaw).
    private static readonly Vector3[] ParkingSpots = {
        new Vector3(119.0f, 183.0f, 90f), new Vector3(119.0f, 170.6f, 90f),
        new Vector3(139.2f, 183.0f, 270f), new Vector3(139.2f, 170.6f, 270f),
    };

    private static readonly Vector2[] ParkSpots = {
        new Vector2(294f, 158.3f), new Vector2(294f, 149.7f),
        new Vector2(337.5f, 158.3f), new Vector2(337.5f, 149.7f),
    };

    [MenuItem("Go Vista/Kota/Pasang Lampu Jalan")]
    private static void ApplyFromMenu() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            Debug.LogWarning("[Lampu Jalan] Hentikan Play mode dulu.");
            return;
        }
        Debug.Log("[Lampu Jalan] " + Apply());
    }

    /// <summary>Bangun ulang tiang baru + pasang light di semua tiang, lalu simpan scene.</summary>
    public static string Apply() {
        Scene world = SceneManager.GetSceneByPath(WorldScenePath);
        if (!world.isLoaded)
            world = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);
        var lamppostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LamppostPath);
        if (lamppostPrefab == null)
            return $"Model {LamppostPath} tidak ditemukan.";

        foreach (GameObject old in world.GetRootGameObjects().Where(g => g.name == RootName).ToArray())
            Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, world);

        var log = new List<string>();
        int placed = 0;
        Dictionary<string, Transform> roads = world.GetRootGameObjects()
            .Where(g => IsRoad(g.name))
            .GroupBy(g => g.name).ToDictionary(g => g.Key, g => g.First().transform);
        foreach (Spot spot in Spots) {
            if (!roads.TryGetValue(spot.Road, out Transform road)) {
                log.Add($"Lewati ({spot.X}, {spot.Z}): jalan '{spot.Road}' tidak ada.");
                continue;
            }
            PlaceLamppost(lamppostPrefab, root.transform, spot, road, ++placed);
        }
        int parkingPlaced = 0;
        foreach (Vector3 p in ParkingSpots) PlaceParkingLamppost(lamppostPrefab, root.transform, p, ++parkingPlaced);
        var parkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParkLampPath);
        int parkPlaced = 0;
        if (parkPrefab == null) log.Add($"Model {ParkLampPath} tidak ditemukan; lampu taman dilewati.");
        else foreach (Vector2 p in ParkSpots) PlaceParkLamp(parkPrefab, root.transform, p, ++parkPlaced);

        int lit = 0;
        foreach (MeshFilter mf in AllLampMeshes(world)) {
            SetupLamp(mf);
            lit++;
        }

        EnsureCycle(world);
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        log.Insert(0, $"{placed} tiang jalan, {parkingPlaced} tiang parkiran, {parkPlaced} lampu taman, {lit} tiang diberi lampu.");
        return string.Join("\n", log);
    }

    private static void PlaceLamppost(GameObject prefab, Transform parent, Spot spot, Transform road, int index) {
        // Arah dari trotoar ke garis tengah jalan (tegak lurus arah jalan). Tikungan (road_013): ke tengah tile,
        // yang berada di atas busur jalannya.
        Vector3 offset = new Vector3(road.position.x - spot.X, 0f, road.position.z - spot.Z);
        bool curve = road.GetComponent<MeshFilter>()?.sharedMesh?.name == "road_013";
        Vector3 along = road.forward;
        Vector3 toCenter = curve ? offset.normalized : (offset - Vector3.Dot(offset, along) * along).normalized;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = $"{StreetMesh} (Gen {index:00})";
        go.transform.SetPositionAndRotation(new Vector3(spot.X, GroundY(spot.X, spot.Z, road.position.y), spot.Z),
            Quaternion.LookRotation(toCenter, Vector3.up)); // lengan lampu = +Z model
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                   StaticEditorFlags.OccludeeStatic);
        // Sama seperti contohlampu: tiang punya collider supaya tidak ditembus pemain/mobil.
        if (go.GetComponent<Collider>() == null) {
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }
    }

    private static bool IsRoad(string name) => name.StartsWith("Road_") || name.StartsWith("road_");

    private static void PlaceParkingLamppost(GameObject prefab, Transform parent, Vector3 spot, int index) {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = $"{StreetMesh} (Gen Parkir {index:00})";
        go.transform.SetPositionAndRotation(new Vector3(spot.x, SurfaceY(spot.x, spot.y, go.transform), spot.y),
            Quaternion.Euler(0f, spot.z, 0f));
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                   StaticEditorFlags.OccludeeStatic);
        if (go.GetComponent<Collider>() == null) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
    }

    /// Permukaan teratas (bukan trigger, bukan objek itu sendiri) di titik (x, z).
    private static float SurfaceY(float x, float z, Transform self) {
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 30f).OrderBy(h => h.distance)) {
            if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(self)) continue;
            return hit.point.y;
        }
        return 23.2f;
    }

    private static void PlaceParkLamp(GameObject prefab, Transform parent, Vector2 p, int index) {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = $"{ParkMesh} (Gen Taman {index:00})";
        float y = 23.2f;
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(p.x, 40f, p.y), Vector3.down, 30f).OrderBy(h => h.distance)) {
            if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(go.transform)) continue;
            y = hit.point.y;
            break;
        }
        go.transform.SetPositionAndRotation(new Vector3(p.x, y, p.y), Quaternion.identity);
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    private static float GroundY(float x, float z, float fallback) {
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, fallback + 10f, z), Vector3.down, 20f)
                     .OrderBy(h => h.distance)) {
            if (hit.collider.isTrigger || hit.collider is TerrainCollider) continue;
            if (IsRoad(hit.collider.transform.root.name)) return hit.point.y;
        }
        return fallback;
    }

    private static IEnumerable<MeshFilter> AllLampMeshes(Scene world) =>
        world.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
            .Where(mf => mf.sharedMesh != null && (mf.sharedMesh.name == StreetMesh || mf.sharedMesh.name == ParkMesh));

    private static void SetupLamp(MeshFilter mf) {
        Transform lamp = mf.transform;
        bool street = mf.sharedMesh.name == StreetMesh;

        // Bangun ulang light kita; light contoh lama (Point Light di contohlampu) dinonaktifkan, tidak dihapus.
        foreach (Transform child in lamp.Cast<Transform>().ToArray()) {
            if (child.name == LightChildName) Object.DestroyImmediate(child.gameObject);
            else if (lamp.name == ExampleLampName && child.GetComponent<Light>() != null) child.gameObject.SetActive(false);
        }

        var lightGo = new GameObject(LightChildName);
        lightGo.transform.SetParent(lamp, false);
        var light = lightGo.AddComponent<Light>();
        light.color = LampColor;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        float intensity;
        if (street) {
            // Tepat di bawah bohlam (submesh Emissive di ujung lengan), menghadap lurus ke bawah.
            lightGo.transform.localPosition = new Vector3(0f, 6.35f, 2.19f);
            lightGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            light.type = LightType.Spot;
            // Sorot lebar supaya trotoar di antara tiang ikut terang.
            light.range = 22f;
            light.spotAngle = 150f;
            light.innerSpotAngle = 85f;
            // Aspal parkiran dealer lebih gelap dari jalan, jadi tiang parkiran lebih terang.
            intensity = lamp.name.Contains("(Gen Parkir") ? 120f : 75f;
        } else {
            // Lampu taman bola: cahaya ke segala arah, jangkauan pendek. Important: mesh taman berukuran besar
            // (park_road_007 45x45 m) hanya menerima 8 lampu per objek, dan tanpa prioritas lampu taman kalah
            // dari lampu jalan di sekelilingnya sehingga jalan setapak tetap gelap.
            lightGo.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            light.type = LightType.Point;
            light.range = 12f;
            light.renderMode = LightRenderMode.ForcePixel;
            intensity = 14f;
        }
        light.intensity = intensity;
        light.enabled = false; // dinyalakan DayNightCycle

        var streetLamp = lamp.GetComponent<StreetLamp>();
        if (streetLamp == null) streetLamp = lamp.gameObject.AddComponent<StreetLamp>();
        streetLamp.Configure(light, lamp.GetComponent<Renderer>(), 1, intensity);
        EditorUtility.SetDirty(streetLamp);
    }

    /// Pastikan ada DayNightCycle; nilai tampilan yang sudah diatur di Inspector tidak disentuh,
    /// hanya referensi matahari dan shader skybox yang diisi kalau masih kosong.
    private static void EnsureCycle(Scene world) {
        DayNightCycle cycle = world.GetRootGameObjects()
            .Select(g => g.GetComponentInChildren<DayNightCycle>(true)).FirstOrDefault(c => c != null);
        if (cycle == null) {
            var go = new GameObject(CycleName);
            SceneManager.MoveGameObjectToScene(go, world);
            cycle = go.AddComponent<DayNightCycle>();
        }
        var so = new SerializedObject(cycle);
        SerializedProperty sunProp = so.FindProperty("sun");
        GameObject sun = world.GetRootGameObjects().FirstOrDefault(g => g.name == "Directional Light");
        if (sunProp.objectReferenceValue == null && sun != null) sunProp.objectReferenceValue = sun.GetComponent<Light>();
        SerializedProperty shaderProp = so.FindProperty("cubemapSkyShader");
        if (shaderProp.objectReferenceValue == null) shaderProp.objectReferenceValue = Shader.Find(SkyShaderName);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

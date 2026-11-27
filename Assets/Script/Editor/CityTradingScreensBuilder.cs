using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tahap 3 polish grafik kota: layar chart trading animasi (shader GoVista/TradingScreen) di fasad gedung
/// sekitar plaza spawn + satu kiosk layar berdiri di plaza. Menu: Go Vista > Kota > Pasang Layar Trading.
///
/// Layar ditempel lewat raycast ke collider gedung, jadi posisinya pas di dinding. Semua hasil ada di bawah
/// root "City Trading Screens" di WorldScene; menjalankan menu lagi menghapus root lama lalu membangun ulang.
/// Untuk menambah/memindah layar: ubah daftar Facades / Kiosks di bawah lalu jalankan menu lagi.
/// </summary>
public static class CityTradingScreensBuilder {
    private const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    private const string RootName = "City Trading Screens";
    private const string ShaderName = "GoVista/TradingScreen";
    private const string MaterialFolder = "Assets/Materials/City";
    private const string HousingMaterialPath = MaterialFolder + "/ScreenHousing.mat";
    // Jarak layar dari dinding supaya tidak z-fighting, dan dari mana raycast dimulai.
    private const float WallOffset = 0.06f;
    private const float RayStartDistance = 80f;
    // Rangka layar fasad: tebal minimum dan lebar tepi di sekeliling layar.
    private const float HousingMinDepth = 0.25f;
    private const float HousingBorder = 0.35f;
    private const int MaterialVariants = 3;

    private readonly struct Facade {
        public readonly string Building;
        public readonly Vector3 Facing; // arah dinding menghadap (dari gedung ke luar)
        public readonly float Height;   // tinggi pusat layar (world Y)
        public readonly float Width;
        public readonly float ScreenHeight;
        public readonly float Side;     // geser ke samping sepanjang dinding (m)
        public readonly string Title;

        public Facade(string building, Vector3 facing, float height, float width, float screenHeight, float side,
                      string title) {
            Building = building;
            Facing = facing;
            Height = height;
            Width = width;
            ScreenHeight = screenHeight;
            Side = side;
            Title = title;
        }
    }

    private readonly struct Kiosk {
        public readonly string Name;
        public readonly Vector3 Position; // titik di tanah
        public readonly float Yaw;        // arah muka layar
        public readonly string Title;

        public Kiosk(string name, Vector3 position, float yaw, string title) {
            Name = name;
            Position = position;
            Yaw = yaw;
            Title = title;
        }
    }

    private static readonly Facade[] Facades = {
        new Facade("business_center_001 (Trading Academy)", Vector3.right, 33f, 12f, 6.5f, 0f, "TRADING ACADEMY  •  LIVE"),
        new Facade("business_center_005 (Banking Center)", Vector3.back, 36f, 9f, 5f, 0f, "GO VISTA MARKET"),
        // Menara kembar: raycast di tengah lewat celah, jadi layar dipasang di podium bawah.
        new Facade("skyscraper_001 (Vista Palace)", Vector3.right, 28.5f, 10f, 5.6f, 0f, "VISTA PALACE  •  IDX"),
    };

    private static readonly Kiosk[] Kiosks = {
        new Kiosk("Plaza Market Kiosk", new Vector3(252f, 23.2f, 170f), 202f, "GO VISTA  •  LIVE"),
    };

    [MenuItem("Go Vista/Kota/Pasang Layar Trading")]
    private static void ApplyFromMenu() {
        Debug.Log("[Layar Trading] " + Apply());
    }

    /// <summary>Bangun ulang semua layar di WorldScene lalu simpan scene. Mengembalikan ringkasan.</summary>
    public static string Apply() {
        Scene world = SceneManager.GetSceneByPath(WorldScenePath);
        if (!world.isLoaded)
            world = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            return $"Shader {ShaderName} tidak ditemukan.";

        foreach (GameObject old in world.GetRootGameObjects().Where(g => g.name == RootName).ToArray())
            Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, world);

        Material[] materials = EnsureMaterials(shader);
        Material housing = EnsureHousingMaterial();
        var log = new List<string>();
        int index = 0;
        foreach (Facade facade in Facades)
            log.Add(BuildFacade(world, root.transform, facade, materials[index++ % materials.Length], housing));
        foreach (Kiosk kiosk in Kiosks)
            log.Add(BuildKiosk(root.transform, kiosk, materials[index++ % materials.Length], housing));

        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        return string.Join("\n", log);
    }

    private static Material[] EnsureMaterials(Shader shader) {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "City");
        var result = new Material[MaterialVariants];
        for (int i = 0; i < MaterialVariants; i++) {
            string path = $"{MaterialFolder}/TradingScreen_{i + 1}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Seed", i * 3.7f + 1f);
            mat.SetFloat("_Speed", 0.45f + 0.2f * i);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            result[i] = mat;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    /// Rangka layar: logam gelap (URP Lit) supaya tidak memakai palet warna kota.
    private static Material EnsureHousingMaterial() {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(HousingMaterialPath);
        if (mat == null) {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, HousingMaterialPath);
        }
        mat.SetColor("_BaseColor", new Color(0.07f, 0.09f, 0.13f));
        mat.SetFloat("_Smoothness", 0.65f);
        mat.SetFloat("_Metallic", 0.4f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static string BuildFacade(Scene world, Transform root, Facade facade, Material material,
                                      Material housingMaterial) {
        Transform building = world.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == facade.Building);
        if (building == null)
            return $"{facade.Building}: gedung tidak ditemukan, dilewati";

        Bounds bounds = WorldBounds(building);
        Vector3 facing = facade.Facing.normalized;
        Vector3 along = Vector3.Cross(Vector3.up, facing).normalized;
        Vector3 target = new Vector3(bounds.center.x, facade.Height, bounds.center.z) + along * facade.Side;
        if (!RaycastBuilding(building, target, facing, out RaycastHit hit))
            return $"{facade.Building}: dinding tidak kena raycast, dilewati";

        Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
        if (normal.sqrMagnitude < 0.5f)
            normal = facing;
        // Fasad sering bercekung (kaca masuk ke dalam). Ambil bidang terdepan dan terbelakang di area layar,
        // lalu pasang layar di bidang terdepan dengan rangka yang menutup sampai ke dinding terdalam.
        if (!SampleFacadeDepth(building, hit.point, normal, facade.Width, facade.ScreenHeight,
                               out float front, out float back))
            return $"{facade.Building}: area layar keluar dari dinding, dilewati";
        Vector3 wallPoint = hit.point - normal * Vector3.Dot(hit.point, normal);
        Vector3 frontPoint = wallPoint + normal * front;
        float depth = Mathf.Max(HousingMinDepth, front - back + 0.1f);
        string label = facade.Building.Split('(').Last().TrimEnd(')');
        Quaternion faceRotation = Quaternion.LookRotation(-normal, Vector3.up); // Quad menghadap -Z lokal

        GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = label + " Screen Housing";
        housing.transform.SetParent(root, false);
        housing.transform.SetPositionAndRotation(frontPoint - normal * (depth / 2f - WallOffset), faceRotation);
        housing.transform.localScale = new Vector3(facade.Width + HousingBorder * 2f,
                                                   facade.ScreenHeight + HousingBorder * 2f, depth);
        housing.GetComponent<Renderer>().sharedMaterial = housingMaterial;
        Object.DestroyImmediate(housing.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(housing, StaticEditorFlags.BatchingStatic);

        GameObject screen = CreateScreen(label + " Screen", root, material, facade.Width, facade.ScreenHeight,
                                         facade.Title);
        screen.transform.SetPositionAndRotation(frontPoint + normal * (WallOffset * 2f), faceRotation);
        return $"{facade.Building}: layar {facade.Width}x{facade.ScreenHeight} di {frontPoint:F1}, " +
               $"rangka {depth:F1} m";
    }

    private static string BuildKiosk(Transform root, Kiosk kiosk, Material material, Material housingMaterial) {
        const float poleHeight = 2.6f;
        const float width = 3.2f;
        const float height = 1.8f;
        var kioskRoot = new GameObject(kiosk.Name);
        kioskRoot.transform.SetParent(root, false);
        kioskRoot.transform.SetPositionAndRotation(kiosk.Position, Quaternion.Euler(0f, kiosk.Yaw, 0f));

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pole.name = "Pole";
        pole.transform.SetParent(kioskRoot.transform, false);
        pole.transform.localPosition = new Vector3(0f, poleHeight / 2f, 0f);
        pole.transform.localScale = new Vector3(0.25f, poleHeight, 0.25f);
        pole.GetComponent<Renderer>().sharedMaterial = housingMaterial;

        GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = "Housing";
        housing.transform.SetParent(kioskRoot.transform, false);
        housing.transform.localPosition = new Vector3(0f, poleHeight + height / 2f, 0f);
        housing.transform.localScale = new Vector3(width + 0.2f, height + 0.2f, 0.18f);
        housing.GetComponent<Renderer>().sharedMaterial = housingMaterial;

        // Dua muka supaya terlihat dari kedua arah.
        for (int side = 0; side < 2; side++) {
            GameObject screen = CreateScreen(side == 0 ? "Screen Front" : "Screen Back", kioskRoot.transform,
                                             material, width, height, kiosk.Title);
            screen.transform.localPosition = new Vector3(0f, poleHeight + height / 2f, side == 0 ? 0.1f : -0.1f);
            screen.transform.localRotation = Quaternion.Euler(0f, side == 0 ? 180f : 0f, 0f);
        }
        GameObjectUtility.SetStaticEditorFlags(pole, StaticEditorFlags.BatchingStatic);
        GameObjectUtility.SetStaticEditorFlags(housing, StaticEditorFlags.BatchingStatic);
        return $"{kiosk.Name}: kiosk di {kiosk.Position:F1}";
    }

    private static GameObject CreateScreen(string name, Transform parent, Material material, float width,
                                           float height, string title) {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        // Aspek per layar lewat MaterialPropertyBlock akan memutus SRP batcher; aspek 1.8 di material cukup,
        // jadi ukuran layar dipilih mendekati 16:9.
        quad.transform.localScale = new Vector3(width, height, 1f);

        if (!string.IsNullOrEmpty(title)) {
            var label = new GameObject("Title", typeof(RectTransform));
            label.transform.SetParent(quad.transform, false);
            var text = label.AddComponent<TextMeshPro>();
            text.text = title;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color(0.85f, 0.95f, 1f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 1f;
            text.fontSizeMax = 40f;
            var rect = (RectTransform)label.transform;
            // Di atas blok header shader (UV y 0.86..0.94), sedikit di depan quad. Skala dibagi ukuran quad
            // supaya teks tidak ikut melar.
            rect.localPosition = new Vector3(-0.08f, 0.40f, -0.01f);
            rect.localScale = new Vector3(1f / width, 1f / height, 1f) * 0.5f;
            rect.sizeDelta = new Vector2(width * 1.5f, height * 0.12f);
        }
        return quad;
    }

    private static bool RaycastBuilding(Transform building, Vector3 target, Vector3 facing, out RaycastHit hit) {
        Physics.SyncTransforms();
        Vector3 origin = target + facing * RayStartDistance;
        foreach (RaycastHit h in Physics.RaycastAll(origin, -facing, RayStartDistance * 2f).OrderBy(h => h.distance)) {
            if (h.collider.transform.IsChildOf(building)) {
                hit = h;
                return true;
            }
        }
        hit = default;
        return false;
    }

    /// Raycast di 9 titik area layar (tengah, sudut, tengah sisi). front/back = kedalaman bidang terdepan dan
    /// terbelakang sepanjang normal. false kalau ada titik yang tidak mengenai gedung.
    private static bool SampleFacadeDepth(Transform building, Vector3 center, Vector3 normal, float width,
                                          float height, out float front, out float back) {
        Vector3 along = Vector3.Cross(Vector3.up, normal).normalized;
        front = float.MinValue;
        back = float.MaxValue;
        for (int ix = -1; ix <= 1; ix++) {
            for (int iy = -1; iy <= 1; iy++) {
                Vector3 p = center + along * (ix * width / 2f) + Vector3.up * (iy * height / 2f);
                if (!RaycastBuilding(building, p, normal, out RaycastHit h))
                    return false;
                float d = Vector3.Dot(h.point, normal);
                front = Mathf.Max(front, d);
                back = Mathf.Min(back, d);
            }
        }
        return true;
    }

    private static Bounds WorldBounds(Transform t) {
        Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
        Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(t.position, Vector3.one);
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
}

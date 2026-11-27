using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tata ulang interior dealer Flamingo (showroom 15x15 m + ceruk ruang tunggu). Menu:
/// Go Vista > Dealer Mobil > Tata Showroom Flamingo.
///
///  - 3 podium bulat dengan lis bercahaya; GT di podium tengah berputar (ShowroomTurntable), Goat & Bezerker
///    di podium kiri menghadap pintu. Rigidbody mobil pajangan dibuat kinematic supaya tidak terdorong.
///  - Meja Johnny (+ laptop, collider Cube, Johnny) dipindah utuh ke kanan dekat pintu, Johnny menghadap ke dalam.
///  - Lampu: 6 lampu plafon putih merata + spotlight hangat per mobil; Johnny disorot dari depan (bukan dari atas
///    kepala, yang membuat wajahnya gelap/seram saat malam) + cahaya pengisi lembut. Lampu interior menyala terus.
///  - Ruang tunggu di ceruk kanan belakang (sofa, meja & kursi lama, lampu gantung, TV), tanaman, pigura.
///
/// Semua objek baru ada di bawah root "Flamingo Showroom (Generated)"; menjalankan menu lagi membangun ulang
/// root itu. Objek lama (mobil, meja, Johnny, spotlight, kursi) dipindah ke posisi di daftar bawah.
/// </summary>
public static class FlamingoShowroomBuilder {
    private const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    private const string RootName = "Flamingo Showroom (Generated)";
    private const string MaterialFolder = "Assets/Materials/Dealership";
    private const float Floor = 23.19f;
    private const float Ceiling = 29.1f;
    private const float PodiumHeight = 0.24f;
    private const float CarpetY = Floor + 0.015f;

    private const string NightclubProps = "Assets/Download_Assets/PolygonNightclubs/Prefabs/Props/";
    private const string ShopProps = "Assets/Synty/PolygonShops/Prefabs/Props/";

    private static readonly Color CeilingColor = new Color(1f, 0.97f, 0.92f);
    private static readonly Color SpotColor = new Color(1f, 0.93f, 0.8f);
    private static readonly Color WarmColor = new Color(1f, 0.84f, 0.66f);

    private readonly struct Podium {
        public readonly string Car;
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly float CarYaw;   // arah mobil (diabaikan untuk podium berputar)
        public readonly bool Turntable;
        public readonly string[] Spots; // prop spotlight lama yang dipakai menyorot mobil ini
        public Podium(string car, Vector2 center, float radius, float carYaw, bool turntable, params string[] spots) {
            Car = car; Center = center; Radius = radius; CarYaw = carYaw; Turntable = turntable; Spots = spots;
        }
    }

    // Pintu masuk di (127, 199). Mobil samping menghadap pintu.
    private static readonly Podium[] Podiums = {
        new Podium("Goat", new Vector2(120.6f, 210.6f), 2.6f, 151f, false, "SM_Prop_Light_Spotlight_03"),
        new Podium("Bezerker", new Vector2(120.6f, 202.4f), 2.6f, 118f, false, "SM_Prop_Light_Spotlight_03 (1)"),
        new Podium("GT", new Vector2(126.4f, 207.3f), 2.7f, 200f, true,
            "SM_Prop_Light_Spotlight_03 (2)", "SM_Prop_Light_Spotlight_03 (5)"),
    };

    // Grup meja Johnny: dipindah & diputar bersama (relatif terhadap meja), jadi susunannya tetap sama.
    private const string DeskName = "SM_Prop_Shop_Desk_01 (8)";
    private static readonly string[] DeskGroup = { "NPC_Johnny_CarDealer", "SM_Prop_Computer_Laptop_03", "Cube", "Cube (1)", "Cube (2)" };
    private static readonly Vector3 DeskTarget = new Vector3(129.4f, Floor, 202.6f);
    private const float DeskTargetYaw = 270f; // sebelumnya 180: Johnny menghadap -X (ke dalam ruangan)
    private const string JohnnyName = "NPC_Johnny_CarDealer";
    private const string JohnnySpot = "SM_Prop_Light_Spotlight_03 (4)";

    private static readonly Vector2[] CeilingLights = {
        new Vector2(120.8f, 201.5f), new Vector2(127.2f, 201.5f),
        new Vector2(120.8f, 206.5f), new Vector2(127.2f, 206.5f),
        new Vector2(120.8f, 211.5f), new Vector2(127.2f, 211.5f),
    };

    [MenuItem("Go Vista/Dealer Mobil/Tata Showroom Flamingo")]
    private static void ApplyFromMenu() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            Debug.LogWarning("[Showroom] Hentikan Play mode dulu.");
            return;
        }
        Debug.Log("[Showroom] " + Apply());
    }

    public static string Apply() {
        Scene world = SceneManager.GetSceneByPath(WorldScenePath);
        if (!world.isLoaded)
            world = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);
        var log = new List<string>();

        // Mobil turntable bisa ada di bawah root lama: keluarkan dulu sebelum root dihapus.
        foreach (Podium p in Podiums) {
            GameObject car = Find(world, p.Car);
            if (car != null && car.transform.parent != null && car.transform.root.name == RootName)
                car.transform.SetParent(null, true);
        }
        foreach (GameObject old in world.GetRootGameObjects().Where(g => g.name == RootName).ToArray())
            Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, world);

        Material podiumMat = EnsureMaterial("ShowroomPodium", new Color(0.13f, 0.13f, 0.15f), 0.8f, 0.25f, Color.black);
        Material rimMat = EnsureMaterial("ShowroomPodiumRim", new Color(0.9f, 0.95f, 1f), 0.5f, 0f,
            new Color(0.75f, 0.9f, 1f) * 2.2f);

        log.Add(RaiseCarpet(world));
        foreach (Podium p in Podiums) log.Add(BuildPodium(world, root.transform, p, podiumMat, rimMat));
        log.Add(MoveDeskGroup(world));
        LightJohnny(world, root.transform);
        BuildCeilingLights(root.transform);
        log.Add(BuildLounge(world, root.transform));
        BuildDecor(root.transform);

        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);
        return string.Join("\n", log);
    }

    // ---------- Lantai ----------

    /// Karpet showroom (ubin 2.5 m) tertutup pelat kota building_platform_004 (15x15 m) yang 0.5 cm lebih tinggi.
    /// Pelat sebesar itu hanya menerima 4-8 lampu per objek, jadi lantai interior belang. Karpet dinaikkan 1.5 cm.
    private static string RaiseCarpet(Scene world) {
        GameObject dealer = Find(world, "CarDealership");
        if (dealer == null) return "CarDealership tidak ada; karpet tidak diubah.";
        int raised = 0;
        foreach (Transform t in dealer.GetComponentsInChildren<Transform>(true)) {
            if (!t.name.StartsWith("SM_Bld_Base_Floor_01") || Mathf.Abs(t.position.y - Floor) > 0.02f) continue;
            Vector3 p = t.position;
            if (Mathf.Abs(p.y - CarpetY) < 0.001f) continue;
            t.position = new Vector3(p.x, CarpetY, p.z);
            EditorUtility.SetDirty(t);
            raised++;
        }
        return $"{raised} ubin karpet dinaikkan ke y {CarpetY}.";
    }

    // ---------- Podium + mobil ----------

    private static string BuildPodium(Scene world, Transform root, Podium p, Material podiumMat, Material rimMat) {
        var group = new GameObject($"Podium {p.Car}");
        group.transform.SetParent(root, false);
        group.transform.position = new Vector3(p.Center.x, Floor, p.Center.y);

        GameObject top = Disc("Podium", group.transform, p.Radius, PodiumHeight, podiumMat, true);
        top.transform.localPosition = new Vector3(0f, PodiumHeight * 0.5f, 0f);
        GameObject rim = Disc("Lis Cahaya", group.transform, p.Radius + 0.06f, 0.04f, rimMat, false);
        rim.transform.localPosition = new Vector3(0f, PodiumHeight * 0.5f, 0f);

        GameObject car = Find(world, p.Car);
        if (car == null) return $"Mobil '{p.Car}' tidak ada, podium dibuat tanpa mobil.";
        foreach (Rigidbody rb in car.GetComponentsInChildren<Rigidbody>(true)) {
            rb.isKinematic = true;
            rb.useGravity = false;
            EditorUtility.SetDirty(rb);
        }

        float yaw = p.Turntable ? car.transform.eulerAngles.y : p.CarYaw;
        // Pusatkan mobil (bukan pivot-nya) di tengah podium.
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 centerOffset = rot * Quaternion.Inverse(car.transform.rotation) * (RendererBounds(car).center - car.transform.position);
        Vector3 carPos = new Vector3(p.Center.x - centerOffset.x, Floor + PodiumHeight + 0.04f, p.Center.y - centerOffset.z);

        if (p.Turntable) {
            var pivot = new GameObject("Turntable");
            pivot.transform.SetParent(group.transform, false);
            pivot.transform.position = group.transform.position;
            pivot.AddComponent<ShowroomTurntable>();
            car.transform.SetParent(pivot.transform, true);
        }
        car.transform.SetPositionAndRotation(carPos, rot);
        EditorUtility.SetDirty(car.transform);

        Vector3 target = new Vector3(p.Center.x, Floor + 0.9f, p.Center.y);
        Vector3 toDoor = new Vector3(127f - p.Center.x, 0f, 199f - p.Center.y).normalized;
        for (int i = 0; i < p.Spots.Length; i++) {
            // Dari plafon di depan mobil (sisi pintu); spot kedua di sisi seberang supaya bodi terang merata.
            Vector3 dir = i == 0 ? toDoor : Quaternion.Euler(0f, 150f, 0f) * toDoor;
            Vector3 at = new Vector3(p.Center.x, Ceiling, p.Center.y) + dir * 2.6f;
            AimSpot(world, p.Spots[i], at, target, 50f, 15f, 58f);
        }
        return $"Podium {p.Car} di ({p.Center.x}, {p.Center.y}){(p.Turntable ? " berputar" : "")}.";
    }

    private static GameObject Disc(string name, Transform parent, float radius, float height, Material mat, bool collider) {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // CapsuleCollider bawaan tidak cocok
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f); // cylinder Unity tinggi 2
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // ---------- Meja Johnny ----------

    private static string MoveDeskGroup(Scene world) {
        GameObject desk = Find(world, DeskName);
        if (desk == null) return $"Meja '{DeskName}' tidak ada.";
        Quaternion delta = Quaternion.Euler(0f, DeskTargetYaw, 0f) * Quaternion.Inverse(desk.transform.rotation);
        Vector3 deskPos = desk.transform.position;
        foreach (string name in DeskGroup) {
            GameObject go = Find(world, name);
            if (go == null) continue;
            Vector3 offset = go.transform.position - deskPos;
            go.transform.SetPositionAndRotation(DeskTarget + delta * offset, delta * go.transform.rotation);
            EditorUtility.SetDirty(go.transform);
        }
        desk.transform.SetPositionAndRotation(DeskTarget, Quaternion.Euler(0f, DeskTargetYaw, 0f));
        EditorUtility.SetDirty(desk.transform);
        return $"Meja Johnny dipindah ke ({DeskTarget.x}, {DeskTarget.z}).";
    }

    private static void LightJohnny(Scene world, Transform root) {
        GameObject johnny = Find(world, JohnnyName);
        if (johnny == null) return;
        Vector3 face = johnny.transform.position + Vector3.up * 1.6f;
        Vector3 front = johnny.transform.forward;
        // Key light: dari plafon di depan meja, menyorot wajah secara miring (bukan lurus dari atas).
        AimSpot(world, JohnnySpot, new Vector3(face.x, Ceiling, face.z) + front * 3.2f, face, 28f, 12f, 40f);
        // Fill: cahaya lembut setinggi dada di depan meja supaya bayangan wajah tidak pekat.
        AddPoint(root, "Johnny Fill Light", face + front * 1.6f - Vector3.up * 0.3f, WarmColor, 2.2f, 4f);
    }

    // ---------- Lampu ----------

    private static void AimSpot(Scene world, string propName, Vector3 at, Vector3 target, float intensity, float range, float angle) {
        GameObject prop = Find(world, propName);
        if (prop == null) return;
        Vector3 dir = (target - at).normalized;
        prop.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Quaternion.LookRotation(dir).eulerAngles.y, 0f));
        Light light = prop.GetComponentInChildren<Light>(true);
        if (light != null) {
            light.transform.rotation = Quaternion.LookRotation(dir);
            light.transform.position = at - Vector3.up * 0.2f;
            light.type = LightType.Spot;
            light.color = SpotColor;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.55f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            EditorUtility.SetDirty(light);
        }
        EditorUtility.SetDirty(prop.transform);
    }

    private static Light AddPoint(Transform parent, string name, Vector3 pos, Color color, float intensity, float range) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        return light;
    }

    /// Spot lebar menghadap ke bawah: lantai & mobil terang tanpa membuat dinding dekat lampu silau
    /// (Point Light di plafon 6 m membuat dinding jauh lebih terang daripada lantai).
    private static void AddDownlight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range,
                                     float angle, float innerAngle) {
        Light light = AddPoint(parent, name, pos, color, intensity, range);
        light.type = LightType.Spot;
        light.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        light.spotAngle = angle;
        light.innerSpotAngle = innerAngle;
    }

    private static void BuildCeilingLights(Transform root) {
        var group = new GameObject("Lampu Plafon");
        group.transform.SetParent(root, false);
        int i = 0;
        foreach (Vector2 p in CeilingLights) {
            GameObject fixture = Spawn(ShopProps + "SM_Prop_Lighting_Ceiling_Bar_07.prefab", group.transform,
                new Vector3(p.x, Ceiling, p.y), 0f);
            if (fixture != null) fixture.name = $"Lampu Plafon {++i}";
            AddDownlight(group.transform, $"Cahaya Plafon {i}", new Vector3(p.x, Ceiling - 2.0f, p.y), CeilingColor, 30f, 11f, 165f, 100f);
        }
    }

    // ---------- Ruang tunggu (ceruk x 131.9..136.0, z 206.4..213.8; pintu di dinding kanan z ~209.6..211.4) ----------

    private static string BuildLounge(Scene world, Transform root) {
        var group = new GameObject("Ruang Tunggu");
        group.transform.SetParent(root, false);
        const float cx = 133.7f;
        Spawn(NightclubProps + "Modular/SM_Prop_Sofa_01.prefab", group.transform, new Vector3(cx, Floor, 212.65f), 180f);
        Spawn(ShopProps + "SM_Prop_Computer_TV_Wall_01.prefab", group.transform, new Vector3(cx, Floor + 2.45f, 213.76f), 180f);
        Spawn(ShopProps + "SM_Prop_Lighting_Ceiling_Shaded_01.prefab", group.transform, new Vector3(cx, Ceiling, 211.6f), 0f);
        // Tepat di bawah kap lampu gantung (bawah kap di y ~27.9).
        AddDownlight(group.transform, "Cahaya Ruang Tunggu", new Vector3(cx, 27.75f, 211.6f), WarmColor, 55f, 9f, 155f, 90f);
        Spawn(NightclubProps + "SM_Prop_Plant_Palm_01.prefab", group.transform, new Vector3(132.4f, Floor, 213.2f), 0f);

        // Meja & kursi lama dari dekat pintu dipindah ke sini, kursi menghadap sofa.
        Place(world, "SM_Prop_Table_13", new Vector3(cx, Floor, 211.55f), 0f);
        Place(world, "SM_Prop_Chair_09", new Vector3(cx - 0.8f, Floor, 210.45f), 0f);
        Place(world, "SM_Prop_Chair_09 (1)", new Vector3(cx + 0.8f, Floor, 210.45f), 0f);
        return "Ruang tunggu dibuat di ceruk kanan belakang.";
    }

    // ---------- Tanaman & dinding ----------

    private static void BuildDecor(Transform root) {
        var group = new GameObject("Dekorasi");
        group.transform.SetParent(root, false);
        Spawn(NightclubProps + "SM_Prop_Plant_Palm_01.prefab", group.transform, new Vector3(117.7f, Floor, 213.1f), 30f);
        Spawn(NightclubProps + "SM_Prop_Plant_Palm_01.prefab", group.transform, new Vector3(117.7f, Floor, 199.7f), 110f);
        Spawn(NightclubProps + "SM_Prop_Plant_Fern_01.prefab", group.transform, new Vector3(131.2f, Floor, 199.7f), 0f);
        Spawn(NightclubProps + "SM_Prop_Plant_Fern_02.prefab", group.transform, new Vector3(131.2f, Floor, 205.6f), 60f);
        Spawn(NightclubProps + "SM_Prop_Plant_Palm_02.prefab", group.transform, new Vector3(135.5f, Floor, 207.0f), 200f);

        // Dinding belakang (z 213.78, menghadap -Z) dan dinding kiri (x 117.01, menghadap +X).
        Spawn(NightclubProps + "SM_Prop_Art_Frame_05.prefab", group.transform, new Vector3(123f, Floor + 2.4f, 213.76f), 180f);
        Spawn(NightclubProps + "SM_Prop_Art_Frame_05.prefab", group.transform, new Vector3(128.8f, Floor + 2.4f, 213.76f), 180f);
        Spawn(NightclubProps + "SM_Prop_Display_Board_01.prefab", group.transform, new Vector3(117.03f, Floor + 2.6f, 206.5f), 90f);
    }

    // ---------- Util ----------

    private static GameObject Spawn(string path, Transform parent, Vector3 pos, float yaw) {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) {
            Debug.LogWarning($"[Showroom] Prefab tidak ada: {path}");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        return go;
    }

    private static void Place(Scene world, string name, Vector3 pos, float yaw) {
        GameObject go = Find(world, name);
        if (go == null) return;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        EditorUtility.SetDirty(go.transform);
    }

    private static Bounds RendererBounds(GameObject go) {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    private static GameObject Find(Scene world, string name) =>
        world.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == name)?.gameObject;

    private static Material EnsureMaterial(string name, Color baseColor, float smoothness, float metallic, Color emission) {
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateFolder("Assets/Materials", "Dealership");
        }
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        if (emission.maxColorComponent > 0f) {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }
}

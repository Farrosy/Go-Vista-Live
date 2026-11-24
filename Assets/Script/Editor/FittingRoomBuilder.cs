using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Go Vista > Interior > Pasang Ruang Ganti: memasang FittingRoomStation di setiap bilik ganti scene
/// Interior_ClothingShop. Bilik dikenali dari dinding berpintu (SM_Bld_Base_Wall_Door_01) di bawah objek
/// "FittingRooms"; arah dalam bilik = ke cermin (SM_Prop_Clothes_Mirror_01) terdekat.
///
/// Per bilik: lingkaran cahaya teal di tengah bilik (BuildingEntranceMarker, makin terang saat diinjak),
/// trigger Interactable "Ganti Baju", titik berdiri karakter preview, dan kamera editor ±4,3 m di depan
/// bilik (di toko). Dinding depan + tirai bilik disembunyikan selama editor terbuka.
/// Hanya bilik yang TIRAINYA TERBUKA (pintu bisa dilewati, dicek dengan raycast) yang dipasangi; bilik
/// bertirai tertutup dibiarkan apa adanya. Aman dijalankan ulang (station lama diganti).
/// </summary>
public static class FittingRoomBuilder {
    private const string MenuPath = "Go Vista/Interior/Pasang Ruang Ganti";
    private const string RootName = "FittingRoomStations";
    private const string BoothParentName = "FittingRooms";
    private const string DoorWallName = "SM_Bld_Base_Wall_Door_01";
    private const string CurtainPrefix = "SM_Bld_Door_Curtain";
    private const string MirrorName = "SM_Prop_Clothes_Mirror_01";
    // Cermin di sisi luar dinding bilik (menghadap toko): tujuan avatar berjalan setelah editor ditutup.
    private const string OutsideMirrorName = "SM_Prop_Barber_Mirror_02";
    private const float OutsideMirrorMaxDistance = 3f;   // dari tengah dinding depan bilik
    private const float ExitDoorClearance = 0.5f;        // titik keluar pintu, di luar dinding depan
    private const float MirrorStandDistance = 0.7f;      // berdiri sejauh ini di depan cermin luar
    private const string ActionPath = "Assets/NPCAction/ChangeClothes.asset";
    private const string ShirtIconPath = "Assets/Resources/UI/Icons/icon-shirt.png";
    private const string HintArrowMaterialPath = "Assets/Art/Entrance/FittingHintArrow.mat";

    // Tata letak (meter). Kamera ±1,2 m tinggi seperti panggung CharacterScene.
    private const float SubjectDepth = 1.1f;     // dari dinding depan ke titik berdiri (tengah bilik)
    private const float CameraDistance = 4.3f;   // dari titik berdiri ke kamera (sudah lewat meja & rak toko)
    private const float CameraHeight = 1.22f;
    private const float CameraPitch = 4f;   // sumbu kamera kena ±0,9 m (tengah badan), lalu digeser ke tengah area
    private const float FieldOfView = 33f;
    private const float RingSize = 1.1f;
    private const float GlowHeight = 0.9f;
    private const int GlowSegments = 12;
    private const float PromptHeight = 2.05f;
    private static readonly Vector3 TriggerSize = new Vector3(1.3f, 2.1f, 1.3f);

    [MenuItem(MenuPath)]
    private static void Run() {
        Scene scene = SceneManager.GetSceneByName(VenueMap.ClothingShopSceneName);
        if (!scene.IsValid() || !scene.isLoaded) {
            EditorUtility.DisplayDialog("Ruang Ganti", $"Buka scene {VenueMap.ClothingShopSceneName} dulu.", "OK");
            return;
        }
        List<Booth> booths = FindBooths(scene);
        if (booths.Count == 0) {
            Debug.LogWarning($"[FittingRoom] Tidak ada bilik ({DoorWallName} di bawah {BoothParentName}) di {scene.name}.");
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == RootName) Object.DestroyImmediate(root);
        }
        var stations = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(stations, scene);

        InteractionAction action = EnsureAction();
        var prompt = AssetDatabase.LoadAssetAtPath<InteractPromptWorld>(WorldPromptPrefabs.PromptPath);
        Material ring = BuildingEntranceArt.RingMaterial();
        Material glow = BuildingEntranceArt.GlowMaterial();
        for (int i = 0; i < booths.Count; i++) Place(stations.transform, booths[i], i + 1, action, prompt, ring, glow);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FittingRoom] {booths.Count} ruang ganti dipasang di {scene.name}.");
    }

    // ---------------------------------------------------------------- Bilik

    private sealed class Booth {
        public Renderer Wall;
        public readonly List<Renderer> Hidden = new List<Renderer>();
        public Vector3 Front;   // tengah dinding depan, di lantai
        public Vector3 Inward;  // arah ke dalam bilik (datar)
        public Renderer OutsideMirror; // cermin luar terdekat, boleh null
    }

    private static List<Booth> FindBooths(Scene scene) {
        var walls = new List<Renderer>();
        var curtains = new List<Renderer>();
        var mirrors = new List<Renderer>();
        var outsideMirrors = new List<Renderer>();
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) {
                Transform t = renderer.transform;
                bool inBooths = t.parent && t.parent.name == BoothParentName;
                if (inBooths && t.name == DoorWallName) walls.Add(renderer);
                else if (inBooths && t.name.StartsWith(CurtainPrefix)) curtains.Add(renderer);
                else if (t.name == MirrorName) mirrors.Add(renderer);
                else if (t.name == OutsideMirrorName) outsideMirrors.Add(renderer);
            }
        }

        var booths = new List<Booth>();
        foreach (Renderer wall in walls) {
            Bounds bounds = wall.bounds;
            Renderer mirror = Nearest(mirrors, bounds.center);
            if (!mirror) continue;
            var booth = new Booth { Wall = wall };
            booth.Hidden.Add(wall);
            // Normal dinding = sumbu horizontal tertipis, mengarah ke cermin di dalam bilik.
            Vector3 normal = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
            Vector3 toMirror = mirror.bounds.center - bounds.center;
            booth.Inward = Vector3.Dot(toMirror, normal) >= 0f ? normal : -normal;
            booth.Front = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (Renderer curtain in curtains) {
                Vector3 offset = curtain.bounds.center - bounds.center;
                offset.y = 0f;
                if (offset.magnitude > 0.6f) continue;
                booth.Hidden.Add(curtain);
            }
            Renderer outside = Nearest(outsideMirrors, booth.Front);
            if (outside && Vector3.Distance(outside.bounds.center, bounds.center) <= OutsideMirrorMaxDistance &&
                Vector3.Dot(outside.bounds.center - bounds.center, booth.Inward) < 0f)
                booth.OutsideMirror = outside;
            if (IsDoorwayOpen(booth)) booths.Add(booth);
            else Debug.Log($"[FittingRoom] Bilik di {booth.Front} bertirai tertutup, dilewati.");
        }
        booths.Sort((a, b) => a.Front.z.CompareTo(b.Front.z) != 0 ? b.Front.z.CompareTo(a.Front.z) : a.Front.x.CompareTo(b.Front.x));
        return booths;
    }

    /// Pintu bisa dilewati pemain: tidak ada collider (tirai tertutup) di lubang pintu setinggi pinggang/dada.
    private static bool IsDoorwayOpen(Booth booth) {
        Physics.SyncTransforms();
        Vector3 outside = booth.Front - booth.Inward * 0.5f;
        foreach (float height in new[] { 0.6f, 1.2f }) {
            if (Physics.Raycast(outside + Vector3.up * height, booth.Inward, 1f, ~0, QueryTriggerInteraction.Ignore))
                return false;
        }
        return true;
    }

    private static Renderer Nearest(List<Renderer> renderers, Vector3 point) {
        Renderer best = null;
        float bestDistance = 3f; // cermin harus di bilik yang sama (bilik ±2,5 m)
        foreach (Renderer renderer in renderers) {
            Vector3 offset = renderer.bounds.center - point;
            offset.y = 0f;
            if (offset.magnitude >= bestDistance) continue;
            bestDistance = offset.magnitude;
            best = renderer;
        }
        return best;
    }

    // ---------------------------------------------------------------- Station

    private static void Place(Transform parent, Booth booth, int number, InteractionAction action, InteractPromptWorld prompt,
                              Material ringMaterial, Material glowMaterial) {
        Vector3 towardCamera = -booth.Inward;
        var station = new GameObject("Fitting Room " + number);
        station.transform.SetParent(parent, false);
        station.transform.SetPositionAndRotation(booth.Front + booth.Inward * SubjectDepth, Quaternion.LookRotation(towardCamera));

        var subject = new GameObject("Subject").transform;
        subject.SetParent(station.transform, false);

        var viewpoint = new GameObject("Viewpoint").transform;
        viewpoint.SetParent(station.transform, false);
        viewpoint.SetPositionAndRotation(station.transform.position + towardCamera * CameraDistance + Vector3.up * CameraHeight,
                                         Quaternion.LookRotation(booth.Inward) * Quaternion.Euler(CameraPitch, 0f, 0f));

        MeshRenderer ring = Panel(station.transform, "Ring", ringMaterial, new Vector3(0f, 0.02f, 0f),
                                  Quaternion.Euler(90f, 0f, 0f), new Vector3(RingSize, RingSize, 1f));
        var glows = new MeshRenderer[GlowSegments];
        float radius = RingSize * 0.5f;
        float width = 2f * radius * Mathf.Tan(Mathf.PI / GlowSegments) + 0.01f;
        for (int i = 0; i < GlowSegments; i++) {
            float angle = 360f * i / GlowSegments;
            Quaternion around = Quaternion.Euler(0f, angle, 0f);
            glows[i] = Panel(station.transform, "Glow " + (i + 1), glowMaterial,
                             around * new Vector3(0f, 0f, radius) + Vector3.up * GlowHeight * 0.5f, around,
                             new Vector3(width, GlowHeight, 1f));
        }

        var trigger = station.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, TriggerSize.y * 0.5f - 0.05f, 0f);
        trigger.size = TriggerSize;

        var interactable = station.AddComponent<Interactable>();
        var io = new SerializedObject(interactable);
        io.FindProperty("displayName").stringValue = "Ruang Ganti";
        io.FindProperty("action").objectReferenceValue = action;
        io.FindProperty("promptPrefab").objectReferenceValue = prompt;
        io.FindProperty("promptOffset").vector3Value = new Vector3(0f, PromptHeight, 0f);
        io.ApplyModifiedPropertiesWithoutUndo();

        var marker = station.AddComponent<BuildingEntranceMarker>();
        var mo = new SerializedObject(marker);
        mo.FindProperty("doormat").objectReferenceValue = ring;
        SerializedProperty glowList = mo.FindProperty("glow");
        glowList.arraySize = glows.Length;
        for (int i = 0; i < glows.Length; i++) glowList.GetArrayElementAtIndex(i).objectReferenceValue = glows[i];
        mo.ApplyModifiedPropertiesWithoutUndo();

        var fitting = station.AddComponent<FittingRoomStation>();
        var so = new SerializedObject(fitting);
        so.FindProperty("subject").objectReferenceValue = subject;
        so.FindProperty("viewpoint").objectReferenceValue = viewpoint;
        so.FindProperty("fieldOfView").floatValue = FieldOfView;
        // Kolom cahaya ikut disembunyikan supaya tidak menutupi kaki karakter; cincin lantai tetap jadi alas.
        var hiddenRenderers = new List<Renderer>(booth.Hidden);
        hiddenRenderers.AddRange(glows);
        PlaceExitPath(so, station.transform, booth);
        SerializedProperty hidden = so.FindProperty("hideWhileEditing");
        hidden.arraySize = hiddenRenderers.Count;
        for (int i = 0; i < hiddenRenderers.Count; i++) hidden.GetArrayElementAtIndex(i).objectReferenceValue = hiddenRenderers[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        // Petunjuk tutorial setelah perkenalan kasir (Mira): sorot lingkaran + panah sampai pertama kali diinjak.
        var hint = station.AddComponent<FittingRoomHint>();
        var ho = new SerializedObject(hint);
        ho.FindProperty("arrowMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(HintArrowMaterialPath);
        ho.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Jalan keluar setelah editor ditutup: lewat pintu bilik, lalu berdiri menghadap cermin luar.
    private static void PlaceExitPath(SerializedObject so, Transform station, Booth booth) {
        SerializedProperty path = so.FindProperty("exitPath");
        if (!booth.OutsideMirror) {
            path.arraySize = 0;
            Debug.LogWarning($"[FittingRoom] Tidak ada {OutsideMirrorName} di dekat bilik {booth.Front}; tanpa jalan keluar.");
            return;
        }
        Transform mirror = booth.OutsideMirror.transform;
        Vector3 facing = mirror.forward; // sisi cermin menghadap toko
        facing.y = 0f;
        facing.Normalize();
        Vector3 center = booth.OutsideMirror.bounds.center;

        var door = new GameObject("Exit Door").transform;
        door.SetParent(station, false);
        door.SetPositionAndRotation(booth.Front - booth.Inward * ExitDoorClearance, Quaternion.LookRotation(-booth.Inward));

        var stand = new GameObject("Exit Mirror").transform;
        stand.SetParent(station, false);
        stand.SetPositionAndRotation(new Vector3(center.x, booth.Front.y, center.z) + facing * MirrorStandDistance,
                                     Quaternion.LookRotation(-facing));

        path.arraySize = 2;
        path.GetArrayElementAtIndex(0).objectReferenceValue = door;
        path.GetArrayElementAtIndex(1).objectReferenceValue = stand;
    }

    private static MeshRenderer Panel(Transform parent, string name, Material material, Vector3 position,
                                      Quaternion rotation, Vector3 scale) {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static InteractionAction EnsureAction() {
        var action = AssetDatabase.LoadAssetAtPath<InteractionAction>(ActionPath);
        if (action == null) {
            action = ScriptableObject.CreateInstance<InteractionAction>();
            AssetDatabase.CreateAsset(action, ActionPath);
        }
        action.verb = "Ganti Baju";
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(ShirtIconPath);
        action.icon = icon;
        action.hudIcon = icon;
        EditorUtility.SetDirty(action);
        return action;
    }
}

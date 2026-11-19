using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Go Vista > Interior > Pasang Penanda Pintu Gedung
///
/// 1. Gambar aset (ikon pintu, tekstur + material karpet) dan bangun ulang prefab InteractPrompt.
/// 2. Untuk setiap pintu masuk interior di scene yang terbuka (DoorTravelPoint → venue interior, di bawah
///    objek "*_Entrance"): pasang BuildingEntranceMarker + karpet "Doormat", dan geser prompt "Masuk" ke
///    depan pintu. Posisi dihitung dari fasad bangunan (raycast), jadi aman diulang.
///    (Papan melayang "Sign" versi lama dihapus: dari samping papannya masuk ke dalam bangunan.)
internal static class BuildingEntranceBuilder
{
    private const string MenuPath = "Go Vista/Interior/Pasang Penanda Pintu Gedung";
    private const string EnterActionPath = "Assets/NPCAction/EnterBuilding.asset";
    private const float ProbeStep = 0.1f;
    private const float ProbeTop = 5f;
    private const float GlowHeight = 1.1f;
    private const float MinMatDepth = 0.8f;
    private const float MaxMatDepth = 1.6f;

    [MenuItem(MenuPath)]
    private static void Run()
    {
        Sprite doorIcon = BuildingEntranceArt.DoorIcon();
        Material doormat = BuildingEntranceArt.DoormatMaterial();
        Material glow = BuildingEntranceArt.GlowMaterial();
        WorldPromptPrefabs.RebuildInteractPrompt();
        UseDoorIconForEnterAction(doorIcon);

        var touched = new HashSet<Scene>();
        int count = 0;
        foreach (DoorTravelPoint door in Object.FindObjectsByType<DoorTravelPoint>(FindObjectsInactive.Include))
        {
            if (!IsInteriorEntrance(door, out Transform entrance)) continue;
            Place(entrance, door, doormat, glow);
            touched.Add(entrance.gameObject.scene);
            count++;
        }
        foreach (Scene scene in touched)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[BuildingEntranceBuilder] Prompt diperbarui, {count} pintu gedung dipasangi karpet penanda.");
    }

    private static bool IsInteriorEntrance(DoorTravelPoint door, out Transform entrance)
    {
        entrance = door.transform.parent;
        var target = (VenueId)new SerializedObject(door).FindProperty("targetVenue").intValue;
        return entrance != null && entrance.name.EndsWith("_Entrance") && VenueMap.IsInterior(target);
    }

    private static void UseDoorIconForEnterAction(Sprite doorIcon)
    {
        var action = AssetDatabase.LoadAssetAtPath<InteractionAction>(EnterActionPath);
        if (action == null) return;
        var so = new SerializedObject(action);
        so.FindProperty("icon").objectReferenceValue = doorIcon;
        so.FindProperty("hudIcon").objectReferenceValue = doorIcon;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ penempatan

    private struct Facade
    {
        public float GroundDepth;   // posisi fasad (lokal +z = keluar) setinggi pinggang
        public float UpperDepth;    // posisi fasad di atas pintu
        public float DoorTop;       // tinggi atas lubang pintu dari tanah
    }

    private static void Place(Transform entrance, DoorTravelPoint door, Material doormat, Material glow)
    {
        foreach (string child in new[] { "Sign", "Doormat" })
        {
            Transform old = entrance.Find(child);
            if (old) Object.DestroyImmediate(old.gameObject);
        }

        Facade facade = Probe(entrance);
        float front = Mathf.Max(facade.GroundDepth, facade.UpperDepth);
        var interactable = door.GetComponent<Interactable>();

        var box = door.GetComponent<BoxCollider>();
        float width = (box ? box.size.x : 2f) + 0.2f;
        FlatRun(entrance, facade.GroundDepth, out float start, out float depth, out float floorY);

        var root = new GameObject("Doormat").transform;
        root.SetParent(entrance, false);
        root.localPosition = new Vector3(0f, floorY + 0.03f, start + depth * 0.5f);

        MeshRenderer mat = Panel(root, "Mat", doormat, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                                 new Vector3(width, depth, 1f));   // rebah, atas tekstur = keluar (+z)
        Vector3 glowSize = new Vector3(width, GlowHeight, 1f);
        Vector3 sideSize = new Vector3(depth, GlowHeight, 1f);
        float up = GlowHeight * 0.5f;
        var glows = new[]
        {
            Panel(root, "GlowFront", glow, new Vector3(0f, up, depth * 0.5f), Quaternion.identity, glowSize),
            Panel(root, "GlowLeft", glow, new Vector3(-width * 0.5f, up, 0f), Quaternion.Euler(0f, 90f, 0f), sideSize),
            Panel(root, "GlowRight", glow, new Vector3(width * 0.5f, up, 0f), Quaternion.Euler(0f, 90f, 0f), sideSize),
        };

        // Selalu pasang ulang supaya nilai default terbaru (warna/alpha) ikut terpakai.
        var oldMarker = entrance.GetComponent<BuildingEntranceMarker>();
        if (oldMarker) Object.DestroyImmediate(oldMarker);
        var marker = entrance.gameObject.AddComponent<BuildingEntranceMarker>();
        var so = new SerializedObject(marker);
        so.FindProperty("doormat").objectReferenceValue = mat;
        SerializedProperty glowList = so.FindProperty("glow");
        glowList.arraySize = glows.Length;
        for (int i = 0; i < glows.Length; i++) glowList.GetArrayElementAtIndex(i).objectReferenceValue = glows[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        // Prompt "Masuk": tepat di atas lubang pintu, sedikit keluar dari fasad (prompt juga digambar di atas
        // geometri, jadi tidak akan tertutup lagi).
        Vector3 promptPoint = entrance.TransformPoint(new Vector3(0f, facade.DoorTop + 0.15f, front + 0.4f));
        if (interactable)
        {
            var io = new SerializedObject(interactable);
            io.FindProperty("promptOffset").vector3Value = promptPoint - door.transform.position;
            io.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static MeshRenderer Panel(Transform parent, string name, Material material, Vector3 position,
                                      Quaternion rotation, Vector3 scale)
    {
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

    /// Lantai datar di depan pintu: mulai dari fasad, maju selama tinggi tanah tetap (berhenti di lis/tangga),
    /// supaya karpet tidak terpotong tepian trotoar. Kedalaman dijepit MinMatDepth..MaxMatDepth.
    private static void FlatRun(Transform entrance, float facadeDepth, out float start, out float depth, out float floorY)
    {
        start = facadeDepth + 0.05f;
        floorY = GroundHeight(entrance, start + 0.05f, 0f);
        float end = start;
        for (float z = start + 0.05f; z <= start + MaxMatDepth; z += 0.05f)
        {
            if (Mathf.Abs(GroundHeight(entrance, z, float.MaxValue) - floorY) > 0.03f) break;
            end = z;
        }
        depth = Mathf.Clamp(end - start, MinMatDepth, MaxMatDepth);
    }

    private static float GroundHeight(Transform entrance, float z, float fallback)
    {
        Vector3 origin = entrance.TransformPoint(new Vector3(0f, 2f, z));
        return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)
            ? entrance.InverseTransformPoint(hit.point).y
            : fallback;
    }

    /// Raycast horizontal dari arah jalan ke fasad di beberapa ketinggian untuk mencari lubang pintu.
    private static Facade Probe(Transform entrance)
    {
        float ground = Depth(entrance, 0.6f, -0.2f);
        var facade = new Facade { GroundDepth = ground, UpperDepth = ground, DoorTop = 2.6f };
        for (float h = 1.2f; h <= ProbeTop; h += ProbeStep)
        {
            float depth = Depth(entrance, h, ground);
            if (depth <= ground + 0.12f) continue;
            facade.DoorTop = h;
            facade.UpperDepth = Depth(entrance, h + 0.3f, depth);
            break;
        }
        return facade;
    }

    private static float Depth(Transform entrance, float height, float fallback)
    {
        Vector3 origin = entrance.position + Vector3.up * height + entrance.forward * 6f;
        RaycastHit[] hits = Physics.RaycastAll(origin, -entrance.forward, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (RaycastHit hit in hits)
            if (!hit.collider.transform.IsChildOf(entrance)) best = Mathf.Min(best, hit.distance);
        return best == float.MaxValue ? fallback : 6f - best;
    }
}

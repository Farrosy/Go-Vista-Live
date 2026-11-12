using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Membangun Tur Kota Go Vista di WorldScene: maskot Vio & Vira di Central Plaza (pemberi quest), maskot
/// pemandu yang muncul di depan gedung, area lingkaran + beacon di tiap gedung, dialog, dan QuestData.
///
/// Aman dijalankan ulang: data dan prefab dibuat ulang, tapi posisi objek yang sudah ada di scene
/// (kalau sudah digeser manual) TIDAK diubah. Hapus objeknya dulu kalau ingin posisinya dihitung ulang.
/// </summary>
public static partial class CityTourBuilder {
    private const string ScenePath = "Assets/Scenes/WorldScene.unity";
    private const string RootName = "City Tour";
    private const string SpawnPointName = "PlayerSpawnPoint";
    private const string ReferenceNpcPrefab = "Assets/Prefab/NPC_Kota.prefab";
    private const string TestQuestPath = "Assets/Quests/Quest1.asset";

    private const float PlazaMascotDistance = 6f;   // dari titik spawn, ke arah depan (kalau air mancur tidak ada)
    private const float PlazaMascotGapFromFountain = 2.5f;
    private const float PlazaMascotAngleFromSpawn = 60f;
    private const string PlazaAnchorTag = "Central Plaza";
    // Typo nama air mancur dari Editor (2026-11-12); builder membetulkannya jadi "(Central Plaza)".
    private const string PlazaAnchorTypoTag = "central palza";
    private const float PlazaAreaRadius = 14f;
    private const float TalkRadius = 2.4f;
    private const float RingGapFromDoor = 0.5f;     // jarak tepi lingkaran ke pintu
    private const float MascotSpotFromDoor = 2f;    // maskot muncul 2 m di depan pintu
    private const float DoorRayHeight = 1.5f;
    private const float StopTriggerHeight = 8f;

    [MenuItem("Go Vista/Quest/Buat Tur Kota")]
    public static void Build() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildWithoutPrompt();
    }

    /// Versi tanpa dialog (untuk otomasi): perubahan scene yang belum disimpan di Editor DIBUANG.
    public static void BuildWithoutPrompt() {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();
        Transform spawn = FindByName(scene, SpawnPointName);
        if (spawn == null) {
            Debug.LogError($"[Tour] '{SpawnPointName}' tidak ada di {ScenePath}.");
            return;
        }

        var buildings = new Dictionary<string, Transform>();
        foreach (CityTourContent.Stop stop in CityTourContent.Stops) {
            Transform building = FindBuilding(scene, stop.BuildingTag);
            if (building == null) {
                Debug.LogError($"[Tour] Gedung '({stop.BuildingTag})' tidak ditemukan di WorldScene.");
                return;
            }
            buildings[stop.ObjectiveId] = building;
        }

        List<CityTourContent.Stop> route = PlanRoute(spawn.position, buildings);
        TourData data = BuildData(route);
        TourPrefabs prefabs = BuildPrefabs();
        ConfigureDialogueBox();
        QuestDatabaseSync.Sync();

        Transform root = FindByName(scene, RootName) ?? new GameObject(RootName).transform;
        BuildPlaza(root, spawn, FindPlazaAnchor(scene), data, prefabs);
        BuildGuide(root, prefabs);
        foreach (CityTourContent.Stop stop in route)
            BuildStop(root, stop, buildings[stop.ObjectiveId], data, prefabs);
        HideTestQuest(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Tour] Tur Kota selesai dibuat. Rute: Central Plaza → " +
                  string.Join(" → ", route.Select(s => s.BuildingTag)));
    }

    // ------------------------------------------------------------ Rute

    /// Gedung tengah diurutkan tetangga terdekat dari Central Plaza; gedung finale (Vista Palace) selalu terakhir.
    private static List<CityTourContent.Stop> PlanRoute(Vector3 start, Dictionary<string, Transform> buildings) {
        var remaining = CityTourContent.Stops.Where(s => !s.IsFinale).ToList();
        var route = new List<CityTourContent.Stop>();
        Vector3 current = start;
        while (remaining.Count > 0) {
            CityTourContent.Stop next = remaining.OrderBy(s => FlatDistance(current, BuildingCenter(buildings[s.ObjectiveId]))).First();
            route.Add(next);
            remaining.Remove(next);
            current = BuildingCenter(buildings[next.ObjectiveId]);
        }
        route.AddRange(CityTourContent.Stops.Where(s => s.IsFinale));
        return route;
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private static Vector3 BuildingCenter(Transform building) => RendererBounds(building.gameObject).center;

    // ------------------------------------------------------------ Central Plaza

    private static void BuildPlaza(Transform root, Transform spawn, Transform fountain, TourData data, TourPrefabs prefabs) {
        Transform plaza = FindChild(root, "Central Plaza");
        if (plaza == null) {
            Vector3 position = PlazaMascotPosition(spawn, fountain);
            plaza = new GameObject("Central Plaza").transform;
            plaza.SetParent(root, false);
            plaza.SetPositionAndRotation(position, Quaternion.LookRotation(Flat(spawn.position - position)));
        }

        Transform mascots = EnsurePrefabChild(plaza, "Mascots", prefabs.Duo);
        var duo = mascots.GetComponent<MascotDuo>();
        var talkArea = Ensure<CapsuleCollider>(mascots.gameObject);
        talkArea.isTrigger = true;
        talkArea.radius = TalkRadius;
        talkArea.height = 2f;
        talkArea.center = new Vector3(0f, 0.9f, 0f);

        var trigger = Ensure<DialogueTrigger>(mascots.gameObject);
        var triggerData = new SerializedObject(trigger);
        SetArray(triggerData.FindProperty("stages"), data.Intro);
        triggerData.FindProperty("repeatDialogue").objectReferenceValue = data.AfterTour;
        SerializedProperty questDialogues = triggerData.FindProperty("questDialogues");
        questDialogues.arraySize = 1;
        SerializedProperty reminder = questDialogues.GetArrayElementAtIndex(0);
        reminder.FindPropertyRelative("quest").objectReferenceValue = data.Quest;
        reminder.FindPropertyRelative("when").enumValueIndex = (int)QuestDialogueCondition.QuestActive;
        reminder.FindPropertyRelative("dialogue").objectReferenceValue = data.Reminder;
        triggerData.FindProperty("focusPoint").objectReferenceValue = duo.FocusPoint;
        triggerData.ApplyModifiedPropertiesWithoutUndo();

        SetupInteractable(mascots.gameObject, trigger);

        var giver = Ensure<QuestGiver>(mascots.gameObject);
        var giverData = new SerializedObject(giver);
        SetArray(giverData.FindProperty("quests"), data.Quest);
        giverData.FindProperty("mode").enumValueIndex = (int)QuestGiver.GiveMode.AfterDialogue;
        giverData.FindProperty("giveAfterDialogue").objectReferenceValue = data.Intro;
        giverData.ApplyModifiedPropertiesWithoutUndo();

        BuildIndicator(mascots, data.Quest);

        Transform area = FindChild(plaza, "Plaza Area");
        if (area == null) {
            area = new GameObject("Plaza Area").transform;
            area.SetParent(plaza, false);
            // Di tengah antara spawn dan maskot, supaya keduanya tercakup.
            area.position = Vector3.Lerp(plaza.position, spawn.position, 0.5f);
        }
        var areaCollider = Ensure<SphereCollider>(area.gameObject);
        areaCollider.isTrigger = true;
        areaCollider.radius = PlazaAreaRadius;
        var location = Ensure<QuestLocation>(area.gameObject);
        SetString(location, "locationId", CityTourContent.PlazaLocationId);
    }

    /// Maskot berdiri di samping air mancur Central Plaza (bukan di dalamnya), terlihat dari titik spawn.
    /// Tanpa air mancur: beberapa meter di depan titik spawn.
    private static Vector3 PlazaMascotPosition(Transform spawn, Transform fountain) {
        if (fountain == null) return GroundPoint(spawn.position + Flat(spawn.forward) * PlazaMascotDistance);
        Bounds bounds = RendererBounds(fountain.gameObject);
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        Vector3 side = Quaternion.Euler(0f, PlazaMascotAngleFromSpawn, 0f) * Flat(spawn.position - bounds.center);
        return GroundPoint(bounds.center + side * (radius + PlazaMascotGapFromFountain));
    }

    /// Objek penanda Central Plaza (air mancur). Nama yang typo dibetulkan jadi "<nama> (Central Plaza)".
    private static Transform FindPlazaAnchor(Scene scene) {
        Transform anchor = FindBuilding(scene, PlazaAnchorTag);
        if (anchor != null) return anchor;
        anchor = FindBuilding(scene, PlazaAnchorTypoTag);
        if (anchor == null) return null;
        string baseName = anchor.name.Substring(0, anchor.name.Length - $"({PlazaAnchorTypoTag})".Length).TrimEnd();
        anchor.name = $"{baseName} ({PlazaAnchorTag})";
        Debug.Log($"[Tour] Nama air mancur dibetulkan jadi '{anchor.name}'.", anchor);
        return anchor;
    }

    /// Salin pengaturan prompt dari NPC_Kota supaya tombol bicara tampil sama dengan NPC lain.
    private static void SetupInteractable(GameObject target, DialogueTrigger trigger) {
        var interactable = Ensure<Interactable>(target);
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceNpcPrefab)?.GetComponent<Interactable>();
        if (reference != null) {
            var from = new SerializedObject(reference);
            var to = new SerializedObject(interactable);
            to.FindProperty("action").objectReferenceValue = from.FindProperty("action").objectReferenceValue;
            to.FindProperty("promptPrefab").objectReferenceValue = from.FindProperty("promptPrefab").objectReferenceValue;
            to.FindProperty("promptOffset").vector3Value = new Vector3(0f, MascotHeight + 0.4f, 0f);
            to.ApplyModifiedPropertiesWithoutUndo();
        } else {
            Debug.LogWarning("[Tour] NPC_Kota tidak ditemukan; isi Action dan Prompt Prefab di Interactable maskot secara manual.");
        }
        interactable.displayName = "Vio & Vira";
        while (interactable.onInteract.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(interactable.onInteract, 0);
        UnityEventTools.AddVoidPersistentListener(interactable.onInteract, trigger.StartDialogue);
        EditorUtility.SetDirty(interactable);
    }

    private static void BuildIndicator(Transform mascots, QuestData quest) {
        Transform existing = FindChild(mascots, "Quest Mark");
        // TextMeshPro butuh RectTransform; menambahkannya ke objek ber-Transform biasa menghancurkan Transform lama.
        GameObject markObject = existing != null ? existing.gameObject : new GameObject("Quest Mark", typeof(RectTransform));
        var text = Ensure<TMPro.TextMeshPro>(markObject);
        Transform mark = text.transform;
        mark.SetParent(mascots, false);
        // Di atas label prompt "Vio & Vira" (MascotHeight + 0.4) supaya tidak saling menimpa.
        mark.localPosition = new Vector3(0f, MascotHeight + 1.05f, 0f);
        text.text = "!";
        text.fontSize = 8f;
        text.fontStyle = TMPro.FontStyles.Bold;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Gold;
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/Fredoka-Bold SDF.asset");
        if (font != null) text.font = font;
        // Material bersama (bayangan gelap) dari HUD. Jangan set outline lewat properti teks: di edit mode itu
        // membuat salinan material yang ikut tersimpan di scene.
        var shadow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Fredoka-Bold SDF Underlay.mat");
        if (shadow != null) text.fontSharedMaterial = shadow;
        text.rectTransform.sizeDelta = new Vector2(2f, 2f);
        Ensure<Billboard>(mark.gameObject);

        var indicator = Ensure<QuestAvailableIndicator>(mascots.gameObject);
        var serialized = new SerializedObject(indicator);
        serialized.FindProperty("quest").objectReferenceValue = quest;
        serialized.FindProperty("indicator").objectReferenceValue = mark.gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------ Pemandu & gedung

    private static void BuildGuide(Transform root, TourPrefabs prefabs) {
        Transform guide = FindChild(root, "Tour Guide");
        if (guide == null) {
            guide = new GameObject("Tour Guide").transform;
            guide.SetParent(root, false);
        }
        Transform duo = EnsurePrefabChild(guide, "Mascots", prefabs.Duo);
        Transform poof = EnsurePrefabChild(guide, "Poof", prefabs.Poof);
        var component = Ensure<CityTourGuide>(guide.gameObject);
        var serialized = new SerializedObject(component);
        serialized.FindProperty("duo").objectReferenceValue = duo.GetComponent<MascotDuo>();
        serialized.FindProperty("poof").objectReferenceValue = poof.GetComponent<ParticleSystem>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildStop(Transform root, CityTourContent.Stop stop, Transform building,
        TourData data, TourPrefabs prefabs) {
        string name = "Stop - " + stop.BuildingTag;
        Transform stopTransform = FindChild(root, name);
        if (stopTransform == null) {
            // Lingkaran tepat di depan pintu: tepinya sekitar 0,5 m dari pintu, maskot muncul di antara pintu
            // dan tengah lingkaran sambil menghadap keluar (ke arah pemain datang).
            Vector3 door = DoorPoint(building, stop, out Vector3 outward);
            stopTransform = new GameObject(name).transform;
            stopTransform.SetParent(root, false);
            stopTransform.position = GroundPoint(door + outward * (RingRadius + RingGapFromDoor));
            var newSpot = new GameObject("Mascot Spot").transform;
            newSpot.SetParent(stopTransform, false);
            newSpot.position = GroundPoint(door + outward * MascotSpotFromDoor);
            newSpot.rotation = Quaternion.LookRotation(outward);
        }
        Transform spot = FindChild(stopTransform, "Mascot Spot");

        Transform marker = EnsurePrefabChild(stopTransform, "Marker", prefabs.Marker);
        var trigger = Ensure<CapsuleCollider>(stopTransform.gameObject);
        trigger.isTrigger = true;
        trigger.radius = RingRadius;
        trigger.height = StopTriggerHeight;
        trigger.center = new Vector3(0f, StopTriggerHeight * 0.5f - 1f, 0f);

        var component = Ensure<CityTourStop>(stopTransform.gameObject);
        var serialized = new SerializedObject(component);
        serialized.FindProperty("quest").objectReferenceValue = data.Quest;
        serialized.FindProperty("objectiveId").stringValue = stop.ObjectiveId;
        serialized.FindProperty("dialogue").objectReferenceValue = data.StopDialogues[stop.ObjectiveId];
        serialized.FindProperty("mascotSpot").objectReferenceValue = spot;
        serialized.FindProperty("marker").objectReferenceValue = marker.gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Titik di fasad tepat di pintu utama (sisi +Z lokal gedung, atau −Z kalau DoorOnBack; digeser DoorLocalX), dicari dengan raycast
    /// ke mesh gedung supaya tetap pas walau bentuk gedung tidak kotak.
    private static Vector3 DoorPoint(Transform building, CityTourContent.Stop stop, out Vector3 outward) {
        Bounds bounds = RendererBounds(building.gameObject);
        outward = Flat(stop.DoorOnBack ? -building.forward : building.forward);
        Vector3 right = Flat(building.right);
        Vector3 door = new Vector3(bounds.center.x, bounds.min.y + DoorRayHeight, bounds.center.z) + right * stop.DoorLocalX;
        Vector3 origin = door + outward * (bounds.extents.magnitude + 10f);
        float distance = bounds.extents.magnitude * 2f + 20f;
        if (RaycastBuilding(building, origin, -outward, distance, out Vector3 facade)) return facade;

        // Sebagian gedung tidak punya collider: pasang MeshCollider sementara hanya untuk raycast ini.
        var temporary = new List<MeshCollider>();
        foreach (MeshFilter filter in building.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh != null && filter.GetComponent<Collider>() == null) {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                temporary.Add(collider);
            }
        try {
            Physics.SyncTransforms();
            if (RaycastBuilding(building, origin, -outward, distance, out facade)) return facade;
        } finally {
            foreach (MeshCollider collider in temporary) Object.DestroyImmediate(collider);
        }
        Debug.LogWarning($"[Tour] Fasad '{stop.BuildingTag}' tidak kena raycast; pakai batas gedung.", building);
        return new Vector3(door.x, bounds.min.y, door.z) + outward * Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z)));
    }

    private static bool RaycastBuilding(Transform building, Vector3 origin, Vector3 direction, float distance, out Vector3 point) {
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore)
                     .OrderBy(h => h.distance))
            if (hit.transform.IsChildOf(building)) {
                point = new Vector3(hit.point.x, RendererBounds(building.gameObject).min.y, hit.point.z);
                return true;
            }
        point = default;
        return false;
    }

    // ------------------------------------------------------------ Quest testing (Q1 Twin/Joseph)

    /// Q1 hanya untuk testing: QuestGiver-nya dimatikan (NPC Twin dan Joseph tetap ada). Nyalakan lagi
    /// komponen QuestGiver di NPC Twin kalau quest ini dibutuhkan.
    private static void HideTestQuest(Scene scene) {
        var testQuest = AssetDatabase.LoadAssetAtPath<QuestData>(TestQuestPath);
        if (testQuest == null) return;
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        foreach (QuestGiver giver in rootObject.GetComponentsInChildren<QuestGiver>(true)) {
            var quests = new SerializedObject(giver).FindProperty("quests");
            for (int i = 0; i < quests.arraySize; i++) {
                if (quests.GetArrayElementAtIndex(i).objectReferenceValue != testQuest) continue;
                giver.enabled = false;
                EditorUtility.SetDirty(giver);
                Debug.Log($"[Tour] QuestGiver Q1 di '{giver.name}' dimatikan (quest testing).", giver);
            }
        }
    }

    // ------------------------------------------------------------ Helper

    private static Transform FindBuilding(Scene scene, string tag) {
        string suffix = $"({tag})";
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        foreach (Transform t in rootObject.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }

    private static Transform FindByName(Scene scene, string name) {
        foreach (GameObject rootObject in scene.GetRootGameObjects()) {
            if (rootObject.name == name) return rootObject.transform;
            foreach (Transform t in rootObject.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        }
        return null;
    }

    private static Transform FindChild(Transform parent, string name) {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    /// Instance prefab sebagai child (dibuat kalau belum ada). Instance lama ikut perubahan prefab terbaru.
    private static Transform EnsurePrefabChild(Transform parent, string name, GameObject prefab) {
        Transform existing = FindChild(parent, name);
        if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab) return existing;
        Vector3 position = existing != null ? existing.localPosition : Vector3.zero;
        Quaternion rotation = existing != null ? existing.localRotation : Quaternion.identity;
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        instance.transform.SetLocalPositionAndRotation(position, rotation);
        return instance.transform;
    }

    // Jangan pakai '??': di Editor GetComponent mengembalikan "fake null" yang tidak dianggap null oleh '??'.
    private static T Ensure<T>(GameObject target) where T : Component =>
        target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();

    private static void SetArray(SerializedProperty array, Object value) {
        array.arraySize = 1;
        array.GetArrayElementAtIndex(0).objectReferenceValue = value;
    }

    private static void SetString(Object target, string field, string value) {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    // Mulai sedikit di atas titik (bukan dari langit), supaya kanopi pintu atau atap tidak dianggap tanah.
    private const float GroundProbeHeight = 2f;

    private static Vector3 GroundPoint(Vector3 point) {
        var origin = new Vector3(point.x, point.y + GroundProbeHeight, point.z);
        return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbeHeight + 10f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point
            : point;
    }
}

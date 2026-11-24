using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Quest utama #2 "Tampil Beda": Go Vista > Quest > Buat Tampil Beda. Aman dijalankan ulang (posisi objek yang sudah
/// ada tidak ditimpa). "Buat Tur Kota" juga memanggil sambungan WorldScene di sini, supaya membangun ulang tur tidak
/// menghapus tawaran Tampil Beda.
///  - Asset: QuestData + dialog tawaran (Vista Palace, Central Plaza), pengingat, dan pujian Mira.
///  - WorldScene: tawaran di akhir dialog Vista Palace, tawaran cadangan + pengingat + tanda ! + pin quest di Central
///    Plaza, area tujuan di depan pintu Clothing Shop.
///  - Interior_ClothingShop: area tujuan di pintu dalam, titik GPS ke Mira dan bilik, tujuan "ganti gaya" di station.
///  - NPC_Mira.prefab: dialog pujian selama tujuan "tunjukkan-mira" bisa dikerjakan.
public static partial class CityTourBuilder {
    private const string TampilBedaQuestPath = "Assets/Quests/TampilBeda.asset";
    private const string TampilBedaDialogueFolder = "Assets/DialougeData/TampilBeda";
    private const string ClothingShopScenePath = "Assets/Scenes/Interiors/Interior_ClothingShop.unity";
    private const string MiraPrefabPath = "Assets/Characters/NPC/Mira/NPC_Mira.prefab";
    private const string MiraIntroPath = "Assets/DialougeData/Mira/mira-intro.asset";
    private const string ClothingDoorParent = "ClothingShop_Entrance";
    private const string TampilBedaRootName = "Tampil Beda";
    private const string FinaleStopName = "Stop - Vista Palace";
    private const string InteriorSpawnName = "Spawn_InteriorEntrance";
    private const float ShopAreaRadius = 3f;
    // Checkpoint di depan meja kasir: ukuran sama dengan lingkaran ruang ganti (FittingRoomBuilder).
    private const float MiraCheckpointSize = 1.1f;
    private const float MiraCheckpointGlowHeight = 0.9f;
    private const int MiraCheckpointSegments = 12;
    private const float MiraCheckpointDistance = 2f; // dari Mira ke arah hadapnya (depan meja kasir)
    private static readonly Color GoldActive = new Color32(255, 236, 150, 255);
    // Cari titik lantai kosong (tanpa meja/rak) di sekitar titik depan Mira: jari-jari pencarian, langkah, tinggi cek.
    private const float ClearSearchRadius = 1.5f;
    private const float ClearSearchStep = 0.25f;
    private const float ClearCheckHeight = 1.2f;

    private sealed class TampilBedaData {
        public QuestData Quest;
        public DialogueData PalaceOffer, PlazaOffer, Reminder, ShowMira;
    }

    [MenuItem("Go Vista/Quest/Buat Tampil Beda")]
    public static void BuildTampilBeda() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildTampilBedaWithoutPrompt();
    }

    /// Versi tanpa dialog (untuk otomasi): perubahan scene yang belum disimpan di Editor DIBUANG.
    public static void BuildTampilBedaWithoutPrompt() {
        if (BuildTampilBedaData() == null) return;
        QuestDatabaseSync.Sync();

        // Membuka scene (Single) membongkar asset yang tidak dipakai scene itu, jadi referensi asset yang dimuat
        // sebelumnya jadi "null palsu" dan tersimpan kosong. Muat ulang dari disk setelah tiap scene dibuka.
        Scene world = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!WireTampilBedaWorld(world, LoadTampilBeda())) return;
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);

        Scene shop = EditorSceneManager.OpenScene(ClothingShopScenePath, OpenSceneMode.Single);
        if (!WireTampilBedaShop(shop, LoadTampilBeda().Quest)) return;
        EditorSceneManager.MarkSceneDirty(shop);
        EditorSceneManager.SaveScene(shop);

        WireMiraPrefab(LoadTampilBeda());
        AssetDatabase.SaveAssets();
        Debug.Log("[Tampil Beda] Quest utama #2 selesai dibuat (WorldScene, Interior_ClothingShop, NPC_Mira).");
    }

    // ------------------------------------------------------------ Asset

    private static TampilBedaData BuildTampilBedaData() {
        var miraIntro = AssetDatabase.LoadAssetAtPath<DialogueData>(MiraIntroPath);
        var tour = AssetDatabase.LoadAssetAtPath<QuestData>(QuestPath);
        if (miraIntro == null || tour == null) {
            Debug.LogError($"[Tampil Beda] Butuh {MiraIntroPath} dan {QuestPath} (jalankan Buat Tur Kota dulu).");
            return null;
        }
        System.IO.Directory.CreateDirectory(TampilBedaDialogueFolder);
        var data = new TampilBedaData {
            PalaceOffer = BuildOfferDialogue(TampilBedaContent.PalaceOfferDialogueId, TampilBedaContent.PalaceOfferBeforeQuestion,
                TampilBedaContent.PalaceOfferQuestion, TampilBedaContent.PalaceYesReply, TampilBedaContent.PalaceLaterReply),
            PlazaOffer = BuildOfferDialogue(TampilBedaContent.PlazaOfferDialogueId, TampilBedaContent.PlazaOfferBeforeQuestion,
                TampilBedaContent.PlazaOfferQuestion, TampilBedaContent.PlazaYesReply, TampilBedaContent.PlazaLaterReply),
            Reminder = BuildTampilBedaDialogue(TampilBedaContent.ReminderDialogueId, TampilBedaContent.Reminder),
            ShowMira = BuildMiraDialogue(miraIntro),
        };

        QuestData quest = LoadOrCreate<QuestData>(TampilBedaQuestPath);
        quest.questId = TampilBedaContent.QuestId;
        quest.title = TampilBedaContent.QuestTitle;
        quest.description = TampilBedaContent.QuestDescription;
        quest.isMainQuest = true;
        quest.requiredQuest = tour;
        quest.sequentialObjectives = true;
        quest.rewardCoins = TampilBedaContent.RewardCoins;
        quest.objectives = new[] {
            new QuestObjective {
                objectiveId = TampilBedaContent.ShopObjectiveId, description = TampilBedaContent.ShopObjectiveText,
                type = QuestObjectiveType.GoToLocation, locationId = TampilBedaContent.ShopLocationId,
            },
            new QuestObjective {
                objectiveId = TampilBedaContent.MiraObjectiveId, description = TampilBedaContent.MiraObjectiveText,
                type = QuestObjectiveType.FinishDialogue, dialogue = miraIntro, completeIfDialogueDone = true,
            },
            new QuestObjective {
                objectiveId = TampilBedaContent.StyleObjectiveId, description = TampilBedaContent.StyleObjectiveText,
                type = QuestObjectiveType.Manual,
            },
            new QuestObjective {
                objectiveId = TampilBedaContent.ShowObjectiveId, description = TampilBedaContent.ShowObjectiveText,
                type = QuestObjectiveType.FinishDialogue, dialogue = data.ShowMira,
            },
        };
        EditorUtility.SetDirty(quest);
        data.Quest = quest;
        AssetDatabase.SaveAssets();
        return data;
    }

    /// Dialog Vio & Vira dengan pertanyaan "Ayo!" / "Nanti saja" (tanya lagi nanti) di akhir.
    private static DialogueData BuildOfferDialogue(string id, IEnumerable<CityTourContent.Line> before,
        CityTourContent.Line question, CityTourContent.Line yesReply, CityTourContent.Line laterReply) {
        DialogueData dialogue = LoadOrCreate<DialogueData>($"{TampilBedaDialogueFolder}/{id}.asset");
        var lines = before.Select(MainLine).ToList();
        DialogueLine ask = MainLine(question);
        ask.isQuestion = true;
        ask.question = new DialogueQuestion {
            questionId = "tampil-beda",
            options = new[] {
                new DialogueOption {
                    optionId = "yes", text = CityTourContent.IntroYes, isCorrect = true,
                    reply = new[] { ReplyLine(yesReply) },
                },
                new DialogueOption {
                    optionId = "later", text = CityTourContent.IntroLater, askAgainLater = true,
                    reply = new[] { ReplyLine(laterReply) },
                },
            },
        };
        lines.Add(ask);
        Fill(dialogue, id, lines);
        return dialogue;
    }

    private static DialogueData BuildTampilBedaDialogue(string id, IEnumerable<CityTourContent.Line> content) {
        DialogueData dialogue = LoadOrCreate<DialogueData>($"{TampilBedaDialogueFolder}/{id}.asset");
        Fill(dialogue, id, content.Select(MainLine).ToList());
        return dialogue;
    }

    /// Dialog pujian Mira: suara, kamera, dan kotak dialog sama dengan perkenalan Mira (pembicara per baris kosong).
    private static DialogueData BuildMiraDialogue(DialogueData miraIntro) {
        string id = TampilBedaContent.ShowMiraDialogueId;
        DialogueData dialogue = LoadOrCreate<DialogueData>($"{TampilBedaDialogueFolder}/{id}.asset");
        Fill(dialogue, id, TampilBedaContent.ShowMira
            .Select(line => MainLine(new CityTourContent.Line("", line.Text))).ToList());
        dialogue.speakerName = miraIntro.speakerName;
        dialogue.voiceClips = miraIntro.voiceClips;
        dialogue.pitchMin = miraIntro.pitchMin;
        dialogue.pitchMax = miraIntro.pitchMax;
        dialogue.voiceVolume = miraIntro.voiceVolume;
        dialogue.charactersPerSecond = miraIntro.charactersPerSecond;
        dialogue.punctuationPause = miraIntro.punctuationPause;
        dialogue.focusCamera = miraIntro.focusCamera;
        dialogue.cameraDistance = miraIntro.cameraDistance;
        dialogue.cameraHeight = miraIntro.cameraHeight;
        dialogue.cameraSideOffset = miraIntro.cameraSideOffset;
        EditorUtility.SetDirty(dialogue);
        return dialogue;
    }

    /// Dipakai BuildWithoutPrompt (Buat Tur Kota) supaya membangun ulang tur tetap menyambungkan Tampil Beda.
    private static void WireTampilBedaIfBuilt(Scene world) {
        TampilBedaData data = LoadTampilBeda();
        if (data.Quest != null && data.PalaceOffer != null && data.PlazaOffer != null && data.Reminder != null)
            WireTampilBedaWorld(world, data);
    }

    /// Asset Tampil Beda dari disk (null kalau belum dibuat).
    private static TampilBedaData LoadTampilBeda() => new TampilBedaData {
        Quest = AssetDatabase.LoadAssetAtPath<QuestData>(TampilBedaQuestPath),
        PalaceOffer = LoadTampilBedaDialogue(TampilBedaContent.PalaceOfferDialogueId),
        PlazaOffer = LoadTampilBedaDialogue(TampilBedaContent.PlazaOfferDialogueId),
        Reminder = LoadTampilBedaDialogue(TampilBedaContent.ReminderDialogueId),
        ShowMira = LoadTampilBedaDialogue(TampilBedaContent.ShowMiraDialogueId),
    };

    private static DialogueData LoadTampilBedaDialogue(string id) =>
        AssetDatabase.LoadAssetAtPath<DialogueData>($"{TampilBedaDialogueFolder}/{id}.asset");

    // ------------------------------------------------------------ WorldScene

    private static bool WireTampilBedaWorld(Scene world, TampilBedaData data) {
        Transform root = FindByName(world, RootName);
        Transform finale = root != null ? FindChild(root, FinaleStopName) : null;
        Transform plaza = root != null ? FindChild(root, "Central Plaza") : null;
        Transform mascots = plaza != null ? FindChild(plaza, "Mascots") : null;
        Transform door = FindByName(world, ClothingDoorParent);
        if (finale == null || mascots == null || door == null) {
            Debug.LogError($"[Tampil Beda] Butuh '{RootName}/{FinaleStopName}', '{RootName}/Central Plaza/Mascots', dan " +
                           $"'{ClothingDoorParent}' di WorldScene (jalankan Buat Tur Kota dulu).");
            return false;
        }

        // Vista Palace: tawaran langsung setelah dialog penutup. Pemberi ini tidak ditandai di peta (pin tetap di plaza).
        var stop = new SerializedObject(finale.GetComponent<CityTourStop>());
        stop.FindProperty("followUpDialogue").objectReferenceValue = data.PalaceOffer;
        stop.ApplyModifiedPropertiesWithoutUndo();
        SetupGiver(GiverFor(finale.gameObject, data.Quest), data.Quest, data.PalaceOffer, false);

        // Central Plaza: tawaran cadangan ("Nanti saja" / akun lama), pengingat, tanda !, pin quest.
        var trigger = new SerializedObject(mascots.GetComponent<DialogueTrigger>());
        SerializedProperty questDialogues = trigger.FindProperty("questDialogues");
        SetQuestDialogue(questDialogues, data.Quest, QuestDialogueCondition.QuestAvailable, data.PlazaOffer);
        SetQuestDialogue(questDialogues, data.Quest, QuestDialogueCondition.QuestActive, data.Reminder);
        trigger.ApplyModifiedPropertiesWithoutUndo();
        SetupGiver(GiverFor(mascots.gameObject, data.Quest), data.Quest, data.PlazaOffer, true);
        var indicator = new SerializedObject(mascots.GetComponent<QuestAvailableIndicator>());
        SetArray(indicator.FindProperty("moreQuests"), data.Quest);
        indicator.ApplyModifiedPropertiesWithoutUndo();

        // Pintu Clothing Shop: area tujuan 1 (GPS + titik emas menunjuk ke sini).
        Transform shopRoot = EnsureChildObject(root, TampilBedaRootName);
        Transform doorTrigger = FindChild(door, "EnterDoor") ?? door;
        Transform area = FindChild(shopRoot, "Clothing Shop Door");
        if (area == null) {
            area = new GameObject("Clothing Shop Door").transform;
            area.SetParent(shopRoot, false);
            area.position = GroundPoint(doorTrigger.position);
        }
        EnsureLocation(area.gameObject, TampilBedaContent.ShopLocationId);

        // Tujuan 2-4 ada di dalam toko. Selama pemain di luar, GPS tetap menunjuk pintu toko; di dalam, titik interior
        // (Mira / bilik) lebih dekat sehingga dipilih (QuestWaypoint memilih titik terdekat).
        EnsureTargetPoint(EnsureChildAt(shopRoot, "Door Target - Kenalan Mira", area.position), data.Quest,
                          TampilBedaContent.MiraObjectiveId);
        EnsureTargetPoint(EnsureChildAt(shopRoot, "Door Target - Ruang Ganti", area.position), data.Quest,
                          TampilBedaContent.StyleObjectiveId);
        EnsureTargetPoint(EnsureChildAt(shopRoot, "Door Target - Tunjukkan Mira", area.position), data.Quest,
                          TampilBedaContent.ShowObjectiveId);
        return true;
    }

    /// QuestGiver untuk quest ini di objek itu (objek bisa punya beberapa QuestGiver, mis. Tur Kota + Tampil Beda).
    private static QuestGiver GiverFor(GameObject target, QuestData quest) {
        foreach (QuestGiver giver in target.GetComponents<QuestGiver>()) {
            SerializedProperty quests = new SerializedObject(giver).FindProperty("quests");
            for (int i = 0; i < quests.arraySize; i++)
                if (quests.GetArrayElementAtIndex(i).objectReferenceValue == quest) return giver;
        }
        return target.AddComponent<QuestGiver>();
    }

    private static void SetupGiver(QuestGiver giver, QuestData quest, DialogueData afterDialogue, bool showOnMap) {
        var serialized = new SerializedObject(giver);
        SetArray(serialized.FindProperty("quests"), quest);
        serialized.FindProperty("mode").enumValueIndex = (int)QuestGiver.GiveMode.AfterDialogue;
        serialized.FindProperty("giveAfterDialogue").objectReferenceValue = afterDialogue;
        serialized.FindProperty("showOnMap").boolValue = showOnMap;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Isi (atau tambah) entri Quest Dialogue untuk quest + kondisi ini, tanpa menyentuh entri lain.
    private static void SetQuestDialogue(SerializedProperty list, QuestData quest, QuestDialogueCondition when,
        DialogueData dialogue, string objectiveId = "") {
        SerializedProperty entry = null;
        for (int i = 0; i < list.arraySize && entry == null; i++) {
            SerializedProperty candidate = list.GetArrayElementAtIndex(i);
            if (candidate.FindPropertyRelative("quest").objectReferenceValue == quest &&
                candidate.FindPropertyRelative("when").enumValueIndex == (int)when &&
                candidate.FindPropertyRelative("objectiveId").stringValue == objectiveId)
                entry = candidate;
        }
        if (entry == null) {
            list.arraySize++;
            entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        }
        entry.FindPropertyRelative("quest").objectReferenceValue = quest;
        entry.FindPropertyRelative("when").enumValueIndex = (int)when;
        entry.FindPropertyRelative("objectiveId").stringValue = objectiveId;
        entry.FindPropertyRelative("dialogue").objectReferenceValue = dialogue;
    }

    // ------------------------------------------------------------ Interior_ClothingShop

    private static bool WireTampilBedaShop(Scene shop, QuestData quest) {
        Transform spawn = FindByName(shop, InteriorSpawnName);
        Transform mira = FindByName(shop, "NPC_Mira");
        FittingRoomStation station = shop.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<FittingRoomStation>(true)).FirstOrDefault();
        GameObject shopRoot = shop.GetRootGameObjects().FirstOrDefault(r => r.name == "Interior_ClothingShop");
        if (spawn == null || mira == null || station == null || shopRoot == null) {
            Debug.LogError($"[Tampil Beda] Butuh '{InteriorSpawnName}', 'NPC_Mira', FittingRoomStation, dan root " +
                           "'Interior_ClothingShop' di Interior_ClothingShop.");
            return false;
        }
        Transform root = EnsureChildObject(shopRoot.transform, TampilBedaRootName);

        // Masuk lewat pintu = tujuan 1 selesai (kalau pemain langsung masuk tanpa melewati area di luar).
        Transform entrance = EnsureChildAt(root, "Shop Entrance", spawn.position);
        EnsureLocation(entrance.gameObject, TampilBedaContent.ShopLocationId);

        // Titik GPS/minimap di dalam toko: satu objek per tujuan (QuestWaypoint memakai satu tujuan per Transform).
        EnsureTargetPoint(EnsureChildAt(root, "Target - Kenalan Mira", mira.position), quest, TampilBedaContent.MiraObjectiveId);
        EnsureTargetPoint(EnsureChildAt(root, "Target - Ruang Ganti", station.transform.position), quest,
                          TampilBedaContent.StyleObjectiveId);
        EnsureTargetPoint(EnsureChildAt(root, "Target - Tunjukkan Mira", mira.position), quest, TampilBedaContent.ShowObjectiveId);
        BuildMiraCheckpoint(root, mira, quest);

        var serialized = new SerializedObject(station);
        serialized.FindProperty("completesQuest").objectReferenceValue = quest;
        serialized.FindProperty("completesObjectiveId").stringValue = TampilBedaContent.StyleObjectiveId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    /// Checkpoint emas bergaya karpet pintu masuk (lingkaran lantai + cahaya pendek, bukan beacon setinggi langit seperti
    /// Tur Kota) di depan meja kasir, selama tujuan "tunjukkan-mira" bisa dikerjakan. Menyala saat diinjak.
    private static void BuildMiraCheckpoint(Transform root, Transform mira, QuestData quest) {
        // Arah hadap Mira diluruskan ke sumbu terdekat (meja kasir sejajar sumbu), supaya lingkaran tepat di depan
        // tengah meja walau Mira berdiri sedikit miring.
        Vector3 front = mira.position + SnapToAxis(mira.forward) * MiraCheckpointDistance;
        Transform checkpoint = EnsureChildObject(root, "Checkpoint - Tunjukkan Mira");
        checkpoint.position = GroundPoint(ClearFloorSpot(front, MiraCheckpointSize * 0.5f));
        Transform old = FindChild(checkpoint, "Marker");
        if (old != null) Object.DestroyImmediate(old.gameObject); // dibuat ulang (dulu prefab Tour_Marker)
        var marker = new GameObject("Marker").transform;
        marker.SetParent(checkpoint, false);
        (MeshRenderer ring, MeshRenderer[] glows) =
            BuildingEntranceArt.BuildFloorRing(marker, MiraCheckpointSize, MiraCheckpointGlowHeight, MiraCheckpointSegments);

        var look = new SerializedObject(marker.gameObject.AddComponent<BuildingEntranceMarker>());
        look.FindProperty("doormat").objectReferenceValue = ring;
        SerializedProperty glowList = look.FindProperty("glow");
        glowList.arraySize = glows.Length;
        for (int i = 0; i < glows.Length; i++) glowList.GetArrayElementAtIndex(i).objectReferenceValue = glows[i];
        look.FindProperty("idleColor").colorValue = Gold;
        look.FindProperty("activeColor").colorValue = GoldActive;
        look.ApplyModifiedPropertiesWithoutUndo();

        var serialized = new SerializedObject(Ensure<QuestObjectiveMarker>(checkpoint.gameObject));
        serialized.FindProperty("quest").objectReferenceValue = quest;
        serialized.FindProperty("objectiveId").stringValue = TampilBedaContent.ShowObjectiveId;
        serialized.FindProperty("marker").objectReferenceValue = marker.gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector3 SnapToAxis(Vector3 direction) {
        Vector3 flat = Flat(direction);
        return Mathf.Abs(flat.x) > Mathf.Abs(flat.z)
            ? new Vector3(Mathf.Sign(flat.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(flat.z));
    }

    /// Titik terdekat ke 'preferred' yang lantainya kosong selebar 'radius' (tidak menembus meja, rak, manekin).
    private static Vector3 ClearFloorSpot(Vector3 preferred, float radius) {
        Physics.SyncTransforms();
        Vector3 best = preferred;
        float bestDistance = float.MaxValue;
        int steps = Mathf.CeilToInt(ClearSearchRadius / ClearSearchStep);
        for (int x = -steps; x <= steps; x++)
        for (int z = -steps; z <= steps; z++) {
            Vector3 candidate = preferred + new Vector3(x * ClearSearchStep, 0f, z * ClearSearchStep);
            float distance = (candidate - preferred).sqrMagnitude;
            if (distance >= bestDistance || distance > ClearSearchRadius * ClearSearchRadius) continue;
            Vector3 bottom = GroundPoint(candidate) + Vector3.up * (radius + 0.05f);
            Vector3 top = bottom + Vector3.up * ClearCheckHeight;
            if (Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore)) continue;
            best = candidate;
            bestDistance = distance;
        }
        if (bestDistance == float.MaxValue)
            Debug.LogWarning("[Tampil Beda] Tidak ada lantai kosong di depan Mira; checkpoint dipasang di titik awal.");
        return best;
    }

    // ------------------------------------------------------------ NPC_Mira.prefab

    private static void WireMiraPrefab(TampilBedaData data) {
        GameObject prefab = PrefabUtility.LoadPrefabContents(MiraPrefabPath);
        try {
            var trigger = prefab.GetComponent<DialogueTrigger>();
            if (trigger == null) {
                Debug.LogError($"[Tampil Beda] {MiraPrefabPath} tidak punya DialogueTrigger.");
                return;
            }
            var serialized = new SerializedObject(trigger);
            SetQuestDialogue(serialized.FindProperty("questDialogues"), data.Quest, QuestDialogueCondition.ObjectiveAvailable,
                             data.ShowMira, TampilBedaContent.ShowObjectiveId);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Tanda "!" di atas kepala Mira juga tampil selama tujuan "tunjukkan-mira" bisa dikerjakan.
            var indicator = prefab.GetComponent<QuestAvailableIndicator>();
            if (indicator != null) {
                var indicatorData = new SerializedObject(indicator);
                indicatorData.FindProperty("objectiveQuest").objectReferenceValue = data.Quest;
                SerializedProperty ids = indicatorData.FindProperty("objectiveIds");
                ids.arraySize = 1;
                ids.GetArrayElementAtIndex(0).stringValue = TampilBedaContent.ShowObjectiveId;
                indicatorData.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, MiraPrefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    // ------------------------------------------------------------ Pembantu

    private static Transform EnsureChildObject(Transform parent, string name) {
        Transform existing = FindChild(parent, name);
        if (existing != null) return existing;
        var created = new GameObject(name).transform;
        created.SetParent(parent, false);
        return created;
    }

    /// Child baru diletakkan di 'position'; child yang sudah ada tidak dipindah (posisi hasil geser manual tetap).
    private static Transform EnsureChildAt(Transform parent, string name, Vector3 position) {
        Transform existing = FindChild(parent, name);
        if (existing != null) return existing;
        Transform created = EnsureChildObject(parent, name);
        created.position = position;
        return created;
    }

    private static void EnsureLocation(GameObject target, string locationId) {
        var area = Ensure<SphereCollider>(target);
        area.isTrigger = true;
        area.radius = ShopAreaRadius;
        SetString(Ensure<QuestLocation>(target), "locationId", locationId);
    }

    private static void EnsureTargetPoint(Transform target, QuestData quest, string objectiveId) {
        var serialized = new SerializedObject(Ensure<QuestTargetPoint>(target.gameObject));
        serialized.FindProperty("quest").objectReferenceValue = quest;
        serialized.FindProperty("objectiveId").stringValue = objectiveId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

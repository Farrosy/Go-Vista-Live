using System.IO;
using Bozo.ModularCharacters;
using UnityEditor;
using UnityEngine;

/// Bakes a BoZo character (default: Sally) into a static prefab, then assembles it into an NPC
/// prefab like NPC_Kota (Interactable + DialogueTrigger + NpcController).
///
/// The bake has to run in Play Mode because BoZo loads and merges outfits at runtime, so the
/// menu enters Play Mode, waits for loading + merging to finish, calls
/// OutfitSystem.SaveCharacterToPrefab(), exits Play Mode, then assembles the NPC prefab.
[InitializeOnLoad]
public static class BozoNpcBuilder
{
    const string CharacterName = "Sally";

    const string BozoRoot = "Assets/Download_Assets/BoZo_StylizedModularCharacters";
    const string BasePrefabPath = BozoRoot + "/Prefabs/BSMC_CharacterBase.prefab";
    const string CharacterObjectPath = BozoRoot + "/CustomCharacters/Resources/Base/" + CharacterName + ".asset";

    const string BakedFolder = "Assets/Characters/Models/Npc/" + CharacterName;
    const string BakedPrefabPath = BakedFolder + "/" + CharacterName + ".prefab";
    const string NpcPrefabPath = "Assets/Prefab/NPC_" + CharacterName + ".prefab";


    const string PendingKey = "GoVista.BozoNpc.PendingBake";
    const string SavedPathKey = "GoVista.BozoNpc.SavedPrefab";
    const float TimeoutSeconds = 90f;
    const float SettleSeconds = 1.5f;

    // Play Mode bake state (lives only while Play Mode is running).
    static OutfitSystem bakeTarget;
    static bool sawBusy;
    static double idleSince;
    static double startedAt;
    static bool saveRequested;

    static BozoNpcBuilder()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Go Vista/NPC/Buat NPC " + CharacterName + " (Bake + Prefab)")]
    static void StartBake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Buat NPC " + CharacterName, "Hentikan Play Mode dulu, lalu jalankan menu ini lagi.", "OK");
            return;
        }
        if (!AssetDatabase.LoadAssetAtPath<CharacterObject>(CharacterObjectPath) ||
            !AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath))
        {
            Debug.LogError($"[BozoNpcBuilder] Aset BoZo tidak ditemukan: {CharacterObjectPath} / {BasePrefabPath}");
            return;
        }
        if (File.Exists(BakedPrefabPath) &&
            !EditorUtility.DisplayDialog("Buat NPC " + CharacterName,
                $"{BakedPrefabPath} sudah ada. Bake ulang dan timpa?", "Bake ulang", "Batal"))
            return;

        SessionState.SetBool(PendingKey, true);
        SessionState.EraseString(SavedPathKey);
        Debug.Log($"[BozoNpcBuilder] Masuk Play Mode untuk bake {CharacterName}...");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Go Vista/NPC/Rakit Ulang Prefab NPC_" + CharacterName + " (tanpa bake)")]
    static void RebuildNpcOnly()
    {
        var baked = AssetDatabase.LoadAssetAtPath<GameObject>(BakedPrefabPath);
        if (!baked)
        {
            EditorUtility.DisplayDialog("Rakit NPC", $"Belum ada hasil bake di {BakedPrefabPath}. Jalankan 'Buat NPC {CharacterName}' dulu.", "OK");
            return;
        }
        BuildNpcPrefab(baked);
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
        {
            SessionState.SetBool(PendingKey, false);
            BeginBakeInPlayMode();
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            StopPolling();
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            string saved = SessionState.GetString(SavedPathKey, "");
            if (string.IsNullOrEmpty(saved)) return;
            SessionState.EraseString(SavedPathKey);
            // Wait one tick so the AssetDatabase is fully back in edit mode.
            EditorApplication.delayCall += () => FinishInEditMode(saved);
        }
    }

    // ---------------- Play Mode: bake ----------------

    static void BeginBakeInPlayMode()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        var data = AssetDatabase.LoadAssetAtPath<CharacterObject>(CharacterObjectPath);

        // Instantiate under an inactive parent so Awake doesn't load the prefab's default character first.
        var holder = new GameObject("__BozoBakeHolder");
        holder.SetActive(false);
        var instance = Object.Instantiate(basePrefab, holder.transform);
        instance.name = CharacterName;

        bakeTarget = instance.GetComponent<OutfitSystem>();
        if (!bakeTarget)
        {
            Debug.LogError("[BozoNpcBuilder] BSMC_CharacterBase tidak punya OutfitSystem.");
            EditorApplication.ExitPlaymode();
            return;
        }
        bakeTarget.characterData = data;
        bakeTarget.prefabName = CharacterName;
        bakeTarget.loadMode = OutfitSystem.LoadMode.OnStart;

        instance.transform.SetParent(null, false);
        Object.Destroy(holder);
        // Activating triggers Awake → Init + LoadFromObject(Sally) → merge.

        sawBusy = false;
        saveRequested = false;
        idleSince = 0;
        startedAt = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (!bakeTarget) { Fail("Objek bake hilang sebelum selesai."); return; }

        double now = EditorApplication.timeSinceStartup;
        if (now - startedAt > TimeoutSeconds) { Fail($"Timeout {TimeoutSeconds}s menunggu load/merge/save."); return; }
        if (saveRequested) return; // waiting for the "Saved Prefab at:" log

        bool busy = bakeTarget.isloading || bakeTarget.isMerging || bakeTarget.isUpdatingTexture;
        if (busy) { sawBusy = true; idleSince = 0; return; }
        if (!sawBusy) return;

        if (idleSince == 0) { idleSince = now; return; }
        if (now - idleSince < SettleSeconds) return;

        saveRequested = true;
        Debug.Log($"[BozoNpcBuilder] {CharacterName} selesai di-load & merge. Menyimpan prefab...");
        bakeTarget.SaveCharacterToPrefab();
    }

    static void OnLog(string message, string stackTrace, LogType type)
    {
        const string marker = "Saved Prefab at: ";
        if (!saveRequested || !message.StartsWith(marker)) return;

        SessionState.SetString(SavedPathKey, message.Substring(marker.Length).Trim());
        StopPolling();
        EditorApplication.delayCall += EditorApplication.ExitPlaymode;
    }

    static void Fail(string reason)
    {
        Debug.LogError("[BozoNpcBuilder] Bake gagal: " + reason);
        StopPolling();
        EditorApplication.ExitPlaymode();
    }

    static void StopPolling()
    {
        EditorApplication.update -= Poll;
        Application.logMessageReceived -= OnLog;
        bakeTarget = null;
    }

    // ---------------- Edit Mode: move + assemble ----------------

    static void FinishInEditMode(string savedPrefabPath)
    {
        savedPrefabPath = savedPrefabPath.Replace('\\', '/');
        string sourceFolder = Path.GetDirectoryName(savedPrefabPath)?.Replace('\\', '/');
        AssetDatabase.Refresh();

        // BoZo saves into CustomCharacters/Resources/<name>. Move it out of Resources so it
        // isn't force-included in every build, and next to the other NPC models.
        if (!string.IsNullOrEmpty(sourceFolder) && sourceFolder != BakedFolder)
        {
            BoZo_NpcPrefabExporter.EnsureFolder(Path.GetDirectoryName(BakedFolder).Replace('\\', '/'));
            if (AssetDatabase.IsValidFolder(BakedFolder)) AssetDatabase.DeleteAsset(BakedFolder);
            string err = AssetDatabase.MoveAsset(sourceFolder, BakedFolder);
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError($"[BozoNpcBuilder] Gagal memindahkan {sourceFolder} → {BakedFolder}: {err}");
                return;
            }
        }

        var baked = AssetDatabase.LoadAssetAtPath<GameObject>(BakedPrefabPath);
        if (!baked) { Debug.LogError($"[BozoNpcBuilder] Prefab hasil bake tidak ditemukan di {BakedPrefabPath}"); return; }

        BuildNpcPrefab(baked);
    }

    static void BuildNpcPrefab(GameObject bakedModel)
    {
        BoZo_NpcPrefabExporter.ApplyNpcAnimator(bakedModel);
        var prefab = BoZo_NpcPrefabExporter.BuildNpcPrefab(bakedModel, CharacterName, NpcPrefabPath, overwriteExisting: true);
        if (!prefab) return;

        Debug.Log($"[BozoNpcBuilder] Selesai: {NpcPrefabPath}. Isi Dialogue (Stages) di DialogueTrigger, lalu drag ke scene.", prefab);
        EditorGUIUtility.PingObject(prefab);
        Selection.activeObject = prefab;
    }

    [MenuItem("Go Vista/NPC/Pasang Navigasi + Hindari Obstacle + Napak Tanah ke NPC Terpilih")]
    static void AddNavigationToSelection()
    {
        int count = 0;
        foreach (var go in Selection.gameObjects)
        {
            // Pasang di root NPC (yang punya DialogueTrigger), bukan di child model.
            var trigger = go.GetComponentInParent<DialogueTrigger>();
            var target = trigger ? trigger.gameObject : go;
            BoZo_NpcPrefabExporter.AddNavigation(target, useUndo: true);
            EditorUtility.SetDirty(target);
            count++;
        }
        Debug.Log($"[BozoNpcBuilder] Navigasi dipasang ke {count} objek. Simpan scene (Ctrl+S).");
    }

    [MenuItem("Go Vista/NPC/Pasang Navigasi + Hindari Obstacle + Napak Tanah ke NPC Terpilih", true)]
    static bool AddNavigationToSelectionValidate() => Selection.gameObjects.Length > 0 && !EditorApplication.isPlaying;
}

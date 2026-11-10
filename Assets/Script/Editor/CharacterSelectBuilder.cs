using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menyiapkan fitur pilih karakter, SEKALI saja (yang sudah ada tidak ditimpa):
///  1. Assets/Resources/Player Female.prefab   : salinan Player.prefab dengan model FemaleChibi.
///  2. Assets/Resources/CharacterDatabase.asset : daftar karakter (Laki-laki, Perempuan).
///  3. Assets/Prefab/CharacterSelectCanvas.prefab : Canvas + panggung preview 3D. Tampilan menu GANTI KARAKTER
///     dibangun lewat kode saat runtime (CharacterSelectView, Docs/Profil/06a). UI lama di prefab (termasuk kolom
///     NAMA PEMAIN, yang sekarang ada di menu PROFIL) dihapus otomatis.
///  4. Instance CharacterSelectCanvas di semua scene lobby (LobbyScenePaths): MainMultiplayerScene dan
///     LobbyReturnScene (scene lobby setelah keluar room). Tanpa ini tombol KARAKTER tidak membuka apa-apa.
///
/// Setelah dibuat, semuanya aset biasa: edit bebas di Inspector / Prefab Mode. Builder hanya membuat
/// yang belum ada. Menu "Go Vista > Setup Pilih Karakter" menjalankan ulang pengecekan yang sama.
/// </summary>
[InitializeOnLoad]
public static class CharacterSelectBuilder {
    private const string MaleAvatarPath = "Assets/Resources/Player.prefab";
    private const string FemaleAvatarPath = "Assets/Resources/Player Female.prefab";
    private const string FemaleModelPath = "Assets/Characters/Animation/FemaleChibiAnimation/idle_loop_unity_fit (1).fbx";
    private const string FemaleControllerPath = "Assets/Characters/Animation/AnimationController/FemaleAnimationController.controller";
    private const string DatabasePath = "Assets/Resources/CharacterDatabase.asset";
    private const string CanvasPrefabPath = "Assets/Prefab/CharacterSelectCanvas.prefab";
    // Semua scene yang punya menu utama (tombol KARAKTER). LobbyReturnScene dimuat setelah keluar room.
    private static readonly string[] LobbyScenePaths = {
        "Assets/Scenes/MainMultiplayerScene.unity",
        "Assets/Scenes/Multiplayer/LobbyReturnScene.unity",
    };

    // Sama dengan setup di DevScene/femaleCharacterControl.unity.
    private static readonly Vector3 FemaleModelPosition = new Vector3(0f, -0.5906249f, 0f);
    private const float FemaleModelScale = 140.9f;

    // Di atas LobbyCanvas (50) & RoomWaitingCanvas (51), di bawah LoginCanvas (100).
    private const int CanvasSortingOrder = 60;
    // Panggung preview diletakkan jauh dari dunia supaya kamera preview hanya melihat karakter.
    private static readonly Vector3 StagePosition = new Vector3(0f, -1000f, 0f);

    // Kamera preview transparan: gradasi kartu preview di menu (CharacterSelectView/ProfileMenu) terlihat di belakang karakter.
    private static readonly Color StageColor = new Color(0f, 0f, 0f, 0f);

    static CharacterSelectBuilder() {
        EditorApplication.delayCall += AutoSetup;
    }

    private static void AutoSetup() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || IsSetupComplete())
            return;
        Setup();
    }

    [MenuItem("Go Vista/Setup Pilih Karakter")]
    private static void SetupFromMenu() {
        string summary = Setup();
        EditorUtility.DisplayDialog("Setup Pilih Karakter", summary, "OK");
    }

    private static bool IsSetupComplete() {
        return File.Exists(FemaleAvatarPath) && File.Exists(DatabasePath) && File.Exists(CanvasPrefabPath) &&
               !PrefabHasLegacyUi() && AllScenesHaveCanvas();
    }

    private static string Setup() {
        var log = new List<string>();
        GameObject female = EnsureFemaleAvatar(log);
        EnsureDatabase(female, log);
        GameObject canvasPrefab = EnsureCanvasPrefab(log);
        if (canvasPrefab != null)
            foreach (string scenePath in LobbyScenePaths)
                log.Add(Path.GetFileNameWithoutExtension(scenePath) + ": " + EnsureCanvasInScene(canvasPrefab, scenePath));
        string summary = string.Join("\n", log);
        Debug.Log("[CharacterSelect] Setup selesai.\n" + summary);
        return summary;
    }

    // ------------------------------------------------------------ 1. Avatar perempuan

    private static GameObject EnsureFemaleAvatar(List<string> log) {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleAvatarPath);
        if (existing != null) {
            log.Add("Player Female.prefab: sudah ada");
            return existing;
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleModelPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FemaleControllerPath);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MaleAvatarPath) == null || model == null || controller == null) {
            log.Add("Player Female.prefab: GAGAL (Player.prefab / model / controller perempuan tidak ditemukan)");
            return null;
        }
        if (!AssetDatabase.CopyAsset(MaleAvatarPath, FemaleAvatarPath)) {
            log.Add("Player Female.prefab: GAGAL menyalin Player.prefab");
            return null;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(FemaleAvatarPath);
        try {
            Animator oldAnimator = root.GetComponentInChildren<Animator>(true);

            var femaleModel = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            femaleModel.name = "FemaleChibi";
            femaleModel.transform.localPosition = FemaleModelPosition;
            femaleModel.transform.localRotation = Quaternion.identity;
            femaleModel.transform.localScale = Vector3.one * FemaleModelScale;
            Animator animator = femaleModel.GetComponent<Animator>();
            if (animator == null) animator = femaleModel.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            if (oldAnimator != null) Object.DestroyImmediate(oldAnimator.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, FemaleAvatarPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
        log.Add("Player Female.prefab: dibuat (cek posisi kaki model di prefab)");
        return AssetDatabase.LoadAssetAtPath<GameObject>(FemaleAvatarPath);
    }

    // ------------------------------------------------------------ 2. Database

    private static void EnsureDatabase(GameObject female, List<string> log) {
        if (File.Exists(DatabasePath)) {
            log.Add("CharacterDatabase.asset: sudah ada");
            return;
        }
        var db = ScriptableObject.CreateInstance<CharacterDatabase>();
        var list = new List<CharacterDatabase.Entry> {
            new CharacterDatabase.Entry {
                id = "male", displayName = "Laki-laki",
                description = "Karakter chibi laki-laki.",
                avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MaleAvatarPath),
            },
        };
        if (female != null) {
            list.Add(new CharacterDatabase.Entry {
                id = "female", displayName = "Perempuan",
                description = "Karakter chibi perempuan.",
                avatarPrefab = female,
            });
        }
        db.EditorSetCharacters(list);
        AssetDatabase.CreateAsset(db, DatabasePath);
        AssetDatabase.SaveAssets();
        log.Add($"CharacterDatabase.asset: dibuat ({list.Count} karakter)");
    }

    // ------------------------------------------------------------ 3. Prefab canvas

    private static GameObject EnsureCanvasPrefab(List<string> log) {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
        if (existing != null) {
            log.Add("CharacterSelectCanvas.prefab: " + RemoveLegacyUiFromPrefab());
            return existing;
        }
        GameObject temp = BuildCanvas();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, CanvasPrefabPath);
        Object.DestroyImmediate(temp);
        log.Add("CharacterSelectCanvas.prefab: dibuat");
        return prefab;
    }

    private static GameObject BuildCanvas() {
        // Root biasa (bukan canvas) supaya Preview Stage tidak ikut digeser/di-scale oleh Canvas.
        var prefabRoot = new GameObject("CharacterSelectCanvas", typeof(CharacterSelectUI));
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                                      typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(prefabRoot.transform, false);
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // Isi menu dibangun saat runtime oleh CharacterSelectUI (CharacterSelectView).
        // Panggung preview 3D, jauh di bawah dunia.
        GameObject stageGo = BuildStage(prefabRoot.transform, out CharacterPreviewStage stage);

        var so = new SerializedObject(prefabRoot.GetComponent<CharacterSelectUI>());
        so.FindProperty("stage").objectReferenceValue = stage;
        so.ApplyModifiedPropertiesWithoutUndo();

        stageGo.SetActive(false);
        return prefabRoot;
    }

    // ------------------------------------------------------------ 3b. Bersihkan UI lama

    private const string LegacyRootName = "Character Select Root";

    private static bool PrefabHasLegacyUi() {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
        return prefab != null && FindDeep(prefab.transform, LegacyRootName) != null;
    }

    /// Hapus UI menu KARAKTER versi lama (Text legacy + kolom NAMA PEMAIN) dari prefab yang sudah ada.
    /// Canvas, panggung preview, dan komponen CharacterSelectUI tetap.
    internal static string RemoveLegacyUiFromPrefab() {
        if (!PrefabHasLegacyUi())
            return "sudah memakai tampilan baru";
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try {
            Transform legacy = FindDeep(root.transform, LegacyRootName);
            if (legacy != null)
                Object.DestroyImmediate(legacy.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            return "UI lama dihapus (tampilan baru dibangun saat runtime)";
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindDeep(Transform parent, string name) {
        foreach (Transform child in parent) {
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject BuildStage(Transform parent, out CharacterPreviewStage stage) {
        var stageGo = new GameObject("Preview Stage", typeof(CharacterPreviewStage));
        stageGo.transform.SetParent(parent, false);
        stageGo.transform.localPosition = StagePosition;

        var turntable = new GameObject("Turntable").transform;
        turntable.SetParent(stageGo.transform, false);

        // Karakter ~2 m (kaki di y -0.6). Kamera di depan (+Z), menghadap ke karakter.
        var camGo = new GameObject("Preview Camera", typeof(Camera));
        camGo.transform.SetParent(stageGo.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0.45f, 4.6f);
        camGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = StageColor;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;
        cam.depth = -10f;

        // Lampu khusus preview (MainMultiplayerScene tidak punya lampu). Hanya aktif saat menu terbuka.
        var lightGo = new GameObject("Key Light", typeof(Light));
        lightGo.transform.SetParent(stageGo.transform, false);
        lightGo.transform.localRotation = Quaternion.Euler(35f, 200f, 0f);
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;

        stage = stageGo.GetComponent<CharacterPreviewStage>();
        var so = new SerializedObject(stage);
        so.FindProperty("previewCamera").objectReferenceValue = cam;
        so.FindProperty("turntable").objectReferenceValue = turntable;
        so.ApplyModifiedPropertiesWithoutUndo();
        return stageGo;
    }

    // ------------------------------------------------------------ 4. Instance di scene lobby

    private static bool AllScenesHaveCanvas() {
        foreach (string scenePath in LobbyScenePaths) {
            if (File.Exists(scenePath) && !SceneHasCanvas(scenePath))
                return false;
        }
        return true;
    }

    private static bool SceneHasCanvas(string scenePath) {
        string guid = AssetDatabase.AssetPathToGUID(CanvasPrefabPath);
        if (string.IsNullOrEmpty(guid)) return false;
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        if (scene.IsValid() && scene.isLoaded) return FindCanvasIn(scene) != null;
        return File.Exists(scenePath) && File.ReadAllText(scenePath).Contains(guid);
    }

    private static string EnsureCanvasInScene(GameObject prefab, string scenePath) {
        if (!File.Exists(scenePath)) return "tidak ditemukan, dilewati";

        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (!wasOpen) {
            try {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            } catch (System.Exception e) {
                Debug.LogWarning($"[CharacterSelect] Tidak bisa membuka {scenePath}: {e.Message}. " +
                                 "Simpan scene yang terbuka, lalu jalankan Go Vista > Setup Pilih Karakter.");
                return "gagal dibuka (lihat Console)";
            }
        }

        if (FindCanvasIn(scene) != null) {
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            return "CharacterSelectCanvas sudah ada";
        }

        PrefabUtility.InstantiatePrefab(prefab, scene);
        if (wasOpen) {
            // Scene sedang dibuka user: jangan auto-save (bisa ikut menyimpan perubahan lain milik user).
            EditorSceneManager.MarkSceneDirty(scene);
            return "CharacterSelectCanvas ditambahkan, silakan Ctrl+S";
        }
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorSceneManager.CloseScene(scene, true);
        return "CharacterSelectCanvas ditambahkan & disimpan";
    }

    private static CharacterSelectUI FindCanvasIn(Scene scene) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            var ui = root.GetComponentInChildren<CharacterSelectUI>(true);
            if (ui != null) return ui;
        }
        return null;
    }

}

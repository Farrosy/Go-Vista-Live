using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menyiapkan data karakter + panggung preview PROFIL, SEKALI saja (yang sudah ada tidak ditimpa):
///  1. Assets/Resources/Player Female.prefab   : salinan Player.prefab dengan model FemaleChibi.
///  2. Assets/Resources/CharacterDatabase.asset : daftar karakter.
///  3. Assets/Prefab/ProfilePreviewStage.prefab : panggung preview 3D (kamera + lampu + meja putar) untuk
///     kolom kiri menu PROFIL. Prefab lama CharacterSelectCanvas.prefab dimigrasi otomatis: UI karakter
///     sudah pindah ke scene sendiri (CharacterScene, lihat CharacterSceneBuilder), jadi Canvas dan
///     komponen pengendalinya dibuang dan file diganti nama (GUID tetap, instance di scene ikut).
///  4. Instance ProfilePreviewStage di semua scene lobby (LobbyScenePaths).
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
    private const string StagePrefabPath = "Assets/Prefab/ProfilePreviewStage.prefab";
    private const string LegacyCanvasPrefabPath = "Assets/Prefab/CharacterSelectCanvas.prefab";
    private const string StageRootName = "ProfilePreviewStage";
    // Semua scene yang punya menu PROFIL (keluar room memuat ulang MainMultiplayerScene).
    private static readonly string[] LobbyScenePaths = {
        "Assets/Scenes/MainMultiplayerScene.unity",
    };

    // Setup model chibi perempuan lama (dulu dari DevScene/femaleCharacterControl.unity, sudah dihapus).
    private static readonly Vector3 FemaleModelPosition = new Vector3(0f, -0.5906249f, 0f);
    private const float FemaleModelScale = 140.9f;

    // Panggung preview diletakkan jauh dari dunia supaya kamera preview hanya melihat karakter.
    // CharacterScene memakai posisi lain (CharacterSceneBuilder) supaya dua panggung tidak saling terlihat.
    private static readonly Vector3 StagePosition = new Vector3(0f, -1000f, 0f);

    // Kamera preview transparan: gradasi kartu/latar menu terlihat di belakang karakter.
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
        return File.Exists(FemaleAvatarPath) && File.Exists(DatabasePath) && File.Exists(StagePrefabPath) &&
               !File.Exists(LegacyCanvasPrefabPath) && AllScenesHaveStage();
    }

    private static string Setup() {
        var log = new List<string>();
        GameObject female = EnsureFemaleAvatar(log);
        EnsureDatabase(female, log);
        GameObject stagePrefab = EnsureStagePrefab(log);
        if (stagePrefab != null)
            foreach (string scenePath in LobbyScenePaths)
                log.Add(Path.GetFileNameWithoutExtension(scenePath) + ": " + EnsureStageInScene(stagePrefab, scenePath));
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

    // ------------------------------------------------------------ 3. Prefab panggung PROFIL

    private static GameObject EnsureStagePrefab(List<string> log) {
        log.Add("ProfilePreviewStage.prefab: " + MigrateLegacyPrefab());
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefabPath);
        if (existing != null) return existing;

        var root = new GameObject(StageRootName);
        GameObject stageGo = BuildStage(root.transform, StagePosition, new Vector2Int(1024, 1024), out _);
        stageGo.SetActive(false); // diaktifkan ProfileMenu saat PROFIL dibuka
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, StagePrefabPath);
        Object.DestroyImmediate(root);
        log.Add("ProfilePreviewStage.prefab: dibuat");
        return prefab;
    }

    /// CharacterSelectCanvas.prefab (menu karakter versi overlay) → ProfilePreviewStage.prefab: buang Canvas
    /// dan pengendali UI (sekarang di CharacterScene), sisakan Preview Stage. GUID tetap, jadi instance di
    /// MainMultiplayerScene otomatis ikut.
    internal static string MigrateLegacyPrefab() {
        if (!File.Exists(LegacyCanvasPrefabPath))
            return File.Exists(StagePrefabPath) ? "sudah ada" : "belum ada";
        if (File.Exists(StagePrefabPath))
            return "GAGAL: " + LegacyCanvasPrefabPath + " dan " + StagePrefabPath + " dua-duanya ada, hapus salah satu";

        GameObject root = PrefabUtility.LoadPrefabContents(LegacyCanvasPrefabPath);
        try {
            foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>()) {
                if (behaviour is CharacterSceneUI) Object.DestroyImmediate(behaviour);
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            Transform canvas = root.transform.Find("Canvas");
            if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
            root.name = StageRootName;
            PrefabUtility.SaveAsPrefabAsset(root, LegacyCanvasPrefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
        string error = AssetDatabase.MoveAsset(LegacyCanvasPrefabPath, StagePrefabPath);
        return string.IsNullOrEmpty(error) ? "dimigrasi dari CharacterSelectCanvas.prefab" : "GAGAL ganti nama: " + error;
    }

    /// Panggung preview (kamera + lampu + meja putar) di <paramref name="position"/>. Dipakai juga CharacterSceneBuilder.
    internal static GameObject BuildStage(Transform parent, Vector3 position, Vector2Int textureSize,
                                          out CharacterPreviewStage stage) {
        var stageGo = new GameObject("Preview Stage", typeof(CharacterPreviewStage));
        stageGo.transform.SetParent(parent, false);
        stageGo.transform.localPosition = position;

        var turntable = new GameObject("Turntable").transform;
        turntable.SetParent(stageGo.transform, false);

        // Karakter 1.8 m (kaki di y -0.08, lantai Floor di y -0.10). Kamera di depan (+Z),
        // sedikit menunduk supaya lantai terlihat.
        var camGo = new GameObject("Preview Camera", typeof(Camera));
        camGo.transform.SetParent(stageGo.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.22f, 4.07f);
        camGo.transform.localRotation = Quaternion.Euler(7f, 180f, 0f);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = StageColor;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;
        cam.depth = -10f;

        // Lampu khusus preview (MainMultiplayerScene tidak punya lampu). Hanya aktif saat panggung aktif.
        var lightGo = new GameObject("Key Light", typeof(Light));
        lightGo.transform.SetParent(stageGo.transform, false);
        lightGo.transform.localRotation = Quaternion.Euler(35f, 200f, 0f);
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;

        EnsureFloor(stageGo.transform);

        stage = stageGo.GetComponent<CharacterPreviewStage>();
        var so = new SerializedObject(stage);
        so.FindProperty("previewCamera").objectReferenceValue = cam;
        so.FindProperty("turntable").objectReferenceValue = turntable;
        so.FindProperty("textureSize").vector2IntValue = textureSize;
        so.ApplyModifiedPropertiesWithoutUndo();
        return stageGo;
    }

    private const string FloorMaterialPath = "Assets/Prefab/Materials/CharacterPreviewFloor.mat";

    /// Alas 3D tepat di bawah kaki (kaki karakter di y -0.08, atas alas di y -0.08). Karena ikut dirender
    /// kamera panggung, karakter selalu menempel di alas, apa pun ukuran layarnya.
    internal static bool EnsureFloor(Transform stage) {
        if (stage.Find("Floor") != null) return false;
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Floor";
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.SetParent(stage, false);
        floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(1.3f, 0.02f, 1.3f);
        var renderer = floor.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
        if (material != null) renderer.sharedMaterial = material;
        return true;
    }

    // ------------------------------------------------------------ 4. Instance di scene lobby

    private static bool AllScenesHaveStage() {
        foreach (string scenePath in LobbyScenePaths) {
            if (File.Exists(scenePath) && !SceneHasStage(scenePath))
                return false;
        }
        return true;
    }

    private static bool SceneHasStage(string scenePath) {
        string guid = AssetDatabase.AssetPathToGUID(StagePrefabPath);
        if (string.IsNullOrEmpty(guid)) return false;
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        if (scene.IsValid() && scene.isLoaded) return FindStageIn(scene) != null;
        return File.Exists(scenePath) && File.ReadAllText(scenePath).Contains(guid);
    }

    private static string EnsureStageInScene(GameObject prefab, string scenePath) {
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

        if (FindStageIn(scene) != null) {
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            return "ProfilePreviewStage sudah ada";
        }

        PrefabUtility.InstantiatePrefab(prefab, scene);
        if (wasOpen) {
            // Scene sedang dibuka user: jangan auto-save (bisa ikut menyimpan perubahan lain milik user).
            EditorSceneManager.MarkSceneDirty(scene);
            return "ProfilePreviewStage ditambahkan, silakan Ctrl+S";
        }
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorSceneManager.CloseScene(scene, true);
        return "ProfilePreviewStage ditambahkan & disimpan";
    }

    private static CharacterPreviewStage FindStageIn(Scene scene) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            var stage = root.GetComponentInChildren<CharacterPreviewStage>(true);
            if (stage != null) return stage;
        }
        return null;
    }
}

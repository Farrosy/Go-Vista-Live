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
///  3. Assets/Prefab/CharacterSelectCanvas.prefab : UI menu KARAKTER + panggung preview 3D.
///  4. Instance CharacterSelectCanvas di MainMultiplayerScene.
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
    private const string MainScenePath = "Assets/Scenes/MainMultiplayerScene.unity";

    // Sama dengan setup di DevScene/femaleCharacterControl.unity.
    private static readonly Vector3 FemaleModelPosition = new Vector3(0f, -0.5906249f, 0f);
    private const float FemaleModelScale = 140.9f;

    // Di atas LobbyCanvas (50) & RoomWaitingCanvas (51), di bawah LoginCanvas (100).
    private const int CanvasSortingOrder = 60;
    // Panggung preview diletakkan jauh dari dunia supaya kamera preview hanya melihat karakter.
    private static readonly Vector3 StagePosition = new Vector3(0f, -1000f, 0f);

    private static readonly Color BackdropColor = new Color(0.03f, 0.05f, 0.09f, 0.98f);
    private static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.14f, 0.97f);
    private static readonly Color StageColor = new Color(0.08f, 0.11f, 0.18f, 1f);
    private static readonly Color CardColor = new Color(0.13f, 0.17f, 0.27f, 1f);
    private static readonly Color PrimaryColor = new Color(0.12f, 0.44f, 0.86f, 1f);
    private static readonly Color SuccessColor = new Color(0.11f, 0.55f, 0.38f, 1f);
    private static readonly Color MutedColor = new Color(0.68f, 0.76f, 0.88f);

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
               SceneHasCanvas();
    }

    private static string Setup() {
        var log = new List<string>();
        GameObject female = EnsureFemaleAvatar(log);
        EnsureDatabase(female, log);
        GameObject canvasPrefab = EnsureCanvasPrefab(log);
        if (canvasPrefab != null)
            log.Add("MainMultiplayerScene: " + EnsureCanvasInMainScene(canvasPrefab));
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
            log.Add("CharacterSelectCanvas.prefab: sudah ada");
            return existing;
        }
        GameObject temp = BuildCanvas();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, CanvasPrefabPath);
        Object.DestroyImmediate(temp);
        log.Add("CharacterSelectCanvas.prefab: dibuat");
        return prefab;
    }

    private static GameObject BuildCanvas() {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        var canvasRect = (RectTransform)canvasGo.transform;

        // Isi menu (disembunyikan sampai tombol KARAKTER ditekan).
        RectTransform root = CreateRect("Character Select Root", canvasRect);
        Stretch(root);
        AddImage(root.gameObject, BackdropColor, null);

        // SafeArea supaya tidak tertutup notch HP.
        RectTransform safe = CreateRect("Safe Area", root);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        Text title = CreateText("Title", safe, font, "PILIH KARAKTER", 56, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        Place(title.rectTransform, 100f, 56f, 1000f, 80f);

        // Preview 3D (RawImage diisi RenderTexture dari Preview Stage saat runtime).
        RectTransform preview = CreateRect("Preview", safe);
        Place(preview, 100f, 170f, 860f, 820f);
        AddImage(preview.gameObject, StageColor, "ui-rounded");
        RectTransform previewImageRect = CreateRect("Preview Image", preview);
        Stretch(previewImageRect, 12f);
        var previewImage = previewImageRect.gameObject.AddComponent<RawImage>();
        var drag = previewImageRect.gameObject.AddComponent<CharacterPreviewDrag>();
        Text hint = CreateText("Drag Hint", preview, font, "Geser untuk memutar", 24, FontStyle.Normal,
                               TextAnchor.MiddleCenter, MutedColor);
        Place(hint.rectTransform, 0f, 760f, 860f, 44f);
        Button previous = CreateButton("Previous Button", preview, font, "<", CardColor, 56);
        Place((RectTransform)previous.transform, 24f, 360f, 104f, 104f);
        Button next = CreateButton("Next Button", preview, font, ">", CardColor, 56);
        Place((RectTransform)next.transform, 732f, 360f, 104f, 104f);

        // Panel info di kanan.
        RectTransform info = CreateRect("Info Panel", safe);
        Place(info, 1020f, 170f, 800f, 820f);
        AddImage(info.gameObject, PanelColor, "ui-rounded");
        Text counter = CreateText("Counter", info, font, "1 / 2", 26, FontStyle.Bold, TextAnchor.MiddleLeft, MutedColor);
        Place(counter.rectTransform, 48f, 44f, 704f, 40f);
        Text nameText = CreateText("Character Name", info, font, "Nama Karakter", 56, FontStyle.Bold,
                                   TextAnchor.MiddleLeft, Color.white);
        Place(nameText.rectTransform, 48f, 96f, 704f, 80f);
        RectTransform badge = CreateRect("Used Badge", info);
        Place(badge, 48f, 196f, 300f, 52f);
        AddImage(badge.gameObject, SuccessColor, "ui-rounded");
        Text badgeText = CreateText("Used Badge Label", badge, font, "SEDANG DIPAKAI", 24, FontStyle.Bold,
                                    TextAnchor.MiddleCenter, Color.white);
        Stretch(badgeText.rectTransform);
        Text description = CreateText("Description", info, font, "Deskripsi karakter.", 30, FontStyle.Normal,
                                      TextAnchor.UpperLeft, MutedColor);
        Place(description.rectTransform, 48f, 280f, 704f, 280f);
        Button select = CreateButton("Select Button", info, font, "PILIH", PrimaryColor, 40);
        Place((RectTransform)select.transform, 48f, 584f, 704f, 112f);
        Button back = CreateButton("Back Button", info, font, "KEMBALI", CardColor, 32);
        Place((RectTransform)back.transform, 48f, 716f, 704f, 80f);

        // Panggung preview 3D, jauh di bawah dunia.
        GameObject stageGo = BuildStage(prefabRoot.transform, out CharacterPreviewStage stage);

        var dragSo = new SerializedObject(drag);
        dragSo.FindProperty("stage").objectReferenceValue = stage;
        dragSo.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(prefabRoot.GetComponent<CharacterSelectUI>());
        so.FindProperty("root").objectReferenceValue = root.gameObject;
        so.FindProperty("stage").objectReferenceValue = stage;
        so.FindProperty("previewImage").objectReferenceValue = previewImage;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descriptionText").objectReferenceValue = description;
        so.FindProperty("counterText").objectReferenceValue = counter;
        so.FindProperty("usedBadge").objectReferenceValue = badge.gameObject;
        so.FindProperty("previousButton").objectReferenceValue = previous;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.FindProperty("selectButton").objectReferenceValue = select;
        so.FindProperty("selectButtonLabel").objectReferenceValue = select.GetComponentInChildren<Text>(true);
        so.FindProperty("backButton").objectReferenceValue = back;
        so.ApplyModifiedPropertiesWithoutUndo();

        SetLayerRecursive(root, canvasGo.layer);
        root.gameObject.SetActive(false);
        stageGo.SetActive(false);
        return prefabRoot;
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

    // ------------------------------------------------------------ 4. Instance di MainMultiplayerScene

    private static bool SceneHasCanvas() {
        string guid = AssetDatabase.AssetPathToGUID(CanvasPrefabPath);
        if (string.IsNullOrEmpty(guid)) return false;
        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        if (scene.IsValid() && scene.isLoaded) return FindCanvasIn(scene) != null;
        return File.Exists(MainScenePath) && File.ReadAllText(MainScenePath).Contains(guid);
    }

    private static string EnsureCanvasInMainScene(GameObject prefab) {
        if (!File.Exists(MainScenePath)) return "tidak ditemukan, dilewati";

        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (!wasOpen) {
            try {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            } catch (System.Exception e) {
                Debug.LogWarning($"[CharacterSelect] Tidak bisa membuka {MainScenePath}: {e.Message}. " +
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
        EditorSceneManager.SaveScene(scene, MainScenePath);
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

    // ------------------------------------------------------------ Helper UI

    private static RectTransform CreateRect(string name, Transform parent) {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image AddImage(GameObject target, Color color, string sprite) {
        var image = target.AddComponent<Image>();
        if (sprite != null) {
            image.sprite = Resources.Load<Sprite>("UI/" + sprite);
            image.type = Image.Type.Sliced;
        }
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, Font font, string text, int size,
                                   FontStyle style, TextAnchor anchor, Color color) {
        RectTransform rect = CreateRect(name, parent);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = anchor;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string text, Color color, int fontSize) {
        RectTransform rect = CreateRect(name, parent);
        Image image = AddImage(rect.gameObject, color, "ui-rounded");
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Text label = CreateText(name + " Label", rect, font, text, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter,
                                Color.white);
        Stretch(label.rectTransform);
        return button;
    }

    // Posisi dari pojok kiri atas parent (x ke kanan, y ke bawah), seperti Place() di LobyUISceneController.
    private static void Place(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect, float inset = 0f) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetLayerRecursive(Transform t, int layer) {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursive(child, layer);
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Membuat modal lobby (loading + alert) mengikuti alur "1 scene edit = 1 prefab":
///  1. Assets/Prefab/LobbyModalCanvas.prefab            (isi dibangun dari kode di bawah)
///  2. Assets/Scenes/Multiplayer/LobbyModalScene.unity  (scene edit berisi instance prefab, overlay terlihat)
///  3. Instance prefab dipasang di MainMultiplayerScene dan LobbyReturnScene, lalu disambungkan ke
///     field "modal" milik LobyUISceneController di scene itu.
/// Setelah dibuat, edit tampilannya di LobbyModalScene lalu Ctrl+S (otomatis di-Apply ke prefab).
/// </summary>
public static class LobbyModalBuilder {
    public const string PrefabPath = "Assets/Prefab/LobbyModalCanvas.prefab";
    public const string EditScenePath = "Assets/Scenes/Multiplayer/LobbyModalScene.unity";
    private const string CanvasName = "LobbyModalCanvas";
    private const int SortingOrder = 110; // di atas LobbyCanvas (50), RoomWaiting (51), CharacterSelect (60), Login (100)
    private const string ControllerModalField = "modal";

    private static readonly string[] HostScenePaths = {
        "Assets/Scenes/MainMultiplayerScene.unity",
        "Assets/Scenes/Multiplayer/LobbyReturnScene.unity",
    };

    private static readonly Vector2 PanelSize = new Vector2(760f, 420f);
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color EditSceneBackground = new Color32(8, 12, 24, 255);

    [MenuItem("Go Vista/Lobby UI/Buat ulang LobbyModalCanvas (prefab + scene)")]
    private static void BuildAllFromMenu() {
        if (File.Exists(PrefabPath) && !EditorUtility.DisplayDialog("Buat ulang LobbyModalCanvas",
                $"{PrefabPath} akan dibangun ulang dari kode. Editan manual di prefab ini akan hilang.",
                "Buat ulang", "Batal"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        BuildAll();
    }

    [MenuItem("Go Vista/Lobby UI/Edit LobbyModalCanvas (LobbyModalScene)")]
    private static void OpenEditScene() {
        if (!File.Exists(EditScenePath)) {
            EditorUtility.DisplayDialog("LobbyModalScene belum ada",
                "Jalankan dulu Go Vista > Lobby UI > Buat ulang LobbyModalCanvas.", "OK");
            return;
        }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(EditScenePath, OpenSceneMode.Single);
    }

    public static void BuildAll() {
        GameObject prefab = BuildPrefab();
        CreateEditScene(prefab);
        foreach (string path in HostScenePaths)
            InstallInHostScene(path, prefab);
        AssetDatabase.SaveAssets();
        Debug.Log("[Lobby UI] LobbyModalCanvas selesai dibuat dan dipasang.");
    }

    // ------------------------------------------------------------ Prefab

    private static GameObject BuildPrefab() {
        var root = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                  typeof(GraphicRaycaster));
        try {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildContents(root);
            root.transform.Find(LobbyModal.OverlayName).gameObject.SetActive(false); // prefab: tertutup
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        } finally {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildContents(GameObject root) {
        // Overlay penuh: Image dengan raycastTarget menangkap semua klik ke UI di belakangnya.
        RectTransform overlay = RedesignUiKit.Stretch(RedesignUiKit.NewRect(LobbyModal.OverlayName, root.transform));
        overlay.gameObject.AddComponent<Image>().color = OverlayColor;

        RectTransform safeArea = RedesignUiKit.Stretch(RedesignUiKit.NewRect("Modal Safe Area", overlay));
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        RectTransform panel = RedesignUiKit.NewRect("Modal Panel", safeArea);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = PanelSize;
        panel.gameObject.AddComponent<ResponsivePanel>();
        RedesignUiKit.Shape(panel.gameObject, RedesignUiKit.CardColor, cornerScale: 1.8f);
        RedesignUiKit.AddOutline(panel, RedesignUiKit.LineColor, cornerScale: 1.8f);

        // Spinner dan judul memakai slot yang sama (loading tanpa judul, alert tanpa spinner).
        RectTransform spinnerSlot = RedesignUiKit.Place(RedesignUiKit.NewRect("Modal Spinner Slot", panel),
                                                        (PanelSize.x - 96f) * 0.5f, 52f, 96f, 96f);
        RectTransform spinner = RedesignUiKit.Stretch(RedesignUiKit.NewRect("Modal Spinner", spinnerSlot));
        Image spinnerImage = RedesignUiKit.Shape(spinner.gameObject, RedesignUiKit.BlueColor, "ui-dashed-ring");
        spinnerImage.type = Image.Type.Simple;
        spinnerImage.preserveAspect = true;
        spinnerImage.raycastTarget = false;

        Text title = RedesignUiKit.Label("Modal Title", panel, "Belum ada room", 44, true, TextAnchor.MiddleCenter,
                                         Color.white);
        RedesignUiKit.Place(title.rectTransform, 48f, 60f, PanelSize.x - 96f, 76f);

        Text message = RedesignUiKit.Label("Modal Message", panel,
                                           "Belum ada room tersedia. Buat room sendiri atau coba lagi.", 30, false,
                                           TextAnchor.MiddleCenter, RedesignUiKit.GhostTextColor);
        RedesignUiKit.Place(message.rectTransform, 48f, 160f, PanelSize.x - 96f, 120f);

        Button button = RedesignUiKit.MenuButton("Modal Action Button", panel, RedesignUiKit.BlueColor,
                                                 RedesignUiKit.VioletColor, RedesignUiKit.VioletLipColor,
                                                 new Color(1f, 1f, 1f, 0.15f), 1.2f, out RectTransform face);
        RedesignUiKit.Place((RectTransform)button.transform, (PanelSize.x - 320f) * 0.5f, 300f, 320f, 92f);
        Text label = RedesignUiKit.CenteredContent("Modal Action", face, null, "OK", 32, 0f, Color.white);

        root.AddComponent<LobbyModal>().Bind(overlay.gameObject, title, message, spinner, button, label);
    }

    // ------------------------------------------------------------ Scene edit

    private static void CreateEditScene(GameObject prefab) {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try {
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = EditSceneBackground;
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = CanvasName;
            // Di scene edit overlay dibuat terlihat supaya bisa diedit. LobbyUIEditScenes mengembalikan
            // status ini ke "nonaktif" saat Apply ke prefab, dan LobbyModal.Awake menutupnya saat Play.
            instance.transform.Find(LobbyModal.OverlayName).gameObject.SetActive(true);

            EditorSceneManager.SaveScene(scene, EditScenePath);
        } finally {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ------------------------------------------------------------ Pasang di scene game

    private static void InstallInHostScene(string path, GameObject prefab) {
        if (!File.Exists(path)) {
            Debug.LogWarning($"[Lobby UI] {path} tidak ditemukan, LobbyModalCanvas tidak dipasang di sana.");
            return;
        }
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (!wasOpen)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        LobbyModal modal = FindOrCreateInstance(scene, prefab);
        int linked = LinkControllers(scene, modal);

        if (wasOpen) {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Lobby UI] {scene.name}: LobbyModalCanvas dipasang ({linked} controller). Silakan Ctrl+S.");
        } else {
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[Lobby UI] {Path.GetFileNameWithoutExtension(path)}: LobbyModalCanvas dipasang " +
                      $"({linked} controller, disimpan).");
        }
    }

    private static LobbyModal FindOrCreateInstance(Scene scene, GameObject prefab) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(root) &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == PrefabPath)
                return root.GetComponent<LobbyModal>();
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = CanvasName;
        return instance.GetComponent<LobbyModal>();
    }

    private static int LinkControllers(Scene scene, LobbyModal modal) {
        int linked = 0;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (LobyUISceneController controller in root.GetComponentsInChildren<LobyUISceneController>(true)) {
                var serialized = new SerializedObject(controller);
                SerializedProperty property = serialized.FindProperty(ControllerModalField);
                if (property == null)
                    continue;
                property.objectReferenceValue = modal;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                linked++;
            }
        }
        return linked;
    }
}

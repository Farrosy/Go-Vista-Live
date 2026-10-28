using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mengatur UI login/register:
///  - Prefab   : Assets/Prefab/LoginCanvas.prefab (sumber utama).
///  - Edit di  : Assets/Scenes/Multiplayer/LoginUIScene.unity (seperti LobyUIScene untuk lobby).
///  - Dipakai  : Assets/Scenes/MainMultiplayerScene.unity (instance prefab yang sama).
///
/// Saat LoginUIScene di-save (Ctrl+S), perubahan pada LoginCanvas otomatis di-Apply ke prefab,
/// sehingga MainMultiplayerScene ikut berubah. Jangan edit LoginCanvas langsung di MainMultiplayerScene,
/// karena override di sana akan menutupi perubahan dari prefab.
///
/// Setup berjalan otomatis sekali saat editor dibuka (kalau scene/instance belum ada),
/// atau manual lewat menu "Go Vista > Setup Login UI".
/// </summary>
[InitializeOnLoad]
public static class LoginCanvasBuilder {
    private const string CanvasName = "LoginCanvas"; // = nama file prefab, supaya nama instance konsisten
    private const string PrefabPath = "Assets/Prefab/LoginCanvas.prefab";
    private const string LoginScenePath = "Assets/Scenes/Multiplayer/LoginUIScene.unity";
    private const string MainScenePath = "Assets/Scenes/MainMultiplayerScene.unity";
    private const string AutoSetupSessionKey = "GoVista.LoginUISetupChecked";

    private static readonly Color BackdropColor = new Color(0.03f, 0.05f, 0.09f, 1f);
    private static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.14f, 0.97f);
    private static readonly Color FieldColor = new Color(0.12f, 0.16f, 0.23f, 1f);
    private static readonly Color AccentColor = new Color(0.16f, 0.55f, 0.95f, 1f);
    private static readonly Color DangerColor = new Color(0.65f, 0.22f, 0.2f);
    private static readonly Color MutedColor = new Color(0.68f, 0.76f, 0.88f);

    static LoginCanvasBuilder() {
        EditorSceneManager.sceneSaving -= ApplyLoginSceneEditsToPrefab;
        EditorSceneManager.sceneSaving += ApplyLoginSceneEditsToPrefab;

        if (SessionState.GetBool(AutoSetupSessionKey, false))
            return;
        SessionState.SetBool(AutoSetupSessionKey, true);
        EditorApplication.delayCall += AutoSetup;
    }

    private static void AutoSetup() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || IsSetupComplete())
            return;
        Setup(interactive: false);
    }

    [MenuItem("Go Vista/Setup Login UI")]
    private static void SetupFromMenu() {
        Setup(interactive: true);
    }

    [MenuItem("Go Vista/Buka Login UI Scene")]
    private static void OpenLoginScene() {
        if (!File.Exists(LoginScenePath))
            Setup(interactive: false);
        if (File.Exists(LoginScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
    }

    private static bool IsSetupComplete() {
        string prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
        return !string.IsNullOrEmpty(prefabGuid) &&
               File.Exists(LoginScenePath) && File.ReadAllText(LoginScenePath).Contains(prefabGuid) &&
               File.Exists(MainScenePath) && File.ReadAllText(MainScenePath).Contains(prefabGuid);
    }

    private static void Setup(bool interactive) {
        GameObject prefab = EnsurePrefab();
        string loginResult = EnsureCanvasInScene(LoginScenePath, prefab, isEditingScene: true);
        string mainResult = EnsureCanvasInScene(MainScenePath, prefab, isEditingScene: false);
        string summary = $"LoginUIScene: {loginResult}\nMainMultiplayerScene: {mainResult}";
        Debug.Log("[Login UI] Setup selesai.\n" + summary);
        if (interactive)
            EditorUtility.DisplayDialog("Setup Login UI", summary, "OK");
    }

    private static GameObject EnsurePrefab() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
            return prefab;

        if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
            AssetDatabase.CreateFolder("Assets", "Prefab");
        GameObject temp = CreateCanvas();
        prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    /// <summary>Pastikan scene punya instance LoginCanvas + EventSystem. Mengembalikan ringkasan untuk log.</summary>
    private static string EnsureCanvasInScene(string path, GameObject prefab, bool isEditingScene) {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        bool created = false;

        if (!wasOpen) {
            try {
                if (File.Exists(path)) {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                } else if (isEditingScene) {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    created = true;
                } else {
                    return "tidak ditemukan, dilewati";
                }
            } catch (System.Exception e) {
                // Misalnya: scene "Untitled" yang belum disimpan sedang terbuka.
                Debug.LogWarning($"[Login UI] Tidak bisa membuka {path}: {e.Message}. Simpan scene yang " +
                                 "sedang terbuka, lalu jalankan Go Vista > Setup Login UI.");
                return "gagal dibuka (lihat Console)";
            }
        }

        bool changed = created;
        if (FindLoginCanvas(scene) == null) {
            PrefabUtility.InstantiatePrefab(prefab, scene);
            changed = true;
        }
        changed |= EnsureEventSystem(scene);
        if (created)
            CreatePreviewCamera(scene);

        if (!changed)
            return "sudah lengkap";
        if (wasOpen) {
            // Scene sedang dibuka user: jangan auto-save (bisa ikut menyimpan perubahan lain milik user).
            EditorSceneManager.MarkSceneDirty(scene);
            return "LoginCanvas ditambahkan, silakan Ctrl+S";
        }

        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.CloseScene(scene, true);
        return created ? "dibuat" : "LoginCanvas ditambahkan & disimpan";
    }

    /// <summary>Saat LoginUIScene disimpan, terapkan perubahan LoginCanvas ke prefab (MainMultiplayerScene ikut berubah).</summary>
    private static void ApplyLoginSceneEditsToPrefab(Scene scene, string path) {
        if (path != LoginScenePath)
            return;

        LoginUIController controller = FindLoginCanvas(scene);
        if (controller == null)
            return;
        GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(controller.gameObject);
        if (instanceRoot == null || !PrefabUtility.HasPrefabInstanceAnyOverrides(instanceRoot, false))
            return;

        PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
        Debug.Log("[Login UI] Perubahan LoginCanvas di LoginUIScene diterapkan ke prefab. " +
                  "MainMultiplayerScene ikut berubah.");
    }

    private static LoginUIController FindLoginCanvas(Scene scene) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            LoginUIController controller = root.GetComponentInChildren<LoginUIController>(true);
            if (controller != null)
                return controller;
        }
        return null;
    }

    private static void CreatePreviewCamera(Scene scene) {
        // Hanya supaya Game view tidak menampilkan "No cameras rendering" saat preview UI.
        var cameraObject = new GameObject("Preview Camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackdropColor;
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
    }

    private static GameObject CreateCanvas() {
        var canvasObject = new GameObject(CanvasName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // di atas Loby Canvas (50) dan Room Waiting Canvas (51)
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Layar penuh yang menutupi (dan memblokir klik ke) lobby di belakangnya.
        RectTransform screen = CreateRect("Login Screen", canvasObject.transform, Vector2.zero);
        Stretch(screen);
        screen.gameObject.AddComponent<Image>().color = BackdropColor;

        // Panel memakai VerticalLayoutGroup supaya elemen yang disembunyikan (mode Masuk) tidak meninggalkan celah.
        RectTransform panel = CreateRect("Login Panel", screen, new Vector2(560f, 0f));
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = PanelColor;
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 32, 36);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text title = CreateText("Login Title", panel, "MASUK", 30, FontStyle.Bold, 48f, Color.white);
        Text subtitle = CreateText("Login Subtitle", panel, "Masuk ke akun Go Vista kamu.", 15,
                                   FontStyle.Normal, 34f, MutedColor);
        CreateText("Username Label", panel, "USERNAME", 14, FontStyle.Bold, 26f, Color.white);
        InputField username = CreateInputField("Username Input", panel, "Username",
                                                InputField.ContentType.Standard);
        CreateText("Password Label", panel, "PASSWORD", 14, FontStyle.Bold, 26f, Color.white);
        InputField password = CreateInputField("Password Input", panel, "Password",
                                                InputField.ContentType.Password);
        Text confirmLabel = CreateText("Confirm Password Label", panel, "KONFIRMASI PASSWORD", 14,
                                       FontStyle.Bold, 26f, Color.white);
        InputField confirm = CreateInputField("Confirm Password Input", panel, "Ulangi password",
                                              InputField.ContentType.Password);
        Text hint = CreateText("Password Hint", panel, AuthService.UsernameRules + "\n" + AuthService.PasswordRules,
                               12, FontStyle.Normal, 40f, MutedColor);
        Text status = CreateText("Login Status", panel, "", 15, FontStyle.Normal, 40f, Color.white);
        Button submit = CreateButton("Login Submit Button", panel, "MASUK", 54f, AccentColor, out Text submitLabel);
        Button switchMode = CreateButton("Login Switch Mode Button", panel, "Belum punya akun? Daftar", 42f,
                                         FieldColor, out Text switchLabel);

        // Badge kecil di kiri atas: nama akun + tombol keluar.
        RectTransform badge = CreateRect("Account Badge", canvasObject.transform, new Vector2(400f, 48f));
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0f, 1f);
        badge.anchoredPosition = new Vector2(20f, -20f);
        badge.gameObject.AddComponent<Image>().color = PanelColor;
        Text accountName = CreateText("Account Name", badge, "Masuk sebagai -", 14, FontStyle.Bold, 0f, Color.white);
        Stretch(accountName.rectTransform);
        accountName.rectTransform.offsetMin = new Vector2(14f, 0f);
        accountName.rectTransform.offsetMax = new Vector2(-124f, 0f);
        Button signOut = CreateButton("Account Sign Out Button", badge, "KELUAR", 36f, DangerColor, out _);
        RectTransform signOutRect = signOut.GetComponent<RectTransform>();
        signOutRect.anchorMin = signOutRect.anchorMax = signOutRect.pivot = new Vector2(1f, 0.5f);
        signOutRect.sizeDelta = new Vector2(104f, 36f);
        signOutRect.anchoredPosition = new Vector2(-6f, 0f);
        badge.gameObject.SetActive(false);

        var controller = canvasObject.AddComponent<LoginUIController>();
        var so = new SerializedObject(controller);
        so.FindProperty("loginScreen").objectReferenceValue = screen.gameObject;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subtitleText").objectReferenceValue = subtitle;
        so.FindProperty("usernameInput").objectReferenceValue = username;
        so.FindProperty("passwordInput").objectReferenceValue = password;
        so.FindProperty("confirmPasswordInput").objectReferenceValue = confirm;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("submitButton").objectReferenceValue = submit;
        so.FindProperty("submitLabel").objectReferenceValue = submitLabel;
        so.FindProperty("switchModeButton").objectReferenceValue = switchMode;
        so.FindProperty("switchModeLabel").objectReferenceValue = switchLabel;
        so.FindProperty("accountBadge").objectReferenceValue = badge.gameObject;
        so.FindProperty("accountNameText").objectReferenceValue = accountName;
        so.FindProperty("signOutButton").objectReferenceValue = signOut;
        SerializedProperty registerOnly = so.FindProperty("registerOnlyObjects");
        GameObject[] registerOnlyObjects = { confirmLabel.gameObject, confirm.gameObject, hint.gameObject };
        registerOnly.arraySize = registerOnlyObjects.Length;
        for (int i = 0; i < registerOnlyObjects.Length; i++)
            registerOnly.GetArrayElementAtIndex(i).objectReferenceValue = registerOnlyObjects[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (GameObject item in registerOnlyObjects)
            item.SetActive(false);
        return canvasObject;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size) {
        var item = new GameObject(name, typeof(RectTransform));
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        return rect;
    }

    private static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Text CreateText(string name, Transform parent, string content, int size, FontStyle style,
                                   float height, Color color) {
        RectTransform rect = CreateRect(name, parent, new Vector2(0f, height));
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, float height, Color color,
                                       out Text labelText) {
        RectTransform rect = CreateRect(name, parent, new Vector2(0f, height));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;

        labelText = CreateText(name + " Label", rect, label, 15, FontStyle.Bold, 0f, Color.white);
        labelText.alignment = TextAnchor.MiddleCenter;
        Stretch(labelText.rectTransform);
        return button;
    }

    private static InputField CreateInputField(string name, Transform parent, string placeholder,
                                               InputField.ContentType contentType) {
        RectTransform rect = CreateRect(name, parent, new Vector2(0f, 50f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = FieldColor;
        InputField input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;

        Text valueText = CreateText(name + " Text", rect, "", 16, FontStyle.Normal, 0f, Color.white);
        Stretch(valueText.rectTransform);
        valueText.rectTransform.offsetMin = new Vector2(14f, 4f);
        valueText.rectTransform.offsetMax = new Vector2(-14f, -4f);
        valueText.supportRichText = false;

        Text placeholderText = CreateText(name + " Placeholder", rect, placeholder, 16, FontStyle.Italic, 0f,
                                          new Color(0.58f, 0.65f, 0.76f));
        Stretch(placeholderText.rectTransform);
        placeholderText.rectTransform.offsetMin = new Vector2(14f, 4f);
        placeholderText.rectTransform.offsetMax = new Vector2(-14f, -4f);

        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.contentType = contentType;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    private static bool EnsureEventSystem(Scene scene) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.GetComponentInChildren<EventSystem>(true) != null)
                return false;
        }
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        SceneManager.MoveGameObjectToScene(eventSystem, scene);
        return true;
    }
}

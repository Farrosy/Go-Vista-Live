using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static RedesignUiKit;

/// <summary>
/// Desain dialog PENGATURAN. Menu: Go Vista > Desain Baru > Panel Pengaturan.
///
/// Membangun SATU prefab, Assets/Resources/UI/SettingsPanel.prefab (komponen SettingsPanel), lalu
/// memasangnya sebagai nested prefab di dua tempat:
///   - LobbyCanvas.prefab       : "Settings Overlay/Settings Dialog" (menu utama lobby)
///   - RoomWaitingCanvas.prefab : "Pause Settings Panel" (menu pause di dalam game)
/// Jadi lobby dan in-game selalu memakai dialog yang sama. Untuk mengubah tampilan, edit
/// SettingsPanel.prefab langsung, atau ubah Build() di bawah lalu jalankan menu ini lagi.
/// </summary>
public static class SettingsPanelRedesign {
    private const string PanelPrefabPath = "Assets/Resources/UI/SettingsPanel.prefab";
    private const string LobbyCanvasPath = "Assets/Prefab/LobbyCanvas.prefab";
    private const string WaitingCanvasPath = "Assets/Prefab/RoomWaitingCanvas.prefab";
    private const float Width = 860f;
    // Baris akun ("Masuk sebagai …") pindah ke menu PROFIL (Docs/Profil/08a), jadi dialog lebih pendek.
    private const float AccountRowHeight = 70f;
    private const float Height = 840f - AccountRowHeight;
    private const float DoneY = 698f - AccountRowHeight;
    private const float Pad = 44f;
    private const float Inner = Width - Pad * 2f; // 772

    [MenuItem("Go Vista/Desain Baru/Panel Pengaturan")]
    private static void ApplyFromMenu() {
        if (!EditorUtility.DisplayDialog("Desain baru panel Pengaturan",
                "SettingsPanel.prefab akan dibangun ulang (editan manual di prefab itu hilang), lalu dipasang di " +
                "LobbyCanvas (menu utama) dan RoomWaitingCanvas (menu pause). Bagian lain kedua prefab tidak berubah.",
                "Pasang", "Batal"))
            return;
        Apply();
    }

    /// <summary>Bangun prefab panel lalu pasang di lobby dan menu pause.</summary>
    public static void Apply() {
        GameObject panelPrefab = BuildPanelPrefab();
        string lobby = Install(panelPrefab, LobbyCanvasPath, "Settings Overlay", "Settings Dialog");
        string pause = Install(panelPrefab, WaitingCanvasPath, null, "Pause Settings Panel");
        Debug.Log($"[Desain Baru] {PanelPrefabPath} dibuat.\nLobbyCanvas: {lobby}\nRoomWaitingCanvas: {pause}");
    }

    private static GameObject BuildPanelPrefab() {
        var root = new GameObject("Settings Panel", typeof(RectTransform));
        try {
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            root.AddComponent<ResponsivePanel>();
            Build(rect);

            var panel = root.AddComponent<SettingsPanel>();
            panel.Init(Find<Slider>(rect, "Settings Sensitivity Slider"), Find<Text>(rect, "Settings Sensitivity Value"),
                       null, Find<Button>(rect, "Settings Controls Button"),
                       Find<Button>(rect, "Settings Done Button"), Find<Button>(rect, "Settings Close Button"));
            SetLayerRecursive(rect, LayerMask.NameToLayer("UI"));
            root.SetActive(false); // dibuka lewat SettingsPanel.Open()
            return PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        } finally {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Ganti objek <paramref name="targetName"/> di prefab canvas dengan instance SettingsPanel.prefab
    /// (nama, parent, urutan, dan posisi tengah dipertahankan).
    /// </summary>
    private static string Install(GameObject panelPrefab, string canvasPath, string parentName, string targetName) {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(canvasPath) == null)
            return "tidak ditemukan, dilewati";
        GameObject root = PrefabUtility.LoadPrefabContents(canvasPath);
        try {
            Transform searchRoot = parentName != null ? FindDeep(root.transform, parentName) : root.transform;
            Transform old = searchRoot != null ? FindDeep(searchRoot, targetName) : null;
            if (old == null)
                return $"\"{targetName}\" tidak ditemukan, dilewati";

            Transform parent = old.parent;
            int siblingIndex = old.GetSiblingIndex();
            int layer = old.gameObject.layer;
            Object.DestroyImmediate(old.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, parent);
            instance.name = targetName; // nama lama dipertahankan supaya mudah dicari di Hierarchy
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.SetSiblingIndex(siblingIndex);
            SetLayerRecursive(rect, layer);
            instance.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, canvasPath);
            return "dipasang";
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T Find<T>(Transform root, string name) where T : Component {
        Transform found = FindDeep(root, name);
        T component = found != null ? found.GetComponent<T>() : null;
        if (component == null)
            Debug.LogWarning($"[Desain Baru] {typeof(T).Name} \"{name}\" tidak ditemukan di panel Pengaturan.");
        return component;
    }

    private static void Build(RectTransform dialog) {
        ClearChildren(dialog);
        dialog.sizeDelta = new Vector2(Width, Height);
        Shape(dialog.gameObject, CardColor, cornerScale: 1.8f);
        AddOutline(dialog, LineColor, cornerScale: 1.8f);
        Image accent = Place(NewRect("Settings Accent Line", dialog), 70f, 0f, Width - 140f, 3f)
            .gameObject.AddComponent<Image>();
        accent.color = new Color(CyanTopColor.r, CyanTopColor.g, CyanTopColor.b, 0.85f);
        accent.raycastTarget = false;

        // Header: ikon gear (sama dengan tile PENGATURAN di menu utama), judul, tombol tutup.
        IconTile("Settings Title Icon", dialog, "icon-settings", CyanTopColor, CyanBottomColor, Pad, 40f, 68f, 36f);
        Place(Label("Settings Title", dialog, "PENGATURAN", 44, true, TextAnchor.MiddleLeft, Color.white)
                  .rectTransform, 132f, 40f, 560f, 68f);
        Button close = GhostButton("Settings Close Button", dialog, "icon-x", "", 1, 30f);
        Place((RectTransform)close.transform, Width - Pad - 64f, 42f, 64f, 64f);
        RemoveEmptyLabel(close.transform);

        // ---- KAMERA
        // TODO: bagian AUDIO (volume musik / efek) bisa ditambah dengan pola yang sama setelah game punya audio.
        Place(Label("Settings Camera Section", dialog, "KAMERA", 20, true, TextAnchor.MiddleLeft, MutedColor)
                  .rectTransform, Pad, 136f, 400f, 28f);
        RectTransform card = Place(NewRect("Settings Camera Card", dialog), Pad, 174f, Inner, 236f);
        GradientShape(card.gameObject, SectionTopColor, SectionBottomColor, cornerScale: 1.3f).raycastTarget = false;
        AddOutline(card, new Color(1f, 1f, 1f, 0.07f), cornerScale: 1.3f);

        IconTile("Settings Camera Icon", card, "icon-camera", BlueColor, VioletColor, 28f, 28f, 56f, 30f);
        Place(Label("Settings Sensitivity Label", card, "Sensitivitas kamera", 28, true, TextAnchor.MiddleLeft,
                    Color.white).rectTransform, 100f, 24f, 500f, 38f);
        Place(Label("Settings Sensitivity Hint", card, "Seberapa cepat kamera berputar saat digeser", 22, false,
                    TextAnchor.MiddleLeft, SubColor).rectTransform, 100f, 62f, 520f, 30f);

        // Nilai (1.0x) di dalam pil. Teksnya diisi LobyUISceneController.UpdateSettingsLabels.
        RectTransform pill = Place(NewRect("Settings Sensitivity Pill", card), Inner - 28f - 112f, 30f, 112f, 54f);
        Shape(pill.gameObject, new Color(CyanTopColor.r, CyanTopColor.g, CyanTopColor.b, 0.15f), cornerScale: 0.8f)
            .raycastTarget = false;
        AddOutline(pill, new Color(CyanTopColor.r, CyanTopColor.g, CyanTopColor.b, 0.5f), cornerScale: 0.8f);
        Stretch(Label("Settings Sensitivity Value", pill, "1.0x", 28, true, TextAnchor.MiddleCenter, CyanTextColor)
                    .rectTransform);

        // − slider + : tombol menggeser 0.1x per ketukan (lebih presisi daripada menggeser di layar HP).
        StepSlider("Settings Sensitivity Slider", card, 28f, 116f, Inner - 56f, GameSettings.MinCameraSensitivity,
                   GameSettings.MaxCameraSensitivity, 0.1f, BlueColor, BlueColor);
        Place(Label("Settings Sensitivity Min", card, "Lambat", 20, false, TextAnchor.MiddleLeft, MutedColor)
                  .rectTransform, 108f, 184f, 200f, 28f);
        Place(Label("Settings Sensitivity Max", card, "Cepat", 20, false, TextAnchor.MiddleRight, MutedColor)
                  .rectTransform, Inner - 108f - 200f, 184f, 200f, 28f);

        // ---- KONTROL: baris yang bisa diketuk (panah = membuka layar lain).
        Place(Label("Settings Controls Section", dialog, "KONTROL", 20, true, TextAnchor.MiddleLeft, MutedColor)
                  .rectTransform, Pad, 438f, 400f, 28f);
        Button controls = MenuButton("Settings Controls Button", dialog, TileTopColor, TileBottomColor, TileLipColor,
                                     new Color(1f, 1f, 1f, 0.1f), 1.2f, out RectTransform controlsFace);
        Place((RectTransform)controls.transform, Pad, 476f, Inner, 124f);
        IconTile("Settings Controls Icon", controlsFace, "icon-gamepad", PinkTopColor, PinkBottomColor,
                 28f, 22f, 72f, 40f);
        Place(Label("Settings Controls Title", controlsFace, "Atur kontrol", 28, true, TextAnchor.MiddleLeft,
                    Color.white).rectTransform, 122f, 22f, 540f, 38f);
        Place(Label("Settings Controls Hint", controlsFace, "Pindahkan dan ubah ukuran tombol layar", 22, false,
                    TextAnchor.MiddleLeft, SubColor).rectTransform, 122f, 60f, 560f, 30f);
        Icon("Settings Controls Chevron", controlsFace, "icon-chevron-right", SubColor, Inner - 28f - 40f, 38f, 40f);

        // ---- SELESAI: tombol utama, gaya sama dengan MAIN.
        Button done = MenuButton("Settings Done Button", dialog, BlueColor, VioletColor, VioletLipColor,
                                 new Color(1f, 1f, 1f, 0.18f), 1.2f, out RectTransform doneFace);
        Place((RectTransform)done.transform, Pad, DoneY, Inner, 100f);
        CenteredContent("Settings Done", doneFace, "icon-check", "SELESAI", 34, 36f, Color.white);
    }

    /// Patch SettingsPanel.prefab yang sudah ada (editan manual lain tetap): hapus baris akun, naikkan SELESAI,
    /// perpendek dialog. Aman diulang. Dipanggil Go Vista > Profile > Pasang Profile.
    public static string RemoveAccountRow() {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath) == null)
            return "tidak ditemukan, dilewati";
        GameObject root = PrefabUtility.LoadPrefabContents(PanelPrefabPath);
        try {
            Transform avatar = FindDeep(root.transform, "Settings Account Avatar");
            Transform label = FindDeep(root.transform, "Settings Account");
            if (avatar == null && label == null)
                return "baris akun sudah tidak ada";
            if (avatar != null) Object.DestroyImmediate(avatar.gameObject);
            if (label != null) Object.DestroyImmediate(label.gameObject);

            var dialog = (RectTransform)root.transform;
            dialog.sizeDelta = new Vector2(dialog.sizeDelta.x, dialog.sizeDelta.y - AccountRowHeight);
            if (FindDeep(root.transform, "Settings Done Button") is RectTransform done)
                done.anchoredPosition += new Vector2(0f, AccountRowHeight);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
            return "baris akun dihapus, SELESAI dinaikkan";
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // GhostButton dengan label kosong: hapus Text kosongnya supaya ikon benar-benar di tengah.
    private static void RemoveEmptyLabel(Transform button) {
        foreach (Text text in button.GetComponentsInChildren<Text>(true)) {
            if (string.IsNullOrEmpty(text.text))
                Object.DestroyImmediate(text.gameObject);
        }
    }
}

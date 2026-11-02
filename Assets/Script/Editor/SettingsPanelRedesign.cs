using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static RedesignUiKit;

/// <summary>
/// Desain baru dialog PENGATURAN di lobby (Assets/Prefab/LobbyCanvas.prefab).
/// Menu: Go Vista > Desain Baru > Panel Pengaturan.
///
/// Yang diganti hanya ISI "Settings Dialog" (di dalam "Settings Overlay"). Objek dialognya sendiri
/// (ResponsivePanel, posisi) tetap. Nama objek yang dicari LobyUISceneController dipertahankan:
///   Settings Close Button, Settings Sensitivity Slider, Settings Sensitivity Value,
///   Settings Controls Button, Settings Account, Settings Done Button
/// jadi LobyUISceneController TIDAK perlu diubah.
///
/// Catatan: menu lama "Go Vista > Lobby UI > Perbarui panel Pengaturan" dan "Reset LobbyCanvas ke default"
/// membangun versi lama dari kode. Kalau tidak sengaja dipakai, jalankan menu ini lagi.
/// </summary>
public static class SettingsPanelRedesign {
    private const string PrefabPath = "Assets/Prefab/LobbyCanvas.prefab";
    private const float Width = 860f;
    private const float Pad = 44f;
    private const float Inner = Width - Pad * 2f; // 772

    [MenuItem("Go Vista/Desain Baru/Panel Pengaturan")]
    private static void Apply() {
        if (!EditorUtility.DisplayDialog("Desain baru panel Pengaturan",
                "Isi \"Settings Dialog\" di LobbyCanvas.prefab akan diganti dengan desain baru. " +
                "Editan manual di dalam dialog itu hilang; bagian lain prefab tidak berubah.", "Pasang", "Batal"))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            Transform overlay = FindDeep(root.transform, "Settings Overlay");
            Transform dialog = overlay != null ? FindDeep(overlay, "Settings Dialog") : null;
            if (dialog == null) {
                Debug.LogWarning("[Desain Baru] \"Settings Overlay/Settings Dialog\" tidak ditemukan di " + PrefabPath +
                                 ". Jalankan dulu Go Vista > Lobby UI > Perbarui panel Pengaturan, lalu ulangi.");
                return;
            }
            Build((RectTransform)dialog);
            SetLayerRecursive(dialog, overlay.gameObject.layer);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[Desain Baru] Panel Pengaturan di LobbyCanvas.prefab sudah diganti ke desain baru.");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Build(RectTransform dialog) {
        ClearChildren(dialog);
        dialog.sizeDelta = new Vector2(Width, 840f);
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

        // ---- Akun: avatar inisial + "Masuk sebagai ..." (teks diisi LobyUISceneController).
        RectTransform avatar = Place(NewRect("Settings Account Avatar", dialog), Pad, 628f, 44f, 44f);
        GradientShape(avatar.gameObject, PinkTopColor, AvatarBottomColor, "ui-circle").raycastTarget = false;
        Text initial = Label("Settings Account Initial", avatar, "?", 22, true, TextAnchor.MiddleCenter, Color.white);
        Stretch(initial.rectTransform);
        avatar.gameObject.AddComponent<PlayerNameDisplay>().Init(null, initial);
        Place(Label("Settings Account", dialog, "", 22, false, TextAnchor.MiddleLeft, SubColor).rectTransform,
              Pad + 58f, 628f, Inner - 58f, 44f);

        // ---- SELESAI: tombol utama, gaya sama dengan MAIN.
        Button done = MenuButton("Settings Done Button", dialog, BlueColor, VioletColor, VioletLipColor,
                                 new Color(1f, 1f, 1f, 0.18f), 1.2f, out RectTransform doneFace);
        Place((RectTransform)done.transform, Pad, 698f, Inner, 100f);
        CenteredContent("Settings Done", doneFace, "icon-check", "SELESAI", 34, 36f, Color.white);
    }

    // GhostButton dengan label kosong: hapus Text kosongnya supaya ikon benar-benar di tengah.
    private static void RemoveEmptyLabel(Transform button) {
        foreach (Text text in button.GetComponentsInChildren<Text>(true)) {
            if (string.IsNullOrEmpty(text.text))
                Object.DestroyImmediate(text.gameObject);
        }
    }
}

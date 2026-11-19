using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Membangun popup HADIAH LOGIN HARIAN: Assets/Resources/UI/DailyLoginPopup.prefab (komponen DailyLoginPopup).
/// Menu: Go Vista > Desain Baru > Hadiah Login Harian. Sprite dari Assets/GoVista_HUD_Assets/HadiahLogin.
///
/// Prefab punya Canvas sendiri (overlay, sorting 53: di atas HUD & menu pause, di bawah layar mati 55),
/// jadi tidak perlu dipasang di scene. Ukuran desain panel 1480x980 (referensi 1920x1080), dikecilkan
/// ResponsivePanel kalau layar lebih kecil. Editan manual di prefab hilang kalau menu ini dijalankan lagi.
/// </summary>
public static class DailyLoginPopupBuilder {
    private const string PrefabPath = "Assets/Resources/UI/DailyLoginPopup.prefab";
    private const string SpriteFolder = "Assets/GoVista_HUD_Assets/HadiahLogin/";
    private const string BoldFontPath = "Assets/Fonts/Fredoka-Bold SDF.asset";
    private const string MediumFontPath = "Assets/Fonts/Fredoka-Medium SDF.asset";
    private const int SortingOrder = 53;

    private const float PanelWidth = 1480f;
    private const float PanelHeight = 980f;
    private const float Pad = 40f;
    private const float TileWidth = 321f;
    private const float TileHeight = 242f;
    private const float TileGap = 24f;
    private const float GridTop = 262f;

    private static readonly Color SubtitleColor = new Color32(190, 200, 222, 255);
    private static readonly Color BannerColor = new Color32(255, 200, 120, 255);
    private static readonly Color BigSubColor = new Color32(222, 200, 244, 255);
    private static readonly Color TagTextColor = new Color32(84, 52, 0, 255);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);

    private static TMP_FontAsset _bold, _medium;

    [MenuItem("Go Vista/Desain Baru/Hadiah Login Harian")]
    private static void BuildFromMenu() {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Hadiah Login Harian",
                $"{PrefabPath} akan dibangun ulang (editan manual di prefab itu hilang).", "Bangun", "Batal"))
            return;
        Build();
    }

    public static void Build() {
        _bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
        _medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumFontPath);
        if (_bold == null || _medium == null)
            Debug.LogWarning("[Hadiah Login] Font Fredoka SDF tidak ditemukan, pakai font default TMP.");

        var root = new GameObject("DailyLoginPopup", typeof(RectTransform));
        try {
            root.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // Latar gelap menutup layar dan menahan sentuhan ke game di belakang.
            RectTransform dim = Stretch(NewRect("Dim", root.transform));
            dim.gameObject.AddComponent<Image>().color = DimColor;

            RectTransform panel = NewRect("Panel", root.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            Sliced(panel.gameObject, "dl_panel_bg").raycastTarget = true;
            panel.gameObject.AddComponent<ResponsivePanel>();

            var popup = root.AddComponent<DailyLoginPopup>();
            var so = new SerializedObject(popup);
            BuildHeader(panel, so);
            BuildBanner(panel, so);
            BuildTiles(panel, so);
            BuildClaimButton(panel, so);
            BuildToast(panel, so);
            so.FindProperty("tileNormal").objectReferenceValue = LoadSprite("dl_tile_normal");
            so.FindProperty("tileReady").objectReferenceValue = LoadSprite("dl_tile_today");
            so.FindProperty("bigTileNormal").objectReferenceValue = LoadSprite("dl_tile_big");
            so.FindProperty("bigTileReady").objectReferenceValue = LoadSprite("dl_tile_big_today");
            so.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursive(root.transform, root.layer);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[Hadiah Login] {PrefabPath} dibuat.");
        } finally {
            Object.DestroyImmediate(root);
        }
    }

    // ---------------------------------------------------------------- Bagian

    private static void BuildHeader(RectTransform panel, SerializedObject so) {
        Picture("Header Icon", panel, "dl_header_icon", Pad, 30f, 92f, 92f);
        Text("Title", panel, "HADIAH LOGIN HARIAN", 50f, _bold, Color.white, TextAlignmentOptions.Left,
             156f, 28f, 1150f, 58f);
        TMP_Text subtitle = Text("Subtitle", panel, "Login tiap hari untuk menabung hadiah. Klaim sebelum hari ke-8!",
                                 27f, _medium, SubtitleColor, TextAlignmentOptions.Left, 156f, 88f, 1150f, 38f);
        subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 20f;
        subtitle.fontSizeMax = 27f;

        RectTransform close = Place(NewRect("Close Button", panel), PanelWidth - Pad - 92f, 26f, 92f, 92f);
        Image closeImage = close.gameObject.AddComponent<Image>();
        closeImage.sprite = LoadSprite("dl_close_button");
        Button closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
    }

    private static void BuildBanner(RectTransform panel, SerializedObject so) {
        RectTransform banner = Place(NewRect("Banner", panel), Pad, 148f, PanelWidth - Pad * 2f, 76f);
        Sliced(banner.gameObject, "dl_banner_reset");
        Picture("Banner Icon", banner, "icon_reset", 24f, 18f, 40f, 40f);
        TMP_Text text = Text("Banner Text", banner, "Tabungan hadiah: 50 G. Klaim sebelum hari ke-8, setelah itu hangus.",
                             27f, _bold, BannerColor, TextAlignmentOptions.Left, 82f, 0f, PanelWidth - Pad * 2f - 106f, 76f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = 27f;
        so.FindProperty("bannerText").objectReferenceValue = text;
    }

    private static void BuildTiles(RectTransform panel, SerializedObject so) {
        SerializedProperty tiles = so.FindProperty("tiles");
        tiles.arraySize = DailyLoginRules.CycleDays;
        for (int day = 1; day <= DailyLoginRules.CycleDays; day++) {
            bool big = day == DailyLoginRules.CycleDays;
            int index = day - 1;
            float x, y, w, h;
            if (big) {
                x = Pad + (TileWidth + TileGap) * 3f;
                y = GridTop;
                w = PanelWidth - Pad - x;
                h = TileHeight * 2f + TileGap;
            } else {
                x = Pad + (TileWidth + TileGap) * (index % 3);
                y = GridTop + (TileHeight + TileGap) * (index / 3);
                w = TileWidth;
                h = TileHeight;
            }

            RectTransform tile = Place(NewRect("Day " + day, panel), x, y, w, h);
            Image background = Sliced(tile.gameObject, big ? "dl_tile_big" : "dl_tile_normal");
            CanvasGroup group = tile.gameObject.AddComponent<CanvasGroup>();
            TMP_Text amount;
            GameObject badge;
            if (big) {
                Text("Day Label", tile, "Hari " + day, 34f, _bold, Color.white, TextAlignmentOptions.Center, 0f, 84f, w, 46f);
                float icon = 170f;
                Picture("Reward Icon", tile, "dl_icon_gift_big", (w - icon) * 0.5f, 146f, icon, icon);
                badge = Picture("Check Badge", tile, "dl_check_badge", (w + icon) * 0.5f - 46f, 140f, 56f, 56f).gameObject;
                Text("Big Title", tile, "Hadiah Besar", 44f, _bold, Color.white, TextAlignmentOptions.Center, 0f, 336f, w, 56f);
                amount = Text("Amount", tile, "500 G", 30f, _medium, BigSubColor, TextAlignmentOptions.Center, 0f, 398f, w, 44f);
            } else {
                Text("Day Label", tile, "Hari " + day, 30f, _bold, Color.white, TextAlignmentOptions.Center, 0f, 22f, w, 42f);
                float icon = 88f;
                Picture("Reward Icon", tile, "dl_icon_coin", (w - icon) * 0.5f, 76f, icon, icon);
                badge = Picture("Check Badge", tile, "dl_check_badge", (w + icon) * 0.5f - 30f, 68f, 46f, 46f).gameObject;
                amount = Text("Amount", tile, DailyLoginRules.Reward(day) + " G", 30f, _bold, Color.white,
                              TextAlignmentOptions.Center, 0f, 176f, w, 44f);
            }
            badge.SetActive(false);

            // Tag "HARI INI" menempel di tepi atas tile.
            RectTransform tag = NewRect("Today Tag", tile);
            tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 1f);
            tag.pivot = new Vector2(0.5f, 0.5f);
            tag.anchoredPosition = Vector2.zero;
            tag.sizeDelta = new Vector2(150f, 42f);
            Sliced(tag.gameObject, "dl_today_tag");
            TMP_Text tagText = Text("Today Tag Label", tag, "HARI INI", 22f, _bold, TagTextColor, TextAlignmentOptions.Center,
                                    0f, 0f, 150f, 42f);
            tagText.characterSpacing = 2f;
            tag.gameObject.SetActive(day == 1);

            SerializedProperty element = tiles.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("background").objectReferenceValue = background;
            element.FindPropertyRelative("group").objectReferenceValue = group;
            element.FindPropertyRelative("todayTag").objectReferenceValue = tag.gameObject;
            element.FindPropertyRelative("checkBadge").objectReferenceValue = badge;
            element.FindPropertyRelative("amount").objectReferenceValue = amount;
        }
    }

    private static void BuildClaimButton(RectTransform panel, SerializedObject so) {
        const float width = 660f, height = 112f;
        RectTransform rect = Place(NewRect("Claim Button", panel), (PanelWidth - width) * 0.5f, 808f, width, height);
        Image image = Sliced(rect.gameObject, "dl_button_klaim");
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.disabledColor = Color.white; // tombol nonaktif memakai sprite abu-abu sendiri
        button.colors = colors;

        // Isi di atas "bibir" bawah tombol.
        RectTransform content = Stretch(NewRect("Claim Content", rect), bottom: 14f);
        HorizontalLayoutGroup row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 16f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        Image icon = Picture("Claim Icon", content, "icon_klaim_gift", 0f, 0f, 44f, 44f);
        LayoutElement iconSize = icon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = iconSize.preferredHeight = 44f;
        TMP_Text label = Text("Claim Label", content, "KLAIM HADIAH HARI 1", 40f, _bold, Color.white,
                              TextAlignmentOptions.Center, 0f, 0f, 480f, 60f);
        label.textWrappingMode = TextWrappingModes.NoWrap;

        so.FindProperty("claimButton").objectReferenceValue = button;
        so.FindProperty("claimImage").objectReferenceValue = image;
        so.FindProperty("claimLabel").objectReferenceValue = label;
        so.FindProperty("claimIcon").objectReferenceValue = icon.gameObject;
        so.FindProperty("claimSprite").objectReferenceValue = LoadSprite("dl_button_klaim");
        so.FindProperty("claimDisabledSprite").objectReferenceValue = LoadSprite("dl_button_disabled");
    }

    private static void BuildToast(RectTransform panel, SerializedObject so) {
        const float width = 720f, height = 96f;
        RectTransform toast = Place(NewRect("Toast", panel), (PanelWidth - width) * 0.5f, 470f, width, height);
        Sliced(toast.gameObject, "dl_toast");
        TMP_Text text = Text("Toast Text", toast, "+50 G masuk ke saldo kamu!", 34f, _bold, Color.white,
                             TextAlignmentOptions.Center, 0f, 0f, width, height);
        toast.gameObject.SetActive(false);
        so.FindProperty("toast").objectReferenceValue = toast.gameObject;
        so.FindProperty("toastText").objectReferenceValue = text;
    }

    // ---------------------------------------------------------------- Helper

    private static Sprite LoadSprite(string name) {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
        if (sprite == null) Debug.LogWarning($"[Hadiah Login] Sprite {SpriteFolder}{name}.png tidak ditemukan.");
        return sprite;
    }

    private static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>Posisi dari pojok kiri atas parent (sama seperti RedesignUiKit.Place).</summary>
    private static RectTransform Place(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform Stretch(RectTransform rect, float bottom = 0f) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(0f, bottom);
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image Sliced(GameObject target, string sprite) {
        Image image = target.AddComponent<Image>();
        image.sprite = LoadSprite(sprite);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return image;
    }

    private static Image Picture(string name, Transform parent, string sprite, float x, float y, float w, float h) {
        RectTransform rect = Place(NewRect(name, parent), x, y, w, h);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = LoadSprite(sprite);
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(string name, Transform parent, string content, float size, TMP_FontAsset font,
                                 Color color, TextAlignmentOptions alignment, float x, float y, float w, float h) {
        RectTransform rect = Place(NewRect(name, parent), x, y, w, h);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.raycastTarget = false;
        return text;
    }

    private static void SetLayerRecursive(Transform t, int layer) {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursive(child, layer);
    }
}

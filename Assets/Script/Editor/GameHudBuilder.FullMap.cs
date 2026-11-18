using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Bagian HUD untuk peta kota layar penuh (GoVistaFullMap) dan area ketuk di atas radar untuk membukanya.
/// Tata letak: peta memenuhi layar (laut di luar hasil scan + ombak animasi), panel kaca gelap di kanan berisi
/// kartu tujuan dan daftar tempat penting, tombol zoom / posisiku melayang di tepi kanan peta.
/// Sprite (sudut bulat, lingkaran, ikon garis) ada di Assets/Art/Minimap/FullMap, di luar atlas HUD:
/// panel ini modal, jadi beberapa draw call tambahan tidak berpengaruh ke HUD saat bermain.
public static partial class GameHudBuilder {
    // Sama dengan Daftar Quest: di atas chat (52), di bawah layar mati (55). Keduanya tidak pernah terbuka bersamaan.
    private const int FullMapSortingOrder = 53;
    private const string FullMapArt = "Assets/Art/Minimap/FullMap/";
    private const string ShoreDataPath = "Assets/Art/Minimap/Generated/MinimapShore.asset";
    private const string WavesMaterialPath = FullMapArt + "MapShoreWaves.mat";

    private const float MapPanelWidth = 600f;
    private const float MapPanelMargin = 24f;
    private const float MapPanelPadding = 32f;
    private const float MapCardHeight = 196f;
    private const float MapRowHeight = 80f;
    private const float MapControlSize = 84f;

    private static readonly Color MapGlass = new Color32(14, 24, 42, 252); // hampir pejal: peta di belakang panel tidak membayang
    private static readonly Color MapGlassLight = new Color32(14, 24, 42, 214);
    private static readonly Color MapGlassBorder = new Color(1f, 1f, 1f, 0.09f);
    private static readonly Color MapSurface = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color MapMuted = new Color32(160, 178, 204, 255);
    private static readonly Color MapHint = new Color32(191, 235, 255, 210);
    private static readonly Color MapClear = new Color32(240, 84, 96, 255);
    // Cadangan kalau garis pantai belum di-bake: warna laut hasil scan saat ini (#22807F).
    private static readonly Color MapSeaFallback = new Color32(34, 128, 127, 255);

    private static void BuildFullMap(Transform root, RectTransform safeArea, Fonts fonts) {
        Canvas openCanvas;
        Button openButton = BuildMapOpenButton(safeArea, out openCanvas);

        RectTransform panelRoot = Stretch(NewRect("Full Map", root));
        var panelCanvas = panelRoot.gameObject.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = FullMapSortingOrder;
        panelRoot.gameObject.AddComponent<GraphicRaycaster>();
        var panelGroup = panelRoot.gameObject.AddComponent<CanvasGroup>();

        var shore = AssetDatabase.LoadAssetAtPath<MinimapShoreData>(ShoreDataPath);
        if (shore == null)
            Debug.LogWarning("[HUD] Garis pantai peta belum di-bake; peta penuh tanpa ombak. " +
                             "Jalankan \"Go Vista/Minimap/Bake garis pantai peta penuh\" lalu bangun ulang HUD.");

        // ---------------------------------------------------------------- Peta (layar penuh)
        // Viewport: area peta yang menerima seret, zoom, dan ketuk. Pivot di tengah (dipakai GoVistaFullMap).
        RectTransform viewport = Stretch(NewRect("Viewport", panelRoot));
        Image sea = viewport.gameObject.AddComponent<Image>();
        sea.color = shore != null ? shore.waterColor : MapSeaFallback; // laut di luar hasil scan
        sea.raycastTarget = true; // juga menahan sentuhan ke kontrol di belakang panel
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = Stretch(NewRect("Content", viewport));
        var map = NewRect("Map", content).gameObject.AddComponent<RawImage>();
        map.raycastTarget = false;
        Center(map.rectTransform, 0f, 0f, 1000f, 1000f);

        var waves = NewRect("Waves", content).gameObject.AddComponent<RawImage>();
        waves.raycastTarget = false;
        waves.material = EnsureWavesMaterial();
        if (shore != null) waves.texture = shore.distanceField;
        Center(waves.rectTransform, 0f, 0f, 1000f, 1000f);

        RectTransform overlay = Stretch(NewRect("Overlay", content));
        var route = NewRect("Route", overlay).gameObject.AddComponent<UiPolyline>();
        route.raycastTarget = false;
        Stretch(route.rectTransform);
        RectTransform icons = Stretch(NewRect("Icons", overlay));

        var template = NewRect("Icon Template", icons).gameObject.AddComponent<Image>();
        template.raycastTarget = false;
        template.preserveAspect = true;
        Center(template.rectTransform, 0f, 0f, 32f, 32f);

        // Tujuan quest: titik emas yang sama dengan marker quest di radar.
        var questDot = NewRect("Quest Target", icons).gameObject.AddComponent<Image>();
        questDot.sprite = MapSprite("ui_circle.png");
        questDot.color = WaypointGold;
        questDot.raycastTarget = false;
        Center(questDot.rectTransform, 0f, 0f, 30f, 30f);
        questDot.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.75f);

        // ---------------------------------------------------------------- Kontrol (dalam safe area)
        RectTransform safe = Stretch(NewRect("Safe Area", panelRoot));
        safe.gameObject.AddComponent<SafeAreaFitter>();

        RectTransform side = BuildMapSidePanel(safe, fonts, out SidePanelParts parts);

        // Tombol bulat di tepi kanan peta, tengah vertikal.
        float controlsRight = MapPanelMargin + MapPanelWidth + MapPanelMargin;
        RectTransform controls = NewRect("Map Controls", safe);
        controls.anchorMin = controls.anchorMax = controls.pivot = new Vector2(1f, 0.5f);
        controls.sizeDelta = new Vector2(MapControlSize, MapControlSize * 3f + 12f + 28f);
        controls.anchoredPosition = new Vector2(-controlsRight, 0f);
        Button zoomIn = MapRoundButton("Zoom In", controls, "icon_plus.png", 0f);
        Button zoomOut = MapRoundButton("Zoom Out", controls, "icon_minus.png", -(MapControlSize + 12f));
        Button center = MapRoundButton("Center Button", controls, "icon_locate.png", -(2f * MapControlSize + 40f));

        // Petunjuk gestur di kiri bawah peta.
        Image hintChip = MapRounded("Hint Chip", safe, MapGlassLight);
        hintChip.pixelsPerUnitMultiplier = 1.2f;
        RectTransform hintRect = hintChip.rectTransform;
        hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = Vector2.zero;
        hintRect.anchoredPosition = new Vector2(MapPanelMargin, MapPanelMargin);
        var hintLayout = hintChip.gameObject.AddComponent<HorizontalLayoutGroup>();
        hintLayout.padding = new RectOffset(26, 26, 14, 16);
        hintLayout.childControlWidth = hintLayout.childControlHeight = true;
        var hintFit = hintChip.gameObject.AddComponent<ContentSizeFitter>();
        hintFit.horizontalFit = hintFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Text("Hint", hintRect, fonts.Medium, "Seret: geser     Cubit / scroll: zoom     Ketuk: pasang tujuan", 24f, MapHint,
            TextAlignmentOptions.MidlineLeft);

        var view = viewport.gameObject.AddComponent<GoVistaFullMap>();
        Bind(view, ("panelCanvas", panelCanvas), ("panelGroup", panelGroup), ("seaBackground", sea), ("mapImage", map),
            ("wavesImage", waves), ("routeLine", route), ("iconLayer", icons), ("iconTemplate", template),
            ("questIcon", questDot.rectTransform), ("sidePanel", side), ("mapControls", controls),
            ("destinationIconBackground", parts.IconBackground), ("destinationIcon", parts.Icon),
            ("destinationCaption", parts.Caption), ("destinationTitle", parts.Title),
            ("destinationHint", parts.Hint),
            ("pinSprite", MapSprite("icon_pin.png")), ("flagSprite", MapSprite("icon_flag.png")),
            ("legendContent", parts.LegendContent), ("legendRowTemplate", parts.RowTemplate),
            ("openButtonCanvas", openCanvas), ("openButton", openButton), ("closeButton", parts.Close),
            ("clearButton", parts.Clear), ("centerButton", center), ("zoomInButton", zoomIn), ("zoomOutButton", zoomOut),
            ("shore", shore));
        panelCanvas.enabled = false;
    }

    private struct SidePanelParts {
        public Image IconBackground, Icon;
        public TextMeshProUGUI Caption, Title, Hint;
        public RectTransform LegendContent;
        public Button RowTemplate, Close, Clear;
    }

    private static RectTransform BuildMapSidePanel(RectTransform safe, Fonts fonts, out SidePanelParts parts) {
        parts = default;
        RectTransform side = NewRect("Side Panel", safe);
        side.anchorMin = new Vector2(1f, 0f);
        side.anchorMax = new Vector2(1f, 1f);
        side.pivot = new Vector2(1f, 0.5f);
        side.sizeDelta = new Vector2(MapPanelWidth, -2f * MapPanelMargin);
        side.anchoredPosition = new Vector2(-MapPanelMargin, 0f);

        Image shadow = MapSliced("Shadow", side, "ui_shadow.png", new Color(0f, 0f, 0f, 0.55f));
        Stretch(shadow.rectTransform, -28f, -22f, -28f, -34f);
        Image glass = MapRounded("Glass", side, MapGlass);
        glass.raycastTarget = true; // panel menahan sentuhan supaya tidak menggeser peta di belakangnya
        Stretch(glass.rectTransform);
        Image border = MapSliced("Border", side, "ui_rounded_outline.png", MapGlassBorder);
        Stretch(border.rectTransform);

        // Kepala: judul + tombol tutup.
        float inner = MapPanelWidth - 2f * MapPanelPadding;
        TextMeshProUGUI kicker = Text("Kicker", side, fonts.SemiBold, "GO VISTA", 22f, MapMuted, TextAlignmentOptions.BottomLeft);
        kicker.characterSpacing = 12f;
        TopLeft(kicker.rectTransform, MapPanelPadding, 26f, 300f, 30f);
        TextMeshProUGUI title = Text("Title", side, fonts.Bold, "Peta Kota", 50f, Color.white, TextAlignmentOptions.TopLeft);
        TopLeft(title.rectTransform, MapPanelPadding, 56f, 360f, 64f);

        Image closeBg = MapCircle("Close Button", side, MapSurface);
        RectTransform closeRect = closeBg.rectTransform;
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-MapPanelPadding + 6f, -30f);
        closeRect.sizeDelta = new Vector2(72f, 72f);
        MapIcon("Icon", closeRect, "icon_close.png", Color.white, 30f);
        parts.Close = MapPressButton(closeBg);

        // Kartu tujuan.
        Image card = MapRounded("Destination Card", side, MapSurface);
        RectTransform cardRect = TopLeft(card.rectTransform, MapPanelPadding, 140f, inner, MapCardHeight);

        parts.IconBackground = MapCircle("Icon Bubble", cardRect, new Color(1f, 1f, 1f, 0.15f));
        TopLeft(parts.IconBackground.rectTransform, 22f, 24f, 76f, 76f);
        parts.Icon = MapIcon("Icon", parts.IconBackground.rectTransform, "icon_pin.png", Color.white, 40f);

        const float textLeft = 22f + 76f + 20f;
        parts.Caption = Text("Caption", cardRect, fonts.SemiBold, "TUJUAN", 21f, MapMuted, TextAlignmentOptions.BottomLeft);
        parts.Caption.characterSpacing = 8f;
        TopLeft(parts.Caption.rectTransform, textLeft, 24f, inner - textLeft - 22f, 30f);
        parts.Title = Text("Title", cardRect, fonts.Bold, "Belum ada tujuan", 34f, Color.white, TextAlignmentOptions.TopLeft);
        parts.Title.overflowMode = TextOverflowModes.Ellipsis;
        parts.Title.enableAutoSizing = true; // nama tempat panjang mengecil dulu sebelum dipotong
        parts.Title.fontSizeMin = 26f;
        parts.Title.fontSizeMax = 34f;
        TopLeft(parts.Title.rectTransform, textLeft, 56f, inner - textLeft - 22f, 48f);

        Image divider = NewRect("Divider", cardRect).gameObject.AddComponent<Image>();
        divider.color = new Color(1f, 1f, 1f, 0.07f);
        divider.raycastTarget = false;
        TopLeft(divider.rectTransform, 22f, 120f, inner - 44f, 2f);

        // Baris bawah: jarak rute (teks kaya) atau petunjuk. GoVistaFullMap menyempitkannya saat tombol Hapus tampil.
        const float clearWidth = 124f;
        parts.Hint = Text("Hint", cardRect, fonts.Medium, "Ketuk peta atau pilih tempat di bawah", 22f, MapHint,
            TextAlignmentOptions.MidlineLeft);
        parts.Hint.overflowMode = TextOverflowModes.Ellipsis;
        parts.Hint.richText = true;
        TopLeft(parts.Hint.rectTransform, 22f, 126f, inner - 44f, 66f);

        Image clearBg = MapRounded("Clear Button", cardRect, new Color(MapClear.r, MapClear.g, MapClear.b, 0.18f));
        clearBg.pixelsPerUnitMultiplier = 1.1f;
        RectTransform clearRect = clearBg.rectTransform;
        clearRect.anchorMin = clearRect.anchorMax = clearRect.pivot = new Vector2(1f, 0f);
        clearRect.anchoredPosition = new Vector2(-18f, 16f);
        clearRect.sizeDelta = new Vector2(clearWidth, 50f);
        TextMeshProUGUI clearLabel = Text("Label", clearRect, fonts.Bold, "Hapus", 24f, MapClear, TextAlignmentOptions.Center);
        Stretch(clearLabel.rectTransform, 0f, 0f, 0f, 3f);
        parts.Clear = MapPressButton(clearBg);
        clearBg.gameObject.SetActive(false);

        // Daftar tempat penting.
        const float listTop = 140f + MapCardHeight + 34f;
        TextMeshProUGUI section = Text("Section", side, fonts.SemiBold, "TEMPAT PENTING", 21f, MapMuted,
            TextAlignmentOptions.BottomLeft);
        section.characterSpacing = 8f;
        TopLeft(section.rectTransform, MapPanelPadding, listTop, inner, 30f);

        RectTransform scrollRect = Stretch(NewRect("Places", side), MapPanelPadding, listTop + 44f, MapPanelPadding - 8f,
            MapPanelPadding - 8f);
        scrollRect.gameObject.AddComponent<RectMask2D>();
        Image scrollHit = scrollRect.gameObject.AddComponent<Image>();
        scrollHit.color = Transparent; // supaya area kosong di antara baris tetap bisa digulir
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        scroll.viewport = scrollRect;

        RectTransform list = NewRect("Content", scrollRect);
        list.anchorMin = new Vector2(0f, 1f);
        list.anchorMax = new Vector2(1f, 1f);
        list.pivot = new Vector2(0.5f, 1f);
        list.sizeDelta = new Vector2(-8f, 0f); // ruang di kanan untuk efek tekan
        list.anchoredPosition = new Vector2(-4f, 0f);
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = list;
        parts.LegendContent = list;

        Image row = MapRounded("Row Template", list, MapSurface);
        row.pixelsPerUnitMultiplier = 1.3f;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = MapRowHeight;
        Image rowIcon = NewRect("Icon", row.rectTransform).gameObject.AddComponent<Image>();
        rowIcon.preserveAspect = true;
        rowIcon.raycastTarget = false;
        RectTransform rowIconRect = rowIcon.rectTransform;
        rowIconRect.anchorMin = rowIconRect.anchorMax = rowIconRect.pivot = new Vector2(0f, 0.5f);
        rowIconRect.anchoredPosition = new Vector2(18f, 0f);
        rowIconRect.sizeDelta = new Vector2(50f, 50f);
        TextMeshProUGUI rowLabel = Text("Label", row.rectTransform, fonts.SemiBold, "Tempat", 29f, Color.white,
            TextAlignmentOptions.MidlineLeft);
        rowLabel.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(rowLabel.rectTransform, 86f, 0f, 56f, 2f);
        Image chevron = MapIcon("Chevron", row.rectTransform, "icon_chevron.png", new Color(1f, 1f, 1f, 0.45f), 24f);
        RectTransform chevronRect = chevron.rectTransform;
        chevronRect.anchorMin = chevronRect.anchorMax = new Vector2(1f, 0.5f);
        chevronRect.anchoredPosition = new Vector2(-30f, 0f);
        parts.RowTemplate = MapPressButton(row, 0.97f);
        return side;
    }

    /// Area ketuk transparan di atas lingkaran radar (minimap kiri atas).
    /// Punya Canvas sendiri karena grup minimap tidak menerima sentuhan.
    private static Button BuildMapOpenButton(RectTransform safeArea, out Canvas canvas) {
        RectTransform rect = TopLeft(NewRect("Map Open Button", safeArea), HudLayout.MinimapLeft, HudLayout.MinimapTop,
            HudLayout.MinimapSize, HudLayout.MinimapSize);
        canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = HudSortingOrder;
        rect.gameObject.AddComponent<GraphicRaycaster>();

        var hit = NewRect("Hit Area", rect).gameObject.AddComponent<Image>();
        hit.color = Transparent;
        Center(hit.rectTransform, 0f, 0f, RadarDiameter, RadarDiameter);

        // Lencana "perbesar" di bawah radar dihapus (2026-11-18); radar tetap bisa diketuk untuk membuka peta.
        return PlainButton(hit);
    }

    // ------------------------------------------------------------ Pembantu peta penuh

    private static Button MapRoundButton(string name, RectTransform parent, string icon, float y) {
        Image shadow = MapSliced(name + " Shadow", parent, "ui_shadow.png", new Color(0f, 0f, 0f, 0.45f));
        RectTransform shadowRect = shadow.rectTransform;
        shadowRect.anchorMin = shadowRect.anchorMax = shadowRect.pivot = new Vector2(0.5f, 1f);
        shadowRect.anchoredPosition = new Vector2(0f, y + 18f);
        shadowRect.sizeDelta = new Vector2(MapControlSize + 40f, MapControlSize + 44f);
        shadow.pixelsPerUnitMultiplier = 1.4f;

        Image circle = MapCircle(name, parent, MapGlass);
        RectTransform rect = circle.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(MapControlSize, MapControlSize);
        MapIcon("Icon", rect, icon, Color.white, 36f);
        return MapPressButton(circle);
    }

    private static Button MapPressButton(Image target, float pressedScale = 0.9f) {
        target.raycastTarget = true;
        Button button = target.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = target;
        ColorBlock colors = button.colors;
        colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        target.gameObject.AddComponent<UiPressScale>().Configure(target.rectTransform, pressedScale);
        return button;
    }

    private static Image MapIcon(string name, RectTransform parent, string sprite, Color color, float size) {
        Image icon = NewRect(name, parent).gameObject.AddComponent<Image>();
        icon.sprite = MapSprite(sprite);
        icon.color = color;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Center(icon.rectTransform, 0f, 0f, size, size);
        return icon;
    }

    private static Image MapCircle(string name, Transform parent, Color color) {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = MapSprite("ui_circle.png");
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image MapRounded(string name, Transform parent, Color color) =>
        MapSliced(name, parent, "ui_rounded.png", color);

    private static Image MapSliced(string name, Transform parent, string sprite, Color color) {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = MapSprite(sprite);
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// Sprite dari Assets/Art/Minimap/FullMap. Import diatur di sini (sprite, tanpa mipmap, border 9-slice).
    private static Sprite MapSprite(string file) {
        string path = FullMapArt + file;
        if (AssetImporter.GetAtPath(path) is TextureImporter importer) {
            Vector4 border = file == "ui_shadow.png" ? new Vector4(52f, 52f, 52f, 52f)
                : file.StartsWith("ui_rounded") ? new Vector4(28f, 28f, 28f, 28f)
                : Vector4.zero;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border ||
                importer.mipmapEnabled) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogError($"[HUD] Sprite tidak ditemukan: {path}");
        return sprite;
    }

    private static Material EnsureWavesMaterial() {
        var material = AssetDatabase.LoadAssetAtPath<Material>(WavesMaterialPath);
        if (material != null) return material;
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(FullMapArt + "MapShoreWaves.shader");
        if (shader == null) {
            Debug.LogError("[HUD] Shader ombak peta tidak ditemukan.");
            return null;
        }
        material = new Material(shader) { name = "MapShoreWaves" };
        AssetDatabase.CreateAsset(material, WavesMaterialPath);
        return material;
    }
}

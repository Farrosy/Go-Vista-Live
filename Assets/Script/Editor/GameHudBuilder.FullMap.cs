using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Bagian HUD untuk peta kota layar penuh (GoVistaFullMap) dan area ketuk di atas radar untuk membukanya.
/// Panel modal seperti Daftar Quest: kartu besar, peta di tengah, info tujuan + tombol di bawah.
public static partial class GameHudBuilder {
    // Sama dengan Daftar Quest: di atas chat (52), di bawah layar mati (55). Keduanya tidak pernah terbuka bersamaan.
    private const int FullMapSortingOrder = 53;
    private const float MapCardMarginX = 60f;
    private const float MapCardMarginY = 36f;
    private const float MapHeaderHeight = 104f;
    private const float MapFooterHeight = 116f;
    private const float MapPadding = 28f;
    private const float MapButtonWidth = 270f;
    private const float MapZoomButtonWidth = 96f;

    private static readonly Color MapBackdrop = new Color32(6, 10, 22, 200);
    private static readonly Color MapWater = new Color32(18, 44, 78, 255);
    private static readonly Color MapHint = new Color32(191, 235, 255, 200);

    private static void BuildFullMap(Transform root, RectTransform safeArea, Fonts fonts) {
        Canvas openCanvas;
        Button openButton = BuildMapOpenButton(safeArea, out openCanvas);

        RectTransform panelRoot = Stretch(NewRect("Full Map", root));
        var panelCanvas = panelRoot.gameObject.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = FullMapSortingOrder;
        panelRoot.gameObject.AddComponent<GraphicRaycaster>();
        Image backdrop = panelRoot.gameObject.AddComponent<Image>();
        backdrop.color = MapBackdrop;
        backdrop.raycastTarget = true; // menahan sentuhan ke kontrol di belakang panel

        Image card = SlicedImage("Card", panelRoot, QuestFolder + "quest_panel_bg.png");
        card.raycastTarget = true;
        Stretch(card.rectTransform, MapCardMarginX, MapCardMarginY, MapCardMarginX, MapCardMarginY);
        RectTransform cardRect = card.rectTransform;

        // Font HUD memakai atlas statis (ASCII + "−…"), jadi teks di sini hanya memakai karakter itu.
        TextMeshProUGUI title = Text("Title", cardRect, fonts.Bold, "Peta Kota", 38f, Color.white, TextAlignmentOptions.MidlineLeft);
        TopLeft(title.rectTransform, 40f, 0f, 300f, MapHeaderHeight);
        TextMeshProUGUI hint = Text("Hint", cardRect, fonts.Medium,
            "Seret: geser   |   Cubit / scroll: zoom   |   Ketuk peta: pasang / hapus tujuan", 24f, MapHint,
            TextAlignmentOptions.MidlineLeft);
        TopLeft(hint.rectTransform, 260f, 0f, 1200f, MapHeaderHeight);

        Image close = PlainImage("Close Button", cardRect, QuestFolder + "quest_close_button.png");
        close.raycastTarget = true;
        close.rectTransform.anchorMin = close.rectTransform.anchorMax = close.rectTransform.pivot = new Vector2(1f, 1f);
        close.rectTransform.anchoredPosition = new Vector2(-24f, -(MapHeaderHeight - 72f) * 0.5f);
        close.rectTransform.sizeDelta = new Vector2(72f, 72f);
        Button closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;

        // Viewport: area peta yang menerima seret, zoom, dan ketuk. Pivot di tengah (dipakai GoVistaFullMap).
        RectTransform viewport = Stretch(NewRect("Viewport", cardRect), MapPadding, MapHeaderHeight, MapPadding, MapFooterHeight);
        Image viewportHit = viewport.gameObject.AddComponent<Image>();
        viewportHit.color = MapWater; // di luar hasil scan
        viewportHit.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var map = NewRect("Map", viewport).gameObject.AddComponent<RawImage>();
        map.raycastTarget = false;
        Center(map.rectTransform, 0f, 0f, 1000f, 1000f);

        RectTransform overlay = Stretch(NewRect("Overlay", viewport));
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
        questDot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        questDot.color = WaypointGold;
        questDot.raycastTarget = false;
        Center(questDot.rectTransform, 0f, 0f, 30f, 30f);
        questDot.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.75f);

        RectTransform zoom = NewRect("Zoom", viewport);
        zoom.anchorMin = zoom.anchorMax = zoom.pivot = new Vector2(1f, 0f);
        zoom.anchoredPosition = new Vector2(-16f, 16f);
        zoom.sizeDelta = new Vector2(MapZoomButtonWidth, 210f);
        Button zoomIn = MapButton("Zoom In", zoom, fonts, "+", 44f, MapZoomButtonWidth, out RectTransform zoomInRect);
        zoomInRect.anchorMin = zoomInRect.anchorMax = zoomInRect.pivot = new Vector2(0.5f, 1f);
        zoomInRect.anchoredPosition = Vector2.zero;
        Button zoomOut = MapButton("Zoom Out", zoom, fonts, "−", 44f, MapZoomButtonWidth, out RectTransform zoomOutRect);
        zoomOutRect.anchorMin = zoomOutRect.anchorMax = zoomOutRect.pivot = new Vector2(0.5f, 0f);
        zoomOutRect.anchoredPosition = Vector2.zero;

        RectTransform footer = NewRect("Footer", cardRect);
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.offsetMin = new Vector2(MapPadding + 12f, 0f);
        footer.offsetMax = new Vector2(-MapPadding, MapFooterHeight);

        TextMeshProUGUI info = Text("Info", footer, fonts.Bold, "Ketuk peta untuk memasang tujuan", 30f, Color.white,
            TextAlignmentOptions.MidlineLeft);
        Stretch(info.rectTransform, 0f, 0f, 2f * MapButtonWidth + 40f, 0f);
        Button clear = MapButton("Clear Button", footer, fonts, "Hapus Tujuan", 28f, MapButtonWidth, out RectTransform clearRect);
        clearRect.anchorMin = clearRect.anchorMax = clearRect.pivot = new Vector2(1f, 0.5f);
        clearRect.anchoredPosition = new Vector2(0f, 4f);
        Button center = MapButton("Center Button", footer, fonts, "Posisiku", 28f, MapButtonWidth, out RectTransform centerRect);
        centerRect.anchorMin = centerRect.anchorMax = centerRect.pivot = new Vector2(1f, 0.5f);
        centerRect.anchoredPosition = new Vector2(-MapButtonWidth - 20f, 4f);

        var view = viewport.gameObject.AddComponent<GoVistaFullMap>();
        Bind(view, ("panelCanvas", panelCanvas), ("mapImage", map), ("routeLine", route), ("iconLayer", icons),
            ("iconTemplate", template), ("questIcon", questDot.rectTransform), ("infoLabel", info),
            ("openButtonCanvas", openCanvas), ("openButton", openButton), ("closeButton", closeButton),
            ("clearButton", clear), ("centerButton", center), ("zoomInButton", zoomIn), ("zoomOutButton", zoomOut));
        panelCanvas.enabled = false;
    }

    /// Area ketuk transparan di atas lingkaran radar (minimap kiri atas). Punya Canvas sendiri karena grup
    /// minimap tidak menerima sentuhan.
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
        return PlainButton(hit);
    }

    private static Button MapButton(string name, RectTransform parent, Fonts fonts, string text, float size, float width,
        out RectTransform rect) {
        const string spriteBase = "Buttons/btn_secondary";
        Image image = SlicedImage(name, parent, spriteBase + ".png");
        image.raycastTarget = true;
        rect = image.rectTransform;
        rect.sizeDelta = new Vector2(width, SpriteHeight(image));
        var button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.SpriteSwap;
        button.targetGraphic = image;
        button.spriteState = new SpriteState {
            pressedSprite = Load(spriteBase + "_pressed.png"),
            disabledSprite = Load(spriteBase + "_disabled.png"),
        };
        TextMeshProUGUI label = Text("Label", rect, fonts.Bold, text, size, Color.white, TextAlignmentOptions.Midline);
        Stretch(label.rectTransform, 12f, 0f, 12f, 7f);
        return button;
    }
}

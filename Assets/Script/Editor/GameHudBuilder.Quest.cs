using TMPro;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.UI;

/// Bagian quest dari GameHudCanvas: tombol Quest (di bawah koin dan tombol pause, rata kanan dengan pause)
/// dan panel modal Daftar Quest. Aset dari Assets/GoVista_HUD_Assets/Quest.
public static partial class GameHudBuilder {
    // Di atas chat (52) supaya panel modal menggelapkan semua HUD, di bawah layar mati (55).
    private const int QuestPanelSortingOrder = 53;
    private const string QuestFolder = "Quest/";

    // Tombol pause di RoomWaitingCanvas: 96x96, 30 px dari kanan atas.
    private const float QuestButtonRight = 30f;
    private const float QuestButtonTop = 140f;
    private const float QuestButtonWidth = 200f;
    private const float QuestButtonHeight = 72f;
    private const float QuestBadgeSize = 40f;

    private const float CardWidth = 780f;
    private const float CardHeaderHeight = 110f;
    private const float CardPadding = 28f;
    private const float RowHeaderHeight = 80f;

    private static readonly Color QuestBackdrop = new Color32(6, 10, 22, 150);
    private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

    private static void BuildQuest(Transform root, RectTransform safeArea, Fonts fonts) {
        RectTransform group = new GameObject("Quest", typeof(RectTransform)).GetComponent<RectTransform>();
        group.SetParent(root, false);
        Stretch(group);

        (Canvas buttonCanvas, Button openButton, RectTransform badge) = BuildQuestButton(safeArea, fonts);
        RectTransform panelRoot = Stretch(NewRect("Quest Panel", group));
        var panelCanvas = panelRoot.gameObject.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = QuestPanelSortingOrder;
        panelRoot.gameObject.AddComponent<GraphicRaycaster>();

        Image backdrop = panelRoot.gameObject.AddComponent<Image>();
        backdrop.color = QuestBackdrop;
        backdrop.raycastTarget = true; // menahan sentuhan ke kontrol di belakang panel
        Button backdropButton = PlainButton(backdrop);

        Image card = SlicedImage("Card", panelRoot, QuestFolder + "quest_panel_bg.png");
        card.raycastTarget = true; // ketuk di dalam kartu tidak menutup panel
        Center(card.rectTransform, 0f, 0f, CardWidth, 400f);

        TextMeshProUGUI title = Text("Title", card.rectTransform, fonts.Bold, "Daftar Quest", 38f, Color.white,
            TextAlignmentOptions.MidlineLeft);
        TopLeft(title.rectTransform, 40f, 0f, 500f, CardHeaderHeight);

        Image close = PlainImage("Close Button", card.rectTransform, QuestFolder + "quest_close_button.png");
        close.raycastTarget = true;
        close.rectTransform.anchorMin = close.rectTransform.anchorMax = close.rectTransform.pivot = new Vector2(1f, 1f);
        close.rectTransform.anchoredPosition = new Vector2(-24f, -(CardHeaderHeight - 72f) * 0.5f);
        close.rectTransform.sizeDelta = new Vector2(72f, 72f);
        Button closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;

        RectTransform viewport = Stretch(NewRect("Viewport", card.rectTransform), CardPadding, CardHeaderHeight, CardPadding, CardPadding);
        Image viewportHit = viewport.gameObject.AddComponent<Image>();
        viewportHit.color = Transparent; // area geser daftar
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 14f;
        contentLayout.childControlWidth = contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = card.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        TextMeshProUGUI empty = Text("Empty Label", viewport, fonts.Medium,
            "Belum ada quest.\nBicara dengan NPC untuk mendapatkan quest.", 26f, TextBlue, TextAlignmentOptions.Center);
        empty.textWrappingMode = TextWrappingModes.Normal;
        Stretch(empty.rectTransform);

        QuestRowView row = BuildQuestRow(content, fonts);

        var view = group.gameObject.AddComponent<QuestHudView>();
        Bind(view, ("buttonCanvas", buttonCanvas), ("openButton", openButton), ("badge", badge),
            ("panelCanvas", panelCanvas), ("backdropButton", backdropButton), ("closeButton", closeButton),
            ("card", card.rectTransform), ("listContent", content), ("scroll", scroll),
            ("rowTemplate", row), ("emptyLabel", empty.gameObject));
        SetFloat(view, "headerHeight", CardHeaderHeight);
        SetFloat(view, "bottomPadding", CardPadding);
    }

    private static (Canvas, Button, RectTransform) BuildQuestButton(RectTransform safeArea, Fonts fonts) {
        RectTransform rect = NewRect("Quest Button", safeArea);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-QuestButtonRight, -QuestButtonTop);
        rect.sizeDelta = new Vector2(QuestButtonWidth, QuestButtonHeight);
        var canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = HudSortingOrder;
        rect.gameObject.AddComponent<GraphicRaycaster>();

        Image background = SlicedImage("Background", rect, QuestFolder + "quest_button_bg.png");
        Stretch(background.rectTransform);
        background.raycastTarget = true;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        Image icon = PlainImage("Icon", rect, QuestFolder + "icon_quest.png");
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(24f, 0f);
        icon.rectTransform.sizeDelta = new Vector2(44f, 44f);

        TextMeshProUGUI label = Text("Label", rect, fonts.Bold, "Quest", 30f, Color.white, TextAlignmentOptions.MidlineLeft);
        Stretch(label.rectTransform, 80f, 0f, 12f, 4f);

        Image badge = PlainImage("New Badge", rect, QuestFolder + "quest_badge.png");
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 1f);
        badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        badge.rectTransform.anchoredPosition = new Vector2(2f, -2f);
        badge.rectTransform.sizeDelta = new Vector2(QuestBadgeSize, QuestBadgeSize);
        TextMeshProUGUI mark = Text("Mark", badge.rectTransform, fonts.Bold, "!", 28f, Color.white, TextAlignmentOptions.Midline);
        Stretch(mark.rectTransform, 0f, 0f, 0f, 2f);

        return (canvas, button, badge.rectTransform);
    }

    private static QuestRowView BuildQuestRow(RectTransform content, Fonts fonts) {
        Image background = SlicedImage("Quest Row Template", content, QuestFolder + "quest_row_normal.png");
        var layout = background.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 8, 0, 0);
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        RectTransform header = NewRect("Header", background.rectTransform);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeaderHeight;
        Image headerHit = header.gameObject.AddComponent<Image>();
        headerHit.color = Transparent;
        Button toggle = PlainButton(headerHit);

        TextMeshProUGUI title = Text("Title", header, fonts.SemiBold, "Quest", 30f, Color.white, TextAlignmentOptions.MidlineLeft);
        title.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(title.rectTransform, 0f, 0f, RowHeaderHeight + 4f, 2f);

        RectTransform pin = NewRect("Pin Button", header);
        pin.anchorMin = pin.anchorMax = pin.pivot = new Vector2(1f, 0.5f);
        pin.anchoredPosition = Vector2.zero;
        pin.sizeDelta = new Vector2(RowHeaderHeight, RowHeaderHeight);
        Image pinHit = pin.gameObject.AddComponent<Image>();
        pinHit.color = Transparent;
        Button pinButton = PlainButton(pinHit);
        Image pinIcon = PlainImage("Icon", pin, QuestFolder + "icon_pin_off.png");
        Center(pinIcon.rectTransform, 0f, 0f, 44f, 44f);

        RectTransform details = NewRect("Details", background.rectTransform);
        var detailsLayout = details.gameObject.AddComponent<VerticalLayoutGroup>();
        detailsLayout.padding = new RectOffset(0, 20, 0, 0);
        detailsLayout.spacing = 12f;
        detailsLayout.childControlWidth = detailsLayout.childControlHeight = true;
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;
        TextMeshProUGUI description = WrappedText("Description", details, fonts.Medium, 24f, TextBlue);
        TextMeshProUGUI objectives = WrappedText("Objectives", details, fonts.Medium, 24f, Color.white);
        objectives.lineSpacing = 12f;
        details.gameObject.SetActive(false);

        var view = background.gameObject.AddComponent<QuestRowView>();
        Bind(view, ("background", background), ("normalSprite", Load(QuestFolder + "quest_row_normal.png")),
            ("pinnedSprite", Load(QuestFolder + "quest_row_pinned.png")), ("title", title), ("toggleButton", toggle),
            ("pinButton", pinButton), ("pinIcon", pinIcon), ("pinOn", Load(QuestFolder + "icon_pin_on.png")),
            ("pinOff", Load(QuestFolder + "icon_pin_off.png")), ("details", details.gameObject),
            ("description", description), ("objectives", objectives), ("layout", layout));
        background.gameObject.SetActive(false); // template, disalin QuestHudView per quest
        return view;
    }

    private static TextMeshProUGUI WrappedText(string name, Transform parent, TMP_FontAsset font, float size, Color color) {
        TextMeshProUGUI text = Text(name, parent, font, "", size, color, TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.richText = true;
        return text;
    }

    /// Tombol tanpa efek warna, untuk area sentuh transparan.
    private static Button PlainButton(Image target) {
        target.raycastTarget = true;
        Button button = target.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = target;
        return button;
    }

    /// Atlas HUD dibuat sebelum folder Quest ada; tambahkan folder itu sekali saja.
    /// Folder Minimap sengaja TIDAK di-pack: atlas membuang tepi transparan sprite, sehingga titik pusat busur
    /// Filled Radial360 bergeser dan busur HP/O₂ terpotong.
    private static void EnsureQuestSpritesInAtlas() {
        string questGuid = AssetDatabase.AssetPathToGUID(AssetRoot + "Quest");
        // SpriteAtlasAsset tidak membuka daftar packable, jadi cek GUID folder di file atlas.
        if (string.IsNullOrEmpty(questGuid) || !System.IO.File.Exists(AtlasPath) ||
            System.IO.File.ReadAllText(AtlasPath).Contains(questGuid)) return;
        SpriteAtlasAsset atlas = SpriteAtlasAsset.Load(AtlasPath);
        if (atlas == null) return;
        atlas.Add(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(AssetRoot + "Quest") });
        SpriteAtlasAsset.Save(atlas, AtlasPath);
        ((SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath)).SaveAndReimport();
    }
}

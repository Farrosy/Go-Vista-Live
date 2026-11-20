using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Satu tab rail kiri editor.</summary>
public sealed class EditorTabRefs {
    public Button Button;
    public GameObject Selected;
    public Image Icon;
    public TextMeshProUGUI Label;
}

/// <summary>Referensi UI editor karakter (mockup v2 layar 2), diisi CustomizerEditorView.Build.</summary>
public sealed class CustomizerEditorRefs {
    public GameObject Root;
    public Button BackButton;
    public Button UndoButton;
    public Button SaveButton;
    public TextMeshProUGUI NameText;
    public GameObject DirtyPill;
    public RawImage Preview;
    /// <summary>Area tengah di antara rail dan panel kanan (mode ruang ganti: karakter dibidik ke tengah area ini).</summary>
    public RectTransform StageArea;
    public GameObject Loading;
    public Button BodyCamButton;
    public GameObject BodyCamOn;
    public Button FaceCamButton;
    public GameObject FaceCamOn;
    public EditorTabRefs[] Tabs;

    public Image HeaderIcon;
    public TextMeshProUGUI HeaderTitle;
    public RectTransform SubTabRow;
    public ScrollRect SubTabScroll;
    public GameObject OutfitPanel;
    public RectTransform GridContent;
    public ScrollRect GridScroll;
    public ShapePanelRefs Shapes;
    public StickerPanelRefs Stickers;

    public GameObject ColorSection;
    public TextMeshProUGUI ColorTitle;
    public Button ColorModeButton;
    public GameObject ColorModeOn;
    public Button PatternModeButton;
    public GameObject PatternModeOn;
    public CanvasGroup PatternModeGroup;
    public GameObject ColorArea;
    public PatternAreaRefs Pattern;
    public RectTransform ChannelList;
    public ScrollRect ChannelScroll;
    public HsvColorPicker Picker;
    public Button MatchButton;
    public TextMeshProUGUI MatchLabel;
    public Button KeepColorsButton;
    public GameObject KeepColorsOn;

    public GameObject MatchPopup;
    public RectTransform MatchList;
    public ColorPopupRefs ColorPopup;
    public CustomizerUi.ConfirmDialog Discard;
    /// <summary>Pilih slot yang ditimpa saat slot penuh (ruang ganti): Cancel = Karakter 1, Confirm = Karakter 2.</summary>
    public CustomizerUi.ConfirmDialog Overwrite;
    public Button OverwriteClose;
    public TextMeshProUGUI Toast;
}

/// <summary>
/// Layar editor karakter (mockup v2 layar 2), layar penuh di scene KARAKTER: bar atas (KEMBALI, judul,
/// URUNGKAN · SIMPAN), rail kategori kiri, karakter 3D di tengah, panel kanan (sub-tab, grid
/// item, semua bagian warna + HSV picker). Isi dinamis (chip, item, bagian warna) dibuat controller
/// lewat fungsi Create* di bawah.
/// </summary>
public static class CustomizerEditorView {
    private const float Edge = 48f;
    private const float TopBarY = 32f;
    private const float TopBarHeight = 76f;
    private const float ContentTop = 128f;
    private const float ContentBottom = 32f;
    private const float RailWidth = 156f;
    private const float SideWidth = 720f;
    private const float Gap = 24f;
    private const float Pad = 28f;
    private const float InnerWidth = SideWidth - Pad * 2f;
    private const float HeaderHeight = 60f;
    private const float SubTabY = Pad + HeaderHeight + 16f;
    private const float ChipHeight = 48f;
    private const float GridTop = SubTabY + ChipHeight + 16f;
    private const float ColorHeight = 330f;
    private const float ChannelWidth = 210f;
    private const float ScrollbarWidth = 6f;
    /// Jarak scrollbar dari tepi kanan grid (masih di dalam padding panel).
    private const float ScrollbarGap = 10f;
    private const float MoreHintHeight = 70f;
    private const float ColorAreaHeight = 214f;
    /// Pudar penanda scroll di daftar bagian warna (lebih pendek dari grid karena areanya cuma 214px).
    private const float ChannelHintHeight = 40f;
    /// Lebar pudar + panah ▶ di kanan baris sub-tab (tampil selama masih ada chip di kanan).
    private const float SubTabHintWidth = 64f;
    private const int GridColumns = 5;
    private const float GridGap = 10f;
    public const float ItemSize = (InnerWidth - GridGap * (GridColumns - 1)) / GridColumns;

    private static readonly Color DirtyFill = new Color32(0x2E, 0x26, 0x0E, 0xFF);

    /// <param name="categories">Tab rail kiri; null = EditorCategories.All (editor lobby).</param>
    /// <param name="inWorld">Mode ruang ganti: tanpa latar gradasi, karakter tampil di dalam toko (kamera
    /// panggung merender langsung ke layar di belakang UI ini).</param>
    public static CustomizerEditorRefs Build(Transform canvas, ChatUiAssets a, IReadOnlyList<EditorCategory> categories = null,
                                             bool inWorld = false) {
        categories ??= EditorCategories.All;
        var refs = new CustomizerEditorRefs();
        RectTransform root = ChatUiKit.Stretch(ChatUiKit.CreateRect("Customizer Editor View", canvas), 0f, 0f, 0f, 0f);
        refs.Root = root.gameObject;
        if (inWorld) CreateClickBlocker(root);
        else CustomizerUi.CreateStageBackground(root, a);
        RectTransform safe = ChatUiKit.Stretch(ChatUiKit.CreateRect("Safe Area", root), 0f, 0f, 0f, 0f);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        BuildTopBar(safe, a, refs, inWorld ? "RUANG GANTI" : "UBAH KARAKTER");
        BuildRail(safe, a, refs, categories);
        BuildStage(safe, a, refs);
        BuildSidePanel(safe, a, refs);
        refs.Discard = CustomizerUi.CreateConfirmDialog(root, a, "Buang perubahan?",
            "Perubahan yang belum disimpan akan hilang.", "BATAL", "BUANG");
        refs.Overwrite = CustomizerUi.CreateConfirmDialog(root, a, "Slot karakter penuh",
            "Pilih karakter yang ditimpa dengan penampilan ini.", CustomSlotRules.SlotName(0).ToUpperInvariant(),
            CustomSlotRules.SlotName(1).ToUpperInvariant());
        refs.OverwriteClose = FriendsUiKit.CreateOutlineButton("Close", refs.Overwrite.Title.rectTransform.parent, a, "",
                                                               Color.white, FriendsTheme.NeutralFill, a.closeIcon, 28);
        ChatUiKit.PlaceTopRight((RectTransform)refs.OverwriteClose.transform, 24f, 24f, 64f, 64f);
        BuildMatchPopup(root, a, refs);
        refs.ColorPopup = CustomizerEditorPanels.BuildColorPopup(root, a);
        refs.Toast = CustomizerUi.CreateToast(root, a);
        UiTextFit.ApplyAll(root);
        root.gameObject.SetActive(false);
        return refs;
    }

    // ---------------------------------------------------------------- Bar atas

    /// Latar transparan selebar layar: menahan klik ke HUD/dunia di belakang, tapi toko tetap terlihat.
    private static void CreateClickBlocker(RectTransform root) {
        RectTransform blocker = ChatUiKit.Stretch(ChatUiKit.CreateRect("Click Blocker", root), 0f, 0f, 0f, 0f);
        blocker.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
    }

    private static void BuildTopBar(RectTransform parent, ChatUiAssets a, CustomizerEditorRefs refs, string titleText) {
        refs.BackButton = CustomizerUi.CreateBackButton(parent, a, Edge, TopBarY, TopBarHeight);
        float x = Edge + TopBarHeight + 20f;
        TextMeshProUGUI title = ChatUiKit.CreateText("Title", parent, a.font, titleText, FriendsTheme.TitleSize - 6,
                                                     Color.white, FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, x, TopBarY + 6f, 420f, 64f);

        RectTransform name = FriendsUiKit.CreatePill(parent, a, "Karakter 1 · Anime Cowok", FriendsTheme.Card,
                                                     ProfileTheme.Tag, FriendsTheme.SmallSize - 2, 44f,
                                                     new Color(1f, 1f, 1f, 0.2f));
        name.anchorMin = name.anchorMax = name.pivot = new Vector2(0f, 1f);
        name.anchoredPosition = new Vector2(x + 430f, -(TopBarY + 16f));
        name.sizeDelta = new Vector2(330f, 44f);
        refs.NameText = name.GetComponentInChildren<TextMeshProUGUI>();

        RectTransform dirty = FriendsUiKit.CreatePill(parent, a, "Belum disimpan", DirtyFill, ProfileTheme.Warning,
                                                      FriendsTheme.SmallSize - 2, 44f, ProfileTheme.Warning);
        dirty.anchorMin = dirty.anchorMax = dirty.pivot = new Vector2(0f, 1f);
        dirty.anchoredPosition = new Vector2(x + 774f, -(TopBarY + 16f));
        refs.DirtyPill = dirty.gameObject;

        refs.SaveButton = FriendsUiKit.CreateSolidButton("Save Button", parent, a, "SIMPAN", FriendsTheme.GreenTop,
                                                         FriendsTheme.GreenBottom, FriendsTheme.GreenLip, a.checkIcon, 24);
        ChatUiKit.PlaceTopRight((RectTransform)refs.SaveButton.transform, Edge, TopBarY, 210f, TopBarHeight);
        refs.UndoButton = FriendsUiKit.CreateOutlineButton("Undo Button", parent, a, "URUNGKAN", Color.white,
                                                           FriendsTheme.NeutralFill, ProfileMenuBuilder.LoadIcon("icon-undo"), 24);
        ChatUiKit.PlaceTopRight((RectTransform)refs.UndoButton.transform, Edge + 224f, TopBarY, 250f, TopBarHeight);
    }

    // ---------------------------------------------------------------- Rail kategori

    private static void BuildRail(RectTransform parent, ChatUiAssets a, CustomizerEditorRefs refs,
                                  IReadOnlyList<EditorCategory> categories) {
        RectTransform rail = StretchColumn(parent, a, "Rail", Edge, RailWidth, fromRight: false);
        VerticalLayoutGroup layout = rail.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 16, 16);
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;

        refs.Tabs = new EditorTabRefs[categories.Count];
        for (int i = 0; i < refs.Tabs.Length; i++) refs.Tabs[i] = BuildTab(rail, a, categories[i]);
    }

    private static EditorTabRefs BuildTab(RectTransform rail, ChatUiAssets a, EditorCategory category) {
        var tab = new EditorTabRefs();
        RectTransform rect = ChatUiKit.CreateRect("Tab " + category.Label, rail);
        Image hit = ChatUiKit.AddShape(rect.gameObject, a.rounded, new Color(0f, 0f, 0f, 0f), 1.4f);
        tab.Button = rect.gameObject.AddComponent<Button>();
        tab.Button.targetGraphic = hit;

        RectTransform selected = ChatUiKit.Stretch(ChatUiKit.CreateRect("Selected", rect), 0f, 0f, 0f, 0f);
        ChatUiKit.AddShape(selected.gameObject, a.rounded, FriendsTheme.TabSelected, 1.4f).raycastTarget = false;
        RectTransform bar = ChatUiKit.CreateRect("Bar", selected);
        bar.anchorMin = new Vector2(0f, 0.22f);
        bar.anchorMax = new Vector2(0f, 0.78f);
        bar.pivot = new Vector2(0f, 0.5f);
        bar.sizeDelta = new Vector2(5f, 0f);
        bar.anchoredPosition = Vector2.zero;
        bar.gameObject.AddComponent<Image>().color = Color.white;
        bar.gameObject.AddComponent<UIVerticalGradient>().SetColors(ProfileTheme.PinkTop, FriendsTheme.BlueTop);
        tab.Selected = selected.gameObject;

        tab.Icon = ChatUiKit.AddIcon("Icon", rect, ProfileMenuBuilder.LoadIcon(category.Icon), FriendsTheme.Muted);
        tab.Icon.rectTransform.anchorMin = tab.Icon.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);
        tab.Icon.rectTransform.sizeDelta = new Vector2(36f, 36f);
        tab.Icon.rectTransform.anchoredPosition = Vector2.zero;
        tab.Label = ChatUiKit.CreateText("Label", rect, a.font, category.Label, 18, FriendsTheme.Muted, FontStyles.Bold,
                                         TextAlignmentOptions.Center);
        tab.Label.rectTransform.anchorMin = new Vector2(0f, 0f);
        tab.Label.rectTransform.anchorMax = new Vector2(1f, 0.38f);
        tab.Label.rectTransform.offsetMin = tab.Label.rectTransform.offsetMax = Vector2.zero;
        return tab;
    }

    // ---------------------------------------------------------------- Panggung

    private static void BuildStage(RectTransform parent, ChatUiAssets a, CustomizerEditorRefs refs) {
        RectTransform area = ChatUiKit.Stretch(ChatUiKit.CreateRect("Stage Area", parent), Edge + RailWidth + Gap,
                                               ContentTop, Edge + SideWidth + Gap, ContentBottom);
        refs.StageArea = area;
        // AspectRatioFitter (FitInParent) mengukur dari PARENT dan mengabaikan offset rect-nya sendiri. Karena itu
        // gambar ditaruh di dalam kotak yang sudah dikurangi ruang toggle (atas) + petunjuk (bawah); kalau tidak,
        // alas karakter tertimpa tulisan "Geser untuk memutar".
        RectTransform box = ChatUiKit.Stretch(ChatUiKit.CreateRect("Preview Box", area), 0f, 64f, 0f, 40f);
        RectTransform previewRect = ChatUiKit.CreateRect("Preview", box);
        AspectRatioFitter fitter = previewRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = CustomizerPickView.PreviewAspect;
        refs.Preview = previewRect.gameObject.AddComponent<RawImage>();

        CustomizerEditorPanels.CreateCameraToggle(area, a, out refs.BodyCamButton, out refs.BodyCamOn,
                                                  out refs.FaceCamButton, out refs.FaceCamOn);
        TextMeshProUGUI hint = ChatUiKit.CreateText("Drag Hint", area, a.font, "Geser untuk memutar", FriendsTheme.SmallSize,
                                                    FriendsTheme.Muted, FontStyles.Normal, TextAlignmentOptions.Center);
        hint.rectTransform.anchorMin = new Vector2(0f, 0f);
        hint.rectTransform.anchorMax = new Vector2(1f, 0f);
        hint.rectTransform.pivot = new Vector2(0.5f, 0f);
        hint.rectTransform.offsetMin = new Vector2(0f, 0f);
        hint.rectTransform.offsetMax = new Vector2(0f, 34f);
        refs.Loading = CustomizerUi.CreateLoadingPill(area, a, 0f);
        ((RectTransform)refs.Loading.transform).anchoredPosition += new Vector2(0f, -64f); // di bawah toggle kamera
    }

    // ---------------------------------------------------------------- Panel kanan

    private static void BuildSidePanel(RectTransform parent, ChatUiAssets a, CustomizerEditorRefs refs) {
        RectTransform panel = CustomizerUi.CreateSidePanel(parent, a, Edge, ContentTop, SideWidth, ContentBottom);
        panel.name = "Side Panel";

        RectTransform header = ProfileMenuBuilder.IconBox(panel, a, ProfileMenuBuilder.LoadIcon("icon-shirt"),
                                                          FriendsTheme.BlueTop, FriendsTheme.BlueBottom, Pad, Pad, HeaderHeight);
        refs.HeaderIcon = header.Find("Icon").GetComponent<Image>();
        refs.HeaderTitle = ChatUiKit.CreateText("Header Title", panel, a.font, "ATASAN", 40, Color.white, FontStyles.Bold);
        ChatUiKit.Place(refs.HeaderTitle.rectTransform, Pad + HeaderHeight + 18f, Pad, 400f, HeaderHeight);

        BuildSubTabRow(panel, a, refs);

        BuildOutfitPanel(panel, a, refs);
        refs.Shapes = CustomizerEditorPanels.BuildShapePanel(panel, Pad, GridTop);
        refs.Stickers = CustomizerEditorPanels.BuildStickerPanel(panel, a, Pad, GridTop, InnerWidth);
    }

    private static void BuildOutfitPanel(RectTransform panel, ChatUiAssets a, CustomizerEditorRefs refs) {
        RectTransform outfit = ChatUiKit.Stretch(ChatUiKit.CreateRect("Outfit Panel", panel), Pad, GridTop, Pad, Pad);
        refs.OutfitPanel = outfit.gameObject;

        // Grid item: atas panel sampai di atas bagian warna (tinggi ikut layar).
        RectTransform scrollRect = ChatUiKit.Stretch(ChatUiKit.CreateRect("Item Scroll", outfit), 0f, 0f, 0f, ColorHeight + 14f);
        scrollRect.gameObject.AddComponent<RectMask2D>();
        refs.GridScroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        refs.GridScroll.horizontal = false;
        refs.GridScroll.movementType = ScrollRect.MovementType.Clamped;
        refs.GridScroll.scrollSensitivity = 30f;
        refs.GridContent = ChatUiKit.CreateRect("Items", scrollRect);
        refs.GridContent.anchorMin = new Vector2(0f, 1f);
        refs.GridContent.anchorMax = new Vector2(1f, 1f);
        refs.GridContent.pivot = new Vector2(0.5f, 1f);
        refs.GridContent.sizeDelta = Vector2.zero;
        // Isi = satu atau beberapa seksi (judul tipe + grid), mis. "Alis" dan "Bulu Mata" (CreateItemSection).
        VerticalLayoutGroup sections = refs.GridContent.gameObject.AddComponent<VerticalLayoutGroup>();
        sections.spacing = 8f;
        sections.childControlWidth = sections.childControlHeight = true;
        sections.childForceExpandWidth = true;
        sections.childForceExpandHeight = false;
        ContentSizeFitter fit = refs.GridContent.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        refs.GridScroll.content = refs.GridContent;
        refs.GridScroll.viewport = scrollRect;
        BuildScrollIndicators(outfit, scrollRect, a, refs.GridScroll);

        BuildColorSection(outfit, a, refs);
    }

    /// Supaya pemain tahu masih ada item di bawah (mis. "Kilau Mata" di bawah "Pupil"): scrollbar tipis di sisi
    /// kanan (tampil hanya kalau isinya tidak muat) + pudar dan panah ▼ di bawah grid sampai discroll ke ujung.
    private static void BuildScrollIndicators(RectTransform outfit, RectTransform viewport, ChatUiAssets a,
                                              ScrollRect scroll) {
        RectTransform bar = CreateScrollbar("Item Scrollbar", outfit, a, scroll);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0f, 1f);
        // Di celah padding kanan panel, sejajar tinggi grid (tidak menutupi item).
        bar.offsetMin = new Vector2(ScrollbarGap, ColorHeight + 14f);
        bar.offsetMax = new Vector2(ScrollbarGap + ScrollbarWidth, 0f);

        RectTransform hint = CreateMoreHint("More Below Hint", outfit, a, viewport, 28f);
        hint.anchorMin = new Vector2(0f, 0f);
        hint.anchorMax = new Vector2(1f, 0f);
        hint.pivot = new Vector2(0.5f, 0f);
        hint.offsetMin = new Vector2(0f, ColorHeight + 14f);
        hint.offsetMax = new Vector2(0f, ColorHeight + 14f + MoreHintHeight);
    }

    /// Scrollbar vertikal tipis (AutoHide: hanya tampil kalau isi tidak muat). Posisi diatur pemanggil.
    private static RectTransform CreateScrollbar(string name, RectTransform parent, ChatUiAssets a, ScrollRect scroll) {
        RectTransform bar = ChatUiKit.CreateRect(name, parent);
        ChatUiKit.AddShape(bar.gameObject, a.rounded, new Color(1f, 1f, 1f, 0.06f), 6f);
        RectTransform slide = ChatUiKit.Stretch(ChatUiKit.CreateRect("Sliding Area", bar), 0f, 0f, 0f, 0f);
        RectTransform handle = ChatUiKit.Stretch(ChatUiKit.CreateRect("Handle", slide), 0f, 0f, 0f, 0f);
        Image handleImage = ChatUiKit.AddShape(handle.gameObject, a.rounded, FriendsTheme.Muted, 6f);
        Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return bar;
    }

    /// Pudar ke warna panel + panah ▼ di bawah viewport, hilang saat sudah discroll ke ujung (ScrollMoreHint).
    /// Posisi diatur pemanggil; dibuat di luar viewport supaya tidak ikut di-mask.
    private static RectTransform CreateMoreHint(string name, RectTransform parent, ChatUiAssets a,
                                                RectTransform viewport, float arrowSize) {
        RectTransform hint = ChatUiKit.CreateRect(name, parent);
        Image fade = hint.gameObject.AddComponent<Image>();
        fade.color = Color.white;
        fade.raycastTarget = false;
        Color panel = FriendsTheme.Panel;
        hint.gameObject.AddComponent<UIVerticalGradient>().SetColors(new Color(panel.r, panel.g, panel.b, 0f), panel);
        Image arrow = ChatUiKit.AddIcon("Arrow", hint, a.chevron, FriendsTheme.Neutral);
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.5f, 0f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = new Vector2(arrowSize, arrowSize);
        arrowRect.anchoredPosition = new Vector2(0f, arrowSize * 0.65f);
        arrowRect.localRotation = Quaternion.Euler(0f, 0f, -90f); // chevron kanan → bawah

        viewport.gameObject.AddComponent<ScrollMoreHint>().SetHint(hint.gameObject.AddComponent<CanvasGroup>());
        return hint;
    }

    private static void BuildColorSection(RectTransform outfit, ChatUiAssets a, CustomizerEditorRefs refs) {
        RectTransform section = ChatUiKit.CreateRect("Color Section", outfit);
        section.anchorMin = new Vector2(0f, 0f);
        section.anchorMax = new Vector2(1f, 0f);
        section.pivot = new Vector2(0.5f, 0f);
        section.offsetMin = Vector2.zero;
        section.offsetMax = new Vector2(0f, ColorHeight);
        refs.ColorSection = section.gameObject;

        refs.ColorTitle = ChatUiKit.CreateText("Color Title", section, a.font, "Warna", FriendsTheme.SmallSize, FriendsTheme.Neutral,
                                               FontStyles.Bold);
        refs.ColorTitle.richText = true; // nama item ditulis putih (<color>)
        ChatUiKit.Place(refs.ColorTitle.rectTransform, 0f, 0f, InnerWidth - 250f, 40f);
        BuildColorModeToggle(section, a, refs);

        RectTransform colorArea = ChatUiKit.CreateRect("Color Area", section);
        ChatUiKit.Place(colorArea, 0f, 50f, InnerWidth, ColorAreaHeight);
        refs.ColorArea = colorArea.gameObject;
        BuildChannelList(colorArea, a, refs);

        RectTransform pickerHost = ChatUiKit.CreateRect("Picker Host", colorArea);
        float pickerX = ChannelWidth + 16f;
        ChatUiKit.Place(pickerHost, pickerX, 0f, InnerWidth - pickerX, ColorAreaHeight);
        refs.Picker = HsvColorPicker.Build(pickerHost, a, InnerWidth - pickerX);
        refs.Pattern = CustomizerEditorPanels.BuildPatternArea(section, a, 50f, InnerWidth);

        refs.MatchButton = FriendsUiKit.CreateOutlineButton("Match Button", section, a, "Samakan dengan…", Color.white,
                                                            FriendsTheme.NeutralFill, null, 18);
        var matchRect = (RectTransform)refs.MatchButton.transform;
        matchRect.anchorMin = matchRect.anchorMax = matchRect.pivot = new Vector2(0f, 0f);
        matchRect.anchoredPosition = Vector2.zero;
        matchRect.sizeDelta = new Vector2(300f, 50f);
        refs.MatchLabel = FriendsUiKit.LabelOf(refs.MatchButton);
        refs.KeepColorsButton = CreateSwitch(section, a, "Simpan warna saat ganti", out refs.KeepColorsOn);
        var keepRect = (RectTransform)refs.KeepColorsButton.transform;
        keepRect.anchorMin = keepRect.anchorMax = keepRect.pivot = new Vector2(1f, 0f);
        keepRect.anchoredPosition = Vector2.zero;
        keepRect.sizeDelta = new Vector2(300f, 50f);
    }

    /// Baris chip sub-tab bisa digeser ke samping: Wajah (6 chip) dan Aksesori lebih lebar dari panel, dan chip
    /// tidak boleh dipampatkan. Pudar + panah ▶ di kanan menandakan masih ada sub-tab yang belum terlihat.
    private static void BuildSubTabRow(RectTransform panel, ChatUiAssets a, CustomizerEditorRefs refs) {
        RectTransform viewport = ChatUiKit.CreateRect("Sub Tabs", panel);
        ChatUiKit.Place(viewport, Pad, SubTabY, InnerWidth, ChipHeight);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        RectTransform row = ChatUiKit.CreateRect("Sub Tab Row", viewport);
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 0.5f);
        row.sizeDelta = Vector2.zero;
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fit = row.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = row;
        scroll.viewport = viewport;
        refs.SubTabRow = row;
        refs.SubTabScroll = scroll;

        RectTransform hint = ChatUiKit.CreateRect("Sub Tab More Hint", panel);
        ChatUiKit.Place(hint, Pad + InnerWidth - SubTabHintWidth, SubTabY, SubTabHintWidth, ChipHeight);
        // Gradien BoZo-UI hanya vertikal (atas → bawah): diputar 90° supaya transparan di kiri, pekat di kanan.
        RectTransform fade = ChatUiKit.CreateRect("Fade", hint);
        fade.anchorMin = fade.anchorMax = fade.pivot = new Vector2(0.5f, 0.5f);
        fade.sizeDelta = new Vector2(ChipHeight, SubTabHintWidth);
        fade.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Image fadeImage = fade.gameObject.AddComponent<Image>();
        fadeImage.color = Color.white;
        fadeImage.raycastTarget = false;
        Color color = FriendsTheme.Panel;
        fade.gameObject.AddComponent<UIVerticalGradient>().SetColors(new Color(color.r, color.g, color.b, 0f), color);
        Image arrow = ChatUiKit.AddIcon("Arrow", hint, a.chevron, FriendsTheme.Neutral);
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = arrowRect.anchorMax = arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.sizeDelta = new Vector2(22f, 22f);
        arrowRect.anchoredPosition = Vector2.zero;
        ScrollMoreHint more = viewport.gameObject.AddComponent<ScrollMoreHint>();
        more.SetHorizontal(true);
        more.SetHint(hint.gameObject.AddComponent<CanvasGroup>());
    }

    /// Daftar bagian warna (Base, Warna 2, ...) bisa discroll: kepala punya lebih banyak bagian dari yang muat di
    /// 214px. Tinggi baris tetap 46px (sama dengan item lain, tidak dipampatkan); scrollbar di celah ke picker +
    /// pudar/panah ▼ menandakan masih ada bagian di bawah.
    private static void BuildChannelList(RectTransform colorArea, ChatUiAssets a, CustomizerEditorRefs refs) {
        RectTransform viewport = ChatUiKit.CreateRect("Channels", colorArea);
        ChatUiKit.Place(viewport, 0f, 0f, ChannelWidth, ColorAreaHeight);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        RectTransform content = ChatUiKit.CreateRect("Channel List", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup list = content.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 6f;
        list.childControlWidth = list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        scroll.viewport = viewport;
        refs.ChannelList = content;
        refs.ChannelScroll = scroll;

        RectTransform bar = CreateScrollbar("Channel Scrollbar", colorArea, a, scroll);
        ChatUiKit.Place(bar, ChannelWidth + 5f, 0f, ScrollbarWidth, ColorAreaHeight);
        RectTransform hint = CreateMoreHint("Channel More Hint", colorArea, a, viewport, 22f);
        ChatUiKit.Place(hint, 0f, ColorAreaHeight - ChannelHintHeight, ChannelWidth, ChannelHintHeight);
    }

    /// Toggle "Warna | Motif" di kanan judul bagian warna (mockup layar 2). Motif aktif hanya kalau baju mendukung.
    private static void BuildColorModeToggle(RectTransform section, ChatUiAssets a, CustomizerEditorRefs refs) {
        RectTransform toggle = ChatUiKit.CreateRect("Color Mode", section);
        ChatUiKit.PlaceTopRight(toggle, 0f, 0f, 240f, 44f);
        ChatUiKit.AddShape(toggle.gameObject, a.rounded, FriendsTheme.Card, 2.4f);
        HorizontalLayoutGroup row = toggle.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(4, 4, 4, 4);
        row.spacing = 4f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = true;
        refs.ColorModeButton = CreateChip(toggle, a, "Warna", out refs.ColorModeOn);
        refs.PatternModeButton = CreateChip(toggle, a, "Motif", out refs.PatternModeOn);
        refs.PatternModeGroup = refs.PatternModeButton.gameObject.AddComponent<CanvasGroup>();
        foreach (Button chip in new[] { refs.ColorModeButton, refs.PatternModeButton }) {
            LayoutElement size = chip.GetComponent<LayoutElement>();
            size.preferredHeight = 36f;
            FriendsUiKit.LabelOf(chip).fontSize = 18f;
        }
    }

    private static void BuildMatchPopup(RectTransform root, ChatUiAssets a, CustomizerEditorRefs refs) {
        RectTransform overlay = ChatUiKit.Stretch(ChatUiKit.CreateRect("Match Overlay", root), 0f, 0f, 0f, 0f);
        Image dim = overlay.gameObject.AddComponent<Image>();
        dim.color = FriendsTheme.Dim;
        RectTransform panel = CustomizerUi.CreatePanel(overlay, a, 560f, 560f);
        TextMeshProUGUI title = ChatUiKit.CreateText("Title", panel, a.font, "Samakan warna dengan", 34, Color.white,
                                                     FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, 36f, 32f, 488f, 50f);
        refs.MatchList = ChatUiKit.Stretch(ChatUiKit.CreateRect("Options", panel), 36f, 100f, 36f, 36f);
        VerticalLayoutGroup list = refs.MatchList.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 8f;
        list.childControlWidth = list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        Button close = overlay.gameObject.AddComponent<Button>();
        close.targetGraphic = dim;
        close.onClick.AddListener(() => overlay.gameObject.SetActive(false));
        refs.MatchPopup = overlay.gameObject;
        overlay.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- Isi dinamis

    /// <summary>Seksi grid item; judul hanya kalau sub-tab berisi beberapa tipe (mis. "Alis", "Bulu Mata").</summary>
    public static RectTransform CreateItemSection(Transform parent, ChatUiAssets a, string title) {
        if (!string.IsNullOrEmpty(title)) {
            TextMeshProUGUI label = ChatUiKit.CreateText("Section " + title, parent, a.font, title, 20, FriendsTheme.Neutral,
                                                         FontStyles.Bold);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        }
        RectTransform grid = ChatUiKit.CreateRect("Grid " + title, parent);
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(ItemSize, ItemSize);
        layout.spacing = new Vector2(GridGap, GridGap);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = GridColumns;
        return grid;
    }

    /// <summary>Kotak warna kulit siap pakai (tab Kulit).</summary>
    public static Button CreateSwatchItem(Transform parent, ChatUiAssets a, Color color, out GameObject selected) {
        Button button = CreateItem(parent, a, null, "", out selected);
        Transform icon = button.transform.Find("Icon");
        if (icon) Object.Destroy(icon.gameObject);
        RectTransform dot = ChatUiKit.Stretch(ChatUiKit.CreateRect("Swatch", button.transform), 18f, 18f, 18f, 18f);
        ChatUiKit.AddShape(dot.gameObject, a.circle, color).raycastTarget = false;
        dot.SetSiblingIndex(1);
        return button;
    }

    /// <summary>Kotak bernomor besar + label (Wajah · Ekspresi), ukuran sama dengan item outfit.</summary>
    public static Button CreateNumberItem(Transform parent, ChatUiAssets a, string number, string label,
                                          out GameObject selected) {
        Button button = CreateItem(parent, a, null, label, out selected);
        Transform icon = button.transform.Find("Icon");
        if (icon) Object.Destroy(icon.gameObject);
        TextMeshProUGUI digit = ChatUiKit.CreateText("Number", button.transform, a.font, number, 44, Color.white,
                                                     FontStyles.Bold, TextAlignmentOptions.Center);
        ChatUiKit.Stretch(digit.rectTransform, 8f, 8f, 8f, 30f);
        digit.raycastTarget = false;
        digit.transform.SetSiblingIndex(1);
        return button;
    }

    public static Button CreateChip(Transform parent, ChatUiAssets a, string label, out GameObject selected) {
        RectTransform rect = ChatUiKit.CreateRect("Chip " + label, parent);
        Image bg = ChatUiKit.AddShape(rect.gameObject, a.rounded, FriendsTheme.Card, 2.2f);
        RectTransform on = ChatUiKit.Stretch(ChatUiKit.CreateRect("Selected", rect), 0f, 0f, 0f, 0f);
        ChatUiKit.AddShape(on.gameObject, a.rounded, FriendsTheme.TabSelected, 2.2f).raycastTarget = false;
        FriendsUiKit.AddBorder(on, new Color(1f, 1f, 1f, 0.25f), 2.2f);
        selected = on.gameObject;
        TextMeshProUGUI text = ChatUiKit.CreateText("Label", rect, a.font, label, 20, Color.white, FontStyles.Bold,
                                                    TextAlignmentOptions.Center);
        ChatUiKit.Stretch(text.rectTransform, 18f, 0f, 18f, 0f);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = text.GetPreferredValues(label).x + 48f; // + ruang huruf tebal
        size.preferredHeight = ChipHeight;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        return button;
    }

    /// <summary>Kotak item di grid: ikon outfit (atau ikon "Kosong") + nama + centang saat dipakai.</summary>
    public static Button CreateItem(Transform parent, ChatUiAssets a, Sprite icon, string label, out GameObject selected) {
        RectTransform rect = ChatUiKit.CreateRect("Item " + label, parent);
        Image bg = ChatUiKit.AddShape(rect.gameObject, a.rounded, FriendsTheme.Card, 1.6f);
        FriendsUiKit.AddBorder(rect, FriendsTheme.CardBorder, 1.6f);
        Image image = ChatUiKit.AddIcon("Icon", rect, icon, icon != null ? Color.white : FriendsTheme.Muted);
        ChatUiKit.Stretch(image.rectTransform, 14f, 8f, 14f, 30f);
        TextMeshProUGUI text = ChatUiKit.CreateText("Label", rect, a.font, label, 14, FriendsTheme.Neutral, FontStyles.Normal,
                                                    TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = new Vector2(0f, 0f);
        text.rectTransform.anchorMax = new Vector2(1f, 0f);
        text.rectTransform.pivot = new Vector2(0.5f, 0f);
        text.rectTransform.offsetMin = new Vector2(4f, 4f);
        text.rectTransform.offsetMax = new Vector2(-4f, 28f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;

        RectTransform on = ChatUiKit.Stretch(ChatUiKit.CreateRect("Selected", rect), -3f, -3f, -3f, -3f);
        ChatUiKit.AddShape(on.gameObject, FriendsUiKit.OutlineSprite, ProfileTheme.Tag, 1.6f).raycastTarget = false;
        RectTransform check = ChatUiKit.CreateRect("Check", on);
        ChatUiKit.PlaceTopRight(check, 9f, 9f, 28f, 28f);
        ChatUiKit.AddShape(check.gameObject, a.circle, ProfileTheme.Tag).raycastTarget = false;
        Image tick = ChatUiKit.AddIcon("Tick", check, a.checkIcon, FriendsTheme.Panel);
        ChatUiKit.Stretch(tick.rectTransform, 6f, 6f, 6f, 6f);
        selected = on.gameObject;
        on.gameObject.SetActive(false);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        return button;
    }

    private const float DimmedItemAlpha = 0.35f;

    /// <summary>
    /// Item yang tidak bisa dipakai (mis. mata anime di kepala Basic): tampil abu-abu tapi tetap bisa diketuk,
    /// supaya editor bisa menjelaskan alasannya lewat toast.
    /// </summary>
    public static void DimItem(Button item) {
        if (!item.TryGetComponent(out CanvasGroup group)) group = item.gameObject.AddComponent<CanvasGroup>();
        group.alpha = DimmedItemAlpha;
    }

    /// <summary>Satu baris bagian warna: bulatan warna + nama bagian ("Base", "Warna 2", ...).</summary>
    public static Button CreateChannel(Transform parent, ChatUiAssets a, string label, out Image swatch,
                                       out GameObject selected) {
        RectTransform rect = ChatUiKit.CreateRect("Channel " + label, parent);
        Image bg = ChatUiKit.AddShape(rect.gameObject, a.rounded, FriendsTheme.Card, 1.4f);
        RectTransform on = ChatUiKit.Stretch(ChatUiKit.CreateRect("Selected", rect), 0f, 0f, 0f, 0f);
        ChatUiKit.AddShape(on.gameObject, a.rounded, FriendsTheme.TabSelected, 1.4f).raycastTarget = false;
        FriendsUiKit.AddBorder(on, ProfileTheme.Tag, 1.4f);
        selected = on.gameObject;

        RectTransform ring = ChatUiKit.CreateRect("Ring", rect);
        ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(0f, 0.5f);
        ring.anchoredPosition = new Vector2(12f, 0f);
        ring.sizeDelta = new Vector2(30f, 30f);
        ChatUiKit.AddShape(ring.gameObject, a.circle, Color.white).raycastTarget = false;
        RectTransform dot = ChatUiKit.Stretch(ChatUiKit.CreateRect("Color", ring), 3f, 3f, 3f, 3f);
        swatch = ChatUiKit.AddShape(dot.gameObject, a.circle, Color.white);
        swatch.raycastTarget = false;

        TextMeshProUGUI text = ChatUiKit.CreateText("Label", rect, a.font, label, 18, Color.white, FontStyles.Bold);
        ChatUiKit.Stretch(text.rectTransform, 54f, 0f, 8f, 0f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 18f;
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.preferredHeight = 46f;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        return button;
    }

    public static Button CreateListOption(Transform parent, ChatUiAssets a, string label) {
        Button button = FriendsUiKit.CreateOutlineButton("Option " + label, parent, a, label, Color.white,
                                                         FriendsTheme.NeutralFill, null, 22);
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        return button;
    }

    // ---------------------------------------------------------------- Bantuan

    private static RectTransform StretchColumn(RectTransform parent, ChatUiAssets a, string name, float edge, float width,
                                               bool fromRight) {
        RectTransform panel = ChatUiKit.CreateRect(name, parent);
        float side = fromRight ? 1f : 0f;
        panel.anchorMin = new Vector2(side, 0f);
        panel.anchorMax = new Vector2(side, 1f);
        panel.pivot = new Vector2(side, 1f);
        panel.offsetMin = new Vector2(fromRight ? -edge - width : edge, ContentBottom);
        panel.offsetMax = new Vector2(fromRight ? -edge : edge + width, -ContentTop);
        ChatUiKit.AddShape(panel.gameObject, a.rounded, FriendsTheme.Panel, 1.8f);
        FriendsUiKit.AddBorder(panel, FriendsTheme.CardBorder, 1.8f);
        // Garis tepi jangan ikut diatur VerticalLayoutGroup rail (kalau tidak, jadi kotak kosong di atas).
        panel.Find("Border").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return panel;
    }

    private static Button CreateSwitch(RectTransform parent, ChatUiAssets a, string label, out GameObject on) {
        RectTransform rect = ChatUiKit.CreateRect("Keep Colors", parent);
        Image hit = ChatUiKit.AddShape(rect.gameObject, a.rounded, new Color(0f, 0f, 0f, 0f));
        RectTransform track = ChatUiKit.CreateRect("Track", rect);
        track.anchorMin = track.anchorMax = track.pivot = new Vector2(0f, 0.5f);
        track.anchoredPosition = Vector2.zero;
        track.sizeDelta = new Vector2(52f, 30f);
        ChatUiKit.AddShape(track.gameObject, a.rounded, FriendsTheme.Card, 3f).raycastTarget = false;
        RectTransform onTrack = ChatUiKit.Stretch(ChatUiKit.CreateRect("On", track), 0f, 0f, 0f, 0f);
        ChatUiKit.AddShape(onTrack.gameObject, a.rounded, FriendsTheme.GreenBottom, 3f).raycastTarget = false;
        RectTransform knobOn = ChatUiKit.CreateRect("Knob", onTrack);
        knobOn.anchorMin = knobOn.anchorMax = knobOn.pivot = new Vector2(1f, 0.5f);
        knobOn.anchoredPosition = new Vector2(-4f, 0f);
        knobOn.sizeDelta = new Vector2(22f, 22f);
        ChatUiKit.AddShape(knobOn.gameObject, a.circle, Color.white).raycastTarget = false;
        RectTransform knobOff = ChatUiKit.CreateRect("Knob Off", track);
        knobOff.anchorMin = knobOff.anchorMax = knobOff.pivot = new Vector2(0f, 0.5f);
        knobOff.anchoredPosition = new Vector2(4f, 0f);
        knobOff.sizeDelta = new Vector2(22f, 22f);
        ChatUiKit.AddShape(knobOff.gameObject, a.circle, FriendsTheme.Muted).raycastTarget = false;
        knobOff.SetAsFirstSibling();
        on = onTrack.gameObject;

        TextMeshProUGUI text = ChatUiKit.CreateText("Label", rect, a.font, label, 17, FriendsTheme.Neutral, FontStyles.Normal);
        ChatUiKit.Stretch(text.rectTransform, 62f, 0f, 0f, 0f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        return button;
    }
}

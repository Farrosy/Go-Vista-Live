using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum FriendsTab { Friends, Requests, Add }

/// Satu tombol tab (TEMAN 8 / PERMINTAAN 2 / TAMBAH +).
public sealed class FriendsTabRefs {
    public Button Button;
    public Image Background;
    public GameObject Border;
    public TextMeshProUGUI Label;
    public GameObject Counter;
    public TextMeshProUGUI CounterText;
}

public sealed class FriendsMenuRefs {
    public Canvas Canvas;
    public Button CloseButton;
    public RectTransform OnlinePill;
    public TextMeshProUGUI OnlineText;
    public readonly FriendsTabRefs[] Tabs = new FriendsTabRefs[3];

    public GameObject ListPage;
    public ScrollRect Scroll;
    public RectTransform ListContent;

    public GameObject AddPage;
    public TMP_InputField SearchInput;
    public Button SendButton;
    public GameObject ResultBox;
    public Image ResultBackground;
    public Image ResultIcon;
    public TextMeshProUGUI ResultText;

    public GameObject StatePage;
    public Image StateIcon;
    public TextMeshProUGUI StateTitle;
    public TextMeshProUGUI StateBody;
    public Button StateButton;
    public TextMeshProUGUI StateButtonLabel;
}

/// Membangun panel TEMAN lewat kode, mengikuti Docs/Friends/menu-teman-*.png:
/// header (ikon hijau, "TEMAN", pil online, tombol ×), tab TEMAN / PERMINTAAN / TAMBAH, lalu satu dari tiga
/// halaman: daftar (scroll), form tambah, atau keadaan (kosong / gagal / guest).
/// Tombol pembuka ada di tile TEMAN menu utama (lihat FriendsMainMenuTile).
public static class FriendsMenuBuilder {
    public const int CanvasSortingOrder = 60; // di atas Loby Canvas (50), di bawah login (100).
    private const float PanelWidth = 1200f;
    private const float PanelHeight = 880f;
    private const float Margin = 52f;
    private const float ContentTop = 236f;
    private const float ContentWidth = PanelWidth - Margin * 2f;
    private const float ContentHeight = PanelHeight - ContentTop - 40f;

    public static void Build(Transform owner, ChatUiAssets assets, FriendsMenuRefs refs) {
        refs.Canvas = CreateCanvas(owner);
        RectTransform dim = ChatUiKit.Stretch(ChatUiKit.CreateRect("Dim", refs.Canvas.transform), 0f, 0f, 0f, 0f);
        dim.gameObject.AddComponent<Image>().color = FriendsTheme.Dim;
        RectTransform safeArea = ChatUiKit.Stretch(ChatUiKit.CreateRect("Safe Area", refs.Canvas.transform), 0f, 0f, 0f, 0f);
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        RectTransform panel = ChatUiKit.CreateRect("Friends Panel", safeArea);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.gameObject.AddComponent<ResponsivePanel>();
        ChatUiKit.AddShape(panel.gameObject, assets.rounded, FriendsTheme.Panel, 1.8f);
        FriendsUiKit.AddBorder(panel, FriendsTheme.CardBorder, 1.8f);
        RectTransform accent = ChatUiKit.CreateRect("Accent", panel);
        ChatUiKit.Place(accent, 80f, 0f, PanelWidth - 160f, 3f);
        accent.gameObject.AddComponent<Image>().color = new Color(FriendStatus.RoomColor.r, FriendStatus.RoomColor.g,
                                                                  FriendStatus.RoomColor.b, 0.7f);

        BuildHeader(panel, assets, refs);
        BuildTabs(panel, assets, refs);
        BuildListPage(panel, assets, refs);
        BuildAddPage(panel, assets, refs);
        BuildStatePage(panel, assets, refs);
        refs.Canvas.gameObject.SetActive(false);
    }

    private static Canvas CreateCanvas(Transform owner) {
        var canvasObject = new GameObject("Friends Canvas", typeof(RectTransform), typeof(Canvas),
                                          typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(owner, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void BuildHeader(RectTransform panel, ChatUiAssets assets, FriendsMenuRefs refs) {
        RectTransform iconBox = ChatUiKit.CreateRect("Header Icon", panel);
        ChatUiKit.Place(iconBox, Margin - 8f, 38f, 72f, 72f);
        ChatUiKit.AddShape(iconBox.gameObject, assets.rounded, Color.white, 1.3f).raycastTarget = false;
        iconBox.gameObject.AddComponent<UIVerticalGradient>().SetColors(FriendsTheme.GreenTop, FriendsTheme.GreenBottom);
        Image icon = ChatUiKit.AddIcon("Icon", iconBox, assets.friendsIcon, Color.white);
        ChatUiKit.Stretch(icon.rectTransform, 14f, 14f, 14f, 14f);

        TextMeshProUGUI title = ChatUiKit.CreateText("Title", panel, assets.font, "TEMAN", FriendsTheme.TitleSize,
                                                     Color.white, FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, 136f, 38f, 260f, 72f);
        float titleWidth = title.GetPreferredValues("TEMAN").x;

        refs.OnlinePill = FriendsUiKit.CreatePill(panel, assets, "● 0 online", ChatTheme.OnlinePill, ChatTheme.Online,
                                                  FriendsTheme.SmallSize, 44f, ChatTheme.Online);
        refs.OnlinePill.anchorMin = refs.OnlinePill.anchorMax = refs.OnlinePill.pivot = new Vector2(0f, 1f);
        refs.OnlinePill.anchoredPosition = new Vector2(136f + titleWidth + 20f, -52f);
        refs.OnlineText = refs.OnlinePill.GetComponentInChildren<TextMeshProUGUI>();

        refs.CloseButton = FriendsUiKit.CreateOutlineButton("Close Button", panel, assets, "", Color.white,
                                                            FriendsTheme.NeutralFill, assets.closeIcon, 28);
        ChatUiKit.PlaceTopRight((RectTransform)refs.CloseButton.transform, Margin - 8f, 40f, 72f, 72f);
    }

    private static void BuildTabs(RectTransform panel, ChatUiAssets assets, FriendsMenuRefs refs) {
        RectTransform bar = ChatUiKit.CreateRect("Tabs", panel);
        ChatUiKit.Place(bar, Margin, 142f, ContentWidth, 70f);
        string[] labels = { "TEMAN", "PERMINTAAN", "TAMBAH" };
        float width = (ContentWidth - 24f) / 3f;
        for (int i = 0; i < labels.Length; i++)
            refs.Tabs[i] = BuildTab(bar, assets, labels[i], i, i * (width + 12f), width);
    }

    private static FriendsTabRefs BuildTab(RectTransform bar, ChatUiAssets assets, string label, int index, float x,
                                           float width) {
        var tab = new FriendsTabRefs();
        RectTransform root = ChatUiKit.CreateRect("Tab " + label, bar);
        ChatUiKit.Place(root, x, 0f, width, 70f);
        tab.Background = ChatUiKit.AddShape(root.gameObject, assets.rounded, FriendsTheme.TabSelected, 1.4f);
        FriendsUiKit.AddBorder(root, new Color(1f, 1f, 1f, 0.14f));
        tab.Border = root.Find("Border").gameObject;
        tab.Button = root.gameObject.AddComponent<Button>();
        tab.Button.targetGraphic = tab.Background;
        tab.Button.transition = Selectable.Transition.None;

        RectTransform content = ChatUiKit.Stretch(ChatUiKit.CreateRect("Content", root), 8f, 0f, 8f, 0f);
        HorizontalLayoutGroup row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 12f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        tab.Label = ChatUiKit.CreateText("Label", content, assets.font, label, FriendsTheme.BodySize + 2,
                                         FriendsTheme.Muted, FontStyles.Bold, TextAlignmentOptions.Center);
        tab.Label.characterSpacing = 6f;

        if (index == (int)FriendsTab.Add) {
            Image icon = ChatUiKit.AddIcon("Icon", content, assets.userPlusIcon, FriendsTheme.Muted);
            LayoutElement size = icon.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = 30f;
            tab.Counter = icon.gameObject; // ikon ikut diwarnai sesuai tab aktif
        } else {
            bool requests = index == (int)FriendsTab.Requests;
            Color fill = requests ? FriendsTheme.Badge : new Color32(0x1E, 0x3A, 0x3A, 0xFF);
            Color text = requests ? Color.white : ChatTheme.Online;
            RectTransform counter = FriendsUiKit.CreatePill(content, assets, "0", fill, text, FriendsTheme.SmallSize, 34f);
            counter.SetParent(content, false);
            LayoutElement size = counter.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 34f;
            size.preferredHeight = 34f;
            tab.Counter = counter.gameObject;
            tab.CounterText = counter.GetComponentInChildren<TextMeshProUGUI>();
        }
        return tab;
    }

    private static void BuildListPage(RectTransform panel, ChatUiAssets assets, FriendsMenuRefs refs) {
        RectTransform viewport = ChatUiKit.CreateRect("List Page", panel);
        ChatUiKit.Place(viewport, Margin, ContentTop, ContentWidth, ContentHeight);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear; // area kosong tetap bisa di-drag

        RectTransform content = ChatUiKit.CreateRect("List Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 4, 16);
        layout.spacing = 12f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.viewport = viewport;
        scroll.content = content;
        refs.ListPage = viewport.gameObject;
        refs.Scroll = scroll;
        refs.ListContent = content;
    }

    private static void BuildAddPage(RectTransform panel, ChatUiAssets assets, FriendsMenuRefs refs) {
        RectTransform page = ChatUiKit.CreateRect("Add Page", panel);
        ChatUiKit.Place(page, Margin, ContentTop, ContentWidth, ContentHeight);
        refs.AddPage = page.gameObject;

        TextMeshProUGUI label = ChatUiKit.CreateText("Label", page, assets.font, "TAMBAH TEMAN", FriendsTheme.SmallSize,
                                                     FriendsTheme.Muted, FontStyles.Bold);
        label.characterSpacing = 8f;
        ChatUiKit.Place(label.rectTransform, 0f, 8f, ContentWidth, 34f);

        refs.SearchInput = CreateInput(page, assets, "Contoh: Bayu#1234");
        ChatUiKit.Place((RectTransform)refs.SearchInput.transform, 0f, 54f, ContentWidth, 92f);

        refs.SendButton = FriendsUiKit.CreateSolidButton("Send Button", page, assets, "KIRIM PERMINTAAN",
                                                         FriendsTheme.GreenTop, FriendsTheme.GreenBottom,
                                                         FriendsTheme.GreenLip, assets.userPlusIcon, 28);
        ChatUiKit.Place((RectTransform)refs.SendButton.transform, 0f, 164f, ContentWidth, 96f);

        TextMeshProUGUI hint = ChatUiKit.CreateText("Hint", page, assets.font,
            "Masukkan nama temanmu persis seperti yang tampil di profilnya, termasuk #angka di belakangnya.",
            FriendsTheme.SmallSize, FriendsTheme.Muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        hint.textWrappingMode = TextWrappingModes.Normal;
        ChatUiKit.Place(hint.rectTransform, 0f, 274f, ContentWidth, 64f);

        BuildResultBox(page, assets, refs);
    }

    private static void BuildResultBox(RectTransform page, ChatUiAssets assets, FriendsMenuRefs refs) {
        RectTransform box = ChatUiKit.CreateRect("Result", page);
        ChatUiKit.Place(box, 0f, 352f, ContentWidth, 76f);
        refs.ResultBackground = ChatUiKit.AddShape(box.gameObject, assets.rounded, FriendsTheme.TealFill, 1.4f);
        refs.ResultBackground.raycastTarget = false;
        FriendsUiKit.AddBorder(box, new Color(1f, 1f, 1f, 0.2f));
        refs.ResultIcon = ChatUiKit.AddIcon("Icon", box, assets.checkIcon, ChatTheme.Online);
        FriendsUiKit.PlaceInRow(refs.ResultIcon.rectTransform, 26f, 32f, 32f);
        refs.ResultText = ChatUiKit.CreateText("Text", box, assets.font, "", FriendsTheme.BodySize, ChatTheme.Online,
                                               FontStyles.Bold);
        FriendsUiKit.PlaceInRow(refs.ResultText.rectTransform, 74f, ContentWidth - 100f, 60f);
        refs.ResultBox = box.gameObject;
        box.gameObject.SetActive(false);
    }

    private static void BuildStatePage(RectTransform panel, ChatUiAssets assets, FriendsMenuRefs refs) {
        RectTransform page = ChatUiKit.CreateRect("State Page", panel);
        ChatUiKit.Place(page, Margin, ContentTop, ContentWidth, ContentHeight);
        refs.StatePage = page.gameObject;

        RectTransform circle = FriendsUiKit.CreateIconCircle(page, assets, assets.friendsIcon, FriendsTheme.Muted, 136f);
        ChatUiKit.Place(circle, (ContentWidth - 136f) * 0.5f, 70f, 136f, 136f);
        refs.StateIcon = circle.Find("Icon").GetComponent<Image>();

        refs.StateTitle = ChatUiKit.CreateText("Title", page, assets.font, "", 34, Color.white, FontStyles.Bold,
                                               TextAlignmentOptions.Center);
        ChatUiKit.Place(refs.StateTitle.rectTransform, 0f, 230f, ContentWidth, 48f);
        refs.StateBody = ChatUiKit.CreateText("Body", page, assets.font, "", FriendsTheme.BodySize, FriendsTheme.Muted,
                                              FontStyles.Normal, TextAlignmentOptions.Top);
        refs.StateBody.textWrappingMode = TextWrappingModes.Normal;
        ChatUiKit.Place(refs.StateBody.rectTransform, 160f, 290f, ContentWidth - 320f, 80f);

        refs.StateButton = FriendsUiKit.CreateSolidButton("Action Button", page, assets, "", FriendsTheme.GreenTop,
                                                          FriendsTheme.GreenBottom, FriendsTheme.GreenLip,
                                                          assets.userPlusIcon, 28);
        ChatUiKit.Place((RectTransform)refs.StateButton.transform, (ContentWidth - 360f) * 0.5f, 392f, 360f, 92f);
        refs.StateButtonLabel = FriendsUiKit.LabelOf(refs.StateButton);
    }

    private static TMP_InputField CreateInput(RectTransform parent, ChatUiAssets assets, string placeholderText) {
        RectTransform root = ChatUiKit.CreateRect("Search Input", parent);
        Image background = ChatUiKit.AddShape(root.gameObject, assets.rounded, ChatTheme.Field, 1.4f);
        FriendsUiKit.AddBorder(root, new Color(ChatTheme.FocusCyan.r, ChatTheme.FocusCyan.g, ChatTheme.FocusCyan.b, 0.5f));
        RectTransform viewport = ChatUiKit.Stretch(ChatUiKit.CreateRect("Text Area", root), 26f, 8f, 26f, 8f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = ChatUiKit.CreateText("Placeholder", viewport, assets.font, placeholderText,
                                                           FriendsTheme.NameSize, FriendsTheme.Muted, FontStyles.Italic);
        ChatUiKit.Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);
        TextMeshProUGUI text = ChatUiKit.CreateText("Text", viewport, assets.font, "", FriendsTheme.NameSize, Color.white);
        ChatUiKit.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.overflowMode = TextOverflowModes.Overflow;

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.fontAsset = assets.font;
        input.pointSize = FriendsTheme.NameSize;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.characterLimit = 40;
        input.shouldHideMobileInput = false;
        input.caretColor = ChatTheme.FocusCyan;
        input.customCaretColor = true;
        // Spasi ditolak langsung (nama friendlist tidak pernah berisi spasi).
        input.onValidateInput = (_, _, c) => char.IsWhiteSpace(c) ? '\0' : c;
        return input;
    }
}

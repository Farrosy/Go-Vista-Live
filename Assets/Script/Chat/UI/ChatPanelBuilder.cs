using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Referensi UI yang dibuat ChatPanelBuilder, dipakai ChatPanel.
public sealed class ChatPanelRefs {
    public Canvas Canvas;
    public RectTransform Panel;
    public CanvasGroup PanelGroup;
    public Button ToggleButton;
    public GameObject UnreadBadge;
    public TextMeshProUGUI UnreadText;
    public Button CollapseButton;
    public TextMeshProUGUI OnlineText;
    public ScrollRect Scroll;
    public RectTransform Content;
    public TMP_InputField Input;
    public Button SendButton;
}

/// Membangun canvas chat lewat kode (tanpa prefab) di pojok KIRI ATAS safe area, posisi tetap.
///  - Tertutup: hanya tombol 💬 bulat (+ badge jumlah pesan belum dibaca).
///  - Terbuka : panel "CHAT ROOM" di tempat yang sama: header (ikon, judul, jumlah online, tutup),
///              daftar pesan yang bisa di-scroll, dan kolom ketik + tombol kirim.
/// Ukuran mengikuti referensi 1920x1080 (CanvasScaler match 0.5), sama dengan canvas lobby.
public static class ChatPanelBuilder {
    public const int CanvasSortingOrder = 52; // di atas Room Waiting Canvas (51), di bawah transisi venue.
    private const float ListPadding = 16f;
    private const float RowSpacing = 14f;

    public static ChatPanelRefs Build(Transform owner, ChatUiAssets assets) {
        var refs = new ChatPanelRefs();
        refs.Canvas = CreateCanvas(owner);
        RectTransform safeArea = ChatUiKit.Stretch(ChatUiKit.CreateRect("Chat Safe Area", refs.Canvas.transform), 0, 0, 0, 0);
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        BuildToggle(safeArea, assets, refs);
        BuildPanel(safeArea, assets, refs);
        return refs;
    }

    private static Canvas CreateCanvas(Transform owner) {
        var canvasObject = new GameObject("Chat Canvas", typeof(RectTransform), typeof(Canvas),
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

    private static void BuildToggle(RectTransform safeArea, ChatUiAssets assets, ChatPanelRefs refs) {
        float size = ChatTheme.ToggleSize;
        // Lingkaran pink = garis tepi; lingkaran gelap sedikit lebih kecil di atasnya = latar tombol.
        Button toggle = ChatUiKit.CreateButton("Chat Toggle Button", safeArea, assets.circle, ChatTheme.AccentPink);
        ChatUiKit.Place((RectTransform)toggle.transform, ChatTheme.ScreenMargin, ChatTheme.ScreenMargin, size, size);
        RectTransform fill = ChatUiKit.CreateRect("Fill", toggle.transform);
        ChatUiKit.Stretch(fill, 4f, 4f, 4f, 4f);
        ChatUiKit.AddShape(fill.gameObject, assets.circle, ChatTheme.Panel).raycastTarget = false;

        Image icon = ChatUiKit.AddIcon("Icon", toggle.transform, assets.chatIcon, Color.white);
        ChatUiKit.Stretch(icon.rectTransform, 22f, 24f, 22f, 20f);

        RectTransform badge = ChatUiKit.CreateRect("Unread Badge", toggle.transform);
        ChatUiKit.PlaceTopRight(badge, -6f, -6f, 40f, 40f);
        ChatUiKit.AddShape(badge.gameObject, assets.circle, ChatTheme.Badge).raycastTarget = false;
        refs.UnreadText = ChatUiKit.CreateText("Count", badge, assets.font, "", 20, Color.white, FontStyles.Bold,
                                               TextAlignmentOptions.Center);
        ChatUiKit.Stretch(refs.UnreadText.rectTransform, 0, 0, 0, 0);
        badge.gameObject.SetActive(false);

        refs.ToggleButton = toggle;
        refs.UnreadBadge = badge.gameObject;
    }

    private static void BuildPanel(RectTransform safeArea, ChatUiAssets assets, ChatPanelRefs refs) {
        RectTransform panel = ChatUiKit.CreateRect("Chat Panel", safeArea);
        ChatUiKit.Place(panel, ChatTheme.ScreenMargin, ChatTheme.ScreenMargin, ChatTheme.PanelWidth, ChatTheme.PanelHeight);
        ChatUiKit.AddShape(panel.gameObject, assets.rounded, ChatTheme.Panel, 1.6f);
        refs.Panel = panel;
        refs.PanelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        BuildHeader(panel, assets, refs);
        BuildMessageList(panel, assets, refs);
        BuildInputBar(panel, assets, refs);
        panel.gameObject.SetActive(false);
    }

    private static void BuildHeader(RectTransform panel, ChatUiAssets assets, ChatPanelRefs refs) {
        Image icon = ChatUiKit.AddIcon("Header Icon", panel, assets.chatIcon, Color.white);
        ChatUiKit.Place(icon.rectTransform, 24f, 20f, 48f, 48f);
        TextMeshProUGUI title = ChatUiKit.CreateText("Header Title", panel, assets.font, "CHAT ROOM",
                                                     ChatTheme.TitleSize, Color.white, FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, 86f, 20f, 210f, 48f);

        RectTransform pill = ChatUiKit.CreateRect("Online Pill", panel);
        ChatUiKit.Place(pill, 300f, 24f, 150f, 40f);
        ChatUiKit.AddShape(pill.gameObject, assets.rounded, ChatTheme.OnlinePill, 2f).raycastTarget = false;
        RectTransform dot = ChatUiKit.CreateRect("Dot", pill);
        ChatUiKit.Place(dot, 14f, 13f, 14f, 14f);
        ChatUiKit.AddShape(dot.gameObject, assets.circle, ChatTheme.Online).raycastTarget = false;
        refs.OnlineText = ChatUiKit.CreateText("Online Count", pill, assets.font, "0 online", 22, ChatTheme.Online,
                                               FontStyles.Bold);
        ChatUiKit.Place(refs.OnlineText.rectTransform, 36f, 0f, 110f, 40f);

        // Tombol tutup (chevron ke bawah). Area sentuh 72px supaya mudah ditekan jari.
        Button collapse = ChatUiKit.CreateButton("Collapse Button", panel, assets.rounded, new Color(1f, 1f, 1f, 0.06f));
        ChatUiKit.PlaceTopRight((RectTransform)collapse.transform, 16f, 8f, 72f, 72f);
        Image chevron = ChatUiKit.AddIcon("Chevron", collapse.transform, assets.chevron, Color.white);
        ChatUiKit.Stretch(chevron.rectTransform, 22f, 22f, 22f, 22f);
        chevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
        refs.CollapseButton = collapse;

        RectTransform line = ChatUiKit.CreateRect("Header Line", panel);
        ChatUiKit.Place(line, 0f, ChatTheme.HeaderHeight, ChatTheme.PanelWidth, 2f);
        line.gameObject.AddComponent<Image>().color = ChatTheme.Line;
    }

    private static void BuildMessageList(RectTransform panel, ChatUiAssets assets, ChatPanelRefs refs) {
        RectTransform viewport = ChatUiKit.CreateRect("Message Viewport", panel);
        ChatUiKit.Stretch(viewport, 0f, ChatTheme.HeaderHeight + 2f, 0f, ChatTheme.InputBarHeight);
        viewport.gameObject.AddComponent<RectMask2D>();
        // Image transparan supaya area kosong tetap bisa di-drag untuk scroll.
        viewport.gameObject.AddComponent<Image>().color = Color.clear;

        RectTransform content = ChatUiKit.CreateRect("Message Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset((int)ListPadding, (int)ListPadding, (int)ListPadding, (int)ListPadding);
        layout.spacing = RowSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.viewport = viewport;
        scroll.content = content;
        refs.Scroll = scroll;
        refs.Content = content;
    }

    private static void BuildInputBar(RectTransform panel, ChatUiAssets assets, ChatPanelRefs refs) {
        RectTransform line = ChatUiKit.CreateRect("Input Line", panel);
        ChatUiKit.Place(line, 0f, ChatTheme.PanelHeight - ChatTheme.InputBarHeight, ChatTheme.PanelWidth, 2f);
        line.gameObject.AddComponent<Image>().color = ChatTheme.Line;

        float fieldTop = ChatTheme.PanelHeight - ChatTheme.InputBarHeight + 16f;
        refs.Input = CreateInputField(panel, assets);
        ChatUiKit.Place((RectTransform)refs.Input.transform, 24f, fieldTop, 490f, 72f);

        Button send = ChatUiKit.CreateButton("Send Button", panel, assets.sendButton, Color.white);
        ChatUiKit.Place((RectTransform)send.transform, 532f, fieldTop - 2f, 76f, 76f);
        ((Image)send.targetGraphic).preserveAspect = true;
        if (assets.sendButtonPressed != null) {
            send.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = send.spriteState;
            state.pressedSprite = assets.sendButtonPressed;
            send.spriteState = state;
        }
        refs.SendButton = send;
    }

    private static TMP_InputField CreateInputField(RectTransform panel, ChatUiAssets assets) {
        RectTransform root = ChatUiKit.CreateRect("Chat Input", panel);
        Image background = ChatUiKit.AddShape(root.gameObject, assets.rounded, ChatTheme.Field, 1.8f);

        RectTransform viewport = ChatUiKit.CreateRect("Text Area", root);
        ChatUiKit.Stretch(viewport, 20f, 6f, 20f, 6f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = ChatUiKit.CreateText("Placeholder", viewport, assets.font,
                                                           "Ketuk di sini untuk chat…", ChatTheme.BodySize, ChatTheme.Muted);
        ChatUiKit.Stretch(placeholder.rectTransform, 0, 0, 0, 0);
        placeholder.fontStyle = FontStyles.Italic;
        TextMeshProUGUI text = ChatUiKit.CreateText("Text", viewport, assets.font, "", ChatTheme.BodySize, Color.white);
        ChatUiKit.Stretch(text.rectTransform, 0, 0, 0, 0);
        text.overflowMode = TextOverflowModes.Overflow;

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.fontAsset = assets.font;
        input.pointSize = ChatTheme.BodySize;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.richText = false;
        // Batas kasar di kolom ketik (emoji = 2 char UTF-16); batas sebenarnya di ChatRules.Sanitize.
        input.characterLimit = ChatRules.MaxTextLength * 2;
        // Android: tampilkan kolom bawaan di atas keyboard, karena keyboard bisa menutupi panel.
        input.shouldHideMobileInput = false;
        input.caretColor = ChatTheme.FocusCyan;
        input.selectionColor = new Color(ChatTheme.FocusCyan.r, ChatTheme.FocusCyan.g, ChatTheme.FocusCyan.b, 0.35f);
        input.customCaretColor = true;
        return input;
    }
}

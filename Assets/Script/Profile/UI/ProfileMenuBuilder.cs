using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Referensi UI menu PROFIL, diisi ProfileMenuBuilder.
public sealed class ProfileMenuRefs {
    public Canvas Canvas;
    public Button CloseButton;

    // Kiri: karakter
    public RawImage Preview;
    public RectTransform CharacterPill;
    public TextMeshProUGUI CharacterPillText;
    public Button ChangeCharacterButton;

    // Kanan: nama (lihat / edit)
    public LayoutElement NameCard;
    public GameObject ViewGroup;
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI TagText;
    public TextMeshProUGUI NameCaption;
    public GameObject ViewButtons;
    public Button EditButton;
    public Button CopyButton;
    public TextMeshProUGUI CopyLabel;
    public GameObject TagInfoBox;
    public TextMeshProUGUI TagInfoText;

    public GameObject EditGroup;
    public TMP_InputField NameInput;
    public TextMeshProUGUI EditTagText;
    public TextMeshProUGUI CounterText;
    public TextMeshProUGUI EditStatusText;
    public GameObject ErrorBox;
    public TextMeshProUGUI ErrorText;
    public RectTransform EditButtons;
    public Button CancelButton;
    public Button SaveButton;
    public TextMeshProUGUI SaveLabel;
    public Image SaveIcon;

    // Kanan: akun
    public TextMeshProUGUI AccountName;
    public RectTransform AccountBadge;
    public TextMeshProUGUI AccountCaption;
    public Button SignOutButton;

    // Dialog keluar akun
    public GameObject ConfirmOverlay;
    public TextMeshProUGUI ConfirmBody;
    public GameObject ConfirmWarning;
    public RectTransform ConfirmPanel;
    public RectTransform ConfirmButtons;
    public Button ConfirmCancelButton;
    public Button ConfirmSignOutButton;
}

/// Warna & ukuran khusus menu PROFIL (sisanya dari FriendsTheme, supaya gaya sama dengan menu TEMAN).
public static class ProfileTheme {
    public static readonly Color PinkTop = new Color32(0xF4, 0x72, 0xB6, 0xFF);
    public static readonly Color PinkBottom = new Color32(0xDB, 0x27, 0x77, 0xFF);
    public static readonly Color PinkLip = new Color32(0x83, 0x18, 0x43, 0xFF);
    public static readonly Color RedTop = new Color32(0xFB, 0x71, 0x85, 0xFF);
    public static readonly Color RedBottom = new Color32(0xE1, 0x1D, 0x48, 0xFF);
    public static readonly Color RedLip = new Color32(0x88, 0x13, 0x37, 0xFF);
    public static readonly Color Tag = new Color32(0x22, 0xD3, 0xEE, 0xFF);
    public static readonly Color Warning = new Color32(0xFB, 0xBF, 0x24, 0xFF);
    public static readonly Color WarningFill = new Color32(0x2E, 0x26, 0x0E, 0xFF);
    public static readonly Color ErrorFill = new Color32(0x3A, 0x18, 0x24, 0xFF);
    public static readonly Color PreviewTop = new Color32(0x2A, 0x23, 0x5C, 0xFF);
    public static readonly Color PreviewBottom = new Color32(0x14, 0x1A, 0x33, 0xFF);

    public const float PanelWidth = 1520f;
    public const float PanelHeight = 660f;
    public const float Margin = 44f;
    public const float LeftWidth = 500f;
    public const float RightX = Margin + LeftWidth + 44f;
    public const float RightWidth = PanelWidth - RightX - Margin;
    public const float CardPad = 30f;
}

/// Membangun canvas PROFIL lewat kode (Docs/Profil/02..05, 07). Tata letak kolom kanan memakai
/// VerticalLayoutGroup, jadi kartu NAMA bisa memanjang (mode edit / pesan error) tanpa menimpa kartu AKUN.
public static class ProfileMenuBuilder {
    public const int CanvasSortingOrder = 59; // di bawah CharacterSelectCanvas (60) & menu TEMAN (60)

    public static void Build(Transform owner, ChatUiAssets a, ProfileMenuRefs refs) {
        refs.Canvas = CreateCanvas(owner);
        RectTransform dim = ChatUiKit.Stretch(ChatUiKit.CreateRect("Dim", refs.Canvas.transform), 0f, 0f, 0f, 0f);
        dim.gameObject.AddComponent<Image>().color = FriendsTheme.Dim;
        RectTransform safeArea = ChatUiKit.Stretch(ChatUiKit.CreateRect("Safe Area", refs.Canvas.transform), 0f, 0f, 0f, 0f);
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        RectTransform panel = CreatePanel(safeArea, a, "Profile Panel", ProfileTheme.PanelWidth, ProfileTheme.PanelHeight);
        BuildHeader(panel, a, refs);
        BuildCharacterCard(panel, a, refs);
        BuildRightColumn(panel, a, refs);
        BuildConfirmDialog(refs.Canvas.transform, a, refs);
        refs.Canvas.gameObject.SetActive(false);
    }

    private static Canvas CreateCanvas(Transform owner) {
        var canvasObject = new GameObject("Profile Canvas", typeof(RectTransform), typeof(Canvas),
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

    private static RectTransform CreatePanel(Transform parent, ChatUiAssets a, string name, float width, float height) {
        RectTransform panel = ChatUiKit.CreateRect(name, parent);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(width, height);
        panel.gameObject.AddComponent<ResponsivePanel>();
        ChatUiKit.AddShape(panel.gameObject, a.rounded, FriendsTheme.Panel, 1.8f);
        FriendsUiKit.AddBorder(panel, FriendsTheme.CardBorder, 1.8f);
        RectTransform accent = ChatUiKit.CreateRect("Accent", panel);
        ChatUiKit.Place(accent, 80f, 0f, width - 160f, 3f);
        accent.gameObject.AddComponent<Image>().color = Color.white;
        accent.gameObject.AddComponent<UIHorizontalAccent>();
        return panel;
    }

    private static void BuildHeader(RectTransform panel, ChatUiAssets a, ProfileMenuRefs refs) {
        IconBox(panel, a, LoadIcon("icon-user"), ProfileTheme.PinkTop, ProfileTheme.PinkBottom, ProfileTheme.Margin,
                36f, 68f);
        TextMeshProUGUI title = ChatUiKit.CreateText("Title", panel, a.font, "PROFIL", FriendsTheme.TitleSize,
                                                     Color.white, FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, ProfileTheme.Margin + 88f, 36f, 400f, 68f);
        refs.CloseButton = FriendsUiKit.CreateOutlineButton("Close Button", panel, a, "", Color.white,
                                                            FriendsTheme.NeutralFill, a.closeIcon, 28);
        ChatUiKit.PlaceTopRight((RectTransform)refs.CloseButton.transform, ProfileTheme.Margin, 38f, 64f, 64f);
    }

    public static RectTransform IconBox(Transform parent, ChatUiAssets a, Sprite icon, Color top, Color bottom,
                                        float x, float y, float size) {
        RectTransform box = ChatUiKit.CreateRect("Header Icon", parent);
        ChatUiKit.Place(box, x, y, size, size);
        ChatUiKit.AddShape(box.gameObject, a.rounded, Color.white, 1.3f).raycastTarget = false;
        box.gameObject.AddComponent<UIVerticalGradient>().SetColors(top, bottom);
        Image image = ChatUiKit.AddIcon("Icon", box, icon, Color.white);
        ChatUiKit.Stretch(image.rectTransform, size * 0.22f, size * 0.22f, size * 0.22f, size * 0.22f);
        return box;
    }

    // ---------------------------------------------------------------- Kiri: karakter

    private static void BuildCharacterCard(RectTransform panel, ChatUiAssets a, ProfileMenuRefs refs) {
        RectTransform card = ChatUiKit.CreateRect("Character Card", panel);
        ChatUiKit.Place(card, ProfileTheme.Margin, 132f, ProfileTheme.LeftWidth, 484f);
        ChatUiKit.AddShape(card.gameObject, a.rounded, Color.white, 1.6f).raycastTarget = false;
        card.gameObject.AddComponent<UIVerticalGradient>().SetColors(ProfileTheme.PreviewTop, ProfileTheme.PreviewBottom);
        FriendsUiKit.AddBorder(card, FriendsTheme.CardBorder, 1.6f);

        RectTransform previewRect = ChatUiKit.CreateRect("Preview", card);
        ChatUiKit.Place(previewRect, 40f, 16f, ProfileTheme.LeftWidth - 80f, 350f);
        refs.Preview = previewRect.gameObject.AddComponent<RawImage>();
        refs.Preview.color = Color.white;

        refs.CharacterPill = FriendsUiKit.CreatePill(card, a, "-", FriendsTheme.NeutralFill, Color.white,
                                                     FriendsTheme.SmallSize, 42f, new Color(1f, 1f, 1f, 0.2f));
        refs.CharacterPill.anchorMin = refs.CharacterPill.anchorMax = refs.CharacterPill.pivot = new Vector2(0f, 1f);
        refs.CharacterPill.anchoredPosition = new Vector2(20f, -20f);
        refs.CharacterPillText = refs.CharacterPill.GetComponentInChildren<TextMeshProUGUI>();

        refs.ChangeCharacterButton = FriendsUiKit.CreateSolidButton("Change Character Button", card, a, "GANTI KARAKTER",
                                                                    ProfileTheme.PinkTop, ProfileTheme.PinkBottom,
                                                                    ProfileTheme.PinkLip, LoadIcon("icon-swap"), 32);
        ChatUiKit.Place((RectTransform)refs.ChangeCharacterButton.transform, 24f, 370f, ProfileTheme.LeftWidth - 48f, 90f);
    }

    // ---------------------------------------------------------------- Kanan: nama & akun

    private static void BuildRightColumn(RectTransform panel, ChatUiAssets a, ProfileMenuRefs refs) {
        RectTransform column = ChatUiKit.CreateRect("Right Column", panel);
        ChatUiKit.Place(column, ProfileTheme.RightX, 132f, ProfileTheme.RightWidth, 484f);
        VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        SectionLabel(column, a, "NAMA PEMAIN");
        RectTransform nameCard = Card(column, a, "Name Card", 214f, out refs.NameCard);
        ProfileNameCardBuilder.BuildView(nameCard, a, refs);
        ProfileNameCardBuilder.BuildEdit(nameCard, a, refs);

        Spacer(column, 14f);
        SectionLabel(column, a, "AKUN");
        RectTransform accountCard = Card(column, a, "Account Card", 120f, out _);
        BuildAccount(accountCard, a, refs);
    }

    private static void BuildAccount(RectTransform card, ChatUiAssets a, ProfileMenuRefs refs) {
        refs.AccountName = ChatUiKit.CreateText("Account Name", card, a.font, "", FriendsTheme.NameSize + 2, Color.white,
                                                FontStyles.Bold);
        ChatUiKit.Place(refs.AccountName.rectTransform, ProfileTheme.CardPad, 22f, 420f, 44f);
        refs.AccountBadge = FriendsUiKit.CreatePill(card, a, "TERDAFTAR", FriendsTheme.TealFill, FriendsTheme.Teal,
                                                    FriendsTheme.SmallSize - 2, 38f, FriendsTheme.Teal);
        refs.AccountBadge.anchorMin = refs.AccountBadge.anchorMax = refs.AccountBadge.pivot = new Vector2(0f, 1f);
        refs.AccountCaption = ChatUiKit.CreateText("Account Caption", card, a.font, "", FriendsTheme.BodySize,
                                                   FriendsTheme.Muted);
        ChatUiKit.Place(refs.AccountCaption.rectTransform, ProfileTheme.CardPad, 70f, 560f, 32f);

        refs.SignOutButton = FriendsUiKit.CreateOutlineButton("Sign Out Button", card, a, "KELUAR AKUN",
                                                              FriendsTheme.Danger, FriendsTheme.DangerFill,
                                                              LoadIcon("icon-logout"));
        ChatUiKit.PlaceTopRight((RectTransform)refs.SignOutButton.transform, ProfileTheme.CardPad, 28f, 240f, 64f);
    }

    // ---------------------------------------------------------------- Dialog keluar akun (07)

    private static void BuildConfirmDialog(Transform canvas, ChatUiAssets a, ProfileMenuRefs refs) {
        RectTransform overlay = ChatUiKit.Stretch(ChatUiKit.CreateRect("Confirm Overlay", canvas), 0f, 0f, 0f, 0f);
        overlay.gameObject.AddComponent<Image>().color = FriendsTheme.Dim;
        RectTransform panel = CreatePanel(overlay, a, "Confirm Panel", 820f, 380f);
        refs.ConfirmPanel = panel;

        IconBox(panel, a, LoadIcon("icon-logout"), ProfileTheme.RedTop, ProfileTheme.RedBottom, 48f, 44f, 64f);
        TextMeshProUGUI title = ChatUiKit.CreateText("Title", panel, a.font, "Keluar dari akun?", 40, Color.white,
                                                     FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, 132f, 44f, 640f, 64f);

        refs.ConfirmBody = ChatUiKit.CreateText("Body", panel, a.font, "", FriendsTheme.BodySize + 2, FriendsTheme.Muted,
                                                FontStyles.Normal, TextAlignmentOptions.TopLeft);
        refs.ConfirmBody.textWrappingMode = TextWrappingModes.Normal;
        refs.ConfirmBody.richText = true; // username ditebalkan; username hanya huruf/angka/_ (tanpa tag)
        ChatUiKit.Place(refs.ConfirmBody.rectTransform, 48f, 136f, 724f, 72f);

        RectTransform warning = ChatUiKit.CreateRect("Guest Warning", panel);
        ChatUiKit.Place(warning, 48f, 200f, 724f, 76f);
        ChatUiKit.AddShape(warning.gameObject, a.rounded, ProfileTheme.WarningFill, 1.4f).raycastTarget = false;
        FriendsUiKit.AddBorder(warning, new Color(ProfileTheme.Warning.r, ProfileTheme.Warning.g, ProfileTheme.Warning.b, 0.5f));
        TextMeshProUGUI warningText = ChatUiKit.CreateText("Text", warning, a.font,
            "Kamu memakai akun Tamu. Progres, teman, dan karakter yang dipilih akan <u>hilang permanen</u> " +
            "dan tidak bisa dikembalikan.", FriendsTheme.SmallSize, ProfileTheme.Warning, FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        warningText.richText = true;
        warningText.textWrappingMode = TextWrappingModes.Normal;
        ChatUiKit.Stretch(warningText.rectTransform, 22f, 6f, 22f, 6f);
        refs.ConfirmWarning = warning.gameObject;

        RectTransform buttons = ChatUiKit.CreateRect("Buttons", panel);
        ChatUiKit.Place(buttons, 48f, 236f, 724f, 96f);
        refs.ConfirmButtons = buttons;
        refs.ConfirmCancelButton = FriendsUiKit.CreateOutlineButton("Cancel", buttons, a, "BATAL", Color.white,
                                                                    FriendsTheme.NeutralFill, null, 28);
        ChatUiKit.Place((RectTransform)refs.ConfirmCancelButton.transform, 0f, 0f, 352f, 88f);
        refs.ConfirmSignOutButton = FriendsUiKit.CreateSolidButton("Confirm", buttons, a, "KELUAR", ProfileTheme.RedTop,
                                                                   ProfileTheme.RedBottom, ProfileTheme.RedLip, null, 30);
        ChatUiKit.Place((RectTransform)refs.ConfirmSignOutButton.transform, 372f, 0f, 352f, 94f);
        refs.ConfirmOverlay = overlay.gameObject;
        overlay.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- Helper

    public static Sprite LoadIcon(string name) => Resources.Load<Sprite>("UI/Icons/" + name);

    private static void SectionLabel(Transform column, ChatUiAssets a, string text) {
        TextMeshProUGUI label = ChatUiKit.CreateText("Section " + text, column, a.font, text, FriendsTheme.SmallSize,
                                                     FriendsTheme.Muted, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        label.characterSpacing = 8f;
        LayoutElement size = label.gameObject.AddComponent<LayoutElement>();
        size.preferredHeight = size.minHeight = 30f;
    }

    private static void Spacer(Transform column, float height) {
        LayoutElement size = ChatUiKit.CreateRect("Spacer", column).gameObject.AddComponent<LayoutElement>();
        size.preferredHeight = size.minHeight = height;
    }

    private static RectTransform Card(Transform column, ChatUiAssets a, string name, float height, out LayoutElement size) {
        RectTransform card = ChatUiKit.CreateRect(name, column);
        ChatUiKit.AddShape(card.gameObject, a.rounded, FriendsTheme.Card, 1.6f).raycastTarget = false;
        FriendsUiKit.AddBorder(card, FriendsTheme.CardBorder, 1.6f);
        size = card.gameObject.AddComponent<LayoutElement>();
        size.preferredHeight = size.minHeight = height;
        return card;
    }
}

/// Garis aksen tipis di atas panel: gradasi pink → ungu → biru dari kiri ke kanan (seperti mockup).
public sealed class UIHorizontalAccent : BaseMeshEffect {
    private static readonly Color Left = new Color32(0xF4, 0x72, 0xB6, 0xB0);
    private static readonly Color Mid = new Color32(0x8B, 0x5C, 0xF6, 0xB0);
    private static readonly Color Right = new Color32(0x4F, 0x8C, 0xFF, 0xB0);

    public override void ModifyMesh(VertexHelper vh) {
        if (!IsActive() || vh.currentVertCount == 0) return;
        Rect rect = graphic.rectTransform.rect;
        UIVertex vertex = default;
        for (int i = 0; i < vh.currentVertCount; i++) {
            vh.PopulateUIVertex(ref vertex, i);
            float t = Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x);
            vertex.color = t < 0.5f ? Color.Lerp(Left, Mid, t * 2f) : Color.Lerp(Mid, Right, (t - 0.5f) * 2f);
            vh.SetUIVertex(vertex, i);
        }
    }
}

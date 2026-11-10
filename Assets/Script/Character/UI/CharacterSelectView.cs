using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Referensi UI menu GANTI KARAKTER, diisi CharacterSelectView.Build.
public sealed class CharacterSelectRefs {
    public GameObject Root;
    public Button CloseButton;
    public RawImage Preview;
    public Button PreviousButton;
    public Button NextButton;
    public RectTransform Dots;
    public TextMeshProUGUI CounterText;
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI CurrentText;
    public GameObject UsedBadge;
    public TextMeshProUGUI DescriptionText;
    public Button SelectButton;
    public TextMeshProUGUI SelectLabel;
    public Button BackButton;
}

/// Membangun menu GANTI KARAKTER lewat kode (Docs/Profil/06a & 06b), gaya sama dengan menu PROFIL/TEMAN.
/// Kiri: preview 3D (RawImage dari CharacterPreviewStage) dengan tombol ‹ › dan titik halaman.
/// Kanan: "KARAKTER 1 / 2", nama, status (Sedang dipakai: X / badge SEDANG DIPAKAI), deskripsi, PILIH, KEMBALI.
public static class CharacterSelectView {
    private const float PanelWidth = 1620f;
    private const float PanelHeight = 640f;
    private const float Margin = 46f;
    private const float PreviewWidth = 862f;
    private const float InfoX = Margin + PreviewWidth + 46f;
    private const float InfoWidth = PanelWidth - InfoX - Margin;
    private const float ContentTop = 134f;
    private const float PreviewHeight = 462f;
    private const float ArrowSize = 84f;

    public static CharacterSelectRefs Build(Transform canvas, ChatUiAssets a) {
        var refs = new CharacterSelectRefs();
        RectTransform root = ChatUiKit.Stretch(ChatUiKit.CreateRect("Character Select View", canvas), 0f, 0f, 0f, 0f);
        root.gameObject.AddComponent<Image>().color = FriendsTheme.Dim;
        refs.Root = root.gameObject;
        RectTransform safe = ChatUiKit.Stretch(ChatUiKit.CreateRect("Safe Area", root), 0f, 0f, 0f, 0f);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        RectTransform panel = ChatUiKit.CreateRect("Panel", safe);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.gameObject.AddComponent<ResponsivePanel>();
        ChatUiKit.AddShape(panel.gameObject, a.rounded, FriendsTheme.Panel, 1.8f);
        FriendsUiKit.AddBorder(panel, FriendsTheme.CardBorder, 1.8f);
        RectTransform accent = ChatUiKit.CreateRect("Accent", panel);
        ChatUiKit.Place(accent, 80f, 0f, PanelWidth - 160f, 3f);
        accent.gameObject.AddComponent<Image>().color = Color.white;
        accent.gameObject.AddComponent<UIHorizontalAccent>();

        BuildHeader(panel, a, refs);
        BuildPreview(panel, a, refs);
        BuildInfo(panel, a, refs);
        UiTextFit.ApplyAll(root);
        root.gameObject.SetActive(false);
        return refs;
    }

    private static void BuildHeader(RectTransform panel, ChatUiAssets a, CharacterSelectRefs refs) {
        ProfileMenuBuilder.IconBox(panel, a, ProfileMenuBuilder.LoadIcon("icon-swap"), ProfileTheme.PinkTop,
                                   ProfileTheme.PinkBottom, Margin, 40f, 68f);
        TextMeshProUGUI title = ChatUiKit.CreateText("Title", panel, a.font, "GANTI KARAKTER", FriendsTheme.TitleSize,
                                                     Color.white, FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, Margin + 88f, 40f, 700f, 68f);
        refs.CloseButton = FriendsUiKit.CreateOutlineButton("Close Button", panel, a, "", Color.white,
                                                            FriendsTheme.NeutralFill, a.closeIcon, 28);
        ChatUiKit.PlaceTopRight((RectTransform)refs.CloseButton.transform, Margin, 42f, 64f, 64f);
    }

    private static void BuildPreview(RectTransform panel, ChatUiAssets a, CharacterSelectRefs refs) {
        RectTransform card = ChatUiKit.CreateRect("Preview Card", panel);
        ChatUiKit.Place(card, Margin, ContentTop, PreviewWidth, PreviewHeight);
        ChatUiKit.AddShape(card.gameObject, a.rounded, Color.white, 1.6f).raycastTarget = false;
        card.gameObject.AddComponent<UIVerticalGradient>().SetColors(ProfileTheme.PreviewTop, ProfileTheme.PreviewBottom);
        FriendsUiKit.AddBorder(card, FriendsTheme.CardBorder, 1.6f);

        RectTransform previewRect = ChatUiKit.CreateRect("Preview", card);
        ChatUiKit.Place(previewRect, (PreviewWidth - 420f) * 0.5f, 10f, 420f, 400f);
        refs.Preview = previewRect.gameObject.AddComponent<RawImage>();

        refs.PreviousButton = Arrow(card, a, "Previous Button", 24f, true);
        refs.NextButton = Arrow(card, a, "Next Button", PreviewWidth - ArrowSize - 24f, false);

        RectTransform footer = ChatUiKit.CreateRect("Footer", card);
        ChatUiKit.Place(footer, 0f, PreviewHeight - 52f, PreviewWidth, 40f);
        HorizontalLayoutGroup row = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 24f;
        row.childControlWidth = row.childControlHeight = false;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        refs.Dots = ChatUiKit.CreateRect("Dots", footer);
        refs.Dots.sizeDelta = new Vector2(60f, 14f);
        HorizontalLayoutGroup dots = refs.Dots.gameObject.AddComponent<HorizontalLayoutGroup>();
        dots.childAlignment = TextAnchor.MiddleCenter;
        dots.spacing = 10f;
        dots.childControlWidth = dots.childControlHeight = false;
        dots.childForceExpandWidth = dots.childForceExpandHeight = false;
        TextMeshProUGUI hint = ChatUiKit.CreateText("Drag Hint", footer, a.font, "Geser untuk memutar",
                                                    FriendsTheme.SmallSize, FriendsTheme.Muted);
        hint.rectTransform.sizeDelta = new Vector2(hint.GetPreferredValues(hint.text).x + 4f, 40f);
    }

    private static Button Arrow(RectTransform card, ChatUiAssets a, string name, float x, bool left) {
        Button button = FriendsUiKit.CreateOutlineButton(name, card, a, "", Color.white, FriendsTheme.NeutralFill,
                                                         a.chevron, 30);
        ChatUiKit.Place((RectTransform)button.transform, x, (PreviewHeight - ArrowSize) * 0.5f - 10f, ArrowSize, ArrowSize);
        if (left) {
            Transform icon = button.transform.Find("Content/Icon");
            if (icon != null) icon.localEulerAngles = new Vector3(0f, 0f, 180f);
        }
        return button;
    }

    private static void BuildInfo(RectTransform panel, ChatUiAssets a, CharacterSelectRefs refs) {
        refs.CounterText = ChatUiKit.CreateText("Counter", panel, a.font, "KARAKTER 1 / 1", FriendsTheme.SmallSize,
                                                FriendsTheme.Muted, FontStyles.Bold);
        refs.CounterText.characterSpacing = 8f;
        ChatUiKit.Place(refs.CounterText.rectTransform, InfoX, ContentTop - 4f, InfoWidth, 34f);
        refs.NameText = ChatUiKit.CreateText("Character Name", panel, a.font, "-", 64, Color.white, FontStyles.Bold);
        ChatUiKit.Place(refs.NameText.rectTransform, InfoX, ContentTop + 32f, InfoWidth, 80f);

        refs.CurrentText = ChatUiKit.CreateText("Current", panel, a.font, "", FriendsTheme.BodySize, FriendsTheme.Muted);
        refs.CurrentText.richText = true; // nama karakter dari CharacterDatabase (aset sendiri, bukan input pemain)
        ChatUiKit.Place(refs.CurrentText.rectTransform, InfoX, ContentTop + 122f, InfoWidth, 40f);
        RectTransform badge = FriendsUiKit.CreatePill(panel, a, "SEDANG DIPAKAI", FriendsTheme.TealFill,
                                                      FriendsTheme.Teal, FriendsTheme.SmallSize, 42f, FriendsTheme.Teal);
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0f, 1f);
        badge.anchoredPosition = new Vector2(InfoX, -(ContentTop + 120f));
        refs.UsedBadge = badge.gameObject;

        refs.DescriptionText = ChatUiKit.CreateText("Description", panel, a.font, "", FriendsTheme.BodySize + 2,
                                                    new Color(1f, 1f, 1f, 0.8f), FontStyles.Normal,
                                                    TextAlignmentOptions.TopLeft);
        refs.DescriptionText.textWrappingMode = TextWrappingModes.Normal;
        ChatUiKit.Place(refs.DescriptionText.rectTransform, InfoX, ContentTop + 180f, InfoWidth, 90f);

        refs.SelectButton = FriendsUiKit.CreateSolidButton("Select Button", panel, a, "PILIH", ProfileTheme.PinkTop,
                                                           ProfileTheme.PinkBottom, ProfileTheme.PinkLip, a.checkIcon, 34);
        ChatUiKit.Place((RectTransform)refs.SelectButton.transform, InfoX, ContentTop + 270f, InfoWidth, 100f);
        refs.SelectLabel = FriendsUiKit.LabelOf(refs.SelectButton);
        refs.BackButton = FriendsUiKit.CreateOutlineButton("Back Button", panel, a, "KEMBALI", Color.white,
                                                           FriendsTheme.NeutralFill, null, 28);
        ChatUiKit.Place((RectTransform)refs.BackButton.transform, InfoX, ContentTop + 384f, InfoWidth, 80f);
    }
}

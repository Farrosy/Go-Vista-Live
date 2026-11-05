using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Isi kartu NAMA PEMAIN di menu PROFIL: mode lihat (Docs/Profil/02, 05) dan mode edit (03a, 03b, 04).
/// Posisi anak dihitung ulang oleh ProfileMenu sesuai keadaan (pesan error / info tag membuat kartu lebih tinggi).
public static class ProfileNameCardBuilder {
    public const float ViewHeight = 214f;
    public const float GuestViewHeight = 166f;
    public const float TagInfoHeight = 62f;
    public const float EditHeight = 260f;
    public const float ErrorHeight = 74f;
    private const float Pad = ProfileTheme.CardPad;
    private const float InnerWidth = ProfileTheme.RightWidth - Pad * 2f;
    private const float TagBoxWidth = 150f;

    public static void BuildView(RectTransform card, ChatUiAssets a, ProfileMenuRefs refs) {
        RectTransform group = ChatUiKit.Stretch(ChatUiKit.CreateRect("View", card), 0f, 0f, 0f, 0f);
        refs.ViewGroup = group.gameObject;

        refs.NameText = ChatUiKit.CreateText("Name", group, a.font, "", 50, Color.white, FontStyles.Bold,
                                             TextAlignmentOptions.BottomLeft);
        ChatUiKit.Place(refs.NameText.rectTransform, Pad, 18f, InnerWidth - 180f, 66f);
        refs.TagText = ChatUiKit.CreateText("Tag", group, a.font, "", 36, ProfileTheme.Tag, FontStyles.Bold,
                                            TextAlignmentOptions.BottomLeft);
        ChatUiKit.Place(refs.TagText.rectTransform, Pad, 18f, 180f, 62f);

        refs.NameCaption = ChatUiKit.CreateText("Caption", group, a.font, "", FriendsTheme.SmallSize, FriendsTheme.Muted,
                                                FontStyles.Normal, TextAlignmentOptions.TopLeft);
        refs.NameCaption.richText = true; // nama sudah dibatasi huruf/angka/_ (aman untuk rich text)
        refs.NameCaption.textWrappingMode = TextWrappingModes.Normal;
        ChatUiKit.Place(refs.NameCaption.rectTransform, Pad, 90f, InnerWidth, 60f);

        RectTransform buttons = ChatUiKit.CreateRect("Buttons", group);
        ChatUiKit.Place(buttons, Pad, 126f, InnerWidth, 62f);
        refs.ViewButtons = buttons.gameObject;
        refs.EditButton = FriendsUiKit.CreateOutlineButton("Edit Button", buttons, a, "UBAH NAMA", Color.white,
                                                           FriendsTheme.NeutralFill, ProfileMenuBuilder.LoadIcon("icon-edit"));
        ChatUiKit.Place((RectTransform)refs.EditButton.transform, 0f, 0f, 226f, 62f);
        refs.CopyButton = FriendsUiKit.CreateOutlineButton("Copy Button", buttons, a, "SALIN", Color.white,
                                                           FriendsTheme.NeutralFill, ProfileMenuBuilder.LoadIcon("icon-copy"));
        ChatUiKit.Place((RectTransform)refs.CopyButton.transform, 240f, 0f, 190f, 62f);
        refs.CopyLabel = FriendsUiKit.LabelOf(refs.CopyButton);

        refs.TagInfoBox = MessageBox(group, a, "Tag Info", FriendsTheme.TealFill, FriendsTheme.Teal, a.checkIcon,
                                     out refs.TagInfoText);
        ChatUiKit.Place((RectTransform)refs.TagInfoBox.transform, Pad, 202f, InnerWidth, 52f);
        refs.TagInfoBox.SetActive(false);
    }

    public static void BuildEdit(RectTransform card, ChatUiAssets a, ProfileMenuRefs refs) {
        RectTransform group = ChatUiKit.Stretch(ChatUiKit.CreateRect("Edit", card), 0f, 0f, 0f, 0f);
        refs.EditGroup = group.gameObject;

        refs.NameInput = CreateInput(group, a);
        ChatUiKit.Place((RectTransform)refs.NameInput.transform, Pad, 26f, InnerWidth - TagBoxWidth - 14f, 78f);

        RectTransform tagBox = ChatUiKit.CreateRect("Tag Box", group);
        ChatUiKit.PlaceTopRight(tagBox, Pad, 26f, TagBoxWidth, 78f);
        ChatUiKit.AddShape(tagBox.gameObject, a.rounded, ChatTheme.Field, 1.4f).raycastTarget = false;
        FriendsUiKit.AddBorder(tagBox, new Color(1f, 1f, 1f, 0.12f));
        refs.EditTagText = ChatUiKit.CreateText("Tag", tagBox, a.font, "", 32, ProfileTheme.Tag, FontStyles.Bold,
                                                TextAlignmentOptions.Center);
        ChatUiKit.Stretch(refs.EditTagText.rectTransform, 8f, 0f, 8f, 0f);

        TextMeshProUGUI hint = ChatUiKit.CreateText("Hint", group, a.font, PlayerNameRules.Hint, FriendsTheme.SmallSize - 2,
                                                    FriendsTheme.Muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        hint.textWrappingMode = TextWrappingModes.Normal;
        ChatUiKit.Place(hint.rectTransform, Pad, 114f, InnerWidth - 110f, 30f);
        refs.CounterText = ChatUiKit.CreateText("Counter", group, a.font, "", FriendsTheme.SmallSize - 2, FriendsTheme.Muted,
                                                FontStyles.Normal, TextAlignmentOptions.TopRight);
        ChatUiKit.PlaceTopRight(refs.CounterText.rectTransform, Pad, 114f, 100f, 30f);

        refs.EditStatusText = ChatUiKit.CreateText("Status", group, a.font, "", FriendsTheme.SmallSize - 2,
                                                   FriendsTheme.Muted);
        ChatUiKit.Place(refs.EditStatusText.rectTransform, Pad, 150f, InnerWidth, 30f);

        refs.ErrorBox = MessageBox(group, a, "Error", ProfileTheme.ErrorFill, FriendsTheme.Danger,
                                   ProfileMenuBuilder.LoadIcon("icon-x"), out refs.ErrorText);
        ChatUiKit.Place((RectTransform)refs.ErrorBox.transform, Pad, 150f, InnerWidth, 62f);
        refs.ErrorBox.SetActive(false);

        RectTransform buttons = ChatUiKit.CreateRect("Buttons", group);
        ChatUiKit.PlaceTopRight(buttons, Pad, 176f, 420f, 70f);
        refs.EditButtons = buttons;
        refs.CancelButton = FriendsUiKit.CreateOutlineButton("Cancel Button", buttons, a, "BATAL", Color.white,
                                                             FriendsTheme.NeutralFill);
        ChatUiKit.Place((RectTransform)refs.CancelButton.transform, 0f, 2f, 170f, 64f);
        refs.SaveButton = FriendsUiKit.CreateSolidButton("Save Button", buttons, a, "SIMPAN", FriendsTheme.GreenTop,
                                                         FriendsTheme.GreenBottom, FriendsTheme.GreenLip, a.checkIcon, 26);
        ChatUiKit.Place((RectTransform)refs.SaveButton.transform, 186f, 0f, 234f, 70f);
        refs.SaveLabel = FriendsUiKit.LabelOf(refs.SaveButton);
        refs.SaveIcon = refs.SaveButton.transform.Find("Face/Content/Icon")?.GetComponent<Image>();
        group.gameObject.SetActive(false);
    }

    /// Kotak pesan berwarna dengan ikon di kiri (info tag baru / error simpan).
    private static GameObject MessageBox(Transform parent, ChatUiAssets a, string name, Color fill, Color color, Sprite icon,
                                         out TextMeshProUGUI text) {
        RectTransform box = ChatUiKit.CreateRect(name, parent);
        ChatUiKit.AddShape(box.gameObject, a.rounded, fill, 1.4f).raycastTarget = false;
        FriendsUiKit.AddBorder(box, new Color(color.r, color.g, color.b, 0.5f));
        Image image = ChatUiKit.AddIcon("Icon", box, icon, color);
        FriendsUiKit.PlaceInRow(image.rectTransform, 22f, 28f, 28f);
        text = ChatUiKit.CreateText("Text", box, a.font, "", FriendsTheme.SmallSize, color, FontStyles.Bold);
        text.textWrappingMode = TextWrappingModes.Normal;
        FriendsUiKit.PlaceInRow(text.rectTransform, 64f, InnerWidth - 84f, 56f);
        return box.gameObject;
    }

    private static TMP_InputField CreateInput(RectTransform parent, ChatUiAssets a) {
        RectTransform root = ChatUiKit.CreateRect("Name Input", parent);
        Image background = ChatUiKit.AddShape(root.gameObject, a.rounded, ChatTheme.Field, 1.4f);
        FriendsUiKit.AddBorder(root, new Color(ChatTheme.FocusCyan.r, ChatTheme.FocusCyan.g, ChatTheme.FocusCyan.b, 0.6f));
        RectTransform viewport = ChatUiKit.Stretch(ChatUiKit.CreateRect("Text Area", root), 24f, 8f, 24f, 8f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = ChatUiKit.CreateText("Placeholder", viewport, a.font, "Nama pemain",
                                                           FriendsTheme.NameSize + 2, FriendsTheme.Muted, FontStyles.Italic);
        ChatUiKit.Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);
        TextMeshProUGUI text = ChatUiKit.CreateText("Text", viewport, a.font, "", FriendsTheme.NameSize + 2, Color.white);
        ChatUiKit.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.overflowMode = TextOverflowModes.Overflow;

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.fontAsset = a.font;
        input.pointSize = FriendsTheme.NameSize + 2;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.characterLimit = PlayerNameRules.MaxLength;
        input.shouldHideMobileInput = false;
        input.caretColor = ChatTheme.FocusCyan;
        input.customCaretColor = true;
        // Hanya huruf/angka/_ — karakter lain ditolak saat diketik maupun di-paste.
        input.onValidateInput = (_, _, c) => PlayerNameRules.IsAllowedChar(c) ? c : '\0';
        return input;
    }
}

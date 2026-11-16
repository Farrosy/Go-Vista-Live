using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Satu kartu template (4 buah) di layar Pilih Karakter.
public sealed class TemplateCardRefs {
    public Button Button;
    public RawImage Thumbnail;
    public GameObject Selected;
    public GameObject UsedBadge;
}

/// Satu slot "Karakter kamu" (2 buah): isi (Filled) atau kosong (Empty, tombol + Buat).
public sealed class SlotCardRefs {
    public GameObject Filled;
    public Button Button;
    public RawImage Thumbnail;
    public TextMeshProUGUI Name;
    public TextMeshProUGUI Origin;
    public Button EditButton;
    public Button DeleteButton;
    public GameObject Selected;
    public GameObject UsedBadge;
    public Button CreateButton;
}

/// Referensi UI layar Pilih Karakter, diisi CustomizerPickView.Build.
public sealed class CustomizerPickRefs {
    public GameObject Root;
    public Button CloseButton;
    public RawImage Preview;
    public GameObject Loading;
    public TextMeshProUGUI PreviewName;
    public TemplateCardRefs[] Templates;
    public SlotCardRefs[] Slots;
    public TextMeshProUGUI SlotCounter;
    public Button UseButton;
    public TextMeshProUGUI UseLabel;
    public GameObject ConfirmOverlay;
    public TextMeshProUGUI ConfirmTitle;
    public Button ConfirmCancel;
    public Button ConfirmDelete;
    public TextMeshProUGUI Toast;
}

/// <summary>
/// Layar 1 customizer (mockup v2 "Pilih Karakter"): kiri preview 3D, kanan 4 template terkunci +
/// 2 slot "Karakter kamu" + tombol PAKAI. Gaya sama dengan menu PROFIL/TEMAN (FriendsUiKit).
/// Menggantikan carousel lama (CharacterSelectView, sudah dihapus).
/// </summary>
public static class CustomizerPickView {
    private const float PanelWidth = 1620f;
    private const float PanelHeight = 780f;
    private const float Margin = 46f;
    private const float ContentTop = 134f;
    private const float PreviewWidth = 640f;
    private const float PreviewHeight = PanelHeight - ContentTop - Margin;
    private const float RightX = Margin + PreviewWidth + 40f;
    private const float RightWidth = PanelWidth - RightX - Margin;
    private const float CardGap = 14f;
    private const float TemplateHeight = 220f;
    private const float SlotHeight = 168f;
    private const float LabelHeight = 32f;
    private const float UseHeight = 92f;

    public static CustomizerPickRefs Build(Transform canvas, ChatUiAssets a) {
        var refs = new CustomizerPickRefs();
        RectTransform root = ChatUiKit.Stretch(ChatUiKit.CreateRect("Customizer Pick View", canvas), 0f, 0f, 0f, 0f);
        root.gameObject.AddComponent<Image>().color = FriendsTheme.Dim;
        refs.Root = root.gameObject;
        RectTransform safe = ChatUiKit.Stretch(ChatUiKit.CreateRect("Safe Area", root), 0f, 0f, 0f, 0f);
        safe.gameObject.AddComponent<SafeAreaFitter>();
        RectTransform panel = CustomizerUi.CreatePanel(safe, a, PanelWidth, PanelHeight);

        BuildHeader(panel, a, refs);
        BuildPreview(panel, a, refs);
        BuildTemplates(panel, a, refs);
        BuildSlots(panel, a, refs);
        BuildFooter(panel, a, refs);
        BuildConfirm(root, a, refs);
        refs.Toast = CustomizerUi.CreateToast(root, a);
        UiTextFit.ApplyAll(root);
        root.gameObject.SetActive(false);
        return refs;
    }

    private static void BuildHeader(RectTransform panel, ChatUiAssets a, CustomizerPickRefs refs) {
        ProfileMenuBuilder.IconBox(panel, a, ProfileMenuBuilder.LoadIcon("icon-user"), ProfileTheme.PinkTop,
                                   ProfileTheme.PinkBottom, Margin, 40f, 68f);
        TextMeshProUGUI title = ChatUiKit.CreateText("Title", panel, a.font, "KARAKTER", FriendsTheme.TitleSize,
                                                     Color.white, FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, Margin + 88f, 40f, 700f, 68f);
        refs.CloseButton = FriendsUiKit.CreateOutlineButton("Close Button", panel, a, "", Color.white,
                                                            FriendsTheme.NeutralFill, a.closeIcon, 28);
        ChatUiKit.PlaceTopRight((RectTransform)refs.CloseButton.transform, Margin, 42f, 64f, 64f);
    }

    private static void BuildPreview(RectTransform panel, ChatUiAssets a, CustomizerPickRefs refs) {
        RectTransform card = CustomizerUi.CreatePreviewCard(panel, a, Margin, ContentTop, PreviewWidth, PreviewHeight);
        RectTransform previewRect = ChatUiKit.CreateRect("Preview", card);
        ChatUiKit.Place(previewRect, (PreviewWidth - 520f) * 0.5f, 20f, 520f, PreviewHeight - 110f);
        refs.Preview = previewRect.gameObject.AddComponent<RawImage>();

        refs.PreviewName = ChatUiKit.CreateText("Preview Name", card, a.font, "", FriendsTheme.NameSize, Color.white,
                                                FontStyles.Bold, TextAlignmentOptions.Center);
        ChatUiKit.Place(refs.PreviewName.rectTransform, 0f, PreviewHeight - 92f, PreviewWidth, 40f);
        TextMeshProUGUI hint = ChatUiKit.CreateText("Drag Hint", card, a.font, "Geser untuk memutar",
                                                    FriendsTheme.SmallSize, FriendsTheme.Muted,
                                                    FontStyles.Normal, TextAlignmentOptions.Center);
        ChatUiKit.Place(hint.rectTransform, 0f, PreviewHeight - 50f, PreviewWidth, 34f);
        refs.Loading = CustomizerUi.CreateLoadingPill(card, a, PreviewWidth);
    }

    private static void BuildTemplates(RectTransform panel, ChatUiAssets a, CustomizerPickRefs refs) {
        CustomizerUi.CreateGroupLabel(panel, a, "TEMPLATE", "Tidak bisa dihapus", RightX, ContentTop, RightWidth);
        int count = CharacterTemplates.All.Count;
        float width = (RightWidth - CardGap * (count - 1)) / count;
        float y = ContentTop + LabelHeight + 8f;
        refs.Templates = new TemplateCardRefs[count];
        for (int i = 0; i < count; i++) {
            CharacterTemplate template = CharacterTemplates.All[i];
            refs.Templates[i] = BuildTemplateCard(panel, a, template, RightX + i * (width + CardGap), y, width);
        }
    }

    private static TemplateCardRefs BuildTemplateCard(RectTransform panel, ChatUiAssets a, CharacterTemplate template,
                                                      float x, float y, float width) {
        var card = new TemplateCardRefs();
        RectTransform rect = CustomizerUi.CreatePreviewCard(panel, a, x, y, width, TemplateHeight);
        rect.name = "Template " + template.Id;
        card.Button = rect.gameObject.AddComponent<Button>();
        card.Button.targetGraphic = rect.GetComponent<Image>();

        RectTransform thumb = ChatUiKit.CreateRect("Thumbnail", rect);
        ChatUiKit.Place(thumb, 10f, 10f, width - 20f, TemplateHeight - 84f);
        card.Thumbnail = thumb.gameObject.AddComponent<RawImage>();
        card.Thumbnail.raycastTarget = false;

        CustomizerUi.CreateCardLabel(rect, a, template.StyleLabel, template.GenderLabel, TemplateHeight - 72f, width);
        card.Selected = CustomizerUi.CreateSelectionOutline(rect);
        card.UsedBadge = CustomizerUi.CreateUsedBadge(rect, a);
        return card;
    }

    private static void BuildSlots(RectTransform panel, ChatUiAssets a, CustomizerPickRefs refs) {
        float labelY = ContentTop + LabelHeight + 8f + TemplateHeight + 26f;
        refs.SlotCounter = CustomizerUi.CreateGroupLabel(panel, a, "KARAKTER KAMU", "0 / 2", RightX, labelY, RightWidth);
        int count = CustomSlotRules.MaxCustomSlots;
        float width = (RightWidth - CardGap * (count - 1)) / count;
        float y = labelY + LabelHeight + 8f;
        refs.Slots = new SlotCardRefs[count];
        for (int i = 0; i < count; i++) refs.Slots[i] = BuildSlotCard(panel, a, i, RightX + i * (width + CardGap), y, width);
    }

    private static SlotCardRefs BuildSlotCard(RectTransform panel, ChatUiAssets a, int index, float x, float y, float width) {
        var slot = new SlotCardRefs();
        RectTransform filled = CustomizerUi.CreatePreviewCard(panel, a, x, y, width, SlotHeight);
        filled.name = "Slot " + (index + 1);
        slot.Filled = filled.gameObject;
        slot.Button = filled.gameObject.AddComponent<Button>();
        slot.Button.targetGraphic = filled.GetComponent<Image>();

        RectTransform thumb = ChatUiKit.CreateRect("Thumbnail", filled);
        ChatUiKit.Place(thumb, 10f, 10f, 120f, SlotHeight - 20f);
        slot.Thumbnail = thumb.gameObject.AddComponent<RawImage>();
        slot.Thumbnail.raycastTarget = false;

        float infoX = 146f;
        float infoWidth = width - infoX - 16f;
        slot.Name = ChatUiKit.CreateText("Name", filled, a.font, CustomSlotRules.SlotName(index), FriendsTheme.NameSize,
                                         Color.white, FontStyles.Bold);
        ChatUiKit.Place(slot.Name.rectTransform, infoX, 22f, infoWidth, 40f);
        slot.Origin = ChatUiKit.CreateText("Origin", filled, a.font, "", FriendsTheme.SmallSize, FriendsTheme.Muted);
        ChatUiKit.Place(slot.Origin.rectTransform, infoX, 62f, infoWidth, 32f);

        slot.EditButton = FriendsUiKit.CreateOutlineButton("Edit Button", filled, a, "UBAH", Color.white,
                                                           FriendsTheme.NeutralFill, ProfileMenuBuilder.LoadIcon("icon-edit"), 22);
        ChatUiKit.Place((RectTransform)slot.EditButton.transform, infoX, SlotHeight - 68f, infoWidth - 72f, 54f);
        slot.DeleteButton = FriendsUiKit.CreateOutlineButton("Delete Button", filled, a, "", FriendsTheme.Danger,
                                                             FriendsTheme.DangerFill, a.closeIcon, 22);
        ChatUiKit.Place((RectTransform)slot.DeleteButton.transform, infoX + infoWidth - 60f, SlotHeight - 68f, 60f, 54f);

        slot.Selected = CustomizerUi.CreateSelectionOutline(filled);
        slot.UsedBadge = CustomizerUi.CreateUsedBadge(filled, a);
        slot.CreateButton = CustomizerUi.CreateEmptySlot(panel, a, x, y, width, SlotHeight);
        return slot;
    }

    private static void BuildFooter(RectTransform panel, ChatUiAssets a, CustomizerPickRefs refs) {
        refs.UseButton = FriendsUiKit.CreateSolidButton("Use Button", panel, a, "PAKAI KARAKTER INI", FriendsTheme.GreenTop,
                                                        FriendsTheme.GreenBottom, FriendsTheme.GreenLip, a.checkIcon, 30);
        ChatUiKit.Place((RectTransform)refs.UseButton.transform, RightX, PanelHeight - Margin - UseHeight, RightWidth, UseHeight);
        refs.UseLabel = FriendsUiKit.LabelOf(refs.UseButton);
    }

    private static void BuildConfirm(RectTransform root, ChatUiAssets a, CustomizerPickRefs refs) {
        CustomizerUi.ConfirmDialog dialog = CustomizerUi.CreateConfirmDialog(root, a, "Hapus karakter?",
            "Karakter ini akan dihapus dari akunmu dan tidak bisa dikembalikan.", "BATAL", "HAPUS");
        refs.ConfirmOverlay = dialog.Overlay;
        refs.ConfirmTitle = dialog.Title;
        refs.ConfirmCancel = dialog.Cancel;
        refs.ConfirmDelete = dialog.Confirm;
    }
}

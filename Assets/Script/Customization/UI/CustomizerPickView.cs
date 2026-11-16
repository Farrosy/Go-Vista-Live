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
/// Layar 1 scene KARAKTER (mockup v2 "Pilih Karakter"), layar penuh tanpa kartu overlay:
/// bar atas (KEMBALI + judul), karakter 3D langsung di latar panggung (kiri), panel kanan berisi
/// 4 template terkunci + 2 slot "Karakter kamu" + PAKAI KARAKTER INI. Gaya sama dengan menu
/// PROFIL/TEMAN (FriendsUiKit). Tinggi panel & panggung mengikuti layar (atas/bawah tetap).
/// </summary>
public static class CustomizerPickView {
    /// Rasio RenderTexture panggung (lebar/tinggi), sama dengan textureSize di CharacterScene.
    public const float PreviewAspect = 1024f / 1280f;

    private const float Edge = 48f;
    private const float TopBarY = 32f;
    private const float TopBarHeight = 76f;
    private const float ContentTop = 128f;
    private const float ContentBottom = 32f;
    private const float PickerWidth = 872f;
    private const float StageGap = 24f;
    private const float Pad = 30f;
    private const float InnerWidth = PickerWidth - Pad * 2f;
    private const float CardGap = 14f;
    private const float LabelHeight = 32f;
    private const float TemplateHeight = 240f;
    private const float SlotHeight = 200f;
    private const float UseHeight = 76f;

    public static CustomizerPickRefs Build(Transform canvas, ChatUiAssets a) {
        var refs = new CustomizerPickRefs();
        RectTransform root = ChatUiKit.Stretch(ChatUiKit.CreateRect("Customizer Pick View", canvas), 0f, 0f, 0f, 0f);
        refs.Root = root.gameObject;
        CustomizerUi.CreateStageBackground(root, a);
        RectTransform safe = ChatUiKit.Stretch(ChatUiKit.CreateRect("Safe Area", root), 0f, 0f, 0f, 0f);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        BuildTopBar(safe, a, refs);
        BuildStage(safe, a, refs);
        RectTransform picker = CustomizerUi.CreateSidePanel(safe, a, Edge, ContentTop, PickerWidth, ContentBottom);
        picker.name = "Picker Panel";
        float y = BuildTemplates(picker, a, refs, Pad);
        BuildSlots(picker, a, refs, y + 18f);
        BuildFooter(picker, a, refs);
        BuildConfirm(root, a, refs);
        refs.Toast = CustomizerUi.CreateToast(root, a);
        UiTextFit.ApplyAll(root);
        root.gameObject.SetActive(false);
        return refs;
    }

    private static void BuildTopBar(RectTransform parent, ChatUiAssets a, CustomizerPickRefs refs) {
        refs.CloseButton = CustomizerUi.CreateBackButton(parent, a, Edge, TopBarY, TopBarHeight);
        float x = Edge + TopBarHeight + 20f;
        ProfileMenuBuilder.IconBox(parent, a, ProfileMenuBuilder.LoadIcon("icon-user"), ProfileTheme.PinkTop,
                                   ProfileTheme.PinkBottom, x, TopBarY + 4f, 68f);
        TextMeshProUGUI title = ChatUiKit.CreateText("Title", parent, a.font, "KARAKTER", FriendsTheme.TitleSize,
                                                     Color.white, FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, x + 88f, TopBarY + 4f, 600f, 68f);
    }

    /// Panggung kiri: karakter + alas 3D (objek Floor di Preview Stage, ikut dirender kamera) (RawImage dengan rasio RenderTexture) + nama & petunjuk geser.
    private static void BuildStage(RectTransform parent, ChatUiAssets a, CustomizerPickRefs refs) {
        RectTransform area = ChatUiKit.Stretch(ChatUiKit.CreateRect("Stage Area", parent), Edge, ContentTop,
                                               Edge + PickerWidth + StageGap, ContentBottom);

        // Rect preview stretch, isinya dijaga rasio RenderTexture supaya karakter tidak gepeng.
        RectTransform previewRect = ChatUiKit.Stretch(ChatUiKit.CreateRect("Preview", area), 0f, 0f, 0f, 92f);
        AspectRatioFitter fitter = previewRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = PreviewAspect;
        refs.Preview = previewRect.gameObject.AddComponent<RawImage>();

        refs.PreviewName = ChatUiKit.CreateText("Preview Name", area, a.font, "", FriendsTheme.NameSize, Color.white,
                                                FontStyles.Bold, TextAlignmentOptions.Center);
        BottomRow(refs.PreviewName.rectTransform, 38f, 40f);
        TextMeshProUGUI hint = ChatUiKit.CreateText("Drag Hint", area, a.font, "Geser untuk memutar",
                                                    FriendsTheme.SmallSize, FriendsTheme.Muted,
                                                    FontStyles.Normal, TextAlignmentOptions.Center);
        BottomRow(hint.rectTransform, 0f, 34f);
        refs.Loading = CustomizerUi.CreateLoadingPill(area, a, 0f);
    }

    private static float BuildTemplates(RectTransform panel, ChatUiAssets a, CustomizerPickRefs refs, float top) {
        CustomizerUi.CreateGroupLabel(panel, a, "TEMPLATE", "Tidak bisa dihapus", Pad, top, InnerWidth);
        int count = CharacterTemplates.All.Count;
        float width = (InnerWidth - CardGap * (count - 1)) / count;
        float y = top + LabelHeight + 10f;
        refs.Templates = new TemplateCardRefs[count];
        for (int i = 0; i < count; i++) {
            CharacterTemplate template = CharacterTemplates.All[i];
            refs.Templates[i] = BuildTemplateCard(panel, a, template, Pad + i * (width + CardGap), y, width);
        }
        return y + TemplateHeight;
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

    private static void BuildSlots(RectTransform panel, ChatUiAssets a, CustomizerPickRefs refs, float top) {
        refs.SlotCounter = CustomizerUi.CreateGroupLabel(panel, a, "KARAKTER KAMU", "0 / 2", Pad, top, InnerWidth);
        int count = CustomSlotRules.MaxCustomSlots;
        float width = (InnerWidth - CardGap * (count - 1)) / count;
        float y = top + LabelHeight + 10f;
        refs.Slots = new SlotCardRefs[count];
        for (int i = 0; i < count; i++) refs.Slots[i] = BuildSlotCard(panel, a, i, Pad + i * (width + CardGap), y, width);
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
        float infoWidth = width - infoX - 18f;
        slot.Name = ChatUiKit.CreateText("Name", filled, a.font, CustomSlotRules.SlotName(index), FriendsTheme.NameSize,
                                         Color.white, FontStyles.Bold);
        ChatUiKit.Place(slot.Name.rectTransform, infoX, 26f, infoWidth, 40f);
        slot.Origin = ChatUiKit.CreateText("Origin", filled, a.font, "", FriendsTheme.SmallSize, FriendsTheme.Muted);
        ChatUiKit.Place(slot.Origin.rectTransform, infoX, 68f, infoWidth, 32f);

        slot.EditButton = FriendsUiKit.CreateOutlineButton("Edit Button", filled, a, "UBAH", Color.white,
                                                           FriendsTheme.NeutralFill, ProfileMenuBuilder.LoadIcon("icon-edit"), 22);
        ChatUiKit.Place((RectTransform)slot.EditButton.transform, infoX, SlotHeight - 76f, infoWidth - 74f, 58f);
        slot.DeleteButton = FriendsUiKit.CreateOutlineButton("Delete Button", filled, a, "", FriendsTheme.Danger,
                                                             FriendsTheme.DangerFill, a.closeIcon, 22);
        ChatUiKit.Place((RectTransform)slot.DeleteButton.transform, infoX + infoWidth - 62f, SlotHeight - 76f, 62f, 58f);

        slot.Selected = CustomizerUi.CreateSelectionOutline(filled);
        slot.UsedBadge = CustomizerUi.CreateUsedBadge(filled, a);
        slot.CreateButton = CustomizerUi.CreateEmptySlot(panel, a, x, y, width, SlotHeight);
        return slot;
    }

    /// PAKAI KARAKTER INI menempel di bawah panel (ikut tinggi layar).
    private static void BuildFooter(RectTransform panel, ChatUiAssets a, CustomizerPickRefs refs) {
        refs.UseButton = FriendsUiKit.CreateSolidButton("Use Button", panel, a, "PAKAI KARAKTER INI", FriendsTheme.GreenTop,
                                                        FriendsTheme.GreenBottom, FriendsTheme.GreenLip, a.checkIcon, 30);
        var rect = (RectTransform)refs.UseButton.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(Pad, Pad);
        rect.sizeDelta = new Vector2(InnerWidth, UseHeight);
        refs.UseLabel = FriendsUiKit.LabelOf(refs.UseButton);
    }

    /// Baris teks selebar panggung, menempel di bawah dengan jarak <paramref name="bottom"/>.
    private static void BottomRow(RectTransform rect, float bottom, float height) {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(0f, bottom);
        rect.offsetMax = new Vector2(0f, bottom + height);
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

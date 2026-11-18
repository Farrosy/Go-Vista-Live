using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Potongan UI yang dipakai berulang oleh layar customizer (Pilih Karakter + editor), dibangun dengan
/// ChatUiKit/FriendsUiKit supaya gayanya sama dengan menu PROFIL/TEMAN: panel navy + garis aksen,
/// kartu bergradasi ungu (seperti kartu preview Profil), garis seleksi cyan, badge DIPAKAI, dialog konfirmasi.
/// </summary>
public static class CustomizerUi {
    public const float SelectionCorner = 1.6f;
    private const float AccentInset = 80f;
    private const float AccentHeight = 3f;
    private const float BadgeHeight = 34f;
    private const float DialogWidth = 760f;
    private const float DialogHeight = 340f;
    private const float DialogPad = 40f;

    public readonly struct ConfirmDialog {
        public readonly GameObject Overlay;
        public readonly TextMeshProUGUI Title;
        public readonly TextMeshProUGUI Body;
        public readonly Button Cancel;
        public readonly Button Confirm;

        public ConfirmDialog(GameObject overlay, TextMeshProUGUI title, TextMeshProUGUI body, Button cancel, Button confirm) {
            Overlay = overlay;
            Title = title;
            Body = body;
            Cancel = cancel;
            Confirm = confirm;
        }
    }

    public static RectTransform CreatePanel(Transform parent, ChatUiAssets a, float width, float height) {
        RectTransform panel = ChatUiKit.CreateRect("Panel", parent);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(width, height);
        panel.gameObject.AddComponent<ResponsivePanel>();
        ChatUiKit.AddShape(panel.gameObject, a.rounded, FriendsTheme.Panel, 1.8f);
        FriendsUiKit.AddBorder(panel, FriendsTheme.CardBorder, 1.8f);
        RectTransform accent = ChatUiKit.CreateRect("Accent", panel);
        ChatUiKit.Place(accent, AccentInset, 0f, width - AccentInset * 2f, AccentHeight);
        accent.gameObject.AddComponent<Image>().color = Color.white;
        accent.gameObject.AddComponent<UIHorizontalAccent>();
        return panel;
    }

    /// Panel navy menempel di kanan layar, tinggi mengikuti layar (atas/bawah tetap). Isi diletakkan dari atas.
    public static RectTransform CreateSidePanel(Transform parent, ChatUiAssets a, float right, float top, float width,
                                                float bottom) {
        RectTransform panel = ChatUiKit.CreateRect("Panel", parent);
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.offsetMin = new Vector2(-right - width, bottom);
        panel.offsetMax = new Vector2(-right, -top);
        ChatUiKit.AddShape(panel.gameObject, a.rounded, FriendsTheme.Panel, 1.8f);
        FriendsUiKit.AddBorder(panel, FriendsTheme.CardBorder, 1.8f);
        RectTransform accent = ChatUiKit.CreateRect("Accent", panel);
        ChatUiKit.Place(accent, AccentInset, 0f, width - AccentInset * 2f, AccentHeight);
        accent.gameObject.AddComponent<Image>().color = Color.white;
        accent.gameObject.AddComponent<UIHorizontalAccent>();
        return panel;
    }

    /// Latar layar penuh scene karakter: gradasi ungu gelap (sama dengan kartu preview) + cahaya lembut.
    public static RectTransform CreateStageBackground(Transform parent, ChatUiAssets a) {
        RectTransform background = ChatUiKit.Stretch(ChatUiKit.CreateRect("Stage Background", parent), 0f, 0f, 0f, 0f);
        background.gameObject.AddComponent<Image>().color = Color.white; // menahan klik ke lobby di belakang
        background.gameObject.AddComponent<UIVerticalGradient>().SetColors(ProfileTheme.PreviewTop, ProfileTheme.PreviewBottom);
        return background;
    }

    /// Tombol kembali kotak (ikon panah kiri = chevron diputar 180°).
    public static Button CreateBackButton(Transform parent, ChatUiAssets a, float x, float y, float size) {
        Button back = FriendsUiKit.CreateOutlineButton("Back Button", parent, a, "", Color.white, FriendsTheme.NeutralFill,
                                                       a.chevron, 28);
        ChatUiKit.Place((RectTransform)back.transform, x, y, size, size);
        Transform icon = back.transform.Find("Content/Icon");
        if (icon != null) icon.localRotation = Quaternion.Euler(0f, 0f, 180f);
        return back;
    }

    /// Kartu bergradasi ungu gelap (sama dengan kartu preview karakter di Profil).
    public static RectTransform CreatePreviewCard(Transform parent, ChatUiAssets a, float x, float y, float width, float height) {
        RectTransform card = ChatUiKit.CreateRect("Card", parent);
        ChatUiKit.Place(card, x, y, width, height);
        ChatUiKit.AddShape(card.gameObject, a.rounded, Color.white, SelectionCorner);
        card.gameObject.AddComponent<UIVerticalGradient>().SetColors(ProfileTheme.PreviewTop, ProfileTheme.PreviewBottom);
        FriendsUiKit.AddBorder(card, FriendsTheme.CardBorder, SelectionCorner);
        return card;
    }

    /// Garis cyan tebal penanda kartu terpilih (aktif/nonaktif lewat SetActive).
    public static GameObject CreateSelectionOutline(RectTransform card) {
        RectTransform outline = ChatUiKit.Stretch(ChatUiKit.CreateRect("Selected", card), -3f, -3f, -3f, -3f);
        ChatUiKit.AddShape(outline.gameObject, FriendsUiKit.OutlineSprite, ProfileTheme.Tag, SelectionCorner).raycastTarget = false;
        outline.gameObject.SetActive(false);
        return outline.gameObject;
    }

    public static GameObject CreateUsedBadge(RectTransform card, ChatUiAssets a) {
        RectTransform badge = FriendsUiKit.CreatePill(card, a, "DIPAKAI", ProfileTheme.Tag, FriendsTheme.Panel,
                                                      FriendsTheme.SmallSize - 4, BadgeHeight);
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0f, 1f);
        badge.anchoredPosition = new Vector2(10f, -10f);
        badge.gameObject.SetActive(false);
        return badge.gameObject;
    }

    /// Dua baris teks di bawah kartu: judul (mis. "Anime") + keterangan kecil (mis. "Cewek").
    public static void CreateCardLabel(RectTransform card, ChatUiAssets a, string title, string subtitle, float y, float width) {
        TextMeshProUGUI main = ChatUiKit.CreateText("Label", card, a.font, title, FriendsTheme.BodySize, Color.white,
                                                    FontStyles.Bold, TextAlignmentOptions.Center);
        ChatUiKit.Place(main.rectTransform, 0f, y, width, 32f);
        TextMeshProUGUI sub = ChatUiKit.CreateText("Sublabel", card, a.font, subtitle, FriendsTheme.SmallSize - 2,
                                                   FriendsTheme.Muted, FontStyles.Normal, TextAlignmentOptions.Center);
        ChatUiKit.Place(sub.rectTransform, 0f, y + 32f, width, 28f);
    }

    /// Label grup huruf kapital di kiri + keterangan di kanan. Mengembalikan teks kanan (bisa diubah).
    public static TextMeshProUGUI CreateGroupLabel(RectTransform parent, ChatUiAssets a, string left, string right,
                                                   float x, float y, float width) {
        TextMeshProUGUI label = ChatUiKit.CreateText("Group " + left, parent, a.font, left, FriendsTheme.SmallSize,
                                                     FriendsTheme.Muted, FontStyles.Bold);
        label.characterSpacing = 8f;
        ChatUiKit.Place(label.rectTransform, x, y, width * 0.6f, 32f);
        TextMeshProUGUI note = ChatUiKit.CreateText("Group Note", parent, a.font, right, FriendsTheme.SmallSize,
                                                    FriendsTheme.Muted, FontStyles.Normal, TextAlignmentOptions.Right);
        ChatUiKit.Place(note.rectTransform, x + width * 0.4f, y, width * 0.6f, 32f);
        return note;
    }

    /// Slot kosong bergaris tipis dengan "+ Buat dari template".
    public static Button CreateEmptySlot(RectTransform parent, ChatUiAssets a, float x, float y, float width, float height) {
        RectTransform rect = ChatUiKit.CreateRect("Empty Slot", parent);
        ChatUiKit.Place(rect, x, y, width, height);
        Image background = ChatUiKit.AddShape(rect.gameObject, a.rounded, new Color(1f, 1f, 1f, 0.02f), SelectionCorner);
        FriendsUiKit.AddBorder(rect, new Color(1f, 1f, 1f, 0.22f), SelectionCorner);
        FriendsUiKit.AddContent(rect, a, "BUAT DARI TEMPLATE", FriendsTheme.Muted, ProfileMenuBuilder.LoadIcon("icon-plus"),
                                FriendsTheme.SmallSize);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        return button;
    }

    /// Pill "Memuat…" di atas preview, tampil saat BoZo merakit/menggabungkan karakter.
    public static GameObject CreateLoadingPill(RectTransform card, ChatUiAssets a, float cardWidth) {
        RectTransform pill = FriendsUiKit.CreatePill(card, a, "Memuat…", FriendsTheme.NeutralFill, Color.white,
                                                     FriendsTheme.SmallSize, 42f, new Color(1f, 1f, 1f, 0.2f));
        pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0.5f, 1f);
        pill.anchoredPosition = new Vector2(0f, -20f);
        pill.gameObject.SetActive(false);
        return pill.gameObject;
    }

    /// Toast kecil di bawah layar (mis. "Penampilan tersimpan"). Ditampilkan oleh CustomizerToast.
    public static TextMeshProUGUI CreateToast(RectTransform root, ChatUiAssets a) {
        RectTransform pill = FriendsUiKit.CreatePill(root, a, "", FriendsTheme.TealFill, FriendsTheme.Teal,
                                                     FriendsTheme.BodySize, 56f, FriendsTheme.Teal);
        pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0.5f, 0f);
        pill.anchoredPosition = new Vector2(0f, 60f);
        pill.sizeDelta = new Vector2(ToastMinWidth, ToastHeight);
        pill.gameObject.SetActive(false);
        TextMeshProUGUI label = pill.GetComponentInChildren<TextMeshProUGUI>(true);
        ChatUiKit.Stretch(label.rectTransform, ToastPadX, ToastPadY, ToastPadX, ToastPadY);
        return label;
    }

    private const float ToastMinWidth = 620f;
    private const float ToastMaxWidth = 960f;
    private const float ToastHeight = 56f;
    private const float ToastPadX = 32f;
    private const float ToastPadY = 12f;

    /// <summary>
    /// Isi toast dan ubah ukuran pill-nya: lebar ikut teks (dengan padding, tidak mepet tepi); teks yang lebih
    /// panjang dari lebar maksimal turun ke baris berikutnya dan pill ikut meninggi.
    /// </summary>
    public static void SetToastText(TextMeshProUGUI toast, string message) {
        toast.text = message;
        float innerMax = ToastMaxWidth - ToastPadX * 2f;
        Vector2 single = toast.GetPreferredValues(message, Mathf.Infinity, Mathf.Infinity);
        bool wraps = single.x > innerMax;
        toast.textWrappingMode = wraps ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        float width = wraps ? ToastMaxWidth : Mathf.Max(ToastMinWidth, single.x + ToastPadX * 2f);
        float textHeight = wraps ? toast.GetPreferredValues(message, innerMax, Mathf.Infinity).y : single.y;
        float height = Mathf.Max(ToastHeight, textHeight + ToastPadY * 2f);
        ((RectTransform)toast.transform.parent).sizeDelta = new Vector2(width, height);
    }

    public static ConfirmDialog CreateConfirmDialog(RectTransform root, ChatUiAssets a, string title, string body,
                                                    string cancelLabel, string confirmLabel) {
        RectTransform overlay = ChatUiKit.Stretch(ChatUiKit.CreateRect("Confirm Overlay", root), 0f, 0f, 0f, 0f);
        overlay.gameObject.AddComponent<Image>().color = FriendsTheme.Dim;
        RectTransform panel = CreatePanel(overlay, a, DialogWidth, DialogHeight);

        TextMeshProUGUI titleText = ChatUiKit.CreateText("Title", panel, a.font, title, 40, Color.white, FontStyles.Bold);
        ChatUiKit.Place(titleText.rectTransform, DialogPad, DialogPad, DialogWidth - DialogPad * 2f, 56f);
        TextMeshProUGUI bodyText = ChatUiKit.CreateText("Body", panel, a.font, body, FriendsTheme.BodySize,
                                                        FriendsTheme.Neutral, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        ChatUiKit.Place(bodyText.rectTransform, DialogPad, DialogPad + 70f, DialogWidth - DialogPad * 2f, 100f);

        float buttonWidth = (DialogWidth - DialogPad * 2f - 16f) * 0.5f;
        float buttonY = DialogHeight - DialogPad - 84f;
        Button cancel = FriendsUiKit.CreateOutlineButton("Cancel", panel, a, cancelLabel, Color.white, FriendsTheme.NeutralFill);
        ChatUiKit.Place((RectTransform)cancel.transform, DialogPad, buttonY, buttonWidth, 84f);
        Button confirm = FriendsUiKit.CreateSolidButton("Confirm", panel, a, confirmLabel, ProfileTheme.RedTop,
                                                        ProfileTheme.RedBottom, ProfileTheme.RedLip);
        ChatUiKit.Place((RectTransform)confirm.transform, DialogPad + buttonWidth + 16f, buttonY, buttonWidth, 84f);

        overlay.gameObject.SetActive(false);
        return new ConfirmDialog(overlay.gameObject, titleText, bodyText, cancel, confirm);
    }
}

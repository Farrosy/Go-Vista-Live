using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Referensi dialog keranjang belanja editor karakter (CustomizerCartView.Build).</summary>
public sealed class CartRefs {
    public GameObject Overlay;
    public RectTransform List;
    public TextMeshProUGUI Subtitle;
    public TextMeshProUGUI Total;
    public TextMeshProUGUI Balance;
    public TextMeshProUGUI Remaining;
    public TextMeshProUGUI Hint;
    public Button Cancel;
    public Button Confirm;
    public TextMeshProUGUI ConfirmLabel;
}

/// <summary>
/// Dialog "Keranjang Belanja" saat SIMPAN dengan item yang belum dimiliki: daftar item (centang, ikon, nama,
/// kategori, harga), lalu total, saldo, dan sisa saldo. Item yang tidak dicentang tidak dibeli.
/// Gaya mengikuti panel navy editor (CustomizerUi.CreatePanel).
/// </summary>
public static class CustomizerCartView {
    private const float Width = 960f;
    private const float Height = 840f;
    private const float Pad = 40f;
    private const float ListTop = 172f;
    private const float ListHeight = 330f;
    private const float RowHeight = 88f;
    private const float SummaryTop = ListTop + ListHeight + 20f;
    private const float SummaryRow = 40f;
    private const float ButtonHeight = 84f;

    private static readonly Color CheckFill = new Color32(0xF2, 0xB8, 0x2E, 0xFF);
    private static readonly Color PriceColor = new Color32(0xFB, 0xD3, 0x6B, 0xFF);

    public static CartRefs Build(RectTransform root, ChatUiAssets a) {
        var refs = new CartRefs();
        RectTransform overlay = ChatUiKit.Stretch(ChatUiKit.CreateRect("Cart Overlay", root), 0f, 0f, 0f, 0f);
        overlay.gameObject.AddComponent<Image>().color = FriendsTheme.Dim;
        refs.Overlay = overlay.gameObject;
        RectTransform panel = CustomizerUi.CreatePanel(overlay, a, Width, Height);
        float inner = Width - Pad * 2f;

        TextMeshProUGUI title = ChatUiKit.CreateText("Title", panel, a.font, "KERANJANG BELANJA", 40, Color.white, FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, Pad, 34f, inner, 56f);
        refs.Subtitle = ChatUiKit.CreateText("Subtitle", panel, a.font, "", FriendsTheme.SmallSize, FriendsTheme.Muted);
        ChatUiKit.Place(refs.Subtitle.rectTransform, Pad, 92f, inner, 32f);

        TextMeshProUGUI itemHeader = ChatUiKit.CreateText("Header Item", panel, a.font, "ITEM", FriendsTheme.SmallSize - 4,
                                                          FriendsTheme.Muted, FontStyles.Bold);
        ChatUiKit.Place(itemHeader.rectTransform, Pad + 16f, 136f, 300f, 28f);
        TextMeshProUGUI priceHeader = ChatUiKit.CreateText("Header Price", panel, a.font, "HARGA", FriendsTheme.SmallSize - 4,
                                                           FriendsTheme.Muted, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        ChatUiKit.Place(priceHeader.rectTransform, Width - Pad - 316f, 136f, 300f, 28f);

        RectTransform listArea = ChatUiKit.CreateRect("List Area", panel);
        ChatUiKit.Place(listArea, Pad, ListTop, inner, ListHeight);
        ChatUiKit.AddShape(listArea.gameObject, a.rounded, new Color(0f, 0f, 0f, 0.25f), 1.4f);
        CustomizerEditorPanels.CreateVerticalScroll(listArea, "Cart Items", 8f, out refs.List);
        VerticalLayoutGroup layout = refs.List.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);

        refs.Total = SummaryLine(panel, a, "Total item", 0, false);
        refs.Balance = SummaryLine(panel, a, "Saldo kamu", 1, false);
        RectTransform divider = ChatUiKit.CreateRect("Divider", panel);
        ChatUiKit.Place(divider, Pad, SummaryTop + SummaryRow * 2f + 4f, inner, 2f);
        divider.gameObject.AddComponent<Image>().color = FriendsTheme.CardBorder;
        refs.Remaining = SummaryLine(panel, a, "Sisa saldo", 2, true);

        refs.Hint = ChatUiKit.CreateText("Hint", panel, a.font, "", FriendsTheme.SmallSize - 2, FriendsTheme.Muted);
        refs.Hint.textWrappingMode = TextWrappingModes.Normal;
        ChatUiKit.Place(refs.Hint.rectTransform, Pad, SummaryTop + SummaryRow * 3f + 14f, inner, 56f);

        float buttonWidth = (inner - 16f) * 0.5f;
        float buttonY = Height - Pad - ButtonHeight;
        refs.Cancel = FriendsUiKit.CreateOutlineButton("Cancel", panel, a, "BATAL", Color.white, FriendsTheme.NeutralFill);
        ChatUiKit.Place((RectTransform)refs.Cancel.transform, Pad, buttonY, buttonWidth, ButtonHeight);
        refs.Confirm = FriendsUiKit.CreateSolidButton("Confirm", panel, a, "BELI & SIMPAN", FriendsTheme.GreenTop,
                                                      FriendsTheme.GreenBottom, FriendsTheme.GreenLip, a.checkIcon);
        ChatUiKit.Place((RectTransform)refs.Confirm.transform, Pad + buttonWidth + 16f, buttonY, buttonWidth, ButtonHeight);
        refs.ConfirmLabel = refs.Confirm.GetComponentInChildren<TextMeshProUGUI>(true);

        overlay.gameObject.SetActive(false);
        return refs;
    }

    /// <summary>Satu baris item: kotak centang, ikon, nama + kategori, harga. Seluruh baris bisa diketuk.</summary>
    public static Button CreateRow(Transform parent, ChatUiAssets a, Sprite icon, string name, string category, int price,
                                   out GameObject checkOn, out TextMeshProUGUI priceText) {
        RectTransform row = ChatUiKit.CreateRect("Row " + name, parent);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
        Image bg = ChatUiKit.AddShape(row.gameObject, a.rounded, FriendsTheme.Card, 1.4f);
        FriendsUiKit.AddBorder(row, FriendsTheme.CardBorder, 1.4f);

        RectTransform box = ChatUiKit.CreateRect("Check", row);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 0.5f);
        box.anchoredPosition = new Vector2(18f, 0f);
        box.sizeDelta = new Vector2(40f, 40f);
        ChatUiKit.AddShape(box.gameObject, FriendsUiKit.OutlineSprite, FriendsTheme.Muted, 1.2f).raycastTarget = false;
        RectTransform on = ChatUiKit.Stretch(ChatUiKit.CreateRect("On", box), 0f, 0f, 0f, 0f);
        ChatUiKit.AddShape(on.gameObject, a.rounded, CheckFill, 1.2f).raycastTarget = false;
        Image tick = ChatUiKit.AddIcon("Tick", on, a.checkIcon, FriendsTheme.Panel);
        tick.raycastTarget = false;
        ChatUiKit.Stretch(tick.rectTransform, 7f, 7f, 7f, 7f);
        checkOn = on.gameObject;

        RectTransform iconBox = ChatUiKit.CreateRect("Icon Box", row);
        iconBox.anchorMin = iconBox.anchorMax = iconBox.pivot = new Vector2(0f, 0.5f);
        iconBox.anchoredPosition = new Vector2(76f, 0f);
        iconBox.sizeDelta = new Vector2(68f, 68f);
        ChatUiKit.AddShape(iconBox.gameObject, a.rounded, FriendsTheme.TabSelected, 1.2f).raycastTarget = false;
        Image image = ChatUiKit.AddIcon("Icon", iconBox, icon, icon ? Color.white : FriendsTheme.Muted);
        image.raycastTarget = false;
        ChatUiKit.Stretch(image.rectTransform, 6f, 6f, 6f, 6f);

        TextMeshProUGUI title = ChatUiKit.CreateText("Name", row, a.font, name, FriendsTheme.BodySize, Color.white, FontStyles.Bold);
        Line(title.rectTransform, 160f, 12f, 200f);
        TextMeshProUGUI sub = ChatUiKit.CreateText("Category", row, a.font, category, FriendsTheme.SmallSize - 4, FriendsTheme.Muted);
        Line(sub.rectTransform, 160f, 46f, 200f);

        priceText = ChatUiKit.CreateText("Price", row, a.font, CoinRules.FormatBalance(price) + " G", FriendsTheme.BodySize,
                                         PriceColor, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        RectTransform p = priceText.rectTransform;
        p.anchorMin = new Vector2(1f, 0f);
        p.anchorMax = new Vector2(1f, 1f);
        p.pivot = new Vector2(1f, 0.5f);
        p.offsetMin = new Vector2(-180f, 0f);
        p.offsetMax = new Vector2(-20f, 0f);

        Button button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        return button;
    }

    /// Teks satu baris dari kiri (x, y dari atas) sampai <paramref name="right"/> dari kanan baris.
    private static void Line(RectTransform rect, float x, float y, float right) {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(x, -(y + 32f));
        rect.offsetMax = new Vector2(-right, -y);
    }

    private static TextMeshProUGUI SummaryLine(RectTransform panel, ChatUiAssets a, string label, int row, bool strong) {
        float y = SummaryTop + SummaryRow * row + (strong ? 12f : 0f);
        int size = strong ? FriendsTheme.NameSize : FriendsTheme.BodySize;
        Color color = strong ? Color.white : FriendsTheme.Neutral;
        TextMeshProUGUI left = ChatUiKit.CreateText(label, panel, a.font, label, size, color, strong ? FontStyles.Bold : FontStyles.Normal);
        ChatUiKit.Place(left.rectTransform, Pad + 16f, y, 400f, SummaryRow);
        TextMeshProUGUI value = ChatUiKit.CreateText(label + " Value", panel, a.font, "", size, color, FontStyles.Bold,
                                                     TextAlignmentOptions.MidlineRight);
        ChatUiKit.Place(value.rectTransform, Width - Pad - 416f, y, 400f, SummaryRow);
        return value;
    }
}

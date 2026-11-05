using UnityEngine;
using UnityEngine.UI;

/// Tata letak tiga tile di kartu menu utama: KARAKTER · TEMAN · PENGATURAN
/// (Docs/Friends/preview-main-menu-teman.png). Dipakai LobyUISceneController.BuildMainMenu (layout default)
/// dan installer editor yang menambahkan tile TEMAN ke LobbyCanvas.prefab yang sudah ada.
public static class MainMenuTiles {
    public const string FriendsTile = "Main Menu Friends Button";
    public const string CharacterTile = "Main Menu Character Button";
    public const string SettingsTile = "Main Menu Settings Button";

    private const float RowLeft = 40f;
    private const float RowWidth = 560f;
    private const float Gap = 16f;
    public const float Width = (RowWidth - Gap * 2f) / 3f; // ≈ 176
    public const int LabelFontSize = 24;
    /// Jarak teks dari tepi kiri/kanan tile, supaya label panjang ("PENGATURAN") tidak menempel ke tepi tombol.
    public const float TextPadding = 14f;
    public const float TextWidth = Width - TextPadding * 2f;

    public static readonly Color GreenTop = new Color32(0x34, 0xD3, 0x99, 0xFF);
    public static readonly Color GreenBottom = new Color32(0x05, 0x96, 0x69, 0xFF);
    private static readonly Color BadgeColor = new Color32(0xEF, 0x44, 0x5A, 0xFF);

    /// Posisi x tile ke-index (0 = kiri).
    public static float X(int index) => RowLeft + index * (Width + Gap);

    /// Badge merah jumlah permintaan teman di pojok kanan atas kotak ikon tile TEMAN (disembunyikan sampai ada).
    public static void AddBadge(Transform iconBox) {
        if (iconBox == null || iconBox.Find(FriendsTile + " Badge") != null)
            return;
        var badgeObject = new GameObject(FriendsTile + " Badge", typeof(RectTransform), typeof(Image));
        var badge = (RectTransform)badgeObject.transform;
        badge.SetParent(iconBox, false);
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(1f, 1f);
        badge.anchoredPosition = new Vector2(12f, 12f);
        badge.sizeDelta = new Vector2(34f, 34f);
        Image image = badgeObject.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/ui-circle");
        image.color = BadgeColor;
        image.raycastTarget = false;

        var countObject = new GameObject("Count", typeof(RectTransform), typeof(Text));
        var count = (RectTransform)countObject.transform;
        count.SetParent(badge, false);
        count.anchorMin = Vector2.zero;
        count.anchorMax = Vector2.one;
        count.offsetMin = count.offsetMax = Vector2.zero;
        Text text = countObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "0";
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        badgeObject.SetActive(false);
    }

    /// Geser & kecilkan satu tile (dan isinya) ke kolom index. Tinggi/posisi y yang sudah ada dibiarkan.
    public static void Relayout(RectTransform tile, int index) {
        if (tile == null)
            return;
        tile.anchoredPosition = new Vector2(X(index), tile.anchoredPosition.y);
        tile.sizeDelta = new Vector2(Width, tile.sizeDelta.y);
        string name = tile.name;
        Transform face = tile.Find(name + " Face");
        if (face == null)
            return;
        var iconBox = face.Find(name + " Icon Box") as RectTransform;
        if (iconBox != null)
            iconBox.anchoredPosition = new Vector2((Width - iconBox.sizeDelta.x) * 0.5f, iconBox.anchoredPosition.y);
        FitText(face.Find(name + " Label") as RectTransform, LabelFontSize);
        FitText(face.Find(name + " Subtitle") as RectTransform, 0);
    }

    private static void FitText(RectTransform rect, int fontSize) {
        if (rect == null)
            return;
        rect.anchoredPosition = new Vector2(TextPadding, rect.anchoredPosition.y);
        rect.sizeDelta = new Vector2(TextWidth, rect.sizeDelta.y);
        Text text = rect.GetComponent<Text>();
        if (text != null && fontSize > 0)
            text.fontSize = fontSize;
    }
}

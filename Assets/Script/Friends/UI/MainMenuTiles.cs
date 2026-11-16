using UnityEngine;
using UnityEngine.UI;

/// Tata letak empat tile di kartu menu utama: KARAKTER · PROFIL · TEMAN · PENGATURAN.
/// Dipakai LobyUISceneController.BuildMainMenu (layout default) dan installer editor
/// (FriendsMainMenuPatcher) yang menambahkan tile ke LobbyCanvas.prefab yang sudah ada.
public static class MainMenuTiles {
    public const string FriendsTile = "Main Menu Friends Button";
    public const string ProfileTile = "Main Menu Profile Button";
    /// Tile KARAKTER (paling kiri): membuka scene CharacterScene. Dulu nama ini dipakai tile yang sekarang PROFIL.
    public const string CharacterTile = "Main Menu Character Button";
    public const string CharacterIcon = "icon-shirt";
    public const int TileCount = 4;
    public const string SettingsTile = "Main Menu Settings Button";

    private const float RowLeft = 40f;
    private const float RowWidth = 560f;
    private const float Gap = 12f;
    public const float Width = (RowWidth - Gap * (TileCount - 1)) / TileCount; // = 131
    public const int LabelFontSize = 21;
    public const int SubtitleFontSize = 17;
    private const int MinFontSize = 12;

    // Kartu tanpa header sapaan (Docs/Profil/01-main-menu.png): isi naik 112 px, kartu lebih pendek.
    public const float HeaderRemoved = 112f;
    public const float CardHeight = 668f - HeaderRemoved;
    public const float PlayY = 152f - HeaderRemoved;
    public const float TileY = 330f - HeaderRemoved;
    public const float QuitY = 540f - HeaderRemoved;
    /// Jarak teks dari tepi kiri/kanan tile, supaya label panjang ("PENGATURAN") tidak menempel ke tepi tombol.
    public const float TextPadding = 8f;
    public const float TextWidth = Width - TextPadding * 2f;

    public static readonly Color GreenTop = new Color32(0x34, 0xD3, 0x99, 0xFF);
    public static readonly Color GreenBottom = new Color32(0x05, 0x96, 0x69, 0xFF);
    public static readonly Color BlueTop = new Color32(0x4F, 0x8C, 0xFF, 0xFF);
    public static readonly Color BlueBottom = new Color32(0x7B, 0x4D, 0xFF, 0xFF);
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
        if (text == null)
            return;
        if (fontSize > 0)
            text.fontSize = fontSize;
        FitToWidth(text);
    }

    /// Teks tile mengecil sendiri kalau tidak muat (mis. "PENGATURAN", nama pemain panjang), tidak terpotong.
    public static void FitToWidth(Text text) {
        if (text == null)
            return;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = text.fontSize;
        text.resizeTextMinSize = Mathf.Min(MinFontSize, text.fontSize);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    public static Transform FindDeep(Transform parent, string name) {
        foreach (Transform child in parent) {
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}

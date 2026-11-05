using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Warna & ukuran friendlist (mengikuti Docs/Friends/*.png dan tema menu utama Go Vista).
public static class FriendsTheme {
    public static readonly Color Panel = new Color32(0x0D, 0x12, 0x22, 0xF7);
    public static readonly Color Card = new Color32(0x1B, 0x24, 0x40, 0xFF);
    public static readonly Color CardBorder = new Color(1f, 1f, 1f, 0.08f);
    public static readonly Color TabSelected = new Color32(0x25, 0x30, 0x4F, 0xFF);
    public static readonly Color Muted = new Color32(0x8E, 0x9B, 0xBB, 0xFF);
    public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
    public static readonly Color Badge = new Color32(0xEF, 0x44, 0x5A, 0xFF);

    // Tombol solid: gradasi atas→bawah + lip gelap di bawah (seperti tombol menu utama).
    public static readonly Color GreenTop = new Color32(0x34, 0xD3, 0x99, 0xFF);
    public static readonly Color GreenBottom = new Color32(0x10, 0xB9, 0x81, 0xFF);
    public static readonly Color GreenLip = new Color32(0x04, 0x6C, 0x4E, 0xFF);
    public static readonly Color BlueTop = new Color32(0x4F, 0x8C, 0xFF, 0xFF);
    public static readonly Color BlueBottom = new Color32(0x7B, 0x4D, 0xFF, 0xFF);
    public static readonly Color BlueLip = new Color32(0x3B, 0x1F, 0x8F, 0xFF);

    // Tombol outline: latar gelap + garis tepi & teks berwarna.
    public static readonly Color Danger = new Color32(0xFF, 0x7A, 0x8C, 0xFF);
    public static readonly Color DangerFill = new Color32(0x2A, 0x15, 0x20, 0xFF);
    public static readonly Color Neutral = new Color32(0xB4, 0xBE, 0xD6, 0xFF);
    public static readonly Color NeutralFill = new Color32(0x1B, 0x24, 0x40, 0xFF);
    public static readonly Color Teal = new Color32(0x34, 0xD3, 0x99, 0xFF);
    public static readonly Color TealFill = new Color32(0x10, 0x2A, 0x2C, 0xFF);

    public const int TitleSize = 52;
    public const int NameSize = 30;
    public const int BodySize = 24;
    public const int SmallSize = 22;
    public const int ButtonSize = 24;
    public const float LipDepth = 6f;
}

public static class FriendsUiKit {
    private static Sprite _outline;
    private static Sprite _dashedCircle;

    public static Sprite OutlineSprite => _outline != null ? _outline : _outline = Resources.Load<Sprite>("UI/ui-outline");
    public static Sprite DashedCircleSprite =>
        _dashedCircle != null ? _dashedCircle : _dashedCircle = Resources.Load<Sprite>("UI/ui-dashed-circle");

    /// Tombol utama: gradasi + lip, isi = ikon (opsional) + label di tengah.
    public static Button CreateSolidButton(string name, Transform parent, ChatUiAssets assets, string label,
                                           Color top, Color bottom, Color lip, Sprite icon = null,
                                           int fontSize = FriendsTheme.ButtonSize) {
        RectTransform root = ChatUiKit.CreateRect(name, parent);
        RectTransform lipRect = ChatUiKit.Stretch(ChatUiKit.CreateRect("Lip", root), 0f, FriendsTheme.LipDepth, 0f, 0f);
        ChatUiKit.AddShape(lipRect.gameObject, assets.rounded, lip, 1.4f).raycastTarget = false;
        RectTransform face = ChatUiKit.Stretch(ChatUiKit.CreateRect("Face", root), 0f, 0f, 0f, FriendsTheme.LipDepth);
        Image faceImage = ChatUiKit.AddShape(face.gameObject, assets.rounded, Color.white, 1.4f);
        face.gameObject.AddComponent<UIVerticalGradient>().SetColors(top, bottom);
        AddContent(face, assets, label, Color.white, icon, fontSize);
        return AddButton(root.gameObject, faceImage);
    }

    /// Tombol sekunder: latar gelap + garis tepi & teks berwarna.
    public static Button CreateOutlineButton(string name, Transform parent, ChatUiAssets assets, string label,
                                             Color color, Color fill, Sprite icon = null,
                                             int fontSize = FriendsTheme.ButtonSize) {
        RectTransform root = ChatUiKit.CreateRect(name, parent);
        Image background = ChatUiKit.AddShape(root.gameObject, assets.rounded, fill, 1.4f);
        AddBorder(root, new Color(color.r, color.g, color.b, 0.55f));
        AddContent(root, assets, label, color, icon, fontSize);
        return AddButton(root.gameObject, background);
    }

    public static void AddBorder(RectTransform parent, Color color, float cornerScale = 1.4f) {
        RectTransform border = ChatUiKit.Stretch(ChatUiKit.CreateRect("Border", parent), 0f, 0f, 0f, 0f);
        ChatUiKit.AddShape(border.gameObject, OutlineSprite, color, cornerScale).raycastTarget = false;
    }

    /// Ikon + label di tengah (HorizontalLayoutGroup), dipakai semua tombol friendlist.
    public static TextMeshProUGUI AddContent(RectTransform parent, ChatUiAssets assets, string label, Color color,
                                             Sprite icon, int fontSize) {
        RectTransform content = ChatUiKit.Stretch(ChatUiKit.CreateRect("Content", parent), 12f, 0f, 12f, 0f);
        HorizontalLayoutGroup row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 10f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        if (icon != null) {
            Image image = ChatUiKit.AddIcon("Icon", content, icon, color);
            LayoutElement size = image.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = fontSize + 8f;
        }
        TextMeshProUGUI text = ChatUiKit.CreateText("Label", content, assets.font, label, fontSize, color,
                                                    FontStyles.Bold, TextAlignmentOptions.Center);
        text.characterSpacing = 4f;
        // Label tombol diatur HorizontalLayoutGroup (UiTextFit melewatinya), jadi auto size dipasang di sini:
        // kalau tombol terlalu sempit (mis. "TERSALIN"), teks mengecil, bukan terpotong.
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Round(fontSize * UiTextFit.MinRatio);
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static Button AddButton(GameObject target, Graphic graphic) {
        Button button = target.AddComponent<Button>();
        button.targetGraphic = graphic;
        ColorBlock colors = button.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        return button;
    }

    public static TextMeshProUGUI LabelOf(Button button) => button.GetComponentInChildren<TextMeshProUGUI>(true);

    /// Posisi di dalam baris: x >= 0 dari kiri, x < 0 dari kanan; tengah secara vertikal.
    public static RectTransform PlaceInRow(RectTransform rect, float x, float width, float height, float y = 0f) {
        float side = x < 0f ? 1f : 0f;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(side, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    /// Lingkaran gradasi berisi inisial, dengan titik status (null = tanpa titik) di kanan bawah.
    public static RectTransform CreateAvatar(Transform parent, ChatUiAssets assets, string name, float size,
                                             Color? statusDot) {
        RectTransform avatar = ChatUiKit.CreateRect("Avatar", parent);
        avatar.sizeDelta = new Vector2(size, size);
        Color color = ChatRules.NameColor(ChatRules.StableHash(name));
        ChatUiKit.AddShape(avatar.gameObject, assets.circle, Color.white).raycastTarget = false;
        avatar.gameObject.AddComponent<UIVerticalGradient>().SetColors(color, Color.Lerp(color, Color.black, 0.25f));
        TextMeshProUGUI initial = ChatUiKit.CreateText("Initial", avatar, assets.font, ChatRules.Initial(name),
                                                       FriendsTheme.NameSize, Color.white, FontStyles.Bold,
                                                       TextAlignmentOptions.Center);
        ChatUiKit.Stretch(initial.rectTransform, 0f, 0f, 0f, 0f);

        if (statusDot.HasValue) {
            RectTransform ring = ChatUiKit.CreateRect("Status Ring", avatar);
            ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(1f, 0f);
            ring.anchoredPosition = new Vector2(2f, -2f);
            ring.sizeDelta = new Vector2(20f, 20f);
            ChatUiKit.AddShape(ring.gameObject, assets.circle, FriendsTheme.Card).raycastTarget = false;
            RectTransform dot = ChatUiKit.Stretch(ChatUiKit.CreateRect("Status Dot", ring), 4f, 4f, 4f, 4f);
            ChatUiKit.AddShape(dot.gameObject, assets.circle, statusDot.Value).raycastTarget = false;
        }
        return avatar;
    }

    /// Pil kecil berisi teks (mis. angka tab, "3 online", badge KAMU/HOST). Lebar mengikuti teks.
    public static RectTransform CreatePill(Transform parent, ChatUiAssets assets, string text, Color fill, Color textColor,
                                           int fontSize, float height, Color? border = null) {
        RectTransform pill = ChatUiKit.CreateRect("Pill", parent);
        ChatUiKit.AddShape(pill.gameObject, assets.rounded, fill, 2.2f).raycastTarget = false;
        if (border.HasValue) AddBorder(pill, border.Value, 2.2f);
        TextMeshProUGUI label = ChatUiKit.CreateText("Text", pill, assets.font, text, fontSize, textColor, FontStyles.Bold,
                                                     TextAlignmentOptions.Center);
        ChatUiKit.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
        float width = Mathf.Max(height, label.GetPreferredValues(text).x + height * 0.8f);
        pill.sizeDelta = new Vector2(width, height);
        return pill;
    }

    /// Lingkaran gelap bergaris putus-putus berisi ikon (keadaan kosong / gagal).
    public static RectTransform CreateIconCircle(Transform parent, ChatUiAssets assets, Sprite icon, Color iconColor,
                                                 float size) {
        RectTransform circle = ChatUiKit.CreateRect("Icon Circle", parent);
        circle.sizeDelta = new Vector2(size, size);
        ChatUiKit.AddShape(circle.gameObject, assets.circle, FriendsTheme.Card).raycastTarget = false;
        if (DashedCircleSprite != null) {
            RectTransform ring = ChatUiKit.Stretch(ChatUiKit.CreateRect("Ring", circle), 0f, 0f, 0f, 0f);
            ChatUiKit.AddShape(ring.gameObject, DashedCircleSprite, new Color(1f, 1f, 1f, 0.25f)).raycastTarget = false;
        }
        Image image = ChatUiKit.AddIcon("Icon", circle, icon, iconColor);
        ChatUiKit.Stretch(image.rectTransform, size * 0.28f, size * 0.28f, size * 0.28f, size * 0.28f);
        return circle;
    }
}

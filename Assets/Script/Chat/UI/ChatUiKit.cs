using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Aset UI chat. Diisi otomatis oleh menu "Go Vista > Chat > Pasang Chat di MainMultiplayerScene".
/// Sprite bersama (sudut membulat, lingkaran, chevron) diambil dari Assets/Resources/UI kalau kosong.
[Serializable]
public sealed class ChatUiAssets {
    public TMP_FontAsset font;
    public Sprite chatIcon;
    public Sprite sendButton;
    public Sprite sendButtonPressed;
    public Sprite rounded;
    public Sprite circle;
    public Sprite chevron;

    public void ResolveFallbacks() {
        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (rounded == null) rounded = Resources.Load<Sprite>("UI/ui-rounded");
        if (circle == null) circle = Resources.Load<Sprite>("UI/ui-circle");
        if (chevron == null) chevron = Resources.Load<Sprite>("UI/Icons/icon-chevron-right");
    }
}

/// Warna & ukuran chat (sama dengan tema menu Go Vista, lihat Docs/Chat/BACA-SAYA.txt).
public static class ChatTheme {
    public static readonly Color Panel = new Color32(0x0D, 0x12, 0x22, 0xED);
    public static readonly Color BubbleOther = new Color32(0x1B, 0x24, 0x40, 0xFF);
    public static readonly Color BubbleOwnTop = new Color32(0x4F, 0x8C, 0xFF, 0xFF);
    public static readonly Color BubbleOwnBottom = new Color32(0x7B, 0x4D, 0xFF, 0xFF);
    public static readonly Color AccentPink = new Color32(0xF4, 0x72, 0xB6, 0xFF);
    public static readonly Color FocusCyan = new Color32(0x22, 0xD3, 0xEE, 0xFF);
    public static readonly Color Online = new Color32(0x34, 0xD3, 0x99, 0xFF);
    public static readonly Color OnlinePill = new Color32(0x34, 0xD3, 0x99, 0x33);
    public static readonly Color Field = new Color32(0x08, 0x0C, 0x18, 0xFF);
    public static readonly Color Line = new Color(1f, 1f, 1f, 0.10f);
    public static readonly Color Muted = new Color32(0x9A, 0xA6, 0xC4, 0xFF);
    public static readonly Color Badge = new Color32(0xEF, 0x44, 0x44, 0xFF);

    public const float PanelWidth = 640f;
    public const float PanelHeight = 580f;
    public const float HeaderHeight = 88f;
    public const float InputBarHeight = 104f;
    public const float ToggleSize = 104f;
    public const float ScreenMargin = 24f;
    public const int TitleSize = 30;
    public const int BodySize = 26;
    public const int NameSize = 22;
    public const int TimeSize = 19;
}

/// Pembuat elemen UI kecil untuk chat (RectTransform, Image, TMP). Koordinat Place = dari kiri atas parent.
public static class ChatUiKit {
    public static RectTransform CreateRect(string name, Transform parent) {
        var item = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static RectTransform Place(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    public static RectTransform PlaceTopRight(RectTransform rect, float right, float top, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect, float left, float top, float right, float bottom) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    /// cornerScale > 1 = sudut lebih bulat (sprite 9-slice ui-rounded).
    public static Image AddShape(GameObject target, Sprite sprite, Color color, float cornerScale = 1f) {
        Image image = target.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f / cornerScale;
        image.color = color;
        return image;
    }

    public static Image AddIcon(string name, Transform parent, Sprite sprite, Color color) {
        Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// Teks TMP. richText dimatikan untuk isi pesan supaya pemain tidak bisa menyisipkan tag
    /// seperti <size=300> ke layar orang lain. Emoji tetap tampil lewat Emoji Fallback TMP Settings.
    public static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string content,
                                             int size, Color color, FontStyles style = FontStyles.Normal,
                                             TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft) {
        TextMeshProUGUI text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.richText = false;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    public static Button CreateButton(string name, Transform parent, Sprite sprite, Color color) {
        RectTransform rect = CreateRect(name, parent);
        Image image = AddShape(rect.gameObject, sprite, color);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        return button;
    }

    public static void DestroyChildren(Transform parent, ICollection<Transform> keep = null) {
        for (int i = parent.childCount - 1; i >= 0; i--) {
            Transform child = parent.GetChild(i);
            if (keep != null && keep.Contains(child))
                continue;
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }
}

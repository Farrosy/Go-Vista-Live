using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Alat bantu (khusus Editor) untuk membangun UI desain baru Go Vista: kartu gelap, tombol "3D"
/// bergradasi, ikon, slider, dan tombol − / +. Dipakai oleh:
///  - SettingsPanelRedesign  (Go Vista > Desain Baru > Panel Pengaturan)
///  - ControlEditorRedesign  (Go Vista > Desain Baru > Atur Kontrol)
///
/// Koordinat Place() = jarak dari pojok KIRI ATAS parent (x ke kanan, y ke bawah), sama seperti
/// LobyUISceneController. Referensi layar 1920x1080.
/// File ini identik di kedua paket; kalau sudah ada, boleh ditimpa.
/// </summary>
internal static class RedesignUiKit {
    // ------------------------------------------------------------ Warna (sama dengan menu utama v10)
    public static readonly Color CardColor = new Color32(13, 18, 34, 245);
    public static readonly Color LineColor = new Color(1f, 1f, 1f, 0.12f);
    public static readonly Color MutedColor = new Color32(127, 141, 176, 255);
    public static readonly Color SubColor = new Color32(142, 155, 187, 255);
    public static readonly Color GhostColor = new Color32(28, 37, 64, 255);
    public static readonly Color GhostTextColor = new Color32(207, 216, 240, 255);
    public static readonly Color RoundColor = new Color32(38, 48, 79, 255);
    public static readonly Color TrackColor = new Color32(11, 16, 34, 255);
    public static readonly Color SectionTopColor = new Color32(27, 36, 64, 255);
    public static readonly Color SectionBottomColor = new Color32(22, 30, 54, 255);
    public static readonly Color TileTopColor = new Color32(37, 48, 79, 255);
    public static readonly Color TileBottomColor = new Color32(26, 35, 64, 255);
    public static readonly Color TileLipColor = new Color32(10, 15, 30, 255);
    public static readonly Color BlueColor = new Color32(79, 140, 255, 255);
    public static readonly Color VioletColor = new Color32(123, 77, 255, 255);
    public static readonly Color VioletLipColor = new Color32(59, 31, 143, 255);
    public static readonly Color CyanTopColor = new Color32(34, 211, 238, 255);
    public static readonly Color CyanBottomColor = new Color32(8, 145, 178, 255);
    public static readonly Color CyanTextColor = new Color32(103, 232, 249, 255);
    public static readonly Color PinkTopColor = new Color32(244, 114, 182, 255);
    public static readonly Color PinkBottomColor = new Color32(219, 39, 119, 255);
    public static readonly Color AvatarBottomColor = new Color32(139, 92, 246, 255);
    public static readonly Color GreenTopColor = new Color32(52, 211, 153, 255);
    public static readonly Color GreenBottomColor = new Color32(5, 150, 105, 255);
    public static readonly Color GreenLipColor = new Color32(11, 90, 67, 255);
    public const float LipDepth = 8f;

    // ------------------------------------------------------------ Dasar

    public static Sprite LoadSprite(string name) => Resources.Load<Sprite>("UI/" + name);

    public static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        return rect;
    }

    /// <summary>Posisi dari pojok kiri atas parent.</summary>
    public static RectTransform Place(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    /// <summary>Isi penuh parent dengan jarak kiri/atas/kanan/bawah.</summary>
    public static RectTransform Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f,
                                        float bottom = 0f) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    /// <summary>Image dengan sprite UI. cornerScale > 1 = sudut lebih bulat.</summary>
    public static Image Shape(GameObject target, Color color, string sprite = "ui-rounded", float cornerScale = 1f) {
        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.AddComponent<Image>();
        image.sprite = LoadSprite(sprite);
        image.type = sprite == "ui-dashed" || sprite == "ui-grid" ? Image.Type.Tiled
                   : sprite.EndsWith("circle") ? Image.Type.Simple
                   : Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / cornerScale;
        image.color = color;
        return image;
    }

    /// <summary>Shape dengan gradasi atas -> bawah (UIVerticalGradient).</summary>
    public static Image GradientShape(GameObject target, Color top, Color bottom, string sprite = "ui-rounded",
                                 float cornerScale = 1f) {
        Image image = Shape(target, Color.white, sprite, cornerScale);
        var gradient = target.GetComponent<UIVerticalGradient>();
        if (gradient == null)
            gradient = target.AddComponent<UIVerticalGradient>();
        gradient.SetColors(top, bottom);
        return image;
    }

    public static Image AddOutline(RectTransform parent, Color color, string sprite = "ui-outline", float cornerScale = 1f) {
        RectTransform outline = Stretch(NewRect(parent.name + " Outline", parent));
        Image image = Shape(outline.gameObject, color, sprite, cornerScale);
        image.raycastTarget = false;
        return image;
    }

    public static Text Label(string name, Transform parent, string content, int size, bool bold, TextAnchor anchor,
                             Color color) {
        RectTransform rect = NewRect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>Ikon putih dari Assets/Resources/UI/Icons, diwarnai lewat color.</summary>
    public static Image Icon(string name, Transform parent, string icon, Color color, float x, float y, float size) {
        RectTransform rect = Place(NewRect(name, parent), x, y, size, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = LoadSprite("Icons/" + icon);
        image.preserveAspect = true;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Kotak ikon bergradasi (sudut membulat) dengan ikon putih di tengah.</summary>
    public static RectTransform IconTile(string name, Transform parent, string icon, Color top, Color bottom,
                                         float x, float y, float size, float iconSize) {
        RectTransform tile = Place(NewRect(name, parent), x, y, size, size);
        GradientShape(tile.gameObject, top, bottom, cornerScale: size >= 64f ? 1.1f : 0.9f).raycastTarget = false;
        float pad = (size - iconSize) * 0.5f;
        Icon(name + " Icon", tile, icon, Color.white, pad, pad, iconSize);
        return tile;
    }

    // ------------------------------------------------------------ Tombol

    public static ColorBlock PressTint() {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        return colors;
    }

    /// <summary>
    /// Tombol "3D": Lip (bayangan gelap 8 px di bawah) + Face (gradasi, isi tombol ditaruh di face).
    /// MenuButtonPress menurunkan Face saat ditekan. Posisikan hasilnya dengan Place().
    /// </summary>
    public static Button MenuButton(string name, Transform parent, Color top, Color bottom, Color lip, Color border,
                                    float cornerScale, out RectTransform face) {
        RectTransform rect = NewRect(name, parent);
        RectTransform lipRect = Stretch(NewRect(name + " Lip", rect), top: LipDepth);
        Shape(lipRect.gameObject, lip, cornerScale: cornerScale).raycastTarget = false;

        face = Stretch(NewRect(name + " Face", rect), bottom: LipDepth);
        Image faceImage = GradientShape(face.gameObject, top, bottom, cornerScale: cornerScale);
        if (border.a > 0f)
            AddOutline(face, border, cornerScale: cornerScale);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = faceImage;
        button.colors = PressTint();
        rect.gameObject.AddComponent<MenuButtonPress>().Init(face, LipDepth - 2f);
        return button;
    }

    /// <summary>Isi tombol di tengah: [ikon] TEKS. Ikon boleh null.</summary>
    public static Text CenteredContent(string name, RectTransform face, string icon, string label, int fontSize,
                                       float iconSize, Color color) {
        RectTransform content = Stretch(NewRect(name + " Content", face));
        HorizontalLayoutGroup row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 12f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        if (!string.IsNullOrEmpty(icon)) {
            Image image = Icon(name + " Icon", content, icon, color, 0f, 0f, iconSize);
            LayoutElement size = image.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = iconSize;
        }
        Text text = Label(name + " Label", content, label, fontSize, true, TextAnchor.MiddleCenter, color);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    /// <summary>Tombol datar gelap bergaris tepi (aksi sekunder: BATAL, RESET, tutup).</summary>
    public static Button GhostButton(string name, Transform parent, string icon, string label, int fontSize,
                                     float iconSize) {
        RectTransform rect = NewRect(name, parent);
        Image image = Shape(rect.gameObject, GhostColor, cornerScale: 1f);
        AddOutline(rect, new Color(1f, 1f, 1f, 0.1f), cornerScale: 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = PressTint();
        CenteredContent(name, rect, icon, label, fontSize, iconSize, GhostTextColor);
        return button;
    }

    /// <summary>Tombol bulat kecil berisi ikon (− / +).</summary>
    public static Button RoundButton(string name, Transform parent, string icon, float x, float y, float size) {
        RectTransform rect = Place(NewRect(name, parent), x, y, size, size);
        Image image = Shape(rect.gameObject, RoundColor, "ui-circle");
        image.preserveAspect = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = PressTint();
        float iconSize = size * 0.45f;
        Icon(name + " Icon", rect, icon, Color.white, (size - iconSize) * 0.5f, (size - iconSize) * 0.5f, iconSize);
        return button;
    }

    /// <summary>Slider dengan track gelap, isi berwarna, dan handle putih bercincin.</summary>
    public static Slider CreateSlider(string name, Transform parent, float min, float max, Color fillColor, Color ringColor,
                                float handleSize = 48f) {
        RectTransform root = NewRect(name, parent);
        Slider slider = root.gameObject.AddComponent<Slider>();

        RectTransform track = NewRect("Track", root);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.pivot = new Vector2(0.5f, 0.5f);
        track.anchoredPosition = Vector2.zero;
        track.sizeDelta = new Vector2(0f, 16f);
        Shape(track.gameObject, TrackColor, cornerScale: 0.4f);

        RectTransform fillArea = Stretch(NewRect("Fill Area", track));
        RectTransform fill = Stretch(NewRect("Fill", fillArea));
        Shape(fill.gameObject, fillColor, cornerScale: 0.4f).raycastTarget = false;

        // Area geser setinggi handle supaya handle tetap bulat; lebarnya dikurangi satu handle
        // supaya handle tidak keluar dari ujung track.
        RectTransform handleArea = NewRect("Handle Slide Area", root);
        handleArea.anchorMin = new Vector2(0f, 0.5f);
        handleArea.anchorMax = new Vector2(1f, 0.5f);
        handleArea.pivot = new Vector2(0.5f, 0.5f);
        handleArea.anchoredPosition = Vector2.zero;
        handleArea.sizeDelta = new Vector2(-handleSize, handleSize);
        RectTransform handle = NewRect("Handle", handleArea);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(handleSize, 0f);
        Image ring = Shape(handle.gameObject, ringColor, "ui-circle");
        ring.preserveAspect = true;
        RectTransform dot = Stretch(NewRect("Handle Dot", handle), 7f, 7f, 7f, 7f);
        Image dotImage = Shape(dot.gameObject, Color.white, "ui-circle");
        dotImage.preserveAspect = true;
        dotImage.raycastTarget = false;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = ring;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.colors = PressTint();
        return slider;
    }

    /// <summary>Slider + tombol − dan + di kedua sisinya. Tombol menggeser nilai sebesar step.</summary>
    public static Slider StepSlider(string name, RectTransform parent, float x, float y, float width, float min,
                                    float max, float step, Color fillColor, Color ringColor, float buttonSize = 60f) {
        Button minus = RoundButton(name + " Minus Button", parent, "icon-minus", x, y, buttonSize);
        float gap = 20f;
        float sliderWidth = width - (buttonSize + gap) * 2f;
        Slider slider = CreateSlider(name, parent, min, max, fillColor, ringColor);
        Place((RectTransform)slider.transform, x + buttonSize + gap, y, sliderWidth, buttonSize);
        Button plus = RoundButton(name + " Plus Button", parent, "icon-plus", x + width - buttonSize, y, buttonSize);
        minus.gameObject.AddComponent<SliderStepButton>().Init(slider, -step);
        plus.gameObject.AddComponent<SliderStepButton>().Init(slider, step);
        return slider;
    }

    public static void SetLayerRecursive(Transform t, int layer) {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayerRecursive(child, layer);
    }

    public static Transform FindDeep(Transform parent, string name) {
        foreach (Transform child in parent) {
            if (child.name == name)
                return child;
            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>Hapus semua anak (dipakai sebelum membangun ulang isi panel).</summary>
    public static void ClearChildren(Transform parent) {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}

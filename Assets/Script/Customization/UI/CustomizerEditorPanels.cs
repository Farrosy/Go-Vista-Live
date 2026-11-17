using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Slider + label + nilai. Changed saat digeser (preview), Committed saat dilepas (satu langkah URUNGKAN).</summary>
public sealed class SliderRow {
    public GameObject Root;
    public Slider Slider;
    public TextMeshProUGUI Label;
    public TextMeshProUGUI Value;
    public CanvasGroup Group;
    public SliderCommit Commit;
    /// <summary>Format teks nilai (default "30%").</summary>
    public Func<float, string> Format = v => Mathf.RoundToInt(v) + "%";

    public void SetValue(float value) {
        Slider.SetValueWithoutNotify(value);
        Value.text = Format(Slider.value);
    }

    public void SetEnabled(bool enabled) {
        Slider.interactable = enabled;
        Group.alpha = enabled ? 1f : 0.4f;
    }
}

/// <summary>Memberi tahu saat jari/mouse dilepas dari slider.</summary>
public sealed class SliderCommit : MonoBehaviour, IPointerUpHandler {
    public event Action<float> Committed;
    private Slider _slider;

    public void Init(Slider slider) => _slider = slider;

    public void OnPointerUp(PointerEventData eventData) {
        if (_slider && _slider.interactable) Committed?.Invoke(_slider.value);
    }
}

/// <summary>Panel Wajah · Bentuk: daftar slider (dibuat ulang controller sesuai kepala).</summary>
public sealed class ShapePanelRefs {
    public GameObject Root;
    public RectTransform List;
    public ScrollRect Scroll;
}

/// <summary>Panel Stiker (mockup v2 layar 4).</summary>
public sealed class StickerPanelRefs {
    public GameObject Root;
    public RectTransform StickerGrid;
    public TextMeshProUGUI Count;
    public RectTransform LayerList;
    public TextMeshProUGUI EmptyText;
    public GameObject EditArea;
    public Button[] PositionButtons;
    public GameObject[] PositionSelected;
    public Image ColorSwatch;
    public Button ColorButton;
    public SliderRow Size;
    public SliderRow Rotation;
}

/// <summary>Popup HSV picker (warna stiker).</summary>
public sealed class ColorPopupRefs {
    public GameObject Overlay;
    public TextMeshProUGUI Title;
    public HsvColorPicker Picker;
    public Button Done;
}

/// <summary>Area Motif di bagian warna: pilihan motif + ukuran.</summary>
public sealed class PatternAreaRefs {
    public GameObject Root;
    public RectTransform Options;
    public SliderRow Scale;
    public TextMeshProUGUI Note;
}

/// <summary>
/// Pembangun panel editor tahap 1d (dipisah dari CustomizerEditorView supaya file tetap kecil):
/// slider, Wajah · Bentuk, Stiker, popup warna, toggle kamera, dan area Motif.
/// </summary>
public static class CustomizerEditorPanels {
    public const float RowHeight = 56f;
    private const float LabelWidth = 230f;
    private const float ValueWidth = 76f;
    private const float StickerItemSize = 104f;

    // ---------------------------------------------------------------- Slider

    public static SliderRow CreateSliderRow(Transform parent, ChatUiAssets a, string label, float min, float max,
                                            float labelWidth = LabelWidth) {
        var row = new SliderRow();
        RectTransform rect = ChatUiKit.CreateRect("Slider " + label, parent);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
        row.Root = rect.gameObject;
        row.Group = rect.gameObject.AddComponent<CanvasGroup>();

        row.Label = ChatUiKit.CreateText("Label", rect, a.font, label, 20, FriendsTheme.Neutral, FontStyles.Bold);
        LeftColumn(row.Label.rectTransform, 0f, labelWidth);
        row.Label.enableAutoSizing = true;
        row.Label.fontSizeMin = 14f;
        row.Label.fontSizeMax = 20f;
        row.Value = ChatUiKit.CreateText("Value", rect, a.font, "", 20, Color.white, FontStyles.Bold,
                                         TextAlignmentOptions.Right);
        RightColumn(row.Value.rectTransform, ValueWidth);

        RectTransform track = ChatUiKit.CreateRect("Track", rect);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(labelWidth + 14f, -18f);
        track.offsetMax = new Vector2(-(ValueWidth + 14f), 18f);
        row.Slider = BuildSlider(track, a, min, max);
        row.Slider.onValueChanged.AddListener(v => row.Value.text = row.Format(v));
        row.Commit = track.gameObject.AddComponent<SliderCommit>();
        row.Commit.Init(row.Slider);
        return row;
    }

    private static Slider BuildSlider(RectTransform root, ChatUiAssets a, float min, float max) {
        RectTransform background = ChatUiKit.CreateRect("Background", root);
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.offsetMin = new Vector2(0f, -7f);
        background.offsetMax = new Vector2(0f, 7f);
        ChatUiKit.AddShape(background.gameObject, a.rounded, FriendsTheme.Card, 3f);

        RectTransform fillArea = ChatUiKit.Stretch(ChatUiKit.CreateRect("Fill Area", background), 0f, 0f, 0f, 0f);
        RectTransform fill = ChatUiKit.Stretch(ChatUiKit.CreateRect("Fill", fillArea), 0f, 0f, 0f, 0f);
        ChatUiKit.AddShape(fill.gameObject, a.rounded, Color.white, 3f).raycastTarget = false;
        fill.gameObject.AddComponent<UIVerticalGradient>().SetColors(FriendsTheme.BlueTop, FriendsTheme.BlueBottom);

        RectTransform handleArea = ChatUiKit.Stretch(ChatUiKit.CreateRect("Handle Area", root), 16f, 0f, 16f, 0f);
        RectTransform handle = ChatUiKit.CreateRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(34f, 0f);
        Image knob = ChatUiKit.AddShape(handle.gameObject, a.circle, Color.white);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);

        // Area sentuh selebar track supaya mudah digeser di HP.
        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = knob;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        return slider;
    }

    // ---------------------------------------------------------------- Kamera

    /// <summary>Toggle "Seluruh Badan | Wajah" di atas panggung.</summary>
    public static void CreateCameraToggle(RectTransform area, ChatUiAssets a, out Button body, out GameObject bodyOn,
                                          out Button face, out GameObject faceOn) {
        RectTransform seg = ChatUiKit.CreateRect("Camera Toggle", area);
        seg.anchorMin = seg.anchorMax = seg.pivot = new Vector2(0.5f, 1f);
        seg.anchoredPosition = Vector2.zero;
        seg.sizeDelta = new Vector2(380f, 56f);
        ChatUiKit.AddShape(seg.gameObject, a.rounded, new Color32(0x0D, 0x12, 0x22, 0xBF), 3f);
        FriendsUiKit.AddBorder(seg, FriendsTheme.CardBorder, 3f);
        body = SegmentChip(seg, a, "Seluruh Badan", 6f, 210f, out bodyOn);
        face = SegmentChip(seg, a, "Wajah", 222f, 152f, out faceOn);
    }

    private static Button SegmentChip(RectTransform seg, ChatUiAssets a, string label, float x, float width, out GameObject on) {
        RectTransform rect = ChatUiKit.CreateRect("Chip " + label, seg);
        ChatUiKit.Place(rect, x, 6f, width, 44f);
        Image hit = ChatUiKit.AddShape(rect.gameObject, a.rounded, new Color(0f, 0f, 0f, 0f), 2.6f);
        RectTransform selected = ChatUiKit.Stretch(ChatUiKit.CreateRect("Selected", rect), 0f, 0f, 0f, 0f);
        ChatUiKit.AddShape(selected.gameObject, a.rounded, FriendsTheme.TabSelected, 2.6f).raycastTarget = false;
        FriendsUiKit.AddBorder(selected, new Color(1f, 1f, 1f, 0.18f), 2.6f);
        on = selected.gameObject;
        TextMeshProUGUI text = ChatUiKit.CreateText("Label", rect, a.font, label, 20, Color.white, FontStyles.Bold,
                                                    TextAlignmentOptions.Center);
        ChatUiKit.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        return button;
    }

    // ---------------------------------------------------------------- Wajah · Bentuk

    public static ShapePanelRefs BuildShapePanel(RectTransform panel, float pad, float top) {
        var refs = new ShapePanelRefs();
        RectTransform root = ChatUiKit.Stretch(ChatUiKit.CreateRect("Shape Panel", panel), pad, top, pad, pad);
        refs.Root = root.gameObject;
        refs.Scroll = CreateVerticalScroll(root, "Shapes", 6f, out refs.List);
        root.gameObject.SetActive(false);
        return refs;
    }

    // ---------------------------------------------------------------- Stiker

    public static StickerPanelRefs BuildStickerPanel(RectTransform panel, ChatUiAssets a, float pad, float top,
                                                     float innerWidth) {
        var refs = new StickerPanelRefs();
        RectTransform root = ChatUiKit.Stretch(ChatUiKit.CreateRect("Sticker Panel", panel), pad, top, pad, pad);
        refs.Root = root.gameObject;
        CreateVerticalScroll(root, "Sticker Content", 12f, out RectTransform content);

        Section(content, a, "Pilih stiker untuk ditambahkan", null);
        refs.StickerGrid = ChatUiKit.CreateRect("Sticker Grid", content);
        GridLayoutGroup grid = refs.StickerGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(StickerItemSize, StickerItemSize);
        grid.spacing = new Vector2(10f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((innerWidth + 10f) / (StickerItemSize + 10f)));

        refs.Count = Section(content, a, "Terpasang", "0 / 5");
        refs.LayerList = ChatUiKit.CreateRect("Layers", content);
        VerticalLayoutGroup layers = refs.LayerList.gameObject.AddComponent<VerticalLayoutGroup>();
        layers.spacing = 8f;
        layers.childControlWidth = layers.childControlHeight = true;
        layers.childForceExpandWidth = true;
        layers.childForceExpandHeight = false;
        refs.EmptyText = ChatUiKit.CreateText("Empty", content, a.font, "Belum ada stiker. Pilih stiker di atas.",
                                              FriendsTheme.SmallSize - 2, FriendsTheme.Muted, FontStyles.Normal);
        refs.EmptyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

        BuildStickerEditArea(content, a, refs);
        root.gameObject.SetActive(false);
        return refs;
    }

    private static void BuildStickerEditArea(RectTransform content, ChatUiAssets a, StickerPanelRefs refs) {
        RectTransform area = ChatUiKit.CreateRect("Edit Area", content);
        VerticalLayoutGroup list = area.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 12f;
        list.childControlWidth = list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        refs.EditArea = area.gameObject;

        Section(area, a, "Posisi", null);
        RectTransform positions = ChatUiKit.CreateRect("Positions", area);
        GridLayoutGroup grid = positions.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(208f, 52f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        int count = CustomizationRules.DecalPositions.Count;
        refs.PositionButtons = new Button[count];
        refs.PositionSelected = new GameObject[count];
        for (int i = 0; i < count; i++) {
            refs.PositionButtons[i] = CustomizerEditorView.CreateChip(positions, a,
                StickerPositionLabel(CustomizationRules.DecalPositions[i]), out refs.PositionSelected[i]);
        }

        RectTransform colorRow = ChatUiKit.CreateRect("Color Row", area);
        colorRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        TextMeshProUGUI colorLabel = ChatUiKit.CreateText("Label", colorRow, a.font, "Warna stiker", 20, FriendsTheme.Neutral,
                                                          FontStyles.Bold);
        LeftColumn(colorLabel.rectTransform, 0f, LabelWidth);
        RectTransform ring = ChatUiKit.CreateRect("Ring", colorRow);
        ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(0f, 0.5f);
        ring.anchoredPosition = new Vector2(LabelWidth + 14f, 0f);
        ring.sizeDelta = new Vector2(40f, 40f);
        ChatUiKit.AddShape(ring.gameObject, a.circle, Color.white).raycastTarget = false;
        RectTransform dot = ChatUiKit.Stretch(ChatUiKit.CreateRect("Color", ring), 4f, 4f, 4f, 4f);
        refs.ColorSwatch = ChatUiKit.AddShape(dot.gameObject, a.circle, Color.white);
        refs.ColorSwatch.raycastTarget = false;
        refs.ColorButton = FriendsUiKit.CreateOutlineButton("Color Button", colorRow, a, "Ubah warna", Color.white,
                                                            FriendsTheme.NeutralFill, null, 18);
        var buttonRect = (RectTransform)refs.ColorButton.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(LabelWidth + 70f, 0f);
        buttonRect.sizeDelta = new Vector2(190f, 48f);

        refs.Size = CreateSliderRow(area, a, "Ukuran", CustomizationRules.MinDecalSize, CustomizationRules.MaxDecalSize);
        refs.Size.Format = v => Mathf.RoundToInt(Mathf.InverseLerp(CustomizationRules.MinDecalSize,
                                                                   CustomizationRules.MaxDecalSize, v) * 100f) + "%";
        refs.Rotation = CreateSliderRow(area, a, "Putar", -CustomizationRules.MaxDecalRotation,
                                        CustomizationRules.MaxDecalRotation);
        refs.Rotation.Format = v => Mathf.RoundToInt(v) + "°";
    }

    public static string StickerPositionLabel(string position) {
        switch (position) {
            case "Chest": return "Dada";
            case "Back": return "Punggung";
            case "Face": return "Wajah";
            case "LeftArm": return "Lengan Kiri";
            case "RightArm": return "Lengan Kanan";
            case "Leg": return "Kaki";
            default: return position;
        }
    }

    /// <summary>Satu baris stiker terpasang: ikon, "Nama · Posisi", tombol lepas.</summary>
    public static Button CreateLayer(Transform parent, ChatUiAssets a, Sprite icon, string label, out Button remove,
                                     out GameObject selected) {
        RectTransform rect = ChatUiKit.CreateRect("Layer " + label, parent);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        Image bg = ChatUiKit.AddShape(rect.gameObject, a.rounded, FriendsTheme.Card, 1.6f);
        RectTransform on = ChatUiKit.Stretch(ChatUiKit.CreateRect("Selected", rect), 0f, 0f, 0f, 0f);
        ChatUiKit.AddShape(on.gameObject, a.rounded, FriendsTheme.TabSelected, 1.6f).raycastTarget = false;
        FriendsUiKit.AddBorder(on, ProfileTheme.Tag, 1.6f);
        selected = on.gameObject;

        RectTransform thumb = ChatUiKit.CreateRect("Thumb", rect);
        thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(0f, 0.5f);
        thumb.anchoredPosition = new Vector2(10f, 0f);
        thumb.sizeDelta = new Vector2(46f, 46f);
        ChatUiKit.AddShape(thumb.gameObject, a.rounded, FriendsTheme.Panel, 1.4f).raycastTarget = false;
        Image image = ChatUiKit.AddIcon("Icon", thumb, icon, Color.white);
        ChatUiKit.Stretch(image.rectTransform, 5f, 5f, 5f, 5f);

        TextMeshProUGUI text = ChatUiKit.CreateText("Label", rect, a.font, label, 20, Color.white, FontStyles.Bold);
        ChatUiKit.Stretch(text.rectTransform, 70f, 0f, 70f, 0f);
        remove = FriendsUiKit.CreateOutlineButton("Remove", rect, a, "", FriendsTheme.Danger, FriendsTheme.DangerFill,
                                                  a.closeIcon, 18);
        var removeRect = (RectTransform)remove.transform;
        removeRect.anchorMin = removeRect.anchorMax = removeRect.pivot = new Vector2(1f, 0.5f);
        removeRect.anchoredPosition = new Vector2(-8f, 0f);
        removeRect.sizeDelta = new Vector2(48f, 48f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        return button;
    }

    // ---------------------------------------------------------------- Popup warna

    public static ColorPopupRefs BuildColorPopup(RectTransform root, ChatUiAssets a) {
        const float width = 640f;
        const float pickerWidth = width - 72f;
        var refs = new ColorPopupRefs();
        RectTransform overlay = ChatUiKit.Stretch(ChatUiKit.CreateRect("Color Popup", root), 0f, 0f, 0f, 0f);
        overlay.gameObject.AddComponent<Image>().color = FriendsTheme.Dim;
        RectTransform panel = CustomizerUi.CreatePanel(overlay, a, width, 400f);
        refs.Title = ChatUiKit.CreateText("Title", panel, a.font, "Warna stiker", 34, Color.white, FontStyles.Bold);
        ChatUiKit.Place(refs.Title.rectTransform, 36f, 30f, pickerWidth, 50f);
        RectTransform host = ChatUiKit.CreateRect("Picker Host", panel);
        ChatUiKit.Place(host, 36f, 96f, pickerWidth, 180f);
        refs.Picker = HsvColorPicker.Build(host, a, pickerWidth);
        refs.Done = FriendsUiKit.CreateSolidButton("Done", panel, a, "SELESAI", FriendsTheme.GreenTop,
                                                   FriendsTheme.GreenBottom, FriendsTheme.GreenLip, a.checkIcon, 24);
        var doneRect = (RectTransform)refs.Done.transform;
        doneRect.anchorMin = doneRect.anchorMax = doneRect.pivot = new Vector2(1f, 0f);
        doneRect.anchoredPosition = new Vector2(-36f, 30f);
        doneRect.sizeDelta = new Vector2(220f, 68f);
        refs.Overlay = overlay.gameObject;
        overlay.gameObject.SetActive(false);
        return refs;
    }

    // ---------------------------------------------------------------- Motif

    public static PatternAreaRefs BuildPatternArea(RectTransform parent, ChatUiAssets a, float y, float width) {
        var refs = new PatternAreaRefs();
        RectTransform root = ChatUiKit.CreateRect("Pattern Area", parent);
        ChatUiKit.Place(root, 0f, y, width, 214f);
        refs.Root = root.gameObject;

        refs.Options = ChatUiKit.CreateRect("Patterns", root);
        ChatUiKit.Place(refs.Options, 0f, 0f, width, 96f);
        HorizontalLayoutGroup row = refs.Options.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 10f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;

        RectTransform scaleHost = ChatUiKit.CreateRect("Scale", root);
        ChatUiKit.Place(scaleHost, 0f, 108f, width, RowHeight);
        VerticalLayoutGroup hostLayout = scaleHost.gameObject.AddComponent<VerticalLayoutGroup>();
        hostLayout.childControlWidth = hostLayout.childControlHeight = true;
        hostLayout.childForceExpandWidth = true;
        refs.Scale = CreateSliderRow(scaleHost, a, "Ukuran motif", CustomizationRules.MinPatternScale,
                                     CustomizationRules.MaxPatternScale, 170f);
        refs.Scale.Format = v => v.ToString("0.0") + "×";

        refs.Note = ChatUiKit.CreateText("Note", root, a.font, "Warna motif memakai bagian warna 1–3 baju ini.", 17,
                                         FriendsTheme.Muted, FontStyles.Normal);
        ChatUiKit.Place(refs.Note.rectTransform, 0f, 172f, width, 36f);
        root.gameObject.SetActive(false);
        return refs;
    }

    /// <summary>Kotak pilihan motif ("Tanpa", "Plaid", ...).</summary>
    public static Button CreatePatternOption(Transform parent, ChatUiAssets a, Sprite icon, string label, out GameObject selected) {
        Button button = CustomizerEditorView.CreateItem(parent, a, icon, label, out selected);
        LayoutElement size = button.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = size.preferredHeight = 96f;
        return button;
    }

    // ---------------------------------------------------------------- Bantuan

    /// <summary>Judul seksi kecil ("Terpasang", "Posisi") + teks kanan opsional.</summary>
    public static TextMeshProUGUI Section(Transform parent, ChatUiAssets a, string left, string right) {
        RectTransform rect = ChatUiKit.CreateRect("Section " + left, parent);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
        TextMeshProUGUI title = ChatUiKit.CreateText("Title", rect, a.font, left, 20, FriendsTheme.Neutral, FontStyles.Bold);
        ChatUiKit.Stretch(title.rectTransform, 0f, 0f, 120f, 0f);
        if (right == null) return title;
        TextMeshProUGUI value = ChatUiKit.CreateText("Value", rect, a.font, right, 20, FriendsTheme.Muted, FontStyles.Bold,
                                                     TextAlignmentOptions.Right);
        RightColumn(value.rectTransform, 120f);
        return value;
    }

    /// <summary>ScrollRect vertikal memenuhi parent; isi pakai VerticalLayoutGroup + tinggi otomatis.</summary>
    public static ScrollRect CreateVerticalScroll(RectTransform parent, string name, float spacing, out RectTransform content) {
        RectTransform view = ChatUiKit.Stretch(ChatUiKit.CreateRect(name + " Scroll", parent), 0f, 0f, 0f, 0f);
        view.gameObject.AddComponent<RectMask2D>();
        view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // area geser
        ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        content = ChatUiKit.CreateRect(name, view);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup list = content.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = spacing;
        list.childControlWidth = list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        scroll.viewport = view;
        return scroll;
    }

    private static void LeftColumn(RectTransform rect, float x, float width) {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(x, 0f);
        rect.offsetMax = new Vector2(x + width, 0f);
    }

    private static void RightColumn(RectTransform rect, float width) {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(-width, 0f);
        rect.offsetMax = Vector2.zero;
    }
}

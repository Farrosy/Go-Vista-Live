using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static RedesignUiKit;

/// <summary>
/// Desain baru layar ATUR KONTROL (objek "Control Layout Editor" di Assets/MobilePrefabs/MobileUI.prefab).
/// Menu: Go Vista > Desain Baru > Atur Kontrol.
///
/// Objek "Control Layout Editor" dan komponen ControlLayoutEditor-nya TETAP (daftar kontrol, tombol
/// Interact tidak berubah). Yang diganti hanya anak-anaknya:
///   Edit Grid        : grid tipis, tanda sedang mode edit
///   Edit Rings       : lingkaran putus-putus di sekitar tiap tombol (ControlEditRings)
///   Selection Frame  : bingkai putus-putus cyan + titik sudut + label "Joystick · 100%" (TextMirror),
///                      di sebelahnya pilihan Setir / Panah (hanya tampil saat kontrol Setir dipilih)
///   Toolbar          : judul, tab Karakter / Kendaraan, RESET / BATAL / SIMPAN, lalu pilihan + UKURAN − / +
/// Referensi di ControlLayoutEditor (selectionFrame, toolbar, tab, sizeSlider, dst.) diisi ulang otomatis.
/// Sprite tab & pilihan setir: Assets/GoVista_HUD_Assets/AturKontrol.
///
/// Catatan: menu lama "Go Vista > Buat Ulang UI Atur Kontrol" membangun versi lama. Kalau tidak sengaja
/// dipakai, jalankan menu ini lagi.
/// </summary>
public static class ControlEditorRedesign {
    private const string PrefabPath = "Assets/MobilePrefabs/MobileUI.prefab";
    private const string EditorObjectName = "Control Layout Editor";
    private const float ToolbarWidth = 1240f;
    private const float ToolbarHeight = 214f;
    private static readonly Color DimColor = new Color32(4, 6, 16, 120);
    private const float GridAlpha = 0.06f;
    private const string BackdropName = "Control Edit Backdrop";
    private const string AssetFolder = "Assets/GoVista_HUD_Assets/AturKontrol/";
    private static readonly Color TabOnContent = new Color32(4, 34, 43, 255);

    /// <summary>Referensi satu tab / pilihan setir yang diisi ke ControlLayoutEditor.</summary>
    private sealed class Segment {
        public Button button;
        public GameObject on;
        public Graphic[] content;
    }

    [MenuItem("Go Vista/Desain Baru/Atur Kontrol")]
    private static void Apply() {
        if (!EditorUtility.DisplayDialog("Desain baru Atur Kontrol",
                $"Isi \"{EditorObjectName}\" di MobileUI.prefab (toolbar dan bingkai pilihan) akan diganti dengan " +
                "desain baru. Daftar kontrol dan posisi tombol yang tersimpan tidak berubah.", "Pasang", "Batal"))
            return;
        Build();
    }

    /// <summary>Bangun ulang isi "Control Layout Editor" di MobileUI.prefab (tanpa dialog konfirmasi).</summary>
    public static void Build() {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            Transform editorObject = root.transform.Find(EditorObjectName);
            ControlLayoutEditor editor = editorObject != null ? editorObject.GetComponent<ControlLayoutEditor>() : null;
            if (editor == null) {
                Debug.LogWarning($"[Desain Baru] \"{EditorObjectName}\" tidak ditemukan di {PrefabPath}. " +
                                 "Jalankan dulu Go Vista > Buat Ulang UI Atur Kontrol, lalu ulangi.");
                return;
            }
            Build(editor);
            SetLayerRecursive(editorObject, root.layer);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[Desain Baru] Atur Kontrol di MobileUI.prefab sudah diganti ke desain baru.");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Build(ControlLayoutEditor editor) {
        var editorRect = (RectTransform)editor.transform;
        ClearChildren(editorRect);

        // Objek editor (child terakhir canvas) tetap menangkap semua sentuhan, tapi transparan: latar gelap + grid
        // ada di objek terpisah DI BELAKANG tombol (EditModeBackdrop), supaya tombol tetap terang saat diatur.
        Image catcher = editorRect.GetComponent<Image>();
        if (catcher == null)
            catcher = editorRect.gameObject.AddComponent<Image>();
        catcher.sprite = null;
        catcher.color = Color.clear;
        catcher.raycastTarget = true;

        GameObject backdrop = BuildBackdrop((RectTransform)editorRect.parent);
        EditModeBackdrop backdropScript = editor.GetComponent<EditModeBackdrop>();
        if (backdropScript == null)
            backdropScript = editor.gameObject.AddComponent<EditModeBackdrop>();
        backdropScript.Init(backdrop);

        RectTransform rings = Stretch(NewRect("Edit Rings", editorRect));
        RectTransform frame = BuildSelectionFrame(editorRect, out Text frameTag, out GameObject steerToggle,
                                                  out Segment steerWheel, out Segment steerArrow);
        RectTransform toolbar = BuildToolbar(editorRect, out Text selectedLabel, out Slider sizeSlider,
                                             out Text sizeValue, out Button reset, out Button cancel, out Button save,
                                             out Segment characterTab, out Segment vehicleTab);

        frameTag.gameObject.AddComponent<TextMirror>().Init(new[] { selectedLabel, sizeValue }, " · ");
        ControlEditRings ringScript = editor.GetComponent<ControlEditRings>();
        if (ringScript == null)
            ringScript = editor.gameObject.AddComponent<ControlEditRings>();
        // ui-dashed-ring: 512 px & garis tipis, tetap tipis saat diperbesar ke ukuran tombol (ui-dashed-circle cuma 64 px).
        ringScript.Init(rings, frame, LoadSprite("ui-dashed-ring"));

        var so = new SerializedObject(editor);
        so.FindProperty("selectionFrame").objectReferenceValue = frame;
        so.FindProperty("toolbar").objectReferenceValue = toolbar;
        so.FindProperty("selectedLabel").objectReferenceValue = selectedLabel;
        so.FindProperty("sizeSlider").objectReferenceValue = sizeSlider;
        so.FindProperty("sizeValue").objectReferenceValue = sizeValue;
        so.FindProperty("resetButton").objectReferenceValue = reset;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.FindProperty("saveButton").objectReferenceValue = save;
        SetSegment(so, "characterTab", "characterTabOn", "characterTabContent", characterTab);
        SetSegment(so, "vehicleTab", "vehicleTabOn", "vehicleTabContent", vehicleTab);
        so.FindProperty("tabOnContentColor").colorValue = CyanTextColor;
        so.FindProperty("tabOffContentColor").colorValue = GhostTextColor;
        so.FindProperty("steerModeToggle").objectReferenceValue = steerToggle;
        SetSegment(so, "steerWheelButton", "steerWheelOn", "steerWheelContent", steerWheel);
        SetSegment(so, "steerArrowButton", "steerArrowOn", "steerArrowContent", steerArrow);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(editor);
    }

    private static void SetSegment(SerializedObject so, string button, string on, string content, Segment segment) {
        so.FindProperty(button).objectReferenceValue = segment.button;
        so.FindProperty(on).objectReferenceValue = segment.on;
        SerializedProperty array = so.FindProperty(content);
        array.arraySize = segment.content.Length;
        for (int i = 0; i < segment.content.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = segment.content[i];
    }

    private static Sprite LoadAsset(string name) {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetFolder + name + ".png");
        if (sprite == null)
            Debug.LogWarning($"[Desain Baru] Sprite {AssetFolder}{name}.png tidak ditemukan.");
        return sprite;
    }

    private static Image SlicedImage(GameObject target, string sprite, Color color) {
        Image image = target.AddComponent<Image>();
        image.sprite = LoadAsset(sprite);
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    /// <summary>
    /// Satu pilihan (tab / segmen): area sentuh transparan, latar cyan "On" (ditampilkan ControlLayoutEditor saat
    /// aktif), lalu [ikon] Teks di tengah. Warna ikon & teks diatur ControlLayoutEditor sesuai aktif/tidak.
    /// </summary>
    private static Segment BuildSegment(string name, Transform parent, string onSprite, string icon, string label,
                                        int fontSize, float iconSize, float width, float height, bool active) {
        RectTransform rect = NewRect(name, parent);
        rect.sizeDelta = new Vector2(width, height);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = width;
        size.preferredHeight = height;
        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;

        RectTransform on = Stretch(NewRect(name + " On", rect));
        SlicedImage(on.gameObject, onSprite, Color.white).raycastTarget = false;
        on.gameObject.SetActive(active);

        Color color = active ? TabOnContent : GhostTextColor;
        RectTransform content = Stretch(NewRect(name + " Content", rect));
        HorizontalLayoutGroup row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 8f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        RectTransform iconRect = NewRect(name + " Icon", content);
        Image iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.sprite = LoadAsset(icon);
        iconImage.preserveAspect = true;
        iconImage.color = color;
        iconImage.raycastTarget = false;
        LayoutElement iconLayout = iconRect.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = iconLayout.preferredHeight = iconSize;
        Text text = Label(name + " Label", content, label, fontSize, true, TextAnchor.MiddleCenter, color);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return new Segment { button = button, on = on.gameObject, content = new Graphic[] { iconImage, text } };
    }

    /// <summary>
    /// Tab bergaya tombol BATAL (GhostButton: kotak gelap + garis tepi tipis). "On" (garis tepi cyan + isi cyan
    /// tipis) ditampilkan ControlLayoutEditor saat tab aktif; warna ikon & teks juga diatur di sana.
    /// </summary>
    private static Segment BuildTabButton(string name, RectTransform parent, string icon, string label, float x,
                                          bool active) {
        RectTransform rect = Place(NewRect(name, parent), x, 20f, 172f, 72f);
        Image image = Shape(rect.gameObject, GhostColor, cornerScale: 1f);
        AddOutline(rect, new Color(1f, 1f, 1f, 0.1f), cornerScale: 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = PressTint();

        RectTransform on = Stretch(NewRect(name + " On", rect));
        Shape(on.gameObject, new Color(CyanTopColor.r, CyanTopColor.g, CyanTopColor.b, 0.14f), cornerScale: 1f)
            .raycastTarget = false;
        AddOutline(on, CyanTopColor, cornerScale: 1f);
        on.gameObject.SetActive(active);

        Color color = active ? CyanTextColor : GhostTextColor;
        RectTransform content = Stretch(NewRect(name + " Content", rect));
        HorizontalLayoutGroup row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 8f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        RectTransform iconRect = NewRect(name + " Icon", content);
        Image iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.sprite = LoadAsset(icon);
        iconImage.preserveAspect = true;
        iconImage.color = color;
        iconImage.raycastTarget = false;
        LayoutElement iconLayout = iconRect.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = iconLayout.preferredHeight = 28f;
        Text text = Label(name + " Label", content, label, 24, true, TextAnchor.MiddleCenter, color);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return new Segment { button = button, on = on.gameObject, content = new Graphic[] { iconImage, text } };
    }

    /// <summary>Latar mode edit (gelap tipis + grid) sebagai child PERTAMA canvas, di belakang semua tombol.</summary>
    private static GameObject BuildBackdrop(RectTransform canvas) {
        Transform old = canvas.Find(BackdropName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        RectTransform backdrop = Stretch(NewRect(BackdropName, canvas));
        backdrop.SetAsFirstSibling();
        Image dim = backdrop.gameObject.AddComponent<Image>();
        dim.color = DimColor;
        dim.raycastTarget = false;
        // Garis di ui-grid.png buram penuh (alpha 255), jadi tipisnya diatur lewat alpha warna ini.
        Image grid = Shape(Stretch(NewRect("Edit Grid", backdrop)).gameObject, new Color(1f, 1f, 1f, GridAlpha), "ui-grid");
        grid.raycastTarget = false;
        backdrop.gameObject.SetActive(false); // dimunculkan EditModeBackdrop saat editor dibuka
        return backdrop.gameObject;
    }

    /// <summary>Bingkai pilihan. Ukuran & posisinya diatur ControlLayoutEditor saat runtime.</summary>
    private static RectTransform BuildSelectionFrame(RectTransform parent, out Text tagText, out GameObject steerToggle,
                                                     out Segment steerWheel, out Segment steerArrow) {
        RectTransform frame = NewRect("Selection Frame", parent);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
        frame.sizeDelta = new Vector2(320f, 320f);
        Shape(frame.gameObject, new Color(CyanTopColor.r, CyanTopColor.g, CyanTopColor.b, 0.07f), cornerScale: 1.4f)
            .raycastTarget = false;
        AddOutline(frame, CyanTopColor, "ui-dashed");

        // Titik di keempat sudut.
        Vector2[] corners = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
        for (int i = 0; i < corners.Length; i++) {
            RectTransform handle = NewRect("Corner Handle " + (i + 1), frame);
            handle.anchorMin = handle.anchorMax = corners[i];
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(26f, 26f);
            Shape(handle.gameObject, CyanTopColor, "ui-circle").raycastTarget = false;
            RectTransform dot = Stretch(NewRect("Dot", handle), 5f, 5f, 5f, 5f);
            Shape(dot.gameObject, Color.white, "ui-circle").raycastTarget = false;
        }

        // Di atas bingkai: label [ikon geser] Joystick · 100%, lalu (khusus Setir) pilihan Setir / Panah.
        // Lebar baris mengikuti isinya.
        RectTransform header = NewRect("Selection Header", frame);
        header.anchorMin = header.anchorMax = new Vector2(0f, 1f);
        header.pivot = new Vector2(0f, 0f);
        header.anchoredPosition = new Vector2(0f, 14f);
        header.sizeDelta = new Vector2(240f, 60f);
        HorizontalLayoutGroup headerRow = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerRow.spacing = 10f;
        headerRow.childAlignment = TextAnchor.MiddleLeft;
        headerRow.childControlWidth = headerRow.childControlHeight = true;
        headerRow.childForceExpandWidth = headerRow.childForceExpandHeight = false;
        ContentSizeFitter headerFit = header.gameObject.AddComponent<ContentSizeFitter>();
        headerFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        headerFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform tag = NewRect("Selection Tag", header);
        Shape(tag.gameObject, CyanTopColor, cornerScale: 0.6f).raycastTarget = false;
        LayoutElement tagSize = tag.gameObject.AddComponent<LayoutElement>();
        tagSize.minHeight = tagSize.preferredHeight = 48f;
        HorizontalLayoutGroup row = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(14, 16, 0, 0);
        row.spacing = 8f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        Color tagTextColor = new Color32(4, 34, 43, 255);
        Image moveIcon = Icon("Selection Tag Icon", tag, "icon-move", tagTextColor, 0f, 0f, 24f);
        LayoutElement iconSize = moveIcon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = iconSize.preferredHeight = 24f;
        tagText = Label("Selection Tag Label", tag, "Joystick · 100%", 22, true, TextAnchor.MiddleLeft, tagTextColor);
        tagText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // Pilihan Setir / Panah: tipe kontrol belok mobil (GameSettings.DriveControl).
        RectTransform toggle = NewRect("Steer Mode Toggle", header);
        SlicedImage(toggle.gameObject, "ak_steer_toggle_bg", Color.white);
        HorizontalLayoutGroup toggleRow = toggle.gameObject.AddComponent<HorizontalLayoutGroup>();
        toggleRow.padding = new RectOffset(6, 6, 6, 6);
        toggleRow.spacing = 4f;
        toggleRow.childAlignment = TextAnchor.MiddleCenter;
        toggleRow.childControlWidth = toggleRow.childControlHeight = true;
        toggleRow.childForceExpandWidth = toggleRow.childForceExpandHeight = false;
        steerWheel = BuildSegment("Steer Wheel Option", toggle, "ak_steer_seg_on", "icon_steer_setir", "Setir", 20,
                                  24f, 118f, 46f, active: true);
        steerArrow = BuildSegment("Steer Arrow Option", toggle, "ak_steer_seg_on", "icon_steer_panah", "Panah", 20,
                                  24f, 118f, 46f, active: false);
        toggle.gameObject.SetActive(false); // ditampilkan ControlLayoutEditor saat kontrol Setir dipilih
        steerToggle = toggle.gameObject;
        return frame;
    }

    private static RectTransform BuildToolbar(RectTransform parent, out Text selectedLabel, out Slider sizeSlider,
                                              out Text sizeValue, out Button reset, out Button cancel,
                                              out Button save, out Segment characterTab, out Segment vehicleTab) {
        // Anchor atas-tengah. ControlLayoutEditor memindahkannya ke bawah kalau kontrol terpilih ada di atas.
        RectTransform toolbar = NewRect("Toolbar", parent);
        toolbar.anchorMin = toolbar.anchorMax = toolbar.pivot = new Vector2(0.5f, 1f);
        toolbar.anchoredPosition = new Vector2(0f, -24f);
        toolbar.sizeDelta = new Vector2(ToolbarWidth, ToolbarHeight);
        Shape(toolbar.gameObject, CardColor, cornerScale: 1.5f);
        AddOutline(toolbar, LineColor, cornerScale: 1.5f);

        // Baris 1: judul, tab Karakter / Kendaraan, aksi. SIMPAN satu-satunya tombol berwarna.
        IconTile("Toolbar Icon", toolbar, "icon-move", CyanTopColor, CyanBottomColor, 26f, 24f, 64f, 34f);
        Place(Label("Title", toolbar, "ATUR KONTROL", 28, true, TextAnchor.MiddleLeft, Color.white).rectTransform,
              104f, 20f, 236f, 36f);
        // Best fit: lebar huruf beda antar font/perangkat, jangan sampai melebihi dua baris.
        Text hint = Label("Hint", toolbar, "Seret untuk pindah, ketuk untuk pilih", 18, false,
                          TextAnchor.UpperLeft, SubColor);
        Place(hint.rectTransform, 104f, 58f, 230f, 46f);
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        hint.verticalOverflow = VerticalWrapMode.Truncate; // best fit hanya mengecilkan kalau tinggi dibatasi
        hint.resizeTextForBestFit = true;
        hint.resizeTextMinSize = 13;
        hint.resizeTextMaxSize = 18;

        // Tab Karakter / Kendaraan: gaya sama dengan tombol BATAL. Tab aktif = garis tepi + ikon/teks cyan.
        characterTab = BuildTabButton("Character Tab", toolbar, "icon_tab_karakter", "Karakter", 344f, active: true);
        vehicleTab = BuildTabButton("Vehicle Tab", toolbar, "icon_tab_kendaraan", "Kendaraan", 344f + 172f + 14f,
                                    active: false);

        float right = ToolbarWidth - 26f;
        save = MenuButton("Save Button", toolbar, GreenTopColor, GreenBottomColor, GreenLipColor,
                          new Color(1f, 1f, 1f, 0.25f), 1.1f, out RectTransform saveFace);
        Place((RectTransform)save.transform, right - 190f, 18f, 190f, 82f);
        CenteredContent("Save", saveFace, "icon-check", "SIMPAN", 26, 30f, Color.white);
        cancel = GhostButton("Cancel Button", toolbar, null, "BATAL", 24, 0f);
        Place((RectTransform)cancel.transform, right - 190f - 14f - 124f, 20f, 124f, 72f);
        reset = GhostButton("Reset Button", toolbar, "icon-refresh", "RESET", 24, 28f);
        Place((RectTransform)reset.transform, right - 190f - 14f - 124f - 14f - 160f, 20f, 160f, 72f);

        // Baris 2: kontrol terpilih + UKURAN (− slider +) + persen.
        float barWidth = ToolbarWidth - 52f;
        RectTransform bar = Place(NewRect("Size Bar", toolbar), 26f, 118f, barWidth, 76f);
        Shape(bar.gameObject, SectionBottomColor, cornerScale: 1.1f).raycastTarget = false;

        RectTransform chip = Place(NewRect("Selected Chip", bar), 12f, 10f, 260f, 56f);
        SlicedImage(chip.gameObject, "ak_selected_chip", Color.white).raycastTarget = false;
        Image dot = Place(NewRect("Selected Dot", chip), 18f, 22f, 12f, 12f).gameObject.AddComponent<Image>();
        dot.sprite = LoadAsset("ak_selected_chip_dot");
        dot.raycastTarget = false;
        selectedLabel = Label("Selected Label", chip, "Joystick", 24, true, TextAnchor.MiddleLeft, CyanTextColor);
        Place(selectedLabel.rectTransform, 40f, 0f, 212f, 56f);
        selectedLabel.resizeTextForBestFit = true;
        selectedLabel.resizeTextMinSize = 18;
        selectedLabel.resizeTextMaxSize = 24;

        Place(Label("Size Label", bar, "UKURAN", 20, true, TextAnchor.MiddleLeft, MutedColor).rectTransform,
              292f, 0f, 110f, 76f);
        sizeValue = Label("Size Value", bar, "100%", 28, true, TextAnchor.MiddleRight, Color.white);
        Place(sizeValue.rectTransform, barWidth - 14f - 100f, 0f, 100f, 76f);
        float sliderX = 408f;
        float sliderWidth = barWidth - 14f - 100f - 18f - sliderX;
        // − / + mengubah ukuran 5% per ketukan.
        sizeSlider = StepSlider("Size Slider", bar, sliderX, 10f, sliderWidth, ControlLayoutSettings.MinScale,
                                ControlLayoutSettings.MaxScale, 0.05f, CyanTopColor, CyanTopColor, 56f);
        sizeSlider.value = 1f;
        return toolbar;
    }
}

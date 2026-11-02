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
///   Selection Frame  : bingkai putus-putus cyan + titik sudut + label "Joystick · 100%" (TextMirror)
///   Toolbar          : judul, RESET / BATAL / SIMPAN, lalu pilihan + UKURAN dengan tombol − / +
/// Referensi di ControlLayoutEditor (selectionFrame, toolbar, sizeSlider, dst.) diisi ulang otomatis,
/// jadi ControlLayoutEditor.cs TIDAK perlu diubah.
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

    [MenuItem("Go Vista/Desain Baru/Atur Kontrol")]
    private static void Apply() {
        if (!EditorUtility.DisplayDialog("Desain baru Atur Kontrol",
                $"Isi \"{EditorObjectName}\" di MobileUI.prefab (toolbar dan bingkai pilihan) akan diganti dengan " +
                "desain baru. Daftar kontrol dan posisi tombol yang tersimpan tidak berubah.", "Pasang", "Batal"))
            return;

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
        RectTransform frame = BuildSelectionFrame(editorRect, out Text frameTag);
        RectTransform toolbar = BuildToolbar(editorRect, out Text selectedLabel, out Slider sizeSlider,
                                             out Text sizeValue, out Button reset, out Button cancel, out Button save);

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
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(editor);
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
    private static RectTransform BuildSelectionFrame(RectTransform parent, out Text tagText) {
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

        // Label di atas bingkai: [ikon geser] Joystick · 100%. Lebarnya mengikuti teks.
        RectTransform tag = NewRect("Selection Tag", frame);
        tag.anchorMin = tag.anchorMax = tag.pivot = new Vector2(0f, 1f);
        tag.pivot = new Vector2(0f, 0f);
        tag.anchoredPosition = new Vector2(0f, 14f);
        tag.sizeDelta = new Vector2(240f, 48f);
        Shape(tag.gameObject, CyanTopColor, cornerScale: 0.6f).raycastTarget = false;
        HorizontalLayoutGroup row = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(14, 16, 0, 0);
        row.spacing = 8f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        ContentSizeFitter fit = tag.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        Color tagTextColor = new Color32(4, 34, 43, 255);
        Image moveIcon = Icon("Selection Tag Icon", tag, "icon-move", tagTextColor, 0f, 0f, 24f);
        LayoutElement iconSize = moveIcon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = iconSize.preferredHeight = 24f;
        tagText = Label("Selection Tag Label", tag, "Joystick · 100%", 22, true, TextAnchor.MiddleLeft, tagTextColor);
        tagText.horizontalOverflow = HorizontalWrapMode.Overflow;
        return frame;
    }

    private static RectTransform BuildToolbar(RectTransform parent, out Text selectedLabel, out Slider sizeSlider,
                                              out Text sizeValue, out Button reset, out Button cancel,
                                              out Button save) {
        // Anchor atas-tengah. ControlLayoutEditor memindahkannya ke bawah kalau kontrol terpilih ada di atas.
        RectTransform toolbar = NewRect("Toolbar", parent);
        toolbar.anchorMin = toolbar.anchorMax = toolbar.pivot = new Vector2(0.5f, 1f);
        toolbar.anchoredPosition = new Vector2(0f, -24f);
        toolbar.sizeDelta = new Vector2(ToolbarWidth, ToolbarHeight);
        Shape(toolbar.gameObject, CardColor, cornerScale: 1.5f);
        AddOutline(toolbar, LineColor, cornerScale: 1.5f);

        // Baris 1: judul + aksi. SIMPAN satu-satunya tombol berwarna.
        IconTile("Toolbar Icon", toolbar, "icon-move", CyanTopColor, CyanBottomColor, 26f, 24f, 64f, 34f);
        Place(Label("Title", toolbar, "ATUR KONTROL", 32, true, TextAnchor.MiddleLeft, Color.white).rectTransform,
              108f, 22f, 520f, 40f);
        // Best fit: lebar huruf beda antar font/perangkat, jangan sampai turun ke baris kedua.
        Text hint = Label("Hint", toolbar, "Seret tombol untuk memindahkan, ketuk untuk memilih", 20, false,
                          TextAnchor.MiddleLeft, SubColor);
        Place(hint.rectTransform, 108f, 62f, 540f, 28f);
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        hint.verticalOverflow = VerticalWrapMode.Truncate; // best fit hanya mengecilkan kalau tinggi dibatasi
        hint.resizeTextForBestFit = true;
        hint.resizeTextMinSize = 14;
        hint.resizeTextMaxSize = 20;

        float right = ToolbarWidth - 26f;
        save = MenuButton("Save Button", toolbar, GreenTopColor, GreenBottomColor, GreenLipColor,
                          new Color(1f, 1f, 1f, 0.25f), 1.1f, out RectTransform saveFace);
        Place((RectTransform)save.transform, right - 210f, 18f, 210f, 82f);
        CenteredContent("Save", saveFace, "icon-check", "SIMPAN", 26, 30f, Color.white);
        cancel = GhostButton("Cancel Button", toolbar, null, "BATAL", 24, 0f);
        Place((RectTransform)cancel.transform, right - 210f - 14f - 140f, 20f, 140f, 72f);
        reset = GhostButton("Reset Button", toolbar, "icon-refresh", "RESET", 24, 28f);
        Place((RectTransform)reset.transform, right - 210f - 14f - 140f - 14f - 180f, 20f, 180f, 72f);

        // Baris 2: kontrol terpilih + UKURAN (− slider +) + persen.
        float barWidth = ToolbarWidth - 52f;
        RectTransform bar = Place(NewRect("Size Bar", toolbar), 26f, 118f, barWidth, 76f);
        Shape(bar.gameObject, SectionBottomColor, cornerScale: 1.1f).raycastTarget = false;

        RectTransform chip = Place(NewRect("Selected Chip", bar), 12f, 10f, 260f, 56f);
        Shape(chip.gameObject, new Color(CyanTopColor.r, CyanTopColor.g, CyanTopColor.b, 0.14f), cornerScale: 0.8f)
            .raycastTarget = false;
        AddOutline(chip, new Color(CyanTopColor.r, CyanTopColor.g, CyanTopColor.b, 0.5f), cornerScale: 0.8f);
        Shape(Place(NewRect("Selected Dot", chip), 18f, 22f, 12f, 12f).gameObject, CyanTopColor, "ui-circle")
            .raycastTarget = false;
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

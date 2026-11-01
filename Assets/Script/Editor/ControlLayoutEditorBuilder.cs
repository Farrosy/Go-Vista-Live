using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Membuat UI mode ATUR KONTROL di prefab Assets/MobilePrefabs/MobileUI.prefab, SEKALI saja:
///  - objek "Control Layout Editor" (overlay + bingkai pilihan + toolbar Ukuran/Reset/Batal/Simpan),
///  - komponen CustomizableControl di Fixed Joystick, JumpButton, SprintButton, InteractButton,
///  - CanvasScaler MobileUI diubah ke Scale With Screen Size 1920x1080 supaya ukuran tombol sama di semua HP.
///
/// Setelah dibuat, semuanya objek biasa di prefab: warna, teks, ukuran, posisi bebas diedit di Inspector,
/// dan builder ini TIDAK akan menimpanya lagi. Kalau ingin membuat ulang dari awal, pakai menu
/// "Go Vista > Buat Ulang UI Atur Kontrol" (perubahan manual di objek itu akan hilang).
///
/// Mau tambah kontrol lain yang bisa diatur? Cukup pasang komponen CustomizableControl di objeknya.
/// </summary>
[InitializeOnLoad]
public static class ControlLayoutEditorBuilder {
    private const string PrefabPath = "Assets/MobilePrefabs/MobileUI.prefab";
    private const string EditorObjectName = "Control Layout Editor";

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.14f, 0.97f);
    private static readonly Color CardColor = new Color(0.13f, 0.17f, 0.27f, 1f);
    private static readonly Color PrimaryColor = new Color(0.12f, 0.44f, 0.86f, 1f);
    private static readonly Color MutedColor = new Color(0.68f, 0.76f, 0.88f);
    private static readonly Color HighlightColor = new Color(1f, 0.8f, 0.2f, 1f);

    // Nama objek di MobileUI, id simpan, nama yang tampil.
    private static readonly (string objectName, string id, string label)[] Controls = {
        ("Fixed Joystick", "joystick", "Joystick"),
        ("JumpButton", "jump", "Tombol Lompat"),
        ("SprintButton", "sprint", "Tombol Lari"),
        ("InteractButton", "interact", "Tombol Interaksi"),
    };

    static ControlLayoutEditorBuilder() {
        EditorApplication.delayCall += AutoSetup;
    }

    private static void AutoSetup() {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset == null || asset.transform.Find(EditorObjectName) != null)
            return;
        Build(rebuild: false);
    }

    [MenuItem("Go Vista/Buat Ulang UI Atur Kontrol")]
    private static void RebuildMenu() {
        if (!EditorUtility.DisplayDialog("Buat Ulang UI Atur Kontrol",
                $"Objek \"{EditorObjectName}\" di MobileUI akan dihapus dan dibuat ulang. " +
                "Perubahan manual di objek itu akan hilang.", "Buat Ulang", "Batal"))
            return;
        Build(rebuild: true);
    }

    private static void Build(bool rebuild) {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            Transform existing = root.transform.Find(EditorObjectName);
            if (existing != null) {
                if (!rebuild) return;
                Object.DestroyImmediate(existing.gameObject);
            } else {
                UseScaleWithScreenSize(root);
            }

            CustomizableControl[] controls = AddCustomizableControls(root);
            CreateEditorObject(root, controls);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[ControlLayout] UI Atur Kontrol dibuat di {PrefabPath}. Edit bebas di prefab tersebut.");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UseScaleWithScreenSize(GameObject root) {
        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null || scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return;
        // Layout MobileUI dibuat di layar 1920x1080 dengan Constant Pixel Size (scale 1), jadi
        // di 1920x1080 tampilannya tetap sama, dan di layar lain ikut menyesuaikan.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static CustomizableControl[] AddCustomizableControls(GameObject root) {
        var result = new System.Collections.Generic.List<CustomizableControl>();
        foreach (var (objectName, id, label) in Controls) {
            Transform t = FindDeep(root.transform, objectName);
            if (t == null) {
                Debug.LogWarning($"[ControlLayout] \"{objectName}\" tidak ditemukan di MobileUI, dilewati.");
                continue;
            }
            var control = t.GetComponent<CustomizableControl>();
            if (control == null) {
                control = t.gameObject.AddComponent<CustomizableControl>();
                var so = new SerializedObject(control);
                so.FindProperty("id").stringValue = id;
                so.FindProperty("displayName").stringValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            result.Add(control);
        }
        return result.ToArray();
    }

    private static void CreateEditorObject(GameObject root, CustomizableControl[] controls) {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var resources = new DefaultControls.Resources {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };

        // Overlay full-screen: menangkap semua sentuhan selama mode atur kontrol.
        var editorGo = new GameObject(EditorObjectName, typeof(RectTransform), typeof(Image), typeof(ControlLayoutEditor));
        var editorRect = (RectTransform)editorGo.transform;
        editorRect.SetParent(root.transform, false);
        Stretch(editorRect);
        editorGo.GetComponent<Image>().color = DimColor;

        // Bingkai kuning di sekitar kontrol yang dipilih.
        var frame = new GameObject("Selection Frame", typeof(RectTransform), typeof(Image), typeof(Outline));
        var frameRect = (RectTransform)frame.transform;
        frameRect.SetParent(editorRect, false);
        var frameImage = frame.GetComponent<Image>();
        frameImage.color = new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, 0.18f);
        frameImage.raycastTarget = false;
        var outline = frame.GetComponent<Outline>();
        outline.effectColor = HighlightColor;
        outline.effectDistance = new Vector2(4f, -4f);

        // Toolbar.
        var toolbar = new GameObject("Toolbar", typeof(RectTransform), typeof(Image));
        var toolbarRect = (RectTransform)toolbar.transform;
        toolbarRect.SetParent(editorRect, false);
        toolbarRect.anchorMin = toolbarRect.anchorMax = toolbarRect.pivot = new Vector2(0.5f, 1f);
        toolbarRect.sizeDelta = new Vector2(1180f, 300f);
        toolbarRect.anchoredPosition = new Vector2(0f, -24f);
        toolbar.GetComponent<Image>().color = PanelColor;

        CreateText("Title", toolbarRect, font, "ATUR KONTROL", 40, FontStyle.Bold, TextAnchor.MiddleLeft,
                   Color.white, 32f, 24f, 560f, 56f);
        Text selectedLabel = CreateText("Selected Label", toolbarRect, font, "Joystick", 30, FontStyle.Bold,
                                        TextAnchor.MiddleRight, HighlightColor, 600f, 24f, 548f, 56f);
        CreateText("Hint", toolbarRect, font, "Geser tombol untuk memindahkan. Ketuk tombol lain untuk memilih.",
                   24, FontStyle.Normal, TextAnchor.MiddleLeft, MutedColor, 32f, 84f, 1116f, 40f);
        CreateText("Size Label", toolbarRect, font, "UKURAN", 28, FontStyle.Bold, TextAnchor.MiddleLeft,
                   MutedColor, 32f, 140f, 180f, 56f);

        GameObject sliderGo = DefaultControls.CreateSlider(resources);
        sliderGo.name = "Size Slider";
        sliderGo.transform.SetParent(toolbarRect, false);
        Place((RectTransform)sliderGo.transform, 220f, 148f, 720f, 40f);
        var slider = sliderGo.GetComponent<Slider>();
        slider.minValue = ControlLayoutSettings.MinScale;
        slider.maxValue = ControlLayoutSettings.MaxScale;
        slider.value = 1f;

        Text sizeValue = CreateText("Size Value", toolbarRect, font, "100%", 30, FontStyle.Bold,
                                    TextAnchor.MiddleRight, Color.white, 960f, 140f, 188f, 56f);

        Button reset = CreateButton("Reset Button", toolbarRect, resources, font, "RESET", CardColor, 32f);
        Button cancel = CreateButton("Cancel Button", toolbarRect, resources, font, "BATAL", CardColor, 414f);
        Button save = CreateButton("Save Button", toolbarRect, resources, font, "SIMPAN", PrimaryColor, 796f);

        var editor = editorGo.GetComponent<ControlLayoutEditor>();
        var so = new SerializedObject(editor);
        SerializedProperty list = so.FindProperty("controls");
        list.arraySize = controls.Length;
        for (int i = 0; i < controls.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = controls[i];
        so.FindProperty("selectionFrame").objectReferenceValue = frameRect;
        so.FindProperty("toolbar").objectReferenceValue = toolbarRect;
        so.FindProperty("selectedLabel").objectReferenceValue = selectedLabel;
        so.FindProperty("sizeSlider").objectReferenceValue = slider;
        so.FindProperty("sizeValue").objectReferenceValue = sizeValue;
        so.FindProperty("resetButton").objectReferenceValue = reset;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.FindProperty("saveButton").objectReferenceValue = save;
        so.FindProperty("interactButton").objectReferenceValue = root.GetComponentInChildren<InteractButtonUI>(true);
        so.ApplyModifiedPropertiesWithoutUndo();

        SetLayerRecursive(editorGo.transform, root.layer);
        editorRect.SetAsLastSibling();
        editorGo.SetActive(false);
    }

    private static Text CreateText(string name, RectTransform parent, Font font, string text, int size,
                                   FontStyle style, TextAnchor anchor, Color color,
                                   float x, float y, float width, float height) {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        Place(rect, x, y, width, height);
        var label = go.GetComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = anchor;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(string name, RectTransform parent, DefaultControls.Resources resources,
                                       Font font, string text, Color color, float x) {
        GameObject go = DefaultControls.CreateButton(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, x, 212f, 352f, 72f);
        go.GetComponent<Image>().color = color;
        var label = go.GetComponentInChildren<Text>(true);
        label.font = font;
        label.text = text;
        label.fontSize = 30;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        return go.GetComponent<Button>();
    }

    // Posisi dari pojok kiri atas parent (x ke kanan, y ke bawah), seperti Place() di LobyUISceneController.
    private static void Place(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Transform FindDeep(Transform parent, string name) {
        foreach (Transform child in parent) {
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void SetLayerRecursive(Transform t, int layer) {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursive(child, layer);
    }
}

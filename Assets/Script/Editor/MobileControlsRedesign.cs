using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Desain baru tombol layar (joystick, lari, lompat, interaksi) di Assets/MobilePrefabs/MobileUI.prefab.
/// Menu: Go Vista > Desain Baru > Kontrol Layar.
///
/// Sprite ada di Assets/Sprites/Controls (paket GoVista-Controller-Icons). Tombol dicari lewat komponennya
/// (Joystick, JumpButton, SprintButton, InteractButtonUI), bukan nama objek. Yang diubah hanya tampilan:
/// posisi/ukuran yang diatur pemain lewat ATUR KONTROL tidak berubah.
///  - Joystick : latar + kenop baru, berganti ke versi aktif saat disentuh (JoystickPressVisual)
///  - Lompat   : btn-jump, versi ditekan lewat Sprite Swap
///  - Lari     : btn-run, versi aktif saat sprint menyala (SprintButton.activeSprite)
///  - Interaksi: latar kosong btn-blank + ikon aksi PUTIH di atasnya (InteractionAction.hudIcon),
///               karena ikonnya berganti sesuai aksi (bicara, duduk, ...)
/// </summary>
public static class MobileControlsRedesign {
    private const string PrefabPath = "Assets/MobilePrefabs/MobileUI.prefab";
    private const string SpriteFolder = "Assets/Sprites/Controls/";
    // Ukuran ikon aksi relatif terhadap tombol interaksi (sama dengan ikon di btn-run / btn-jump).
    private const float InteractIconScale = 0.42f;

    [MenuItem("Go Vista/Desain Baru/Kontrol Layar")]
    private static void ApplyFromMenu() {
        if (!EditorUtility.DisplayDialog("Desain baru Kontrol Layar",
                "Tampilan joystick dan tombol lari / lompat / interaksi di MobileUI.prefab akan diganti. " +
                "Posisi dan ukuran tombol tidak berubah.", "Pasang", "Batal"))
            return;
        Debug.Log("[Desain Baru] Kontrol Layar:\n" + Apply());
    }

    public static string Apply() {
        ConfigureSpriteImport();
        var log = new System.Text.StringBuilder();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            log.AppendLine("Joystick: " + StyleJoystick(root));
            log.AppendLine("Lompat: " + StyleSpriteSwapButton(root.GetComponentInChildren<JumpButton>(true), "btn-jump"));
            log.AppendLine("Lari: " + StyleSprint(root.GetComponentInChildren<SprintButton>(true)));
            log.AppendLine("Interaksi: " + StyleInteract(root.GetComponentInChildren<InteractButtonUI>(true)));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }

        log.AppendLine("Aksi Talk: " + SetHudIcon("Assets/NPCAction/Talk.asset", "icon-chat"));
        log.AppendLine("Aksi Sit: " + SetHudIcon("Assets/NPCAction/Sit.asset", "icon-sit"));
        return log.ToString();
    }

    // ------------------------------------------------------------ Tombol

    private static string StyleJoystick(GameObject root) {
        Joystick joystick = root.GetComponentInChildren<Joystick>(true);
        if (joystick == null)
            return "Joystick tidak ditemukan, dilewati";

        var so = new SerializedObject(joystick);
        var backgroundRect = so.FindProperty("background").objectReferenceValue as RectTransform;
        var handleRect = so.FindProperty("handle").objectReferenceValue as RectTransform;
        Image background = backgroundRect ? backgroundRect.GetComponent<Image>() : joystick.GetComponent<Image>();
        Image knob = handleRect ? handleRect.GetComponent<Image>() : null;
        if (background == null || knob == null)
            return "Image latar/kenop tidak ditemukan, dilewati";

        SetImage(background, "joystick-bg");
        SetImage(knob, "joystick-knob");
        var visual = joystick.GetComponent<JoystickPressVisual>();
        if (visual == null)
            visual = joystick.gameObject.AddComponent<JoystickPressVisual>();
        visual.Init(background, Load("joystick-bg-active"), knob, Load("joystick-knob-pressed"));
        EditorUtility.SetDirty(visual);
        return "ok";
    }

    private static string StyleSpriteSwapButton(Component control, string spriteName) {
        if (control == null)
            return "tidak ditemukan, dilewati";
        Image image = control.GetComponent<Image>();
        Button button = control.GetComponent<Button>();
        if (image == null || button == null)
            return "Image/Button tidak ada, dilewati";

        SetImage(image, spriteName);
        button.transition = Selectable.Transition.SpriteSwap;
        button.targetGraphic = image;
        SpriteState state = button.spriteState;
        state.pressedSprite = Load(spriteName + "-pressed");
        state.highlightedSprite = null;
        state.selectedSprite = null;
        button.spriteState = state;
        HideOldIcons(control.transform);
        return "ok";
    }

    private static string StyleSprint(SprintButton sprint) {
        if (sprint == null)
            return "tidak ditemukan, dilewati";
        Image image = sprint.GetComponent<Image>();
        SetImage(image, "btn-run");

        var so = new SerializedObject(sprint);
        so.FindProperty("backgroundImage").objectReferenceValue = image;
        so.FindProperty("activeSprite").objectReferenceValue = Load("btn-run-pressed");
        // Status aktif ditunjukkan sprite (cincin cyan), jadi warnanya tidak perlu digelapkan lagi.
        SerializedProperty colors = so.FindProperty("colors");
        colors.FindPropertyRelative("m_NormalColor").colorValue = Color.white;
        colors.FindPropertyRelative("m_HighlightedColor").colorValue = Color.white;
        colors.FindPropertyRelative("m_PressedColor").colorValue = Color.white;
        so.ApplyModifiedPropertiesWithoutUndo();
        HideOldIcons(sprint.transform);
        return "ok";
    }

    private static string StyleInteract(InteractButtonUI interact) {
        if (interact == null)
            return "tidak ditemukan, dilewati";
        string result = StyleSpriteSwapButton(interact, "btn-blank");
        if (result != "ok")
            return result;

        // Ikon aksi tetap dipakai (berganti sesuai aksi), jadi diaktifkan lagi setelah HideOldIcons.
        var so = new SerializedObject(interact);
        var icon = so.FindProperty("iconImage").objectReferenceValue as Image;
        if (icon == null)
            return "ok (tanpa ikon aksi)";
        icon.gameObject.SetActive(true);
        icon.sprite = Load("icon-chat");
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform rect = icon.rectTransform;
        rect.anchorMin = new Vector2(0.5f - InteractIconScale / 2f, 0.5f - InteractIconScale / 2f);
        rect.anchorMax = new Vector2(0.5f + InteractIconScale / 2f, 0.5f + InteractIconScale / 2f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return "ok";
    }

    // ------------------------------------------------------------ Helper

    private static void SetImage(Image image, string spriteName) {
        image.sprite = Load(spriteName);
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = true;
    }

    /// Ikon lama (child Image) dimatikan: ikon baru sudah menyatu di sprite tombol.
    private static void HideOldIcons(Transform button) {
        foreach (Image child in button.GetComponentsInChildren<Image>(true)) {
            if (child.transform != button)
                child.gameObject.SetActive(false);
        }
    }

    private static string SetHudIcon(string actionPath, string spriteName) {
        var action = AssetDatabase.LoadAssetAtPath<InteractionAction>(actionPath);
        if (action == null)
            return "tidak ditemukan, dilewati";
        action.hudIcon = Load(spriteName);
        EditorUtility.SetDirty(action);
        AssetDatabase.SaveAssetIfDirty(action);
        return "ok";
    }

    private static Sprite Load(string spriteName) {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + spriteName + ".png");
        if (sprite == null)
            Debug.LogWarning($"[Desain Baru] Sprite {SpriteFolder}{spriteName}.png tidak ditemukan.");
        return sprite;
    }

    private static void ConfigureSpriteImport() {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder.TrimEnd('/') })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                continue;
            if (importer.textureType == TextureImporterType.Sprite && importer.alphaIsTransparency &&
                importer.spriteImportMode == SpriteImportMode.Single)
                continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}

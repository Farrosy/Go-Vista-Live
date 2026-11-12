using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.U2D;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Membangun HUD gameplay dan layar mati:
///  - Assets/Prefab/GameHudCanvas.prefab, dipasang di MainMultiplayerScene.
///  - Font Asset Fredoka Medium/Bold (atlas statis) dan material underlay.
///  - Komponen WaterSurface pada objek "water" di WorldScene.
/// Ukuran mengikuti README di Assets/GoVista_HUD_Assets (mockup x 1,417 pada referensi 1920x1080).
/// </summary>
public static partial class GameHudBuilder {
    public const string PrefabPath = "Assets/Prefab/GameHudCanvas.prefab";
    private const string CanvasName = "GameHudCanvas";
    private const string HostScenePath = "Assets/Scenes/MainMultiplayerScene.unity";
    private const string WaterScenePath = "Assets/Scenes/WorldScene.unity";
    private const string WaterObjectName = "water";
    private const string AssetRoot = "Assets/GoVista_HUD_Assets/";
    private const string FontRoot = "Assets/Fonts/";
    private const string AtlasPath = AssetRoot + "GoVistaHud.spriteatlasv2";
    private const string UnpackedIcon = "icon_coin_G_large.png"; // besar dan belum dipakai HUD

    // Di atas kontrol mobile (20), di bawah menu pause (51) dan chat (52).
    private const int HudSortingOrder = 25;
    // Di atas chat (52), di bawah login (100) dan transisi venue.
    private const int DeathSortingOrder = 55;
    private const float SlicedMultiplier = 1.41f;
    private const int BubbleCount = 10;
    private const int SpeedLineCount = 18;
    private const int DustCount = 5;
    private const string DeathContentFolder = AssetRoot + "DeathScreens";

    private static readonly Color TextBlue = new Color32(191, 235, 255, 255);
    private static readonly Color TextPaleBlue = new Color32(227, 244, 255, 255);
    private static readonly Color TextCardAmount = new Color32(255, 179, 190, 255);
    private static readonly Color TextPrimaryButton = new Color32(90, 49, 0, 255);
    private static readonly Color BackdropColor = new Color32(13, 42, 92, 214);
    private static readonly Color TitleUnderlay = new Color32(11, 47, 102, 255);
    private static readonly Color TextUnderlay = new Color32(10, 20, 50, 217);
    private static readonly Color FallSubtitle = new Color32(255, 217, 194, 255);
    private static readonly Color FallTip = new Color32(255, 237, 226, 255);
    private static readonly Color FallBackdrop = new Color32(30, 15, 8, 228);
    private static readonly Color FallTitleUnderlay = new Color32(107, 36, 8, 255);
    private static readonly Color CarSubtitle = new Color32(220, 211, 255, 255);
    private static readonly Color CarTip = new Color32(240, 236, 255, 255);
    private static readonly Color CarBackdrop = new Color32(20, 12, 48, 205);
    private static readonly Color CarTitleUnderlay = new Color32(42, 26, 102, 255);

    private sealed class Fonts {
        public TMP_FontAsset Medium, SemiBold, Bold;
        public Material BoldUnderlay, TitleUnderlay, FallTitleUnderlay, CarTitleUnderlay;
    }

    [MenuItem("Go Vista/HUD/Buat ulang GameHudCanvas (prefab + scene)")]
    public static void BuildAll() {
        Fonts fonts = EnsureFonts();
        EnsureSpriteAtlas();
        EnsureQuestSpritesInAtlas();
        GameObject prefab = BuildPrefab(fonts);
        InstallInHostScene(prefab);
        InstallWaterSurface();
        AssetDatabase.SaveAssets();
        Debug.Log("[HUD] GameHudCanvas selesai dibuat dan dipasang.");
    }

    // ------------------------------------------------------------ Font

    private static Fonts EnsureFonts() {
        var fonts = new Fonts {
            Medium = EnsureFontAsset("Fredoka-Medium"),
            SemiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRoot + "Fredoka-SemiBold SDF.asset"),
            Bold = EnsureFontAsset("Fredoka-Bold"),
        };
        fonts.BoldUnderlay = EnsureUnderlayMaterial(fonts.Bold, "Fredoka-Bold SDF Underlay", TextUnderlay, -0.5f);
        fonts.TitleUnderlay = EnsureUnderlayMaterial(fonts.Bold, "Fredoka-Bold SDF Title", TitleUnderlay, -0.45f);
        fonts.FallTitleUnderlay = EnsureUnderlayMaterial(fonts.Bold, "Fredoka-Bold SDF Title Fall", FallTitleUnderlay, -0.45f);
        fonts.CarTitleUnderlay = EnsureUnderlayMaterial(fonts.Bold, "Fredoka-Bold SDF Title Car", CarTitleUnderlay, -0.45f);
        return fonts;
    }

    /// Atlas statis supaya file font tidak berubah sendiri saat game dijalankan di Editor.
    private static TMP_FontAsset EnsureFontAsset(string fontName) {
        string assetPath = FontRoot + fontName + " SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontRoot + fontName + ".ttf");
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
        asset.name = fontName + " SDF";
        AssetDatabase.CreateAsset(asset, assetPath);
        asset.atlasTexture.name = asset.name + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);

        var characters = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++) characters.Append(c);
        characters.Append('−').Append('…');
        if (!asset.TryAddCharacters(characters.ToString(), out string missing))
            Debug.LogWarning($"[HUD] Karakter tidak ada di {fontName}: {missing}");
        asset.atlasPopulationMode = AtlasPopulationMode.Static;

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static Material EnsureUnderlayMaterial(TMP_FontAsset font, string materialName, Color color, float offsetY) {
        string path = FontRoot + materialName + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) {
            material = new Material(font.material) { name = materialName };
            AssetDatabase.CreateAsset(material, path);
        }
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, color);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, offsetY);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ------------------------------------------------------------ Sprite Atlas

    /// Menggabungkan sprite HUD supaya draw call di HP rendah. Vignette layar penuh sengaja di luar atlas.
    private static void EnsureSpriteAtlas() {
        if (File.Exists(AtlasPath)) return;

        var packables = new List<Object>();
        foreach (string folder in new[] { "Badges", "Bars", "Panels", "Buttons" })
            packables.Add(AssetDatabase.LoadAssetAtPath<DefaultAsset>(AssetRoot + folder));
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { AssetRoot + "Icons" })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(UnpackedIcon)) packables.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }
        packables.Add(Load("FX/fx_bubble.png"));

        var atlas = new SpriteAtlasAsset();
        atlas.Add(packables.ToArray());
        SpriteAtlasAsset.Save(atlas, AtlasPath);
        AssetDatabase.ImportAsset(AtlasPath);

        var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
        // Tanpa tight packing dan rotasi, karena sprite Sliced harus tetap berbentuk persegi.
        importer.packingSettings = new SpriteAtlasPackingSettings {
            blockOffset = 1, padding = 4, enableRotation = false, enableTightPacking = false, enableAlphaDilation = false,
        };
        importer.textureSettings = new SpriteAtlasTextureSettings {
            readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear,
        };
        importer.SaveAndReimport();
        EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2Build;
    }

    // ------------------------------------------------------------ Prefab

    private static GameObject BuildPrefab(Fonts fonts) {
        var root = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        try {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = HudSortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Sama dengan canvas lain di game (0,5), supaya posisi HUD tetap sejajar dengan chat dan pause.
            scaler.matchWidthOrHeight = 0.5f;

            var vitals = root.AddComponent<LocalPlayerVitals>();
            Image underwater = FullScreenImage("Underwater Overlay", root.transform, "FX/fx_vignette_water.png");
            Image vignette = FullScreenImage("Red Vignette", root.transform, "FX/fx_vignette_red.png");
            vignette.type = Image.Type.Sliced;
            vignette.fillCenter = false;
            vignette.pixelsPerUnitMultiplier = SlicedMultiplier;
            vignette.enabled = false;
            underwater.enabled = false;
            FallFxView fallFx = BuildFallFx(root.transform, vitals);

            RectTransform safeArea = Stretch(NewRect("HUD Safe Area", root.transform));
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform vitalsArea = Stretch(NewRect("Vitals Group", safeArea));
            var vitalsGroup = vitalsArea.gameObject.AddComponent<CanvasGroup>();
            vitalsGroup.interactable = false;
            vitalsGroup.blocksRaycasts = false;

            (HealthArcView health, OxygenArcView oxygen, CanvasGroup minimapGroup) = BuildMinimap(vitalsArea, fonts);
            BuildQuestWaypoint((RectTransform)minimapGroup.transform, fonts);
            Canvas coinCanvas = BuildCoin(safeArea, fonts);
            DeathScreenView death = BuildDeathScreen(root.transform, fonts);
            BuildQuest(root.transform, safeArea, fonts);
            BuildQuestToast(safeArea, fonts);

            var hud = root.AddComponent<GameHudView>();
            Bind(hud, ("vitals", vitals), ("health", health), ("oxygen", oxygen), ("rootCanvas", canvas),
                ("vitalsGroup", vitalsGroup), ("minimapGroup", minimapGroup), ("coinCanvas", coinCanvas), ("redVignette", vignette),
                ("underwaterOverlay", underwater));
            var flow = root.AddComponent<DeathFlowController>();
            Bind(flow, ("vitals", vitals), ("screen", death), ("hud", hud), ("fallFx", fallFx));

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        } finally {
            Object.DestroyImmediate(root);
        }
    }

    /// Minimap di kiri atas (sesuai storyboard) dengan busur HP di kiri dan O₂ di kanan.
    /// Isi peta sengaja hanya placeholder: "Map Mask/Map Content" adalah tempat tim minimap memasang petanya.
    private static (HealthArcView, OxygenArcView, CanvasGroup) BuildMinimap(RectTransform parent, Fonts fonts) {
        const string folder = "Minimap/";
        RectTransform group = TopLeft(NewRect("Minimap", parent), HudLayout.MinimapLeft, HudLayout.MinimapTop,
            HudLayout.MinimapSize, HudLayout.MinimapSize);
        var minimapGroup = group.gameObject.AddComponent<CanvasGroup>();
        minimapGroup.interactable = false;
        minimapGroup.blocksRaycasts = false;

        Stretch(PlainImage("Backdrop", group, folder + "minimap_backdrop.png").rectTransform);
        Image mask = PlainImage("Map Mask", group, folder + "minimap_mask.png");
        Stretch(mask.rectTransform);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Stretch(PlainImage("Map Content", mask.rectTransform, folder + "minimap_placeholder.png").rectTransform);

        RectTransform healthRect = Stretch(NewRect("Health Arc", group));
        Stretch(PlainImage("Frame", healthRect, folder + "minimap_hp_frame.png").rectTransform);
        Image trail = PlainImage("Damage Trail", healthRect, folder + "minimap_hp_fill.png");
        trail.color = new Color(1f, 1f, 1f, 0.4f);
        Image healthFill = PlainImage("Fill", healthRect, folder + "minimap_hp_fill.png");
        foreach (Image image in new[] { trail, healthFill }) {
            Stretch(image.rectTransform);
            MinimapArc.Setup(image, clockwise: true);
        }
        var health = healthRect.gameObject.AddComponent<HealthArcView>();
        Bind(health, ("fill", healthFill), ("trail", trail));

        RectTransform oxygenRect = Stretch(NewRect("Oxygen Arc", group));
        var oxygenGroup = oxygenRect.gameObject.AddComponent<CanvasGroup>();
        Stretch(PlainImage("Frame", oxygenRect, folder + "minimap_oxygen_frame.png").rectTransform);
        Image oxygenFill = PlainImage("Fill", oxygenRect, folder + "minimap_oxygen_fill.png");
        Stretch(oxygenFill.rectTransform);
        MinimapArc.Setup(oxygenFill, clockwise: false);
        // Belum ada sprite busur O₂ merah: pakai busur HP yang dicerminkan ke kanan.
        Image dangerFill = PlainImage("Danger Fill", oxygenRect, folder + "minimap_hp_fill.png");
        Stretch(dangerFill.rectTransform);
        dangerFill.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        MinimapArc.Setup(dangerFill, clockwise: true);
        dangerFill.gameObject.SetActive(false);

        (Image chip, TextMeshProUGUI chipLabel) = BuildOxygenChip(parent, fonts);
        var oxygen = oxygenRect.gameObject.AddComponent<OxygenArcView>();
        Bind(oxygen, ("arcGroup", oxygenGroup), ("arcRect", oxygenRect), ("fill", oxygenFill),
            ("dangerFill", dangerFill), ("chip", chip), ("chipWarning", Load("Panels/chip_warning_yellow.png")),
            ("chipDanger", Load("Panels/chip_warning_red.png")), ("chipLabel", chipLabel));
        return (health, oxygen, minimapGroup);
    }

    /// Chip "Segera kembali ke darat!" di tengah atas, supaya tetap terlihat walau minimap tertutup chat.
    private static (Image, TextMeshProUGUI) BuildOxygenChip(RectTransform parent, Fonts fonts) {
        const float width = 372f;
        Image chip = SlicedImage("Oxygen Warning Chip", parent, "Panels/chip_warning_yellow.png");
        RectTransform rect = chip.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -30f);
        rect.sizeDelta = new Vector2(width, SpriteHeight(chip));
        Image arrow = PlainImage("Arrow", rect, "Icons/icon_arrow_up.png");
        TopLeft(arrow.rectTransform, 20f, 11f, 24f, 24f);
        TextMeshProUGUI label = Text("Label", rect, fonts.Bold, "Segera kembali ke darat!", 23f, Color.white,
            TextAlignmentOptions.Midline);
        TopLeft(label.rectTransform, 44f, 3f, 320f, 40f);
        chip.gameObject.SetActive(false);
        return (chip, label);
    }

    private static Canvas BuildCoin(RectTransform parent, Fonts fonts) {
        const float pillWidth = 240f;
        RectTransform group = NewRect("Coin", parent);
        group.anchorMin = group.anchorMax = group.pivot = new Vector2(1f, 1f);
        group.anchoredPosition = new Vector2(-193f, -34f);
        group.sizeDelta = new Vector2(pillWidth + 37f, 130f);
        var canvas = group.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = HudSortingOrder;

        Image pill = SlicedImage("Pill", group, "Panels/pill_money.png");
        TopLeft(pill.rectTransform, 37f, 3f, pillWidth, SpriteHeight(pill));
        Image coin = PlainImage("Coin Icon", group, "Icons/icon_coin_G.png");
        TopLeft(coin.rectTransform, 0f, 0f, 74f, 80f);
        TextMeshProUGUI balance = Text("Balance", group, fonts.Bold, "100", 34f, Color.white, TextAlignmentOptions.MidlineRight);
        TopLeft(balance.rectTransform, 80f, 8f, pillWidth - 70f, 50f);

        Image chip = SlicedImage("Minus Chip", group, "Panels/chip_minus.png");
        TopLeft(chip.rectTransform, 140f, 74f, 110f, SpriteHeight(chip));
        var chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
        TextMeshProUGUI chipLabel = Text("Label", chip.rectTransform, fonts.Bold, "−20", 23f, Color.white, TextAlignmentOptions.Midline);
        Stretch(chipLabel.rectTransform, 0f, 0f, 0f, 6f);

        var view = group.gameObject.AddComponent<CoinPillView>();
        Bind(view, ("balanceLabel", balance), ("minusChip", chipGroup), ("minusChipLabel", chipLabel));
        return canvas;
    }

    private static DeathScreenView BuildDeathScreen(Transform parent, Fonts fonts) {
        RectTransform root = Stretch(NewRect("Death Screen", parent));
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = DeathSortingOrder;
        root.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform backdropRect = Stretch(NewRect("Backdrop", root));
        var backdrop = backdropRect.gameObject.AddComponent<CanvasGroup>();
        Image backdropFill = backdropRect.gameObject.AddComponent<Image>();
        backdropFill.color = BackdropColor;
        backdropFill.raycastTarget = true; // menahan sentuhan ke kontrol di belakang layar mati
        Image backdropVignette = FullScreenImage("Vignette", backdropRect, "FX/fx_vignette_water.png");
        GameObject bubblesRoot = BuildBubbles(backdropRect);

        RectTransform badgeRect = Center(NewRect("Badge", root), 0f, 336f, 180f, 180f);
        var badge = badgeRect.gameObject.AddComponent<CanvasGroup>();
        Image circle = PlainImage("Circle", badgeRect, "Badges/badge_drowned.png");
        Stretch(circle.rectTransform);
        Image icon = PlainImage("Icon", badgeRect, "Icons/icon_drowned.png");
        Center(icon.rectTransform, 0f, 2f, 88f, 88f);

        TextMeshProUGUI titleText = Text("Title", root, fonts.Bold, "TENGGELAM!", 159f, Color.white, TextAlignmentOptions.Center);
        titleText.fontSharedMaterial = fonts.TitleUnderlay;
        titleText.characterSpacing = 4f;
        Center(titleText.rectTransform, 0f, 155f, 1500f, 200f);
        CanvasGroup title = titleText.gameObject.AddComponent<CanvasGroup>();

        CanvasGroup subtitle = TextGroup("Subtitle", root, fonts.SemiBold, "Oksigen kamu habis di air.", 42f, TextBlue, 25f, 60f);
        CanvasGroup tip = TextGroup("Tip", root, fonts.Medium,
            "Tips: begitu bar O<sub>2</sub> menguning, segera kembali ke darat.", 28f, TextPaleBlue, -24f, 44f);

        Image cardImage = SlicedImage("Coin Card", root, "Panels/panel_card.png");
        Center(cardImage.rectTransform, 0f, -138f, 283f, 108f);
        var card = cardImage.gameObject.AddComponent<CanvasGroup>();
        TextMeshProUGUI cardLabel = Text("Label", cardImage.rectTransform, fonts.Medium, "Koin hilang", 21f, TextBlue, TextAlignmentOptions.Midline);
        Center(cardLabel.rectTransform, 0f, 24f, 260f, 30f);
        TextMeshProUGUI cardAmount = Text("Amount", cardImage.rectTransform, fonts.Bold, "−50", 37f, TextCardAmount, TextAlignmentOptions.Midline);
        Center(cardAmount.rectTransform, 0f, -17f, 260f, 50f);

        RectTransform buttonsRect = Center(NewRect("Buttons", root), 0f, -283f, 800f, 98f);
        var buttons = buttonsRect.gameObject.AddComponent<CanvasGroup>();
        Button revive = BuildButton("Revive Button", buttonsRect, fonts.Bold, "Bangkit di Darat", 34f, TextPrimaryButton,
            "Buttons/btn_primary", "Icons/icon_respawn.png", -202f, 396f, out TextMeshProUGUI reviveLabel);
        Button lobby = BuildButton("Lobby Button", buttonsRect, fonts.SemiBold, "Kembali ke Lobby", 31f, Color.white,
            "Buttons/btn_secondary", "Icons/icon_home.png", 202f, 380f, out TextMeshProUGUI lobbyLabel);

        TextMeshProUGUI countdownText = Text("Countdown", root, fonts.Medium, "Otomatis bangkit dalam <color=#FFD166>8</color> detik",
            26f, TextPaleBlue, TextAlignmentOptions.Midline);
        Center(countdownText.rectTransform, 0f, -372f, 900f, 40f);
        CanvasGroup countdown = countdownText.gameObject.AddComponent<CanvasGroup>();
        CanvasGroup info = TextGroup("Respawn Info", root, fonts.Medium, string.Empty, 24f, TextPaleBlue, -370f, 40f);

        var view = root.gameObject.AddComponent<DeathScreenView>();
        BindArray(view, "contents", EnsureDeathContents(fonts));
        Bind(view, ("backdrop", backdrop), ("backdropFill", backdropFill), ("backdropVignette", backdropVignette),
            ("bubblesRoot", bubblesRoot), ("badge", badge), ("badgeCircle", circle), ("badgeIcon", icon),
            ("title", title), ("titleText", titleText), ("subtitle", subtitle),
            ("subtitleText", subtitle.GetComponent<TextMeshProUGUI>()), ("tip", tip),
            ("tipText", tip.GetComponent<TextMeshProUGUI>()), ("card", card), ("cardImage", cardImage),
            ("cardLabel", cardLabel), ("cardAmount", cardAmount), ("buttons", buttons), ("reviveButton", revive),
            ("reviveGroup", revive.gameObject.AddComponent<CanvasGroup>()), ("reviveLabel", reviveLabel),
            ("lobbyButton", lobby), ("lobbyGroup", lobby.gameObject.AddComponent<CanvasGroup>()),
            ("lobbyLabel", lobbyLabel), ("info", info), ("infoLabel", info.GetComponent<TextMeshProUGUI>()),
            ("countdown", countdown), ("countdownLabel", countdownText));
        return view;
    }

    private static DeathScreenContent[] EnsureDeathContents(Fonts fonts) {
        if (!AssetDatabase.IsValidFolder(DeathContentFolder))
            AssetDatabase.CreateFolder(AssetRoot.TrimEnd('/'), Path.GetFileName(DeathContentFolder));

        DeathScreenContent drowned = EnsureDeathContent("DeathScreen_Drowned", content => {
            content.cause = DeathCause.Drowned;
            content.title = "TENGGELAM!";
            content.titleSize = 159f;
            content.titleSpacing = 4f;
            content.titleMaterial = fonts.TitleUnderlay;
            content.subtitle = "Oksigen kamu habis di air.";
            content.subtitleColor = TextBlue;
            content.tip = "Tips: begitu bar O<sub>2</sub> menguning, segera kembali ke darat.";
            content.tipColor = TextPaleBlue;
            content.cardLabelColor = TextBlue;
            content.reviveLabel = "Bangkit di Darat";
            content.respawnInfo = string.Empty;
            content.countdownColor = TextPaleBlue;
            content.badge = Load("Badges/badge_drowned.png");
            content.icon = Load("Icons/icon_drowned.png");
            content.iconSize = 88f;
            content.card = Load("Panels/panel_card.png");
            content.lobbyButton = Load("Buttons/btn_secondary.png");
            content.lobbyButtonPressed = Load("Buttons/btn_secondary_pressed.png");
            content.lobbyButtonDisabled = Load("Buttons/btn_secondary_disabled.png");
            content.backdropColor = BackdropColor;
            content.vignette = Load("FX/fx_vignette_water.png");
            content.showBubbles = true;
            content.intro = DeathScreenContent.IntroStyle.Slam;
            content.impactEffects = false;
            content.hideAvatarAfterSeconds = 0.5f;
            content.hideHudAfterSeconds = 0f;
        });

        DeathScreenContent fall = EnsureDeathContent("DeathScreen_Fall", content => {
            content.cause = DeathCause.Fall;
            content.title = "JATUH!";
            content.titleSize = 170f;
            content.titleSpacing = 6f;
            content.titleMaterial = fonts.FallTitleUnderlay;
            content.subtitle = "Kamu jatuh dari tempat yang terlalu tinggi.";
            content.subtitleColor = FallSubtitle;
            content.tip = "Tips: turun lewat tangga atau pijakan yang lebih rendah, jangan langsung lompat.";
            content.tipColor = FallTip;
            content.cardLabelColor = FallSubtitle;
            content.reviveLabel = "Bangkit Lagi";
            content.respawnInfo = "Muncul di titik awal dengan HP penuh";
            content.respawnInfoColor = FallSubtitle;
            content.countdownColor = FallTip;
            content.badge = Load("Badges/badge_fall.png");
            content.icon = Load("Icons/icon_fall.png");
            content.iconSize = 72f;
            content.card = Load("Panels/panel_card_fall.png");
            content.lobbyButton = Load("Buttons/btn_secondary_fall.png");
            content.lobbyButtonPressed = Load("Buttons/btn_secondary_fall_pressed.png");
            content.lobbyButtonDisabled = Load("Buttons/btn_secondary_fall.png");
            content.backdropColor = FallBackdrop;
            content.vignette = Load("FX/fx_vignette_fall.png");
            content.showBubbles = false;
            content.intro = DeathScreenContent.IntroStyle.Drop;
            content.impactEffects = true;
            content.hideAvatarAfterSeconds = 1f;
            content.hideHudAfterSeconds = 1.2f;
        });

        DeathScreenContent car = EnsureDeathContent("DeathScreen_Car", content => {
            content.cause = DeathCause.Car;
            content.title = "TERTABRAK!";
            content.titleSize = 159f;
            content.titleSpacing = 4f;
            content.titleMaterial = fonts.CarTitleUnderlay;
            content.subtitle = "Kamu tertabrak mobil yang sedang melaju.";
            content.subtitleColor = CarSubtitle;
            content.tip = "Tips: lihat kiri dan kanan sebelum menyeberang jalan.";
            content.tipColor = CarTip;
            content.cardLabelColor = CarSubtitle;
            content.reviveLabel = "Bangkit Lagi";
            content.respawnInfo = "Muncul di titik awal dengan HP penuh";
            content.respawnInfoColor = CarSubtitle;
            content.countdownColor = CarTip;
            content.badge = Load("Badges/badge_car.png");
            content.icon = Load("Icons/icon_car_hit.png");
            content.iconSize = 88f;
            content.card = Load("Panels/panel_card_car.png");
            content.lobbyButton = Load("Buttons/btn_secondary_car.png");
            content.lobbyButtonPressed = Load("Buttons/btn_secondary_car_pressed.png");
            content.lobbyButtonDisabled = Load("Buttons/btn_secondary_car.png");
            content.backdropColor = CarBackdrop;
            content.vignette = Load("FX/fx_vignette_car.png");
            content.showBubbles = false;
            content.intro = DeathScreenContent.IntroStyle.Drop;
            content.impactEffects = true;
            content.hideAvatarAfterSeconds = 1f;
            content.hideHudAfterSeconds = 1.2f;
        });

        return new[] { drowned, fall, car };
    }

    private static DeathScreenContent EnsureDeathContent(string assetName, System.Action<DeathScreenContent> fill) {
        string path = $"{DeathContentFolder}/{assetName}.asset";
        var content = AssetDatabase.LoadAssetAtPath<DeathScreenContent>(path);
        if (content == null) {
            content = ScriptableObject.CreateInstance<DeathScreenContent>();
            AssetDatabase.CreateAsset(content, path);
        }
        fill(content);
        EditorUtility.SetDirty(content);
        return content;
    }

    private static FallFxView BuildFallFx(Transform parent, LocalPlayerVitals vitals) {
        RectTransform area = Stretch(NewRect("Fall FX", parent));
        area.gameObject.AddComponent<Canvas>(); // kanvas sendiri supaya efek bergerak tidak membangun ulang HUD

        RectTransform linesRect = Stretch(NewRect("Speed Lines", area));
        var speedLines = linesRect.gameObject.AddComponent<CanvasGroup>();
        speedLines.interactable = false;
        speedLines.blocksRaycasts = false;
        var lines = new RectTransform[SpeedLineCount];
        for (int i = 0; i < SpeedLineCount; i++) {
            Image line = PlainImage($"Line {i + 1}", linesRect, "FX/fx_speed_line.png");
            line.preserveAspect = false;
            lines[i] = Center(line.rectTransform, 0f, 0f, 10f, 400f);
        }

        Image ring = PlainImage("Impact Ring", area, "FX/fx_impact_ring.png");
        Center(ring.rectTransform, 0f, 0f, 510f, 156f);
        var dust = new Image[DustCount];
        for (int i = 0; i < DustCount; i++) {
            dust[i] = PlainImage($"Dust {i + 1}", area, "FX/fx_dust.png");
            Center(dust[i].rectTransform, 0f, 0f, 170f, 79f);
        }
        Image flash = FullScreenImage("Flash", area, "FX/fx_flash.png");

        var view = area.gameObject.AddComponent<FallFxView>();
        Bind(view, ("vitals", vitals), ("area", area), ("speedLines", speedLines), ("flash", flash), ("ring", ring));
        BindArray(view, "lines", lines);
        BindArray(view, "dust", dust);
        return view;
    }

    private static GameObject BuildBubbles(RectTransform parent) {
        RectTransform area = Stretch(NewRect("Bubbles", parent));
        var bubbles = new RectTransform[BubbleCount];
        for (int i = 0; i < BubbleCount; i++) {
            Image bubble = PlainImage($"Bubble {i + 1}", area, "FX/fx_bubble.png");
            bubbles[i] = Center(bubble.rectTransform, 0f, 0f, 48f, 48f);
        }
        var view = area.gameObject.AddComponent<DeathBubbles>();
        BindArray(view, "bubbles", bubbles);
        return area.gameObject;
    }

    private static Button BuildButton(string name, RectTransform parent, TMP_FontAsset font, string text, float size,
        Color textColor, string spriteBase, string iconPath, float centerX, float width, out TextMeshProUGUI label) {
        Image image = SlicedImage(name, parent, spriteBase + ".png");
        image.raycastTarget = true;
        Center(image.rectTransform, centerX, 0f, width, SpriteHeight(image));
        var button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState {
            pressedSprite = Load(spriteBase + "_pressed.png"),
            disabledSprite = Load(spriteBase + "_disabled.png"),
        };
        Image icon = PlainImage("Icon", image.rectTransform, iconPath);
        icon.color = textColor;
        Center(icon.rectTransform, -width * 0.5f + 44f, 3f, 36f, 36f);
        label = Text("Label", image.rectTransform, font, text, size, textColor, TextAlignmentOptions.Midline);
        Stretch(label.rectTransform, 56f, 0f, 16f, 7f);
        return button;
    }

    // ------------------------------------------------------------ Pasang ke scene

    private static void InstallInHostScene(GameObject prefab) {
        Scene scene = OpenSceneIfNeeded(HostScenePath, out bool opened);
        foreach (GameObject rootObject in scene.GetRootGameObjects())
            if (rootObject.name == CanvasName) Object.DestroyImmediate(rootObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = CanvasName;
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    private static void InstallWaterSurface() {
        Scene scene = OpenSceneIfNeeded(WaterScenePath, out bool opened);
        GameObject water = null;
        foreach (GameObject rootObject in scene.GetRootGameObjects())
            if (rootObject.name == WaterObjectName) water = rootObject;
        if (water == null) {
            Debug.LogError($"[HUD] Objek '{WaterObjectName}' tidak ada di {WaterScenePath}.");
        } else if (water.GetComponent<WaterSurface>() == null) {
            water.AddComponent<WaterSurface>();
            EditorSceneManager.SaveScene(scene);
        }
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    private static Scene OpenSceneIfNeeded(string path, out bool opened) {
        Scene scene = SceneManager.GetSceneByPath(path);
        opened = !scene.isLoaded;
        return opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
    }

    // ------------------------------------------------------------ Pembantu

    private static Sprite Load(string relativePath) {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetRoot + relativePath);
        if (sprite == null) Debug.LogError($"[HUD] Sprite tidak ditemukan: {relativePath}");
        return sprite;
    }

    private static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    private static RectTransform TopLeft(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    /// Posisi dihitung dari kiri atas, tapi pivot di tengah supaya bisa berdenyut dari titik tengahnya.
    private static RectTransform CenterPivotTopLeft(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x + width * 0.5f, -y - height * 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform Center(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Image PlainImage(string name, Transform parent, string sprite) {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = Load(sprite);
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static Image SlicedImage(string name, Transform parent, string sprite) {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = Load(sprite);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SlicedMultiplier;
        image.raycastTarget = false;
        return image;
    }

    private static Image FullScreenImage(string name, Transform parent, string sprite) {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = Load(sprite);
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        return image;
    }

    /// Tinggi sprite pada referensi 1080p. Sprite ikut menyimpan bayangan bawah, jadi tingginya jangan diubah.
    private static float SpriteHeight(Image image) => image.sprite.rect.height / SlicedMultiplier;

    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string content, float size,
        Color color, TextAlignmentOptions alignment) {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static CanvasGroup TextGroup(string name, Transform parent, TMP_FontAsset font, string content, float size,
        Color color, float centerY, float height) {
        TextMeshProUGUI text = Text(name, parent, font, content, size, color, TextAlignmentOptions.Midline);
        Center(text.rectTransform, 0f, centerY, 1500f, height);
        return text.gameObject.AddComponent<CanvasGroup>();
    }

    private static void Bind(Object target, params (string field, Object value)[] bindings) {
        var serialized = new SerializedObject(target);
        foreach ((string field, Object value) in bindings) {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) {
                Debug.LogError($"[HUD] Field '{field}' tidak ada di {target.GetType().Name}.");
                continue;
            }
            property.objectReferenceValue = value;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BindArray(Object target, string field, IReadOnlyList<Object> values) {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value) {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

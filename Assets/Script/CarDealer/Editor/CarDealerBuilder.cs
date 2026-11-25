using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Membangun semua aset DEALER MOBIL (NPC Johnny). Menu Go Vista > Dealer Mobil:
///  1. Bangun Katalog, Gambar & Preview: border 9-slice sprite dealer, model preview (tanpa fisika/jaringan),
///     gambar statis kartu (render model), Resources/CarDealer/CarCatalog.asset, dan dialog Johnny.
///  2. Bangun Panel Dealer: Assets/Resources/UI/CarDealerPanel.prefab (panel + tutorial).
///  3. Pasang Johnny di Flamingo: NPC_CarDealerNpc_Johhny di samping parkiran SpawnPointsCarDealership (WorldScene
///     harus terbuka). Kalau Johnny sudah ada di scene, posisinya tidak diubah (hanya prefab-nya yang diperbarui).
/// Editan manual di prefab panel hilang kalau menu 2 dijalankan lagi.
/// </summary>
public static class CarDealerBuilder {
    private const string SpriteFolder = "Assets/GoVista_HUD_Assets/CarDealer/DealershipCar/";
    private const string CloseSpritePath = "Assets/GoVista_HUD_Assets/HadiahLogin/dl_close_button.png";
    private const string ThumbFolder = "Assets/GoVista_HUD_Assets/CarDealer/CarThumbnails";
    private const string PreviewFolder = "Assets/Prefab/Vehicles/Preview";
    private const string CatalogPath = "Assets/Resources/CarDealer/CarCatalog.asset";
    private const string PanelPath = "Assets/Resources/UI/CarDealerPanel.prefab";
    private const string DialogueFolder = "Assets/DialougeData/CarDealer";
    private const string NpcPrefabPath = "Assets/Characters/NPC/CarDealerNpc_Johhny/NPC_CarDealerNpc_Johhny.prefab";
    private const string PortraitPath = "Assets/Download_Assets/BoZo_StylizedModularCharacters/CustomCharacters/Icons/CarDealerNpc_Johhny.png";
    private const string VoicePath = "Assets/Audios/NPC_Sound/generic1.wav";
    private const string KachingPath = "Assets/Audios/Universal_SFX/kaching-sound-fx.mp3";
    private const string BoldFontPath = "Assets/Fonts/Fredoka-Bold SDF.asset";
    private const string MediumFontPath = "Assets/Fonts/Fredoka-Medium SDF.asset";
    private const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    private const string ParkingGroupName = "SpawnPointsCarDealership";
    private const string NpcObjectName = "NPC_Johnny_CarDealer";

    private const string IntroId = "johnny_intro";
    private const string GreetingId = "johnny_greeting";

    /// Mobil yang dijual (prefab Resources/Vehicles/&lt;Nama&gt;_Drivable, dibuat Go Vista > Mobil > 2).
    /// Harga di sini hanya dipakai saat entry baru dibuat; harga yang sudah diisi di CarCatalog.asset tidak ditimpa.
    /// Kartu di grid diurutkan dari harga termurah.
    private static readonly (string id, string name, int price)[] Cars = {
        ("sedan", "Sedan", 0),
        ("suv", "SUV", 200),
        ("bezerker", "Bezerker", 650),
        ("matador", "Matador", 750),
        ("gt", "GT", 800),
        ("mako", "Mako", 1000),
    };

    private const int SortingOrder = 53;   // sama dengan popup Hadiah Login: di atas HUD, di bawah layar mati

    // ---------------------------------------------------------------- Ukuran desain (referensi 1920x1080)
    private const float PanelW = 1560f, PanelH = 780f, Pad = 32f;
    private const float CardW = 316f, CardH = 344f, CardGapX = 26f, CardGapY = 28f;
    private const float DetailX = Pad + CardW * 3f + CardGapX * 2f + 28f;
    private const float DetailW = PanelW - Pad - DetailX;
    private const float DetailH = PanelH - Pad * 2f;
    private const float Inner = 16f;

    private static readonly Color TextLight = new Color32(232, 238, 250, 255);
    private static readonly Color TextMuted = new Color32(170, 182, 206, 255);
    private static readonly Color PriceColor = new Color32(255, 204, 92, 255);
    private static readonly Color SpeedOn = new Color32(34, 196, 222, 255);
    private static readonly Color SpeedOff = new Color32(40, 46, 60, 255);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color TutorialDim = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color HighlightColor = new Color32(34, 196, 222, 255);
    private static readonly Color CloseTint = new Color32(236, 72, 86, 255);

    private static TMP_FontAsset _bold, _medium;

    // ================================================================ 1. Katalog, gambar, preview, dialog

    [MenuItem("Go Vista/Dealer Mobil/1. Bangun Katalog, Gambar & Preview")]
    public static void BuildCatalog() {
        ConfigureSprites();
        EnsureFolder(Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));
        var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(CatalogPath);
        if (catalog == null) {
            catalog = ScriptableObject.CreateInstance<CarCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var log = new List<string>();
        var entries = catalog.cars.Where(e => e != null).ToList();
        foreach ((string id, string name, int price) in Cars) {
            string resource = $"Vehicles/{name}_Drivable";
            string drivablePath = $"Assets/Resources/{resource}.prefab";
            var drivable = AssetDatabase.LoadAssetAtPath<GameObject>(drivablePath);
            if (drivable == null) {
                log.Add($"{name}: {drivablePath} tidak ada (Go Vista > Mobil > 2), dilewati");
                continue;
            }
            GameObject preview = BuildPreviewModel(drivable, name);
            Sprite thumb = RenderThumbnail(preview, name);

            CarCatalog.Entry entry = entries.Find(e => e.id == id);
            if (entry == null) {
                entry = new CarCatalog.Entry { id = id, price = price };
                entries.Add(entry);
            }
            entry.displayName = name;
            entry.drivablePrefab = resource;
            entry.maxSpeedKmh = ReadMaxSpeed(drivable);
            entry.thumbnail = thumb;
            entry.previewModel = preview;
            log.Add($"{name}: {entry.maxSpeedKmh} km/jam, harga {entry.price}");
        }
        catalog.cars = entries.OrderBy(e => e.price).ToArray();   // stabil: harga sama tetap urutan lama
        EditorUtility.SetDirty(catalog);

        BuildDialogues();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Dealer] Katalog {CatalogPath} siap:\n- " + string.Join("\n- ", log));
    }

    /// <summary>Border 9-slice sprite dealer supaya sudut membulat tidak melar.</summary>
    private static void ConfigureSprites() {
        var borders = new Dictionary<string, Vector4> {
            // x = kiri, y = bawah, z = kanan, w = atas (piksel)
            { "dlr_panel_bg", new Vector4(64, 64, 64, 64) },
            { "dlr_card_normal", new Vector4(40, 40, 40, 40) },
            { "dlr_card_selected", new Vector4(44, 44, 44, 44) },
            { "dlr_detail_bg", new Vector4(40, 40, 40, 40) },
            { "dlr_preview_bg", new Vector4(30, 30, 30, 30) },
            { "dlr_name_bg", new Vector4(28, 28, 28, 28) },
            { "dlr_price_box", new Vector4(28, 28, 28, 28) },
            { "dlr_speed_track", new Vector4(22, 22, 22, 22) },
            { "dlr_speed_segment", new Vector4(10, 10, 10, 10) },
            { "dlr_btn_buy", new Vector4(40, 52, 40, 40) },
            { "dlr_btn_bought", new Vector4(40, 52, 40, 40) },
            { "dlr_btn_spawn", new Vector4(40, 52, 40, 40) },
            { "dlr_btn_disabled", new Vector4(40, 52, 40, 40) },
        };
        foreach (KeyValuePair<string, Vector4> pair in borders) {
            string path = SpriteFolder + pair.Key + ".png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == pair.Value) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = pair.Value;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static float ReadMaxSpeed(GameObject drivable) {
        var drive = drivable.GetComponent<CarDriveController>();
        if (drive == null) return 60f;
        SerializedProperty prop = new SerializedObject(drive).FindProperty("maxSpeedKmh");
        return prop != null ? prop.floatValue : 60f;
    }

    /// <summary>
    /// Salinan prefab mobil yang hanya berisi tampilan (Transform + mesh + renderer). Dipakai preview berputar
    /// dan render gambar kartu, supaya tidak ada fisika/RealtimeView/skrip mobil yang ikut jalan.
    /// </summary>
    private static GameObject BuildPreviewModel(GameObject drivable, string carName) {
        EnsureFolder(PreviewFolder);
        string path = $"{PreviewFolder}/{carName}_Preview.prefab";
        var copy = (GameObject)Object.Instantiate(drivable);
        try {
            copy.name = carName + "_Preview";
            // Renderer yang memang mati (penanda pintu EnterL/EnterR, dll.) dibuang.
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                if (!r.enabled || !r.gameObject.activeInHierarchy) {
                    var filter = r.GetComponent<MeshFilter>();
                    Object.DestroyImmediate(r);
                    if (filter) Object.DestroyImmediate(filter);
                }
            StripNonVisual(copy);
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) {
                t.gameObject.layer = 0;
                t.gameObject.isStatic = false;
            }
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            PrefabUtility.SaveAsPrefabAsset(copy, path);
        } finally {
            Object.DestroyImmediate(copy);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static bool IsVisual(Component c) =>
        c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer;

    /// <summary>Hapus semua komponen non-tampilan, urut supaya komponen yang dibutuhkan (RequireComponent) terakhir.</summary>
    private static void StripNonVisual(GameObject root) {
        for (int pass = 0; pass < 10; pass++) {
            bool removedAny = false;
            foreach (Component c in root.GetComponentsInChildren<Component>(true)) {
                if (c == null || IsVisual(c)) continue;
                if (IsRequiredByOthers(c)) continue;
                Object.DestroyImmediate(c);
                removedAny = true;
            }
            if (!removedAny) break;
        }
    }

    private static bool IsRequiredByOthers(Component target) {
        System.Type type = target.GetType();
        foreach (Component other in target.GetComponents<Component>()) {
            if (other == null || other == target || IsVisual(other)) continue;
            foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true)) {
                if (Requires(req.m_Type0, type) || Requires(req.m_Type1, type) || Requires(req.m_Type2, type))
                    return true;
            }
        }
        return false;
    }

    private static bool Requires(System.Type required, System.Type actual) => required != null && required.IsAssignableFrom(actual);

    /// <summary>Render model preview jadi PNG transparan (gambar statis kartu grid).</summary>
    private static Sprite RenderThumbnail(GameObject preview, string carName) {
        EnsureFolder(ThumbFolder);
        string path = $"{ThumbFolder}/{carName}.png";
        const int width = 640, height = 440;

        // Render di scene yang terbuka, jauh di bawah map, pada layer kosong (31): kamera dan cahaya hanya melihat
        // layer itu. (PreviewRenderUtility tidak menghasilkan gambar di URP.)
        const int layer = 31;
        GameObject go = null, camGo = null, lightGo = null;
        RenderTexture rt = null;
        Texture2D tex = null;
        try {
            go = (GameObject)Object.Instantiate(preview);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(new Vector3(0f, -5000f, 0f), Quaternion.Euler(0f, 210f, 0f));
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) {
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }

            camGo = new GameObject("DealerThumbCamera") { hideFlags = HideFlags.HideAndDontSave };
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.aspect = (float)width / height;

            lightGo = new GameObject("DealerThumbLight") { hideFlags = HideFlags.HideAndDontSave };
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << layer;
            light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.52f;
            Quaternion look = Quaternion.Euler(14f, 0f, 0f);
            cam.transform.rotation = look;
            cam.transform.position = bounds.center + look * new Vector3(0f, 0f, -distance);

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
        } finally {
            if (go != null) Object.DestroyImmediate(go);
            if (camGo != null) Object.DestroyImmediate(camGo);
            if (lightGo != null) Object.DestroyImmediate(lightGo);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (tex != null) Object.DestroyImmediate(tex);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer) {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------------------------------------------------------------- Dialog Johnny

    private static void BuildDialogues() {
        EnsureFolder(DialogueFolder);
        var voice = AssetDatabase.LoadAssetAtPath<AudioClip>(VoicePath);
        var portrait = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
        Dialogue($"{DialogueFolder}/Johnny_Intro.asset", IntroId, voice, portrait,
                 "Halo, pendatang baru! Nama saya Johnny.",
                 "Selamat datang di Flamingo, dealer mobil terbaik di kota ini!",
                 "Capek jalan kaki keliling kota? Tenang, di sini kamu bisa punya mobil sendiri! Biar saya tunjukkan caranya.");
        Dialogue($"{DialogueFolder}/Johnny_Greeting.asset", GreetingId, voice, portrait,
                 "Ketemu lagi! Mau lihat-lihat mobil?");
    }

    private static DialogueData Dialogue(string path, string id, AudioClip voice, Sprite portrait, params string[] lines) {
        var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
        if (data == null) {
            data = ScriptableObject.CreateInstance<DialogueData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.speakerName = "Johnny";
        data.dialogueId = id;
        data.portrait = portrait;
        data.voiceClips = voice != null ? new[] { voice } : new AudioClip[0];
        data.lines = lines.Select(t => new DialogueLine { text = t }).ToArray();
        EditorUtility.SetDirty(data);
        return data;
    }

    // ================================================================ 2. Panel

    [MenuItem("Go Vista/Dealer Mobil/2. Bangun Panel Dealer")]
    public static void BuildPanel() {
        _bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
        _medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumFontPath);
        ConfigureSprites();

        var root = new GameObject("CarDealerPanel", typeof(RectTransform));
        try {
            root.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            Image dim = Stretch(NewRect("Dim", root.transform)).gameObject.AddComponent<Image>();
            dim.color = DimColor;

            RectTransform panel = NewRect("Panel", root.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelW, PanelH);
            Sliced(panel.gameObject, "dlr_panel_bg").raycastTarget = true;
            panel.gameObject.AddComponent<ResponsivePanel>();

            var dealer = root.AddComponent<CarDealerPanel>();
            var so = new SerializedObject(dealer);
            BuildGrid(panel, so);
            BuildDetail(panel, so);
            BuildClose(panel, so);
            BuildToast(panel, so);

            AudioSource sfx = root.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            Set(so, "sfxSource", sfx);
            Set(so, "kachingClip", AssetDatabase.LoadAssetAtPath<AudioClip>(KachingPath));

            Set(so, "tutorial", BuildTutorial(root.transform));
            so.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursive(root.transform, root.layer);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            Debug.Log($"[Dealer] {PanelPath} dibuat.");
        } finally {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildGrid(RectTransform panel, SerializedObject so) {
        RectTransform grid = Place(NewRect("Grid", panel), 0f, 0f, PanelW, PanelH);

        // Kartu contoh: tombol + gambar kartu, kotak preview berisi gambar statis, nama di bawah.
        RectTransform card = Place(NewRect("Card Template", grid), Pad, Pad, CardW, CardH);
        Image cardImage = Sliced(card.gameObject, "dlr_card_normal");
        cardImage.raycastTarget = true;
        Button cardButton = card.gameObject.AddComponent<Button>();
        cardButton.targetGraphic = cardImage;
        cardButton.transition = Selectable.Transition.ColorTint;

        const float previewH = 236f;
        RectTransform cardPreview = Place(NewRect("Preview", card), 14f, 14f, CardW - 28f, previewH);
        Sliced(cardPreview.gameObject, "dlr_preview_bg");
        RectTransform thumb = Place(NewRect("Thumbnail", cardPreview), 10f, 10f, CardW - 48f, previewH - 20f);
        Image thumbImage = thumb.gameObject.AddComponent<Image>();
        thumbImage.preserveAspect = true;
        thumbImage.raycastTarget = false;
        Text("Name", card, "Matador", 40f, _bold, Color.white, TextAlignmentOptions.Center,
             10f, 14f + previewH + 6f, CardW - 20f, CardH - previewH - 34f);
        card.gameObject.SetActive(false);

        Set(so, "cardTemplate", card);
        SerializedProperty slots = so.FindProperty("cardSlots");
        slots.arraySize = 6;
        for (int i = 0; i < 6; i++) {
            int col = i % 3, row = i / 3;
            slots.GetArrayElementAtIndex(i).vector2Value =
                new Vector2(Pad + col * (CardW + CardGapX), -(Pad + row * (CardH + CardGapY)));
        }
        Set(so, "cardNormal", LoadSprite("dlr_card_normal"));
        Set(so, "cardSelected", LoadSprite("dlr_card_selected"));
    }

    private static void BuildDetail(RectTransform panel, SerializedObject so) {
        RectTransform detail = Place(NewRect("Detail", panel), DetailX, Pad, DetailW, DetailH);
        Sliced(detail.gameObject, "dlr_detail_bg");
        float w = DetailW - Inner * 2f;
        float y = Inner;

        // Preview 3D berputar (RawImage diisi CarPreviewStage).
        RectTransform previewArea = Place(NewRect("Preview", detail), Inner, y, w, 250f);
        Sliced(previewArea.gameObject, "dlr_preview_bg");
        RectTransform raw = Place(NewRect("Render", previewArea), 6f, 6f, w - 12f, 238f);
        RawImage rawImage = raw.gameObject.AddComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.enabled = false;
        var stage = previewArea.gameObject.AddComponent<CarPreviewStage>();
        var stageSo = new SerializedObject(stage);
        Set(stageSo, "target", rawImage);
        stageSo.FindProperty("resolution").vector2IntValue = new Vector2Int(Mathf.RoundToInt((w - 12f) * 2f), 476);
        stageSo.ApplyModifiedPropertiesWithoutUndo();
        Set(so, "preview", stage);
        Set(so, "previewArea", previewArea);
        y += 250f + 16f;

        RectTransform nameBox = Place(NewRect("Name Box", detail), Inner, y, w, 72f);
        Sliced(nameBox.gameObject, "dlr_name_bg");
        Set(so, "nameLabel", Text("Name", nameBox, "Matador", 42f, _bold, Color.white, TextAlignmentOptions.Center,
                                  0f, 0f, w, 72f));
        y += 72f + 14f;

        // Speed: ikon + label, lalu track berisi 10 kotak.
        RectTransform speedArea = Place(NewRect("Speed", detail), Inner, y, w, 96f);
        Picture("Speed Icon", speedArea, LoadSprite("icon_speed"), 0f, 2f, 32f, 32f).color = SpeedOn;
        Text("Speed Label", speedArea, "Speed", 28f, _bold, TextMuted, TextAlignmentOptions.Left, 42f, 0f, 200f, 36f);
        RectTransform track = Place(NewRect("Track", speedArea), 0f, 42f, w, 54f);
        Sliced(track.gameObject, "dlr_speed_track");
        SerializedProperty segments = so.FindProperty("speedSegments");
        segments.arraySize = CarDealerRules.SpeedSegments;
        const float segPad = 9f, segGap = 6f;
        float segW = (w - segPad * 2f - segGap * (CarDealerRules.SpeedSegments - 1)) / CarDealerRules.SpeedSegments;
        for (int i = 0; i < CarDealerRules.SpeedSegments; i++) {
            RectTransform seg = Place(NewRect($"Segment {i + 1}", track), segPad + i * (segW + segGap), segPad,
                                      segW, 54f - segPad * 2f);
            Image segImage = Sliced(seg.gameObject, "dlr_speed_segment");
            segImage.color = i < 6 ? SpeedOn : SpeedOff;
            segments.GetArrayElementAtIndex(i).objectReferenceValue = segImage;
        }
        so.FindProperty("speedOnColor").colorValue = SpeedOn;
        so.FindProperty("speedOffColor").colorValue = SpeedOff;
        Set(so, "speedArea", speedArea);
        y += 96f + 14f;

        RectTransform priceArea = Place(NewRect("Price", detail), Inner, y, w, 112f);
        Text("Price Label", priceArea, "Price", 28f, _bold, TextMuted, TextAlignmentOptions.Left, 0f, 0f, 200f, 36f);
        RectTransform priceBox = Place(NewRect("Price Box", priceArea), 0f, 42f, w, 70f);
        Sliced(priceBox.gameObject, "dlr_price_box");
        Set(so, "priceLabel", Text("Price Value", priceBox, CarDealerRules.FreeLabel, 40f, _bold, PriceColor,
                                   TextAlignmentOptions.Center, 0f, 0f, w, 70f));
        Set(so, "priceArea", priceArea);
        y += 112f + 18f;

        float buttonH = DetailH - Inner - y;
        RectTransform button = Place(NewRect("Action Button", detail), Inner, y, w, buttonH);
        Image buttonImage = Sliced(button.gameObject, "dlr_btn_buy");
        buttonImage.raycastTarget = true;
        Button actionButton = button.gameObject.AddComponent<Button>();
        actionButton.targetGraphic = buttonImage;
        // Warna disabled = putih: tampilan abu-abu sudah dari sprite dlr_btn_disabled.
        ColorBlock colors = actionButton.colors;
        colors.disabledColor = Color.white;
        actionButton.colors = colors;
        TMP_Text actionLabel = Text("Label", button, "Buy", 50f, _bold, Color.white, TextAlignmentOptions.Center,
                                    0f, 0f, w, buttonH - 10f);
        Set(so, "actionButton", actionButton);
        Set(so, "actionImage", buttonImage);
        Set(so, "actionLabel", actionLabel);
        Set(so, "buySprite", LoadSprite("dlr_btn_buy"));
        Set(so, "disabledSprite", LoadSprite("dlr_btn_disabled"));
        Set(so, "boughtSprite", LoadSprite("dlr_btn_bought"));
        Set(so, "spawnSprite", LoadSprite("dlr_btn_spawn"));
    }

    private static void BuildClose(RectTransform panel, SerializedObject so) {
        const float size = 96f;
        RectTransform close = NewRect("Close Button", panel);
        close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
        close.pivot = new Vector2(0.5f, 0.5f);
        close.anchoredPosition = new Vector2(-12f, -12f);
        close.sizeDelta = new Vector2(size, size);
        Image image = close.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CloseSpritePath);
        image.preserveAspect = true;
        Button button = close.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Set(so, "closeButton", button);
    }

    private static void BuildToast(RectTransform panel, SerializedObject so) {
        RectTransform toast = NewRect("Toast", panel);
        toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 0f);
        toast.pivot = new Vector2(0.5f, 0.5f);
        toast.anchoredPosition = new Vector2(0f, 0f);
        toast.sizeDelta = new Vector2(1100f, 84f);
        Image bg = Sliced(toast.gameObject, "dlr_name_bg");
        bg.color = new Color32(28, 34, 48, 245);
        TMP_Text text = Text("Text", toast, "", 30f, _bold, TextLight, TextAlignmentOptions.Center, 24f, 0f, 1052f, 84f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = 30f;
        toast.gameObject.SetActive(false);
        Set(so, "toast", toast.gameObject);
        Set(so, "toastText", text);
    }

    private static CarDealerTutorial BuildTutorial(Transform root) {
        RectTransform overlay = Stretch(NewRect("Tutorial", root));
        // Penahan sentuhan transparan selebar layar: tombol panel di bawahnya tidak bisa ditekan selama tutorial.
        Image blocker = overlay.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;
        var tutorial = overlay.gameObject.AddComponent<CarDealerTutorial>();
        var so = new SerializedObject(tutorial);
        Set(so, "overlay", overlay);
        foreach (string side in new[] { "Top", "Bottom", "Left", "Right" }) {
            RectTransform dim = NewRect("Dim " + side, overlay);
            Image image = dim.gameObject.AddComponent<Image>();
            image.color = TutorialDim;
            image.raycastTarget = false;
            Set(so, "dim" + side, dim);
        }

        // Garis tepi sorotan: empat batang tipis menempel ke sisi kotak Highlight (ukurannya diatur tutorial).
        RectTransform highlight = NewRect("Highlight", overlay);
        const float thick = 6f;
        var edges = new (string name, Vector2 min, Vector2 max, Vector2 size)[] {
            ("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, thick)),
            ("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, thick)),
            ("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thick, 0f)),
            ("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thick, 0f)),
        };
        foreach (var edge in edges) {
            RectTransform bar = NewRect(edge.name, highlight);
            bar.anchorMin = edge.min;
            bar.anchorMax = edge.max;
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = edge.size;
            Image image = bar.gameObject.AddComponent<Image>();
            image.color = HighlightColor;
            image.raycastTarget = false;
        }
        Set(so, "highlight", highlight);

        RectTransform caption = NewRect("Caption", overlay);
        caption.sizeDelta = new Vector2(560f, 250f);
        Image box = Sliced(caption.gameObject, "dlr_detail_bg");
        box.color = new Color32(255, 255, 255, 250);
        var outline = caption.gameObject.AddComponent<Outline>();
        outline.effectColor = HighlightColor;
        outline.effectDistance = new Vector2(3f, -3f);
        Text("Speaker", caption, "Johnny", 32f, _bold, HighlightColor, TextAlignmentOptions.Left, 28f, 18f, 504f, 42f);
        TMP_Text body = Text("Body", caption, "", 28f, _medium, TextLight, TextAlignmentOptions.TopLeft, 28f, 64f, 504f, 130f);
        body.verticalAlignment = VerticalAlignmentOptions.Top;
        body.enableAutoSizing = true;
        body.fontSizeMin = 20f;
        body.fontSizeMax = 28f;
        TMP_Text hint = Text("Hint", caption, "Ketuk untuk lanjut", 22f, _medium, TextMuted, TextAlignmentOptions.Right,
                             28f, 200f, 504f, 34f);
        Set(so, "caption", caption);
        Set(so, "captionText", body);
        Set(so, "hintText", hint);
        so.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
        return tutorial;
    }

    // ================================================================ 3. NPC di dealer Flamingo

    [MenuItem("Go Vista/Dealer Mobil/3. Pasang Johnny di Flamingo")]
    public static void InstallNpc() {
        Scene world = SceneManager.GetSceneByPath(WorldScenePath);
        if (!world.isLoaded) {
            EditorUtility.DisplayDialog("Pasang Johnny", "Buka WorldScene dulu (boleh additive), lalu jalankan lagi.", "OK");
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
        if (prefab == null) {
            Debug.LogError($"[Dealer] {NpcPrefabPath} tidak ditemukan.");
            return;
        }

        // Johnny bisa saja sudah dipindah manual ke bawah object lain: cari di seluruh hierarchy, bukan root saja.
        GameObject npc = FindInScene(world, NpcObjectName);
        GameObject parking = FindInScene(world, ParkingGroupName);
        if (npc == null && parking == null) {
            Debug.LogError($"[Dealer] '{ParkingGroupName}' tidak ada di WorldScene.");
            return;
        }
        ConfigureNpcPrefab(prefab);

        if (npc == null) {
            npc = (GameObject)PrefabUtility.InstantiatePrefab(prefab, world);
            npc.name = NpcObjectName;
            // Di samping parkiran (sisi -Z grup), menghadap ke parkiran. Geser manual kalau kurang pas.
            Vector3 side = -parking.transform.forward;
            Vector3 position = parking.transform.position + side * 3f;
            npc.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-side, Vector3.up));
            Undo.RegisterCreatedObjectUndo(npc, "Pasang Johnny");
        }
        EditorSceneManager.MarkSceneDirty(world);
        Selection.activeGameObject = npc;
        Debug.Log($"[Dealer] Johnny dipasang di {npc.transform.position}. Simpan WorldScene (Ctrl+S).");
    }

    /// <summary>Isi komponen prefab NPC: dialog intro/sapaan, nama 'Johnny', diam di tempat, buka dealer.</summary>
    private static void ConfigureNpcPrefab(GameObject prefab) {
        var intro = AssetDatabase.LoadAssetAtPath<DialogueData>($"{DialogueFolder}/Johnny_Intro.asset");
        var greeting = AssetDatabase.LoadAssetAtPath<DialogueData>($"{DialogueFolder}/Johnny_Greeting.asset");
        if (intro == null || greeting == null) {
            BuildDialogues();
            intro = AssetDatabase.LoadAssetAtPath<DialogueData>($"{DialogueFolder}/Johnny_Intro.asset");
            greeting = AssetDatabase.LoadAssetAtPath<DialogueData>($"{DialogueFolder}/Johnny_Greeting.asset");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(NpcPrefabPath);
        try {
            var trigger = root.GetComponent<DialogueTrigger>();
            var triggerSo = new SerializedObject(trigger);
            SerializedProperty stages = triggerSo.FindProperty("stages");
            stages.arraySize = 1;
            stages.GetArrayElementAtIndex(0).objectReferenceValue = intro;
            triggerSo.FindProperty("repeatDialogue").objectReferenceValue = greeting;
            triggerSo.ApplyModifiedPropertiesWithoutUndo();

            var interactable = root.GetComponent<Interactable>();
            if (interactable != null) interactable.displayName = "Johnny";

            // Dealer berdiri di tempat (tidak berkeliling). NavMeshAgent dibiarkan: dibutuhkan NpcGroundSnap.
            if (root.TryGetComponent(out NpcObstacleAvoidance avoid)) Object.DestroyImmediate(avoid);
            if (root.TryGetComponent(out NpcNavigation nav)) Object.DestroyImmediate(nav);

            if (!root.GetComponent<CarDealerNpc>()) root.AddComponent<CarDealerNpc>();
            var dealerSo = new SerializedObject(root.GetComponent<CarDealerNpc>());
            dealerSo.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CarCatalog>(CatalogPath);
            dealerSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, NpcPrefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ================================================================ Helper

    private static GameObject FindInScene(Scene scene, string objectName) {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == objectName) return t.gameObject;
        return null;
    }

    private static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static Sprite LoadSprite(string name) {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
        if (sprite == null) Debug.LogWarning($"[Dealer] Sprite {SpriteFolder}{name}.png tidak ditemukan.");
        return sprite;
    }

    private static void Set(SerializedObject so, string property, Object value) {
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null) Debug.LogError($"[Dealer] Field '{property}' tidak ada di {so.targetObject.GetType().Name}.");
        else prop.objectReferenceValue = value;
    }

    private static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>Posisi dari pojok kiri atas parent.</summary>
    private static RectTransform Place(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image Sliced(GameObject target, string sprite) {
        Image image = target.AddComponent<Image>();
        image.sprite = LoadSprite(sprite);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return image;
    }

    private static Image Picture(string name, Transform parent, Sprite sprite, float x, float y, float w, float h) {
        RectTransform rect = Place(NewRect(name, parent), x, y, w, h);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(string name, Transform parent, string content, float size, TMP_FontAsset font,
                                 Color color, TextAlignmentOptions alignment, float x, float y, float w, float h) {
        RectTransform rect = Place(NewRect(name, parent), x, y, w, h);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.raycastTarget = false;
        return text;
    }

    private static void SetLayerRecursive(Transform t, int layer) {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursive(child, layer);
    }
}

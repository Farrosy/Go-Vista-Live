using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Chip ping di kanan koin (ikon WiFi 4 lapis + "45 ms") dan peringatan "Koneksi tidak stabil" di bawahnya.
/// Sprite di Assets/GoVista_HUD_Assets/Ping (dari desain 2026-11-25), ikut dipaket ke sprite atlas HUD.
public static partial class GameHudBuilder {
    private const string PingFolder = "Ping/";
    private const float PingIconSize = 34f;
    private const float PingIconLeft = 12f;
    private const float PingWarningWidth = 340f;
    private const float PingWarningHeight = 78f;
    private const float PingWarningGap = 10f;
    private const float PingWarningIconSize = 52f;
    // pill_ping 220x64: ujung bulat (radius 32) tidak ikut melar.
    private static readonly Vector4 PingPillBorder = new Vector4(32f, 0f, 32f, 0f);
    private static readonly Color PingWarningTitle = new Color32(255, 140, 120, 255);

    /// Pastikan sprite Ping diimport sebagai Sprite (pill dengan border 9-slice) dan masuk atlas HUD.
    private static void EnsurePingSprites() {
        string folder = AssetRoot + PingFolder;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder.TrimEnd('/') })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            Vector4 border = path.EndsWith("pill_ping.png") ? PingPillBorder : Vector4.zero;
            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single && importer.spriteBorder == border &&
                importer.alphaIsTransparency && !importer.mipmapEnabled) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        EnsureFolderInAtlas("Ping");
    }

    private static void BuildPing(RectTransform safeArea, Fonts fonts) {
        RectTransform root = TopLeft(NewRect("Ping", safeArea), HudLayout.PingLeft, HudLayout.PingTop,
            PingWarningWidth, HudLayout.PingHeight + PingWarningGap + PingWarningHeight);

        Image pill = SlicedImage("Ping Chip", root, PingFolder + "pill_ping.png");
        TopLeft(pill.rectTransform, 0f, 0f, HudLayout.PingWidth, HudLayout.PingHeight);
        float iconTop = (HudLayout.PingHeight - PingIconSize) / 2f;
        var bars = new Image[4];
        for (int i = 0; i < bars.Length; i++) {
            bars[i] = PlainImage($"Wifi {i + 1}", pill.rectTransform, PingFolder + $"icon_wifi_{i + 1}.png");
            TopLeft(bars[i].rectTransform, PingIconLeft, iconTop, PingIconSize, PingIconSize);
        }
        float labelLeft = PingIconLeft + PingIconSize + 8f;
        TextMeshProUGUI label = Text("Label", pill.rectTransform, fonts.Bold, "—", 26f, Color.white,
            TextAlignmentOptions.MidlineLeft);
        TopLeft(label.rectTransform, labelLeft, 0f, HudLayout.PingWidth - labelLeft - 14f, HudLayout.PingHeight);
        var view = pill.gameObject.AddComponent<PingIndicatorView>();
        BindArray(view, "bars", bars);
        Bind(view, ("label", label));

        CanvasGroup warning = BuildPingWarning(root, fonts);
        var indicator = root.gameObject.AddComponent<PingIndicator>();
        Bind(indicator, ("view", view), ("chip", pill.gameObject), ("warning", warning));
    }

    private static CanvasGroup BuildPingWarning(RectTransform root, Fonts fonts) {
        Image pill = SlicedImage("Ping Warning", root, "Panels/pill_toast.png");
        RectTransform rect = pill.rectTransform;
        TopLeft(rect, 0f, HudLayout.PingHeight + PingWarningGap, PingWarningWidth, PingWarningHeight);
        // Muncul dari kiri atas (dekat chip ping).
        rect.pivot = new Vector2(0f, 1f);
        var group = pill.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        Image icon = PlainImage("Icon", rect, PingFolder + "icon_connection_bad.png");
        TopLeft(icon.rectTransform, 16f, (PingWarningHeight - PingWarningIconSize) / 2f, PingWarningIconSize,
            PingWarningIconSize);
        const float textLeft = 80f;
        TextMeshProUGUI title = Text("Title", rect, fonts.Bold, "Koneksi tidak stabil", 24f, PingWarningTitle,
            TextAlignmentOptions.MidlineLeft);
        title.fontSharedMaterial = fonts.BoldUnderlay;
        TopLeft(title.rectTransform, textLeft, 10f, PingWarningWidth - textLeft - 16f, 32f);
        TextMeshProUGUI detail = Text("Detail", rect, fonts.Medium, "Cek jaringan internetmu", 20f, Color.white,
            TextAlignmentOptions.MidlineLeft);
        TopLeft(detail.rectTransform, textLeft, 40f, PingWarningWidth - textLeft - 16f, 28f);
        return group;
    }
}

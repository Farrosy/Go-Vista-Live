using MTAssets.EasyMinimapSystem;
using UnityEditor;
using UnityEngine;

/// Bagian HUD untuk radar (GoVistaMinimap) di dalam lingkaran minimap.
/// Radar memakai MinimapRenderer dari Easy Minimap System dengan mask lingkaran sendiri, diletakkan tepat
/// di atas "Map Mask" supaya blip yang menempel di tepi tidak terpotong mask HUD.
public static partial class GameHudBuilder {
    private const string MinimapPluginRoot = "Assets/Plugins/MT Assets/Easy Minimap System/";
    private const string RadarArtRoot = "Assets/Art/Minimap/";
    // Lingkaran di minimap_mask.png: diameter 282 px pada gambar 372 px.
    private const float RadarDiameter = HudLayout.MinimapSize * 282f / 372f;

    private static void BuildRadar(RectTransform minimap, GameObject placeholder) {
        RectTransform rect = Center(NewRect("Radar", minimap), 0f, 0f, RadarDiameter, RadarDiameter);
        rect.SetSiblingIndex(placeholder.transform.parent.GetSiblingIndex() + 1);
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f; // GoVistaMinimap menampilkannya setelah ada peta dan pemain

        var renderer = rect.gameObject.AddComponent<MinimapRenderer>();
        renderer.baseShapeSprite = LoadSpriteAt(MinimapPluginRoot + "Formats/Circle.png");
        renderer.borderColor = Color.clear; // cincin sudah digambar oleh backdrop HUD
        renderer.borderWidthSize = 0f;
        renderer.backgroundSprite = null;
        renderer.foregroundSprite = null;
        renderer.contentTransparency = 100f;
        renderer.renderContent = true;
        renderer.iconsRotationMethod = MinimapRenderer.IconsRotationMethod.CircleShape;
        renderer.showNorthIcon = false; // penanda N belum dibutuhkan (keputusan tim, 2026-11-13)

        var radar = rect.gameObject.AddComponent<GoVistaMinimap>();
        Bind(radar, ("radarGroup", group), ("placeholder", placeholder),
            ("localArrowSprite", LoadSpriteAt(RadarArtRoot + "radar_player_arrow.png", ensureSpriteImport: true)),
            ("remotePlayerSprite", LoadSpriteAt(RadarArtRoot + "radar_blip_square.png", ensureSpriteImport: true)));
        SetFloat(radar, "edgeRadiusPixels", RadarDiameter * 0.5f);
    }

    private static Sprite LoadSpriteAt(string path, bool ensureSpriteImport = false) {
        if (ensureSpriteImport && AssetImporter.GetAtPath(path) is TextureImporter importer &&
            importer.textureType != TextureImporterType.Sprite) {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogError($"[HUD] Sprite tidak ditemukan: {path}");
        return sprite;
    }
}

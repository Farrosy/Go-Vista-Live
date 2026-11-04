using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// Setup aset chat (Fase 1):
/// 1. Sprite di Assets/Sprites/Chat otomatis di-import sebagai Sprite (2D and UI) + alpha transparan.
/// 2. Menu "Go Vista/Chat/Setup Emoji Font" membuat font asset COLOR dari NotoColorEmoji.ttf dan
///    mendaftarkannya ke TMP Settings > Emoji Fallback Text Assets, supaya emoji tampil di semua TMP_Text.
public class ChatSpriteImporter : AssetPostprocessor
{
    private const string ChatSpriteFolder = "Assets/Sprites/Chat/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ChatSpriteFolder))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}

public static class ChatAssetSetup
{
    private const string EmojiFontPath = "Assets/Fonts/NotoColorEmoji.ttf";
    private const string EmojiAssetPath = "Assets/Fonts/NotoColorEmoji Color.asset";
    private const int SamplingPointSize = 90;
    private const int AtlasPadding = 9;
    private const int AtlasSize = 1024;

    [MenuItem("Go Vista/Chat/Setup Emoji Font")]
    public static void SetupEmojiFont()
    {
        TMP_FontAsset emojiAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EmojiAssetPath);
        if (emojiAsset == null)
            emojiAsset = CreateEmojiFontAsset();
        if (emojiAsset == null)
            return;

        RegisterEmojiFallback(emojiAsset);
        Debug.Log($"[ChatAssetSetup] Emoji font siap: {EmojiAssetPath}", emojiAsset);
    }

    private static TMP_FontAsset CreateEmojiFontAsset()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(EmojiFontPath);
        if (font == null)
        {
            Debug.LogError($"[ChatAssetSetup] Font tidak ditemukan di {EmojiFontPath}.");
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            font, SamplingPointSize, AtlasPadding, GlyphRenderMode.COLOR,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
        if (fontAsset == null)
        {
            Debug.LogError("[ChatAssetSetup] Gagal membuat font asset emoji. Pastikan 'Include Font Data' aktif di import font.");
            return null;
        }

        string assetName = Path.GetFileNameWithoutExtension(EmojiAssetPath);
        fontAsset.name = assetName;
        AssetDatabase.CreateAsset(fontAsset, EmojiAssetPath);

        fontAsset.atlasTexture.name = assetName + " Atlas";
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        fontAsset.material.name = assetName + " Material";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(EmojiAssetPath);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EmojiAssetPath);
    }

    private static void RegisterEmojiFallback(TMP_FontAsset emojiAsset)
    {
        TMP_Settings settings = TMP_Settings.instance;
        if (settings == null)
        {
            Debug.LogError("[ChatAssetSetup] TMP Settings tidak ditemukan. Import TMP Essential Resources dulu.");
            return;
        }

        List<TMP_Asset> current = TMP_Settings.emojiFallbackTextAssets ?? new List<TMP_Asset>();
        if (current.Contains(emojiAsset))
            return;

        var updated = new List<TMP_Asset>(current) { emojiAsset };
        TMP_Settings.emojiFallbackTextAssets = updated;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}

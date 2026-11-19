using System.IO;
using UnityEditor;
using UnityEngine;

/// Aset sederhana untuk penanda pintu gedung, digambar lewat kode (placeholder sampai ada aset dari desainer):
///  - icon-door.png : ikon pintu putih (dipakai papan melayang + aksi EnterBuilding)
///  - doormat.png   : bingkai bulat + dua panah ke arah pintu, putih (diwarnai material)
///  - Doormat.mat   : URP Particles/Unlit transparan dengan tekstur karpet
/// Semua putih supaya warnanya diatur dari Image.color / _BaseColor.
internal static class BuildingEntranceArt
{
    public const string Folder = "Assets/Art/Entrance";
    public const string DoorIconPath = Folder + "/icon-door.png";
    public const string DoormatTexturePath = Folder + "/doormat.png";
    public const string DoormatMaterialPath = Folder + "/Doormat.mat";
    public const string GlowTexturePath = Folder + "/doormat-glow.png";
    public const string GlowMaterialPath = Folder + "/DoormatGlow.mat";
    private const string ParticleMaterialPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    public static Sprite DoorIcon()
    {
        if (!File.Exists(DoorIconPath)) WritePng(DoorIconPath, DrawDoorIcon(128));
        ImportAsSprite(DoorIconPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(DoorIconPath);
    }

    public static Material DoormatMaterial()
    {
        if (!File.Exists(DoormatTexturePath)) WritePng(DoormatTexturePath, DrawDoormat(256, 160));
        ImportAsTexture(DoormatTexturePath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DoormatTexturePath);

        var material = AssetDatabase.LoadAssetAtPath<Material>(DoormatMaterialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath)) { name = "Doormat" };
            AssetDatabase.CreateAsset(material, DoormatMaterialPath);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// Material cahaya tepi karpet: gradasi vertikal (terang di bawah, hilang di atas), dua sisi.
    public static Material GlowMaterial()
    {
        if (!File.Exists(GlowTexturePath)) WritePng(GlowTexturePath, DrawGlow(8, 128));
        ImportAsTexture(GlowTexturePath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath);

        var material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath)) { name = "DoormatGlow" };
            AssetDatabase.CreateAsset(material, GlowMaterialPath);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cull", 0f);   // terlihat dari dalam dan luar area karpet
        EditorUtility.SetDirty(material);
        return material;
    }

    // ------------------------------------------------------------------ gambar

    private static Texture2D DrawGlow(int width, int height)
    {
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float t = y / (height - 1f);
            float alpha = Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 1f, t)), 1.1f);   // pita padat di bawah, pudar ke atas
            for (int x = 0; x < width; x++) pixels[y * width + x] = new Color(1, 1, 1, alpha);
        }
        return ToTexture(pixels, width, height);
    }

    /// Pintu: bingkai tebal dengan sudut atas membulat, gagang bulat, garis lantai.
    private static Texture2D DrawDoorIcon(int size)
    {
        var pixels = new Color[size * size];
        float s = size / 128f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float px = x / s, py = y / s;   // koordinat 0..128, y ke atas
            float frame = RoundedRectStroke(px, py, 30, 18, 98, 116, 16, 11);
            float knob = Disc(px, py, 80, 64, 7.5f);
            float floor = Box(px, py, 16, 8, 112, 18);
            pixels[y * size + x] = new Color(1, 1, 1, Mathf.Max(frame, Mathf.Max(knob, floor)));
        }
        return ToTexture(pixels, size, size);
    }

    /// Karpet: bingkai bulat + isi tipis + dua chevron yang menunjuk ke bawah tekstur (= ke arah pintu).
    private static Texture2D DrawDoormat(int width, int height)
    {
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float border = RoundedRectStroke(x, y, 6, 6, width - 6, height - 6, 34, 9);
            float fill = RoundedRectFill(x, y, 6, 6, width - 6, height - 6, 34) * 0.5f;
            float arrows = Mathf.Max(Chevron(x, y, width * 0.5f, height * 0.38f, 30, 9),
                                     Chevron(x, y, width * 0.5f, height * 0.66f, 30, 9) * 0.6f);
            pixels[y * width + x] = new Color(1, 1, 1, Mathf.Clamp01(Mathf.Max(border, Mathf.Max(fill, arrows))));
        }
        return ToTexture(pixels, width, height);
    }

    private static float RoundedRectDistance(float x, float y, float x0, float y0, float x1, float y1, float r)
    {
        float cx = Mathf.Clamp(x, x0 + r, x1 - r);
        float cy = Mathf.Clamp(y, y0 + r, y1 - r);
        return new Vector2(x - cx, y - cy).magnitude - r;   // < 0 di dalam
    }

    private static float RoundedRectFill(float x, float y, float x0, float y0, float x1, float y1, float r) =>
        Mathf.Clamp01(0.5f - RoundedRectDistance(x, y, x0, y0, x1, y1, r));

    private static float RoundedRectStroke(float x, float y, float x0, float y0, float x1, float y1, float r, float w)
    {
        float d = RoundedRectDistance(x, y, x0, y0, x1, y1, r);
        return Mathf.Clamp01(0.5f - Mathf.Abs(d + w * 0.5f) + w * 0.5f);
    }

    private static float Disc(float x, float y, float cx, float cy, float r) =>
        Mathf.Clamp01(r + 0.5f - new Vector2(x - cx, y - cy).magnitude);

    private static float Box(float x, float y, float x0, float y0, float x1, float y1) =>
        Mathf.Clamp01(Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(y - y0, y1 - y)) + 0.5f);

    /// "V" dengan ujung di (cx, cy - half), lengan selebar w.
    private static float Chevron(float x, float y, float cx, float cy, float half, float w)
    {
        float dx = Mathf.Abs(x - cx);
        if (dx > half + w) return 0f;
        float lineY = cy - half + dx * 0.9f;            // lengan miring
        float d = Mathf.Abs(y - lineY) * 0.74f;          // jarak tegak lurus kira-kira
        return Mathf.Clamp01(w * 0.5f + 0.5f - d) * (dx <= half ? 1f : 0f);
    }

    // ------------------------------------------------------------------ file

    private static Texture2D ToTexture(Color[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static void WritePng(string path, Texture2D texture)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
    }

    private static void ImportAsSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType == TextureImporterType.Sprite && importer.alphaIsTransparency) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static void ImportAsTexture(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.wrapMode == TextureWrapMode.Clamp && importer.alphaIsTransparency) return;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }
}

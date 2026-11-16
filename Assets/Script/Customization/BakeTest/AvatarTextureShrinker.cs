using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Tahap 0b (tes bake): setelah BoZo selesai bake, setiap RenderTexture 2024x2024 di material avatar
/// disalin (Blit di GPU) ke RenderTexture yang lebih kecil (mis. 1024 atau 512), lalu RenderTexture-nya dilepas.
/// Tujuannya memangkas ~85 MB memori GPU per avatar. Hanya aman untuk avatar yang tidak diedit lagi.
/// </summary>
public static class AvatarTextureShrinker {
    public readonly struct Result {
        public readonly int TextureCount;
        public readonly long Bytes;
        public readonly float Ms;

        public Result(int textureCount, long bytes, float ms) {
            TextureCount = textureCount;
            Bytes = bytes;
            Ms = ms;
        }
    }

    private const int BytesPerPixel = 4;
    private const float MipChainFactor = 4f / 3f;

    // copyTexture (RT 2024x2024 untuk kerja internal BoZo) bersifat private di vendor. Di tes ini dilepas
    // lewat reflection; kalau dipakai di game, lebih rapi diberi method publik kecil di OutfitSystem.
    private static readonly FieldInfo CopyTextureField =
        typeof(OutfitSystem).GetField("copyTexture", BindingFlags.NonPublic | BindingFlags.Instance);
    // Semua RT kerja BoZo (mainTexture + peta lain). Sebagian tidak terpasang di properti shader,
    // jadi tidak tertangkap lewat material; tetap dilepas setelah bake.
    private static readonly FieldInfo TexturesField =
        typeof(OutfitSystem).GetField("textures", BindingFlags.NonPublic | BindingFlags.Instance);

    public static Result Shrink(OutfitSystem avatar, int size) {
        var timer = Stopwatch.StartNew();
        var converted = new Dictionary<RenderTexture, RenderTexture>();

        foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true)) {
            foreach (Material material in renderer.sharedMaterials) {
                if (material != null) ReplaceRenderTextures(material, size, converted);
            }
        }

        long bytes = 0;
        foreach (KeyValuePair<RenderTexture, RenderTexture> pair in converted) {
            pair.Key.Release();
            bytes += EstimateBytes(pair.Value);
        }
        ReleaseCopyTexture(avatar);
        ReleaseWorkTextures(avatar);

        var owner = avatar.gameObject.AddComponent<ShrunkTextureOwner>();
        owner.Textures.AddRange(converted.Values);
        return new Result(converted.Count, bytes, (float)timer.Elapsed.TotalMilliseconds);
    }

    private static void ReplaceRenderTextures(Material material, int size,
                                              Dictionary<RenderTexture, RenderTexture> converted) {
        foreach (int id in material.GetTexturePropertyNameIDs()) {
            if (!(material.GetTexture(id) is RenderTexture rt)) continue;
            if (!converted.TryGetValue(rt, out RenderTexture small)) {
                small = CopyToSmall(rt, size);
                converted[rt] = small;
            }
            material.SetTexture(id, small);
        }
    }

    /// Murni di GPU (Blit ke RT kecil + mipmap otomatis), tanpa ReadPixels yang membuat CPU menunggu GPU.
    /// Format warna sama dengan sumber, jadi sRGB/linear (normal map) tetap benar.
    private static RenderTexture CopyToSmall(RenderTexture source, int size) {
        var descriptor = new RenderTextureDescriptor(size, size, source.graphicsFormat, 0) {
            useMipMap = true,
            autoGenerateMips = true
        };
        var small = new RenderTexture(descriptor) {
            name = $"{source.name}_{size}",
            wrapMode = source.wrapMode
        };
        small.Create();
        Graphics.Blit(source, small);
        return small;
    }

    private static void ReleaseCopyTexture(OutfitSystem avatar) {
        if (CopyTextureField?.GetValue(avatar) is RenderTexture copy && copy != null) copy.Release();
    }

    private static void ReleaseWorkTextures(OutfitSystem avatar) {
        if (!(TexturesField?.GetValue(avatar) is Dictionary<string, RenderTexture> textures)) return;
        foreach (RenderTexture rt in textures.Values) {
            if (rt != null) rt.Release();
        }
    }

    private static long EstimateBytes(Texture texture) {
        return (long)(texture.width * (long)texture.height * BytesPerPixel * MipChainFactor);
    }
}

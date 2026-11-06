// Otomatis mengatur import sprite HUD Go Vista: tipe Sprite, tanpa mipmap,
// dan border 9-slice dibaca dari sprites.json di folder GoVista_HUD_Assets.
// Letakkan seluruh folder GoVista_HUD_Assets di dalam Assets/ lalu Reimport.
using System.IO;
using UnityEditor;
using UnityEngine;

public class GoVistaHudImporter : AssetPostprocessor
{
    const string Folder = "GoVista_HUD_Assets/";

    [System.Serializable] class Entry { public string file; public int[] nine_slice_border_LBRT; }
    [System.Serializable] class Root { public Entry[] sprites; }

    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        int idx = path.IndexOf(Folder);
        if (idx < 0 || path.Contains("/Preview/")) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.spritePixelsPerUnit = 100;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;

        string root = path.Substring(0, idx + Folder.Length);
        string rel = path.Substring(root.Length);
        string jsonPath = root + "sprites.json";
        if (!File.Exists(jsonPath)) return;

        var data = JsonUtility.FromJson<Root>(File.ReadAllText(jsonPath));
        if (data == null || data.sprites == null) return;

        foreach (var e in data.sprites)
        {
            if (e.file != rel || e.nine_slice_border_LBRT == null || e.nine_slice_border_LBRT.Length != 4) continue;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect; // wajib untuk Image Type = Sliced
            ti.SetTextureSettings(s);
            var b = e.nine_slice_border_LBRT;
            ti.spriteBorder = new Vector4(b[0], b[1], b[2], b[3]); // L, B, R, T
            break;
        }
    }
}

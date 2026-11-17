using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Stiker (decal) dan motif (pattern) BoZo yang bisa dipilih di editor. Sumbernya prefab TexturePackage di
/// Resources/Decal dan Resources/Pattern. Hanya yang teksturnya bisa dimuat ulang oleh BMAC_SaveSystem
/// (Resources "Decal/&lt;nama&gt;" dan "Pattern/&lt;nama&gt;") yang ditampilkan, supaya hasil simpan selalu bisa dimuat.
/// </summary>
public static class TexturePackageCatalog {
    public readonly struct Entry {
        /// <summary>Stiker: nama tekstur (DecalRecord.decal). Motif: path Resources (OutfitRecord.pattern).</summary>
        public readonly string Id;
        public readonly string Label;
        public readonly Sprite Icon;
        public readonly Texture Texture;
        /// <summary>Batas ukuran motif dari BoZo (≤ 0 = tidak bisa diubah).</summary>
        public readonly float MaxScale;

        public Entry(string id, string label, Sprite icon, Texture texture, float maxScale) {
            Id = id;
            Label = label;
            Icon = icon;
            Texture = texture;
            MaxScale = maxScale;
        }
    }

    private const string DecalFolder = "Decal";
    private const string PatternFolder = "Pattern";
    /// Decal teknis BoZo (kilau rambut) yang bukan stiker.
    private static readonly string[] HiddenDecals = { "HairShine" };

    private static Entry[] _stickers;
    private static Entry[] _patterns;

    public static IReadOnlyList<Entry> Stickers => _stickers ??= Load(DecalFolder, TextureType.Decal, false);
    public static IReadOnlyList<Entry> Patterns => _patterns ??= Load(PatternFolder, TextureType.Pattern, true);

    public static bool TryGetSticker(string id, out Entry entry) => TryGet(Stickers, id, out entry);
    public static bool TryGetPattern(string id, out Entry entry) => TryGet(Patterns, id, out entry);

    /// <summary>Tekstur stiker untuk DecalData (sama dengan cara BMAC_SaveSystem memuatnya).</summary>
    public static Texture LoadDecalTexture(string decalName) {
        return string.IsNullOrEmpty(decalName) ? null : Resources.Load<Texture>(DecalFolder + "/" + decalName);
    }

    private static Entry[] Load(string folder, TextureType type, bool idIsPath) {
        var entries = new List<Entry>();
        var seen = new HashSet<string>();
        foreach (TexturePackage package in Resources.LoadAll<TexturePackage>(folder).OrderBy(p => p.name)) {
            if (package.type != type || package.texture == null) continue;
            string textureName = package.texture.name;
            if (HiddenDecals.Any(textureName.Contains) || !seen.Add(textureName)) continue;
            Texture loadable = Resources.Load<Texture>(folder + "/" + textureName);
            if (loadable == null) continue;
            string id = idIsPath ? folder + "/" + textureName : textureName;
            float maxScale = Mathf.Max(package.maxScale.x, package.maxScale.y);
            entries.Add(new Entry(id, Pretty(package.name, folder), package.icon, loadable, maxScale));
        }
        return entries.ToArray();
    }

    private static bool TryGet(IReadOnlyList<Entry> entries, string id, out Entry entry) {
        foreach (Entry candidate in entries) {
            if (candidate.Id != id) continue;
            entry = candidate;
            return true;
        }
        entry = default;
        return false;
    }

    /// "Decal_GoVistaG" → "GoVistaG"
    private static string Pretty(string name, string prefix) {
        string trimmed = name.StartsWith(prefix + "_") ? name.Substring(prefix.Length + 1) : name;
        return trimmed.Replace('_', ' ');
    }
}

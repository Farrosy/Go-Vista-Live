using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Akses ke outfit BoZo di Resources untuk customizer: daftar per tipe (untuk grid item), cek path
/// (whitelist CustomCharacterValidator), dan filter Anime/Stylized sesuai template (requiredTags BoZo).
/// Hasil dimuat sekali lalu di-cache.
/// </summary>
public static class OutfitCatalog {
    private const string AnimeTag = "Anime";
    private const string StylizedTag = "Stylized";

    private static readonly Dictionary<string, Outfit[]> ByType = new Dictionary<string, Outfit[]>();
    private static readonly Dictionary<string, bool> PathExists = new Dictionary<string, bool>();

    /// <summary>Semua outfit tipe ini yang tampil di creator dan cocok dengan gaya template.</summary>
    public static IReadOnlyList<Outfit> ForType(string outfitType, bool animeTemplate) {
        if (string.IsNullOrEmpty(outfitType)) return new Outfit[0];
        if (!ByType.TryGetValue(outfitType, out Outfit[] all)) {
            all = Resources.LoadAll<Outfit>(outfitType).Where(o => o.showCharacterCreator).OrderBy(o => o.name).ToArray();
            ByType[outfitType] = all;
        }
        string blocked = animeTemplate ? StylizedTag : AnimeTag;
        return all.Where(o => o.requiredTags == null || !o.requiredTags.Contains(blocked)).ToArray();
    }

    /// <summary>Path Resources (mis. "Top/BSMC_Top_Hoodie") menunjuk ke outfit BoZo yang ada.</summary>
    public static bool Exists(string path) {
        if (string.IsNullOrEmpty(path)) return false;
        if (!PathExists.TryGetValue(path, out bool exists)) {
            exists = Resources.Load<Outfit>(path) != null;
            PathExists[path] = exists;
        }
        return exists;
    }

    public static string PathOf(string outfitType, Outfit outfit) => outfitType + "/" + outfit.name;

    /// <summary>Tipe outfit dari path "Tipe/Nama"; kosong kalau formatnya salah.</summary>
    public static string TypeOf(string path) {
        if (string.IsNullOrEmpty(path)) return "";
        int slash = path.IndexOf('/');
        return slash <= 0 ? "" : path.Substring(0, slash);
    }
}

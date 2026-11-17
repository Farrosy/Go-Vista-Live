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

    /// <returns>Prefab outfit di path Resources ini, atau null.</returns>
    public static Outfit Load(string path) => string.IsNullOrEmpty(path) ? null : Resources.Load<Outfit>(path);

    /// <summary>Record outfit baru dengan warna bawaan outfit itu untuk semua bagian (lihat DefaultColor).</summary>
    public static OutfitRecord DefaultRecord(string outfitType, Outfit outfit) {
        var record = new OutfitRecord { path = PathOf(outfitType, outfit) };
        int channels = ChannelCount(outfit);
        Material material = SourceMaterial(outfit);
        for (int i = 0; i < channels; i++) record.colors.Add(CharacterRecordConverter.ToHex(DefaultColor(outfit, material, i)));
        return record;
    }

    /// Sama dengan BoZo: defaultColors kalau diisi (SetColorInital), selain itu warna _Color_N material outfit
    /// (Outfit.Awake). Hampir semua outfit BoZo defaultColors-nya kosong, jadi tanpa material warnanya putih polos.
    private static Color DefaultColor(Outfit outfit, Material material, int channel) {
        if (outfit.defaultColors != null && channel < outfit.defaultColors.Length) return outfit.defaultColors[channel];
        string property = "_Color_" + (channel + 1);
        if (material != null && material.HasProperty(property)) {
            Color color = material.GetColor(property);
            color.a = 1f;
            return color;
        }
        return Color.white;
    }

    private static Material SourceMaterial(Outfit outfit) {
        Renderer renderer = outfit != null ? outfit.GetComponentInChildren<Renderer>(true) : null;
        return renderer != null ? renderer.sharedMaterial : null;
    }

    /// <summary>Jumlah bagian warna yang bisa diubah (ColorChannels BoZo, minimal 1).</summary>
    public static int ChannelCount(Outfit outfit) {
        int count = outfit != null && outfit.ColorChannels != null ? outfit.ColorChannels.Length : 0;
        return Mathf.Clamp(count, 1, CustomizationRules.MaxColorChannels);
    }

    /// <summary>Tipe outfit dari path "Tipe/Nama"; kosong kalau formatnya salah.</summary>
    public static string TypeOf(string path) => RecordEdits.TypeOf(path);
}

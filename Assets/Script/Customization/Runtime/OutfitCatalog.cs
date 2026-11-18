using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Akses ke outfit BoZo di Resources untuk customizer: daftar per tipe (untuk grid item), cek path
/// (whitelist CustomCharacterValidator), dan kecocokan bagian wajah dengan gaya kepala yang dipakai
/// (FaceStyleRules). Hasil dimuat sekali lalu di-cache.
/// </summary>
public static class OutfitCatalog {
    private const string HeadType = "Head";

    private static readonly Dictionary<string, Outfit[]> ByType = new Dictionary<string, Outfit[]>();
    private static readonly Dictionary<string, bool> PathExists = new Dictionary<string, bool>();

    /// <summary>Semua outfit tipe ini yang tampil di creator (termasuk yang tidak cocok dengan kepala).</summary>
    public static IReadOnlyList<Outfit> ForType(string outfitType) {
        if (string.IsNullOrEmpty(outfitType)) return new Outfit[0];
        if (!ByType.TryGetValue(outfitType, out Outfit[] all)) {
            all = Resources.LoadAll<Outfit>(outfitType).Where(o => o.showCharacterCreator).OrderBy(o => o.name).ToArray();
            ByType[outfitType] = all;
        }
        return all;
    }

    /// <summary>Outfit tipe ini yang cocok dengan gaya kepala.</summary>
    public static IReadOnlyList<Outfit> ForType(string outfitType, bool animeHead) {
        return ForType(outfitType).Where(o => FaceStyleRules.Fits(RequiredStyle(outfitType, o), animeHead)).ToArray();
    }

    /// <summary>Gaya kepala yang dibutuhkan outfit ini (Any untuk selain bagian wajah).</summary>
    public static FaceStyle RequiredStyle(string outfitType, Outfit outfit) {
        return outfit == null ? FaceStyle.Any : FaceStyleRules.RequiredStyle(outfitType, outfit.requiredTags, outfit.tags);
    }

    /// <summary>Outfit di path ini cocok dengan gaya kepala (path tidak dikenal = cocok).</summary>
    public static bool Fits(string path, bool animeHead) {
        return FaceStyleRules.Fits(RequiredStyle(TypeOf(path), Load(path)), animeHead);
    }

    /// <summary>Kepala yang dipakai record ini bergaya anime (tag BoZo "Anime").</summary>
    public static bool IsAnimeHead(CustomCharacterRecord record) => IsAnimeHead(RecordEdits.Find(record, HeadType)?.path);

    public static bool IsAnimeHead(string headPath) {
        Outfit head = Load(headPath);
        return head != null && FaceStyleRules.IsAnimeHead(head.tags);
    }

    /// <summary>Record outfit dengan warna bawaan untuk path ini, atau null kalau outfit tidak ada.</summary>
    public static OutfitRecord DefaultRecord(string path) {
        Outfit outfit = Load(path);
        return outfit == null ? null : DefaultRecord(TypeOf(path), outfit);
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

    /// Outfit tekstur (make-up, detail wajah) tidak punya Renderer; warnanya ada di Outfit.material.
    private static Material SourceMaterial(Outfit outfit) {
        if (outfit == null) return null;
        Renderer renderer = outfit.GetComponentInChildren<Renderer>(true);
        return renderer != null ? renderer.sharedMaterial : outfit.material;
    }

    /// <summary>Jumlah bagian warna yang bisa diubah (ColorChannels BoZo, minimal 1).</summary>
    public static int ChannelCount(Outfit outfit) {
        int count = outfit != null && outfit.ColorChannels != null ? outfit.ColorChannels.Length : 0;
        return Mathf.Clamp(count, 1, CustomizationRules.MaxColorChannels);
    }

    /// <summary>Tipe outfit dari path "Tipe/Nama"; kosong kalau formatnya salah.</summary>
    public static string TypeOf(string path) => RecordEdits.TypeOf(path);
}

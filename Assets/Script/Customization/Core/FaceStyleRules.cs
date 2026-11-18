using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Gaya kepala yang dibutuhkan satu bagian wajah.</summary>
public enum FaceStyle { Any, Anime, Basic }

/// <summary>
/// Kecocokan bagian wajah dengan gaya kepala yang DIPAKAI (bukan gaya template), mengikuti tag BoZo:
/// kepala punya `tags` Anime/Stylized, bagian wajah punya `requiredTags` (memory: head-eye-compat-decisions).
/// Kepala Basic + mata anime = mata melotot/juling, jadi saat gaya kepala berganti mata ikut diganti.
/// Logika murni tanpa UnityEngine supaya bisa dites EditMode.
/// </summary>
public static class FaceStyleRules {
    public const string AnimeTag = "Anime";
    /// <summary>Tag BoZo untuk kepala Basic.</summary>
    public const string BasicTag = "Stylized";

    public const string AnimeEyes = "Eyes/Eyes_AnimeBasic";
    public const string AnimePupil = "Pupil/Pupil_Round";
    public const string BasicEyes = "Eyes/Eyes_BasicIris";
    public const string BasicPupil = "Pupil/Pupil_BasicPupil";

    private const string EyesType = "Eyes";
    private const string PupilType = "Pupil";

    /// Tipe di tab Wajah yang ikut aturan gaya kepala.
    private static readonly HashSet<string> FaceTypes = new HashSet<string> {
        EyesType, PupilType, "EyeShine", "EyeBrows", "EyeLashes", "MakeUpLips", "MakeUpCheeks", "MakeUpEyes", "FaceDetails"
    };

    /// <summary>Alis &amp; bulu mata hanya ada versi anime; dikembalikan saat kembali ke kepala anime.</summary>
    public static readonly IReadOnlyList<string> RestorableTypes = new[] { "EyeBrows", "EyeLashes" };

    /// <summary>
    /// Gaya kepala yang dibutuhkan item ini. requiredTags dulu; kalau kosong, `tags` gaya (BasicEyeLiner hanya
    /// memakai `tags`). Pengecualian: pupil Basic/Sharp juga cocok di mata anime (dicek visual), Round tetap anime.
    /// </summary>
    public static FaceStyle RequiredStyle(string outfitType, IEnumerable<string> requiredTags, IEnumerable<string> tags) {
        if (!FaceTypes.Contains(outfitType ?? "")) return FaceStyle.Any;
        FaceStyle style = StyleOf(requiredTags);
        if (style == FaceStyle.Any) style = StyleOf(tags);
        if (outfitType == PupilType && style == FaceStyle.Basic) return FaceStyle.Any;
        return style;
    }

    public static bool Fits(FaceStyle required, bool animeHead) {
        return required == FaceStyle.Any || required == (animeHead ? FaceStyle.Anime : FaceStyle.Basic);
    }

    public static bool IsAnimeHead(IEnumerable<string> headTags) => headTags != null && headTags.Contains(AnimeTag);

    /// <summary>Isi toast saat item yang tidak cocok diketuk.</summary>
    public static string IncompatibleMessage(string itemLabel, FaceStyle required) {
        return $"{itemLabel} hanya bisa dipakai di kepala {(required == FaceStyle.Anime ? "Anime" : "Basic")}";
    }

    /// <summary>
    /// Record setelah gaya kepala berganti (panggil hanya saat gaya berubah): mata + pupil diganti ke pasangan
    /// gaya baru (warna bawaan), bagian wajah lain yang tidak cocok dilepas, dan di kepala anime alis/bulu mata
    /// dari <paramref name="restore"/> dipasang lagi (nilai null = sebelumnya Kosong). Record masukan tidak diubah.
    /// </summary>
    /// <param name="fits">Apakah path outfit cocok dengan kepala baru.</param>
    /// <param name="defaultRecord">Record outfit baru dengan warna bawaannya (null kalau path tidak ada).</param>
    public static CustomCharacterRecord ForHeadStyle(CustomCharacterRecord record, bool animeHead, Func<string, bool> fits,
                                                     Func<string, OutfitRecord> defaultRecord,
                                                     IReadOnlyDictionary<string, OutfitRecord> restore) {
        if (record == null) return null;
        CustomCharacterRecord result = record;
        result = WithDefault(result, defaultRecord(animeHead ? AnimeEyes : BasicEyes));
        result = WithDefault(result, defaultRecord(animeHead ? AnimePupil : BasicPupil));
        foreach (OutfitRecord worn in result.outfits.ToList()) {
            string type = RecordEdits.TypeOf(worn?.path);
            if (FaceTypes.Contains(type) && !fits(worn.path)) result = RecordEdits.WithoutOutfit(result, type);
        }
        if (!animeHead || restore == null) return result;
        foreach (string type in RestorableTypes) {
            if (!restore.TryGetValue(type, out OutfitRecord outfit) || outfit == null || !fits(outfit.path)) continue;
            result = RecordEdits.WithOutfit(result, outfit);
        }
        return result;
    }

    /// <summary>Alis/bulu mata yang dipakai di record ini (null = Kosong), untuk dipasang lagi nanti.</summary>
    public static IReadOnlyDictionary<string, OutfitRecord> RememberRestorable(CustomCharacterRecord record) {
        var memory = new Dictionary<string, OutfitRecord>();
        foreach (string type in RestorableTypes) memory[type] = RecordEdits.Find(record, type)?.Clone();
        return memory;
    }

    private static CustomCharacterRecord WithDefault(CustomCharacterRecord record, OutfitRecord outfit) {
        return outfit == null ? record : RecordEdits.WithOutfit(record, outfit);
    }

    private static FaceStyle StyleOf(IEnumerable<string> tags) {
        if (tags == null) return FaceStyle.Any;
        if (tags.Contains(AnimeTag)) return FaceStyle.Anime;
        return tags.Contains(BasicTag) ? FaceStyle.Basic : FaceStyle.Any;
    }
}

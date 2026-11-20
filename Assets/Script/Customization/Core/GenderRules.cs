using System.Collections.Generic;

/// <summary>Untuk siapa outfit/rambut boleh dipakai.</summary>
public enum OutfitGender { Universal, Male, Female }

/// <summary>
/// Outfit khusus cowok / cewek (tahap 1e, checklist user 2026-11-18; memory: gender-outfit-tags). Outfit yang
/// tidak tercantum = universal. Hanya membatasi editor (item gender lain tampil abu-abu + toast);
/// CustomCharacterValidator sengaja tidak ikut. Path = path Resources BoZo "Tipe/Nama".
/// </summary>
public static class GenderRules {
    private static readonly Dictionary<string, OutfitGender> Restricted = new Dictionary<string, OutfitGender> {
        { "LowerFace/BSMC_LowerFace_BushyBeard", OutfitGender.Male },
        { "LowerFace/BSMC_LowerFace_ChinGoatee", OutfitGender.Male },
        { "LowerFace/BSMC_LowerFace_FullBeard", OutfitGender.Male },
        { "LowerFace/BSMC_LowerFace_Goatee", OutfitGender.Male },
        { "LowerFace/BSMC_LowerFace_Mustashe", OutfitGender.Male },
        { "LowerFace/BSMC_LowerFace_MustasheChin", OutfitGender.Male },

        { "Top/Outfit_Top_OpenShoulder", OutfitGender.Female },
        { "Top/Outfit_Top_OpenShoulder_Crop", OutfitGender.Female },
        { "Top/Outfit_Top_OpenShoulder_Straps", OutfitGender.Female },
        { "Top/Outfit_Top_OpenShoulder_Strapsless", OutfitGender.Female },
        { "Overall/BSMC_Overall_SmartDress", OutfitGender.Female },
        { "Overall/BSMC_Overall_Sundress", OutfitGender.Female },
        { "Bottom/BMSC_Bottom_YogaPants", OutfitGender.Female },
        { "Bottom/BSMC_Bottom_Jeans", OutfitGender.Female },
        { "Bottom/BSMC_Bottom_JeanSkirt", OutfitGender.Female },
        { "Bottom/BSMC_Bottom_PleatedSkirt", OutfitGender.Female },
        { "Bottom/BSMC_Bottom_Shorts", OutfitGender.Female },
        { "Bottom/BSMC_Bottom_ShortShorts", OutfitGender.Female },
        { "Bottom/BSMC_Bottom_Skirt", OutfitGender.Female },
        { "Leggings/Leggings_Stocking", OutfitGender.Female },
        { "Feet/BSMC_Feet_SlipOns", OutfitGender.Female },
        { "UnderUpper/UnderUpper_SimpleBra", OutfitGender.Female },
        { "UnderLower/UnderLower_SimplePanties", OutfitGender.Female },
    };

    public static OutfitGender GenderOf(string path) {
        return path != null && Restricted.TryGetValue(path, out OutfitGender gender) ? gender : OutfitGender.Universal;
    }

    public static bool Fits(string path, bool isMale) {
        OutfitGender gender = GenderOf(path);
        return gender == OutfitGender.Universal || gender == (isMale ? OutfitGender.Male : OutfitGender.Female);
    }

    /// <summary>Isi toast saat item gender lain diketuk.</summary>
    public static string BlockedMessage(string itemLabel, OutfitGender gender) {
        return $"{itemLabel} hanya untuk karakter {(gender == OutfitGender.Male ? "cowok" : "cewek")}";
    }
}

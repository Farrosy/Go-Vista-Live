using System.Collections.Generic;
using System.Text;

/// <summary>Satu slider di sub-tab Wajah · Bentuk.</summary>
public readonly struct FaceShapeEntry {
    public readonly string Key;
    public readonly string Label;
    /// <summary>Batas atas slider (50 untuk IrisSize/EyeLidHeight, selain itu 100).</summary>
    public readonly float Max;
    /// <summary>False = kepala ini tidak punya blendshape-nya: slider tampil abu-abu, tidak bisa digeser.</summary>
    public readonly bool Enabled;

    public FaceShapeEntry(string key, string label, float max, bool enabled) {
        Key = key;
        Label = label;
        Max = max;
        Enabled = enabled;
    }
}

/// <summary>
/// Daftar slider bentuk wajah. Kuncinya berasal dari blendshape kepala BoZo ("Shape_*"), jadi berbeda untuk
/// kepala Stylized dan Anime. Aturan (memory character-customizer-revamp, 2026-11-16):
///  - slider badan yang ikut ada di kepala disembunyikan; Stern hanya untuk template cowok;
///  - IrisSize, EyeLidHeight, Stern yang tidak dimiliki kepala ini TETAP tampil tapi nonaktif (tidak disembunyikan).
/// </summary>
public static class FaceShapeCatalog {
    /// Slider yang selalu tampil di atas (urutan tetap), walau kepala tidak punya blendshape-nya.
    private static readonly string[] AlwaysShown = { "IrisSize", "EyeLidHeight", "Stern" };

    private static readonly Dictionary<string, string> Labels = new Dictionary<string, string> {
        { "IrisSize", "Ukuran Iris" }, { "EyeLidHeight", "Kelopak Mata" }, { "Stern", "Stern" },
        { "Squareness", "Wajah Kotak" }, { "Sharpness", "Wajah Tajam" }, { "Roundness", "Wajah Bulat" },
        { "LashLength", "Panjang Bulu Mata" }, { "BrowsThickness", "Tebal Alis" },
        { "LowerBrows", "Alis Turun" }, { "RaiseBrows", "Alis Naik" },
        { "NoseBridgeCurve", "Lengkung Hidung" }, { "NoseWidth", "Lebar Hidung" },
        { "NoseTiltDown", "Hidung Menunduk" }, { "NoseTiltUp", "Hidung Mancung" },
        { "MouthWide", "Lebar Mulut" }, { "MouthWidth", "Lebar Mulut" }, { "MouthThin", "Bibir Tipis" },
        { "EyesOuterCornersLow", "Sudut Mata Turun" }, { "EyesOuterCornersHigh", "Sudut Mata Naik" },
        { "EyesSquare", "Mata Kotak" }, { "EyeRoundness", "Mata Bulat" },
        { "EarsElf", "Telinga Elf" }, { "EarAngle", "Sudut Telinga" }, { "EarLength", "Panjang Telinga" },
    };

    /// <param name="headShapes">Kunci blendshape wajah kepala yang sedang dipakai (OutfitSystem.GetFaceShapes).</param>
    public static List<FaceShapeEntry> Entries(IEnumerable<string> headShapes, bool isMale) {
        var available = new HashSet<string>(headShapes ?? new string[0]);
        var result = new List<FaceShapeEntry>();
        foreach (string key in AlwaysShown) {
            if (!CustomizationRules.IsFaceShapeVisible(key, isMale)) continue;
            result.Add(Entry(key, available.Contains(key)));
        }
        var rest = new List<string>();
        foreach (string key in available) {
            if (System.Array.IndexOf(AlwaysShown, key) >= 0 || !CustomizationRules.IsFaceShapeVisible(key, isMale)) continue;
            rest.Add(key);
        }
        rest.Sort(System.StringComparer.Ordinal);
        foreach (string key in rest) result.Add(Entry(key, true));
        return result;
    }

    /// <summary>Nama slider dalam bahasa Indonesia; kunci yang belum dikenal dipecah per kata ("NoseTip" → "Nose Tip").</summary>
    public static string Label(string key) {
        if (string.IsNullOrEmpty(key)) return "";
        return Labels.TryGetValue(key, out string label) ? label : SplitWords(key);
    }

    private static FaceShapeEntry Entry(string key, bool enabled) {
        return new FaceShapeEntry(key, Label(key), CustomizationRules.MaxFor(key), enabled);
    }

    private static string SplitWords(string key) {
        var builder = new StringBuilder(key.Length + 4);
        for (int i = 0; i < key.Length; i++) {
            if (i > 0 && char.IsUpper(key[i]) && !char.IsUpper(key[i - 1])) builder.Append(' ');
            builder.Append(key[i]);
        }
        return builder.ToString();
    }
}

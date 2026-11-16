using System;
using System.Collections.Generic;

/// <summary>
/// Perubahan kecil pada CustomCharacterRecord untuk editor (ganti/lepas outfit, warna per bagian, motif).
/// Selalu mengembalikan record BARU dan tidak mengubah record masukan. Kalau perubahan tidak valid
/// (warna salah, outfit tidak ada), record masukan yang sama dikembalikan, jadi CustomizationSession
/// tahu tidak ada yang berubah.
/// </summary>
public static class RecordEdits {
    /// <summary>Tipe outfit dari path Resources "Tipe/Nama"; kosong kalau formatnya salah.</summary>
    public static string TypeOf(string path) {
        if (string.IsNullOrEmpty(path)) return "";
        int slash = path.IndexOf('/');
        return slash <= 0 ? "" : path.Substring(0, slash);
    }

    /// <returns>Outfit tipe ini di record, atau null. Jangan diubah langsung, pakai fungsi With*.</returns>
    public static OutfitRecord Find(CustomCharacterRecord record, string outfitType) {
        if (record?.outfits == null) return null;
        foreach (OutfitRecord outfit in record.outfits) {
            if (outfit != null && TypeOf(outfit.path) == outfitType) return outfit;
        }
        return null;
    }

    /// <summary>Pasang outfit; outfit lain dengan tipe yang sama diganti.</summary>
    public static CustomCharacterRecord WithOutfit(CustomCharacterRecord record, OutfitRecord outfit) {
        string type = TypeOf(outfit?.path);
        if (record == null || type.Length == 0) return record;
        CustomCharacterRecord copy = record.Clone();
        int index = copy.outfits.FindIndex(o => o != null && TypeOf(o.path) == type);
        if (index >= 0) copy.outfits[index] = outfit.Clone();
        else copy.outfits.Add(outfit.Clone());
        return copy;
    }

    public static CustomCharacterRecord WithoutOutfit(CustomCharacterRecord record, string outfitType) {
        if (Find(record, outfitType) == null) return record;
        CustomCharacterRecord copy = record.Clone();
        copy.outfits.RemoveAll(o => o != null && TypeOf(o.path) == outfitType);
        return copy;
    }

    /// <param name="channel">Indeks bagian warna dari 0 (BoZo memakai _Color_1 untuk indeks 0).</param>
    public static CustomCharacterRecord WithChannelColor(CustomCharacterRecord record, string outfitType, int channel,
                                                         string hex) {
        if (channel < 0 || channel >= CustomizationRules.MaxColorChannels) return record;
        return EditOutfit(record, outfitType, hex, o => SetPadded(o.colors, channel, hex));
    }

    /// <param name="index">0..2 (CustomizationRules.PatternColorCount).</param>
    public static CustomCharacterRecord WithPatternColor(CustomCharacterRecord record, string outfitType, int index,
                                                         string hex) {
        if (index < 0 || index >= CustomizationRules.PatternColorCount) return record;
        return EditOutfit(record, outfitType, hex, o => SetPadded(o.patternColors, index, hex));
    }

    /// <param name="patternPath">Path tekstur motif di Resources; kosong = tanpa motif.</param>
    public static CustomCharacterRecord WithPattern(CustomCharacterRecord record, string outfitType, string patternPath) {
        if (Find(record, outfitType) == null) return record;
        CustomCharacterRecord copy = record.Clone();
        Find(copy, outfitType).pattern = patternPath ?? "";
        return copy;
    }

    private static CustomCharacterRecord EditOutfit(CustomCharacterRecord record, string outfitType, string hex,
                                                    Action<OutfitRecord> edit) {
        if (!HexColor.IsValid(hex) || Find(record, outfitType) == null) return record;
        CustomCharacterRecord copy = record.Clone();
        edit(Find(copy, outfitType));
        return copy;
    }

    private static void SetPadded(List<string> colors, int index, string hex) {
        while (colors.Count <= index) colors.Add(HexColor.White);
        colors[index] = hex.ToUpperInvariant();
    }
}

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

    /// <summary>Ukuran motif (sama untuk X dan Y), di-clamp ke CustomizationRules.</summary>
    public static CustomCharacterRecord WithPatternScale(CustomCharacterRecord record, string outfitType, float scale) {
        OutfitRecord current = Find(record, outfitType);
        float clamped = CustomizationRules.ClampPatternScale(scale);
        if (current == null || (current.patternScaleX == clamped && current.patternScaleY == clamped)) return record;
        CustomCharacterRecord copy = record.Clone();
        OutfitRecord outfit = Find(copy, outfitType);
        outfit.patternScaleX = clamped;
        outfit.patternScaleY = clamped;
        return copy;
    }

    // ---------------------------------------------------------------- Wajah

    /// <summary>Nilai slider wajah (di-clamp sesuai CustomizationRules). Kunci baru ditambahkan.</summary>
    public static CustomCharacterRecord WithFaceShape(CustomCharacterRecord record, string key, float value) {
        if (record == null || string.IsNullOrEmpty(key) || float.IsNaN(value)) return record;
        float clamped = CustomizationRules.ClampFaceShape(key, value);
        ShapeRecord existing = record.faceShapes?.Find(s => s != null && s.key == key);
        if (existing != null && existing.value == clamped) return record;
        CustomCharacterRecord copy = record.Clone();
        ShapeRecord shape = copy.faceShapes.Find(s => s != null && s.key == key);
        if (shape != null) shape.value = clamped;
        else copy.faceShapes.Add(new ShapeRecord { key = key, value = clamped });
        return copy;
    }

    /// <returns>Nilai slider di record, atau <paramref name="fallback"/> kalau belum ada.</returns>
    public static float FaceShape(CustomCharacterRecord record, string key, float fallback) {
        ShapeRecord shape = record?.faceShapes?.Find(s => s != null && s.key == key);
        return shape != null ? shape.value : fallback;
    }

    // ---------------------------------------------------------------- Stiker

    /// <summary>Tambah stiker baru di posisi Dada; tidak berubah kalau sudah CustomizationRules.MaxDecals.</summary>
    public static CustomCharacterRecord WithDecalAdded(CustomCharacterRecord record, string decalName) {
        if (record == null || string.IsNullOrEmpty(decalName)) return record;
        if ((record.decals?.Count ?? 0) >= CustomizationRules.MaxDecals) return record;
        CustomCharacterRecord copy = record.Clone();
        copy.decals.Add(new DecalRecord { decal = decalName });
        return copy;
    }

    /// <summary>Ganti stiker ke-<paramref name="index"/>; nilai di-clamp, posisi/warna tidak valid diabaikan.</summary>
    public static CustomCharacterRecord WithDecal(CustomCharacterRecord record, int index, DecalRecord decal) {
        if (record?.decals == null || decal == null || index < 0 || index >= record.decals.Count) return record;
        if (string.IsNullOrEmpty(decal.decal) || !CustomizationRules.IsDecalPosition(decal.position) ||
            !HexColor.IsValid(decal.color)) return record;
        DecalRecord clean = new DecalRecord {
            decal = decal.decal,
            position = decal.position,
            size = CustomizationRules.ClampDecalSize(decal.size),
            rotation = CustomizationRules.ClampDecalRotation(decal.rotation),
            color = decal.color.ToUpperInvariant(),
            offsetX = CustomizationRules.ClampDecalOffset(decal.offsetX),
            offsetY = CustomizationRules.ClampDecalOffset(decal.offsetY)
        };
        DecalRecord current = record.decals[index];
        if (current != null && current.decal == clean.decal && current.position == clean.position &&
            current.size == clean.size && current.rotation == clean.rotation && current.color == clean.color &&
            current.offsetX == clean.offsetX && current.offsetY == clean.offsetY)
            return record;
        CustomCharacterRecord copy = record.Clone();
        copy.decals[index] = clean;
        return copy;
    }

    public static CustomCharacterRecord WithoutDecal(CustomCharacterRecord record, int index) {
        if (record?.decals == null || index < 0 || index >= record.decals.Count) return record;
        CustomCharacterRecord copy = record.Clone();
        copy.decals.RemoveAt(index);
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

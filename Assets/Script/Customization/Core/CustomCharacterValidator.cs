using System;
using System.Collections.Generic;

/// <summary>
/// Membersihkan CustomCharacterRecord dari luar (Cloud Save, atau nanti data pemain lain lewat jaringan):
/// template harus dikenal, outfit harus ada di whitelist, warna harus hex valid, slider di-clamp dan
/// disaring sesuai gender template, decal dibatasi. Tidak mengubah input; selalu mengembalikan salinan baru.
/// </summary>
public static class CustomCharacterValidator {
    /// <param name="isOutfitAllowed">Whitelist path outfit (diisi katalog BoZo saat runtime).</param>
    /// <returns>Salinan bersih, atau null kalau template tidak dikenal (pemanggil memakai template asli).</returns>
    public static CustomCharacterRecord Sanitize(CustomCharacterRecord record, Func<string, bool> isOutfitAllowed) {
        if (record == null || !CharacterTemplates.TryGet(record.templateId, out CharacterTemplate template)) return null;

        return new CustomCharacterRecord {
            version = CustomCharacterRecord.CurrentVersion,
            templateId = template.Id,
            outfits = SanitizeOutfits(record.outfits, isOutfitAllowed),
            decals = SanitizeDecals(record.decals),
            faceShapes = SanitizeShapes(record.faceShapes, template.IsMale)
        };
    }

    private static List<OutfitRecord> SanitizeOutfits(List<OutfitRecord> outfits, Func<string, bool> isAllowed) {
        var result = new List<OutfitRecord>();
        if (outfits == null) return result;
        foreach (OutfitRecord outfit in outfits) {
            if (outfit == null || string.IsNullOrEmpty(outfit.path) || !isAllowed(outfit.path)) continue;
            result.Add(new OutfitRecord {
                path = outfit.path,
                colors = SanitizeColors(outfit.colors, CustomizationRules.MaxColorChannels),
                pattern = outfit.pattern ?? "",
                patternColors = SanitizeColors(outfit.patternColors, CustomizationRules.PatternColorCount),
                patternScaleX = Positive(outfit.patternScaleX),
                patternScaleY = Positive(outfit.patternScaleY)
            });
        }
        return result;
    }

    private static List<DecalRecord> SanitizeDecals(List<DecalRecord> decals) {
        var result = new List<DecalRecord>();
        if (decals == null) return result;
        foreach (DecalRecord decal in decals) {
            if (result.Count >= CustomizationRules.MaxDecals) break;
            if (decal == null || string.IsNullOrEmpty(decal.decal)) continue;
            if (!CustomizationRules.IsDecalPosition(decal.position)) continue;
            result.Add(new DecalRecord {
                decal = decal.decal,
                position = decal.position,
                size = CustomizationRules.ClampDecalSize(decal.size),
                rotation = CustomizationRules.ClampDecalRotation(decal.rotation),
                color = HexColor.SanitizeOrWhite(decal.color)
            });
        }
        return result;
    }

    private static List<ShapeRecord> SanitizeShapes(List<ShapeRecord> shapes, bool isMale) {
        var result = new List<ShapeRecord>();
        if (shapes == null) return result;
        foreach (ShapeRecord shape in shapes) {
            if (shape == null || !CustomizationRules.IsFaceShapeVisible(shape.key, isMale)) continue;
            result.Add(new ShapeRecord { key = shape.key, value = CustomizationRules.ClampFaceShape(shape.key, shape.value) });
        }
        return result;
    }

    private static List<string> SanitizeColors(List<string> colors, int max) {
        var result = new List<string>();
        if (colors == null) return result;
        for (int i = 0; i < colors.Count && i < max; i++) result.Add(HexColor.SanitizeOrWhite(colors[i]));
        return result;
    }

    private static float Positive(float value) => float.IsNaN(value) || value <= 0f ? 1f : value;
}

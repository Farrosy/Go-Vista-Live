using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// CustomCharacterRecord (data kita, disimpan di Cloud Save) ↔ CharacterData (format BoZo untuk dimuat ke
/// OutfitSystem). Bentuk badan, body mod, stance, dan material selalu diambil dari template, karena badan
/// dikunci; record hanya membawa outfit, warna, decal, dan slider wajah.
/// </summary>
public static class CharacterRecordConverter {
    /// <summary>Record awal dari template: semua outfit + warna template, tanpa decal tambahan.</summary>
    public static CustomCharacterRecord FromTemplate(CharacterTemplate template) {
        CharacterObject source = LoadTemplate(template);
        if (source == null) return null;
        return new CustomCharacterRecord {
            templateId = template.Id,
            outfits = source.data.outfitDatas.Select(ToOutfitRecord).ToList(),
            decals = TemplateDecals(source.data.outfitDatas),
            faceShapes = ToShapes(source.data.faceIDs, source.data.faceShapes)
        };
    }

    /// Decal bawaan template (mis. logo Go Vista di hoodie) dipetakan ke preset dengan tulang yang sama.
    private static List<DecalRecord> TemplateDecals(List<OutfitData> outfits) {
        var decals = new List<DecalRecord>();
        foreach (OutfitData outfit in outfits) {
            if (outfit.decalDatas == null) continue;
            foreach (DecalData data in outfit.decalDatas) {
                string position = DecalPresets.PositionForBone(data.parent);
                if (position == null || string.IsNullOrEmpty(data.decal)) continue;
                decals.Add(new DecalRecord {
                    decal = data.decal, position = position, size = data.scale, rotation = data.roll, color = ToHex(data.color)
                });
            }
        }
        return decals;
    }

    /// <returns>null kalau template BoZo tidak ditemukan.</returns>
    public static CharacterData ToCharacterData(CustomCharacterRecord record) {
        if (record == null || !CharacterTemplates.TryGet(record.templateId, out CharacterTemplate template)) return null;
        CharacterObject source = LoadTemplate(template);
        if (source == null) return null;

        List<OutfitData> outfits = record.outfits.Select(ToOutfitData).ToList();
        AttachDecals(outfits, record.decals);
        var data = new CharacterData(source.data) {
            characterName = template.Id + "_custom",
            outfitDatas = outfits,
            material = source.data.material
        };
        ApplyFaceShapes(data, record.faceShapes);
        return data;
    }

    public static CharacterObject LoadTemplate(CharacterTemplate template) {
        var source = Resources.Load<CharacterObject>(template.BozoId);
        if (source == null) Debug.LogError($"[Customizer] Template BoZo '{template.BozoId}' tidak ada di Resources.");
        return source;
    }

    private static OutfitRecord ToOutfitRecord(OutfitData data) {
        return new OutfitRecord {
            path = data.outfit ?? "",
            colors = (data.colors ?? new List<Color>()).Select(ToHex).ToList(),
            pattern = data.pattern ?? "",
            patternColors = (data.patternColors ?? new List<Color>()).Select(ToHex).ToList(),
            patternScaleX = data.patternScale.x > 0f ? data.patternScale.x : 1f,
            patternScaleY = data.patternScale.y > 0f ? data.patternScale.y : 1f
        };
    }

    private static OutfitData ToOutfitData(OutfitRecord record) {
        bool hasPattern = !string.IsNullOrEmpty(record.pattern);
        return new OutfitData {
            outfit = record.path,
            colors = record.colors.Select(FromHex).ToList(),
            decal = "",
            decalColors = new List<Color>(),
            pattern = record.pattern ?? "",
            patternColors = record.patternColors.Select(FromHex).ToList(),
            patternScale = hasPattern ? new Vector4(record.patternScaleX, record.patternScaleY, 0f, 0f) : Vector4.zero,
            decalDatas = new List<DecalData>()
        };
    }

    /// Decal menempel ke outfit pembawa pertama yang dipakai (lihat DecalPresets.HostTypes).
    private static void AttachDecals(List<OutfitData> outfits, List<DecalRecord> decals) {
        foreach (DecalRecord decal in decals) {
            if (!DecalPresets.TryGet(decal.position, out DecalPresets.Preset preset)) continue;
            int host = FindHost(outfits, preset.HostTypes);
            if (host < 0) continue;
            OutfitData data = outfits[host];
            data.decalDatas.Add(DecalPresets.ToDecalData(decal, preset));
            outfits[host] = data;
        }
    }

    private static int FindHost(List<OutfitData> outfits, string[] hostTypes) {
        foreach (string type in hostTypes) {
            int index = outfits.FindIndex(o => OutfitCatalog.TypeOf(o.outfit) == type);
            if (index >= 0) return index;
        }
        return -1;
    }

    private static void ApplyFaceShapes(CharacterData data, List<ShapeRecord> shapes) {
        var ids = new List<string>(data.faceIDs ?? new List<string>());
        var values = new List<float>(data.faceShapes ?? new List<float>());
        foreach (ShapeRecord shape in shapes) {
            int index = ids.IndexOf(shape.key);
            if (index >= 0) values[index] = shape.value;
            else {
                ids.Add(shape.key);
                values.Add(shape.value);
            }
        }
        data.faceIDs = ids;
        data.faceShapes = values;
    }

    private static List<ShapeRecord> ToShapes(List<string> ids, List<float> values) {
        var shapes = new List<ShapeRecord>();
        if (ids == null || values == null) return shapes;
        for (int i = 0; i < ids.Count && i < values.Count; i++) shapes.Add(new ShapeRecord { key = ids[i], value = values[i] });
        return shapes;
    }

    public static string ToHex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

    public static Color FromHex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
}

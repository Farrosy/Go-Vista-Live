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
            decals = TemplateDecals(source.data.outfitDatas, template.IsMale),
            faceShapes = ToShapes(source.data.faceIDs, source.data.faceShapes)
        };
    }

    /// Decal bawaan template (mis. logo Go Vista di hoodie) dipetakan ke preset dengan tulang yang sama.
    private static List<DecalRecord> TemplateDecals(List<OutfitData> outfits, bool isMale) {
        var decals = new List<DecalRecord>();
        foreach (OutfitData outfit in outfits) {
            if (outfit.decalDatas == null) continue;
            foreach (DecalData data in outfit.decalDatas) {
                string position = DecalPresets.PositionForBone(data.parent);
                if (position == null || string.IsNullOrEmpty(data.decal) ||
                    !DecalPresets.TryGet(position, out DecalPresets.Preset preset)) continue;
                // Posisi persis template disimpan sebagai geser XYZ dari titik preset.
                Vector3 offset = DecalPresets.OffsetFor(data, preset, isMale);
                decals.Add(new DecalRecord {
                    decal = data.decal, position = position, size = data.scale / preset.SizeScale, rotation = data.roll,
                    color = ToHex(data.color), offsetX = offset.x, offsetY = offset.y
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
        AttachDecals(outfits, record.decals, template.IsMale);
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
    private static void AttachDecals(List<OutfitData> outfits, List<DecalRecord> decals, bool isMale) {
        Dictionary<string, List<DecalData>> byHost =
            DecalsByHost(outfits.Select(o => o.outfit), decals, isMale, loadTextures: false);
        for (int i = 0; i < outfits.Count; i++) {
            if (!byHost.TryGetValue(OutfitCatalog.TypeOf(outfits[i].outfit), out List<DecalData> list)) continue;
            OutfitData data = outfits[i];
            data.decalDatas.AddRange(list);
            outfits[i] = data;
        }
    }

    /// <summary>
    /// Stiker record dikelompokkan per tipe outfit pembawa. Pembawa = tipe pertama di preset yang sedang
    /// dipakai DAN mendukung decal (supportDecals), mis. Dada → Top, lalu Overall, lalu Body. Preset dengan
    /// AllHosts (lengan, kaki, wajah) dipasang ke semua pembawa yang dipakai.
    /// </summary>
    /// <param name="wornPaths">Path outfit yang sedang dipakai.</param>
    /// <param name="loadTextures">Isi DecalData.decalTexture (untuk update avatar live; saat load, BoZo memuatnya sendiri).</param>
    /// <param name="isMale">Template cowok/cewek: titik preset Dada berbeda.</param>
    public static Dictionary<string, List<DecalData>> DecalsByHost(IEnumerable<string> wornPaths, IEnumerable<DecalRecord> decals,
                                                                  bool isMale, bool loadTextures) {
        var hosts = new HashSet<string>();
        foreach (string path in wornPaths) {
            Outfit prefab = OutfitCatalog.Load(path);
            if (prefab != null && prefab.supportDecals) hosts.Add(OutfitCatalog.TypeOf(path));
        }
        var result = new Dictionary<string, List<DecalData>>();
        foreach (DecalRecord decal in decals ?? Enumerable.Empty<DecalRecord>()) {
            if (decal == null || !DecalPresets.TryGet(decal.position, out DecalPresets.Preset preset)) continue;
            DecalData data = DecalPresets.ToDecalData(decal, preset, isMale);
            if (loadTextures) data.decalTexture = TexturePackageCatalog.LoadDecalTexture(decal.decal);
            IEnumerable<string> worn = preset.HostTypes.Where(hosts.Contains);
            foreach (string host in preset.AllHosts ? worn : worn.Take(1)) {
                if (!result.TryGetValue(host, out List<DecalData> list)) result[host] = list = new List<DecalData>();
                list.Add(data);
            }
        }
        return result;
    }

    /// <summary>Bagian badan (bukan baju): ganti kepala/badan tidak membuang stiker.</summary>
    private static readonly HashSet<string> BodyPartTypes = new HashSet<string> { "Head", "Body" };

    /// <summary>
    /// Record tanpa stiker yang sedang menempel di outfit tipe <paramref name="outfitType"/> (dihitung dari
    /// outfit yang dipakai di record ini, jadi panggil SEBELUM outfit itu diganti/dilepas). Stiker tidak
    /// pindah ke baju baru: ganti baju = stikernya hilang, baik stiker bawaan template maupun yang dipasang
    /// pemain. Kepala dan badan bukan baju, jadi tidak membuang stiker.
    /// </summary>
    public static CustomCharacterRecord WithoutDecalsOn(CustomCharacterRecord record, string outfitType) {
        if (record?.decals == null || record.decals.Count == 0 || BodyPartTypes.Contains(outfitType ?? "")) return record;
        List<string> worn = record.outfits.Where(o => o != null).Select(o => o.path).ToList();
        return RecordEdits.WithoutDecals(record, decal =>
            DecalsByHost(worn, new[] { decal }, isMale: true, loadTextures: false).ContainsKey(outfitType));
    }

    /// <summary>
    /// Pasang outfit + buang stiker di baju lama tipe itu, DAN stiker yang akan pindah ke baju baru (mis. stiker
    /// Dada di kulit saat tanpa atasan, lalu atasan dipasang). Ganti baju selalu mulai tanpa stiker.
    /// </summary>
    public static CustomCharacterRecord WithOutfitClearingDecals(CustomCharacterRecord record, OutfitRecord outfit) {
        string type = OutfitCatalog.TypeOf(outfit?.path);
        CustomCharacterRecord worn = RecordEdits.WithOutfit(WithoutDecalsOn(record, type), outfit);
        return WithoutDecalsOn(worn, type);
    }

    /// <summary>Lepas outfit + buang stiker yang menempel di situ (tidak pindah ke kulit).</summary>
    public static CustomCharacterRecord WithoutOutfitClearingDecals(CustomCharacterRecord record, string outfitType) {
        if (RecordEdits.Find(record, outfitType) == null) return record;
        return RecordEdits.WithoutOutfit(WithoutDecalsOn(record, outfitType), outfitType);
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

using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Mengubah avatar BoZo yang sedang tampil di panggung editor TANPA merakit ulang (merakit ulang ~1 detik).
/// Semua lewat jalur update tekstur/blendshape BoZo yang sama dengan creator aslinya:
///  - warna   → Outfit.SetColor (BoZo ikut menyalin ke tipe tertaut, mis. kulit Body ↔ Head);
///  - stiker  → Outfit.decalDatas lalu OnColorChanged (bake ulang tekstur);
///  - motif   → Outfit.SetPattern / SetPatternSize;
///  - wajah   → OutfitSystem.SetShape;
///  - ekspresi → bobot blendshape "Expression_*" langsung di renderer (FaceExpressions).
/// </summary>
public static class LiveAvatarEdits {
    /// <summary>Warna semua bagian dari record ke instance outfit (bagian 1 = indeks 0).</summary>
    public static void ApplyColors(Outfit outfit, OutfitRecord record) {
        if (!outfit || record?.colors == null) return;
        for (int i = 0; i < record.colors.Count; i++) outfit.SetColor(CharacterRecordConverter.FromHex(record.colors[i]), i + 1);
    }

    public static void ApplyPattern(Outfit outfit, OutfitRecord record) {
        if (!outfit || record == null) return;
        Texture texture = TexturePackageCatalog.TryGetPattern(record.pattern, out TexturePackageCatalog.Entry entry)
            ? entry.Texture : null;
        outfit.SetPattern(texture);
        outfit.SetPatternSize(texture ? new Vector2(record.patternScaleX, record.patternScaleY) : Vector2.zero);
    }

    /// <summary>
    /// Pasang ulang semua stiker record ke outfit pembawanya; outfit lain dikosongkan. Panggil saat avatar
    /// tidak sibuk (BoZo melewatkan update tekstur kalau sedang merge/update).
    /// </summary>
    public static void ApplyDecals(OutfitSystem avatar, CustomCharacterRecord record) {
        if (!avatar || record == null) return;
        Dictionary<string, List<DecalData>> byHost =
            CharacterRecordConverter.DecalsByHost(record.outfits.Select(o => o.path), record.decals, IsMale(record),
                                                  loadTextures: true);
        Outfit changed = null;
        foreach (Outfit outfit in avatar.GetOutfits()) {
            if (!outfit || !outfit.supportDecals || outfit.Type == null) continue;
            byHost.TryGetValue(outfit.Type.name, out List<DecalData> wanted);
            wanted ??= new List<DecalData>();
            if (wanted.Count == 0 && (outfit.decalDatas == null || outfit.decalDatas.Count == 0)) continue;
            outfit.decalDatas = wanted;
            changed = outfit;
        }
        if (!changed) return;
        // Satu pemicu saja: update kedua dilewati BoZo selama yang pertama berjalan. Body (materialPriority ≠ 0)
        // menggabung ulang SEMUA tekstur + decal; SetPattern dengan motif yang sama hanya memicu OnColorChanged.
        Outfit trigger = avatar.GetOutfit("Body");
        if (!trigger || trigger.materialPriority == 0) trigger = changed;
        trigger.SetPattern(trigger.GetPattern());
    }

    private static bool IsMale(CustomCharacterRecord record) {
        return !CharacterTemplates.TryGet(record.templateId, out CharacterTemplate template) || template.IsMale;
    }

    /// <summary>Semua slider wajah record ke avatar (dipakai lagi setelah kepala diganti).</summary>
    public static void ApplyFaceShapes(OutfitSystem avatar, CustomCharacterRecord record) {
        if (!avatar || record?.faceShapes == null) return;
        foreach (ShapeRecord shape in record.faceShapes) {
            if (shape != null) avatar.SetShape(shape.key, shape.value);
        }
    }

    /// <summary>
    /// Ekspresi wajah ke semua renderer avatar yang punya blendshape "Expression_*" (kepala terpisah di panggung
    /// editor, atau mesh gabungan hasil merge). Blendshape ekspresi lain dikembalikan ke 0 dulu.
    /// </summary>
    public static void ApplyExpression(GameObject avatar, int expression) {
        if (!avatar) return;
        IReadOnlyDictionary<string, float> weights = FaceExpressions.Weights(expression);
        foreach (SkinnedMeshRenderer renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            Mesh mesh = renderer.sharedMesh;
            if (!mesh) continue;
            for (int i = 0; i < mesh.blendShapeCount; i++) {
                string key = FaceExpressions.KeyOf(mesh.GetBlendShapeName(i));
                if (key == null) continue;
                renderer.SetBlendShapeWeight(i, weights.TryGetValue(key, out float weight) ? weight : 0f);
            }
        }
    }

    /// <summary>Tipe outfit yang warnanya tertaut ke outfit ini (BoZo LinkedColorSets), dengan jumlah bagiannya.</summary>
    public static IEnumerable<(string type, int channels)> LinkedTypes(Outfit prefab) {
        if (prefab == null || prefab.LinkedColorSets == null) yield break;
        foreach (var link in prefab.LinkedColorSets) {
            if (link.linkedType != null) yield return (link.linkedType.name, link.linkedChannelRange);
        }
    }
}

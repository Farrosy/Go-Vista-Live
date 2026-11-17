using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Tombol ACAK di editor: memilih outfit dan warna acak untuk tab pakaian (Rambut, Atasan, Bawahan, Kaki,
/// Aksesori). Kepala, badan, mata, kulit, wajah, dan pakaian dalam tidak diubah; stiker di baju yang diganti
/// ikut hilang (sama seperti ganti baju manual). Aksesori opsional
/// (topi, kacamata, jenggot, dll.) hanya dipakai sesekali supaya hasilnya tidak ramai.
/// </summary>
public static class CustomizerRandomizer {
    private const float OptionalChance = 0.25f;
    private static readonly HashSet<string> OptionalTypes = new HashSet<string> {
        "Overall", "Leggings", "Socks", "Hat", "UpperFace", "LowerFace", "Gloves", "HeadAcc"
    };
    private static readonly HashSet<string> KeptCategories = new HashSet<string> { "Dalaman" };
    private const string BeardType = "LowerFace";

    public static CustomCharacterRecord Randomize(CustomCharacterRecord record, CharacterTemplate template) {
        CustomCharacterRecord result = record;
        foreach (EditorCategory category in EditorCategories.All) {
            if (category.Kind != EditorCategoryKind.Outfits || KeptCategories.Contains(category.Label)) continue;
            foreach (EditorSubTab tab in category.SubTabs) {
                foreach (string type in tab.OutfitTypes) result = RandomizeType(result, type, template);
            }
        }
        return result;
    }

    private static CustomCharacterRecord RandomizeType(CustomCharacterRecord record, string type, CharacterTemplate template) {
        IReadOnlyList<Outfit> options = OutfitCatalog.ForType(type, template.IsAnime);
        bool canBeEmpty = CustomizationRules.CanBeEmpty(type, template.IsMale);
        bool skip = options.Count == 0 || (type == BeardType && !template.IsMale) ||
                    (canBeEmpty && OptionalTypes.Contains(type) && Random.value > OptionalChance);
        if (skip) return canBeEmpty ? CharacterRecordConverter.WithoutOutfitClearingDecals(record, type) : record;

        Outfit outfit = options[Random.Range(0, options.Count)];
        OutfitRecord outfitRecord = OutfitCatalog.DefaultRecord(type, outfit);
        for (int i = 0; i < outfitRecord.colors.Count; i++) outfitRecord.colors[i] = CharacterRecordConverter.ToHex(NiceColor());
        return CharacterRecordConverter.WithOutfitClearingDecals(record, outfitRecord);
    }

    /// Warna acak yang enak dilihat: tidak terlalu pucat dan tidak terlalu gelap.
    private static Color NiceColor() => Color.HSVToRGB(Random.value, Random.Range(0.35f, 0.85f), Random.Range(0.35f, 0.95f));
}

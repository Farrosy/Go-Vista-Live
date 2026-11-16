using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Tahap 0 (tes bake): membuat CharacterData acak dari salah satu dari 4 template BoZo.
/// Rambut, atasan, bawahan, dan sepatu diacak, setiap bagian warnanya diberi warna acak, dan
/// (opsional) atasan diberi decal serta bawahan diberi pattern supaya jalur bake terberat ikut teruji.
/// Hanya dipakai scene AvatarBakeTest, bukan bagian dari game.
/// </summary>
public static class RandomCharacterFactory {
    public static readonly string[] TemplateIds = {
        "1DefaultMale", "1DefaultFemale", "0_DefaultAnimeMale", "0_DefaultAnimeFemale"
    };

    private static readonly string[] RandomizedTypes = { "Top", "Bottom", "Feet", "HairFront", "HairBack" };
    private const string AnimeTag = "Anime";
    private const string StylizedTag = "Stylized";
    private const string DecalName = "Decal_GoVistaG";
    private const string DecalBone = "spine_04";
    private static readonly Vector3 DecalPosition = new Vector3(0.087f, 0.116f, -0.05f);
    private const float DecalScale = 0.15f;
    private const string PatternPath = "Pattern/Pattern_Plaid";
    private static readonly Vector4 PatternScale = new Vector4(2f, 2f, 0f, 0f);
    private const int ColorChannelCount = 9;
    private const int PatternColorCount = 3;

    private static readonly Dictionary<string, Outfit[]> Catalog = new Dictionary<string, Outfit[]>();

    /// <returns>null kalau template tidak ditemukan di Resources.</returns>
    public static CharacterData Create(System.Random rng, bool withDecalAndPattern) {
        string templateId = TemplateIds[rng.Next(TemplateIds.Length)];
        var template = Resources.Load<CharacterObject>(templateId);
        if (template == null) {
            Debug.LogError($"[BakeTest] Template '{templateId}' tidak ditemukan di Resources.");
            return null;
        }

        bool anime = templateId.Contains(AnimeTag);
        var outfits = template.data.outfitDatas
            .Select(source => Randomize(source, rng, anime, withDecalAndPattern))
            .ToList();

        return new CharacterData(template.data) {
            characterName = templateId + "_Random",
            outfitDatas = outfits,
            material = template.data.material
        };
    }

    private static OutfitData Randomize(OutfitData source, System.Random rng, bool anime, bool withExtras) {
        string type = TypeOf(source.outfit);
        if (!RandomizedTypes.Contains(type)) return source;

        Outfit[] choices = ChoicesFor(type, anime);
        if (choices.Length == 0) return source;

        Outfit pick = choices[rng.Next(choices.Length)];
        var result = new OutfitData(source) {
            outfit = type + "/" + pick.name,
            colors = RandomColors(rng, ColorChannelCount),
            decal = "",
            decalColors = new List<Color>(),
            pattern = "",
            patternColors = new List<Color>(),
            partVisibility = null,
            decalDatas = new List<DecalData>()
        };

        if (withExtras && type == "Top") result.decalDatas.Add(RandomDecal(rng));
        if (withExtras && type == "Bottom") {
            result.pattern = PatternPath;
            result.patternColors = RandomColors(rng, PatternColorCount);
            result.patternScale = PatternScale;
        }
        return result;
    }

    private static DecalData RandomDecal(System.Random rng) {
        return new DecalData(DecalName, DecalBone, DecalPosition, 0f, 0f, 0f, DecalScale, RandomColor(rng));
    }

    private static Outfit[] ChoicesFor(string type, bool anime) {
        if (!Catalog.TryGetValue(type, out Outfit[] all)) {
            all = Resources.LoadAll<Outfit>(type).Where(o => o.showCharacterCreator).ToArray();
            Catalog[type] = all;
        }
        string blocked = anime ? StylizedTag : AnimeTag;
        return all.Where(o => o.requiredTags == null || !o.requiredTags.Contains(blocked)).ToArray();
    }

    private static string TypeOf(string outfitPath) {
        if (string.IsNullOrEmpty(outfitPath)) return "";
        int slash = outfitPath.IndexOf('/');
        return slash < 0 ? "" : outfitPath.Substring(0, slash);
    }

    private static List<Color> RandomColors(System.Random rng, int count) {
        var colors = new List<Color>(count);
        for (int i = 0; i < count; i++) colors.Add(RandomColor(rng));
        return colors;
    }

    private static Color RandomColor(System.Random rng) {
        float hue = (float)rng.NextDouble();
        float saturation = 0.3f + 0.6f * (float)rng.NextDouble();
        float value = 0.35f + 0.6f * (float)rng.NextDouble();
        return Color.HSVToRGB(hue, saturation, value);
    }
}

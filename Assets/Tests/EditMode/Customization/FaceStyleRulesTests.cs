using System.Collections.Generic;
using NUnit.Framework;

public class FaceStyleRulesTests {
    private static readonly string[] None = new string[0];
    private static readonly string[] Anime = { FaceStyleRules.AnimeTag };
    private static readonly string[] Basic = { FaceStyleRules.BasicTag };

    // ------------------------------------------------------------ Kecocokan item

    [Test]
    public void Required_tags_decide_the_style_of_a_face_part() {
        Assert.AreEqual(FaceStyle.Anime, FaceStyleRules.RequiredStyle("Eyes", Anime, None));
        Assert.AreEqual(FaceStyle.Basic, FaceStyleRules.RequiredStyle("MakeUpLips", Basic, None));
        Assert.AreEqual(FaceStyle.Any, FaceStyleRules.RequiredStyle("Eyes", None, None));
    }

    [Test]
    public void Eyeliner_with_only_a_style_tag_is_treated_as_that_style() {
        // MakeUpEyes_BasicEyeLiner memakai `tags` (bukan requiredTags) = Stylized.
        Assert.AreEqual(FaceStyle.Basic, FaceStyleRules.RequiredStyle("MakeUpEyes", None, Basic));
    }

    [Test]
    public void Style_tags_on_non_face_parts_are_ignored() {
        Assert.AreEqual(FaceStyle.Any, FaceStyleRules.RequiredStyle("Top", None, Basic));
        Assert.AreEqual(FaceStyle.Any, FaceStyleRules.RequiredStyle("Head", None, Anime));
    }

    [Test]
    public void Hair_follows_head_style_only_through_required_tags() {
        // HangingDreads/Flare/LongDreads: requiredTags Stylized (tembus/tenggelam di kepala Anime, cek visual 2026-11-25).
        Assert.AreEqual(FaceStyle.Basic, FaceStyleRules.RequiredStyle("HairBack", Basic, None));
        Assert.AreEqual(FaceStyle.Basic, FaceStyleRules.RequiredStyle("HairFront", Basic, None));
        Assert.AreEqual(FaceStyle.Any, FaceStyleRules.RequiredStyle("HairFront", None, None));
        Assert.AreEqual(FaceStyle.Any, FaceStyleRules.RequiredStyle("HairBack", None, Anime), "plain tags never block hair");
    }

    [Test]
    public void Basic_pupils_also_fit_anime_heads_but_the_anime_pupil_needs_an_anime_head() {
        // Dicek visual 2026-11-18: Basic/Sharp rapi di mata anime; Round rusak di mata Basic.
        Assert.AreEqual(FaceStyle.Any, FaceStyleRules.RequiredStyle("Pupil", Basic, None));
        Assert.AreEqual(FaceStyle.Anime, FaceStyleRules.RequiredStyle("Pupil", Anime, None));
    }

    [Test]
    public void Fits_matches_the_head_style() {
        Assert.IsTrue(FaceStyleRules.Fits(FaceStyle.Any, animeHead: false));
        Assert.IsTrue(FaceStyleRules.Fits(FaceStyle.Anime, animeHead: true));
        Assert.IsFalse(FaceStyleRules.Fits(FaceStyle.Anime, animeHead: false));
        Assert.IsFalse(FaceStyleRules.Fits(FaceStyle.Basic, animeHead: true));
    }

    [Test]
    public void Incompatible_message_names_the_item_and_the_head_style() {
        Assert.AreEqual("Long Lashes hanya bisa dipakai di kepala Anime",
                        FaceStyleRules.IncompatibleMessage("Long Lashes", FaceStyle.Anime));
        Assert.AreEqual("Eyeliner hanya bisa dipakai di kepala Basic",
                        FaceStyleRules.IncompatibleMessage("Eyeliner", FaceStyle.Basic));
    }

    [Test]
    public void Head_style_comes_from_the_head_tags() {
        Assert.IsTrue(FaceStyleRules.IsAnimeHead(Anime));
        Assert.IsFalse(FaceStyleRules.IsAnimeHead(Basic));
        Assert.IsFalse(FaceStyleRules.IsAnimeHead(null));
    }

    // ------------------------------------------------------------ Ganti gaya kepala

    private static readonly Dictionary<string, FaceStyle> Styles = new Dictionary<string, FaceStyle> {
        { FaceStyleRules.AnimeEyes, FaceStyle.Anime }, { FaceStyleRules.BasicEyes, FaceStyle.Basic },
        { FaceStyleRules.AnimePupil, FaceStyle.Anime }, { "Pupil/Pupil_SharpPupil", FaceStyle.Any },
        { FaceStyleRules.BasicPupil, FaceStyle.Any }, { "EyeShine/EyeShine_DoubleRound", FaceStyle.Anime },
        { "EyeBrows/Brows_BasicBrows", FaceStyle.Anime }, { "EyeLashes/EyeLashes_LongLashes", FaceStyle.Anime },
        { "MakeUpLips/MakeUpLips_SimpleLipstick", FaceStyle.Anime }, { "MakeUpLips/MakeUpLips_BasicLipstick", FaceStyle.Basic },
        { "MakeUpEyes/MakeUpEyes_BasicEyeLiner", FaceStyle.Basic },
        { "HairBack/BSMC_HairBack_Flare", FaceStyle.Basic }, { "HairFront/BSMC_HairFront_HangingDreads", FaceStyle.Basic }
    };

    private static bool FitsFor(string path, bool animeHead) =>
        FaceStyleRules.Fits(Styles.TryGetValue(path, out FaceStyle s) ? s : FaceStyle.Any, animeHead);

    private static OutfitRecord Default(string path) => new OutfitRecord { path = path, colors = new List<string> { "#DEFA17" } };

    private static OutfitRecord Worn(string path) => new OutfitRecord { path = path, colors = new List<string> { "#123456" } };

    private static CustomCharacterRecord AnimeFace() {
        return new CustomCharacterRecord {
            outfits = new List<OutfitRecord> {
                Worn("Head/Head_AnimeYoung"), Worn(FaceStyleRules.AnimeEyes), Worn(FaceStyleRules.AnimePupil),
                Worn("EyeShine/EyeShine_DoubleRound"), Worn("EyeBrows/Brows_BasicBrows"),
                Worn("EyeLashes/EyeLashes_LongLashes"), Worn("MakeUpLips/MakeUpLips_SimpleLipstick"), Worn("Top/Top_Hoodie")
            }
        };
    }

    private static CustomCharacterRecord ToBasic(CustomCharacterRecord record) =>
        FaceStyleRules.ForHeadStyle(record, animeHead: false, path => FitsFor(path, false), Default,
                                    new Dictionary<string, OutfitRecord>());

    [Test]
    public void Switching_to_a_basic_head_uses_basic_eyes_and_pupil() {
        CustomCharacterRecord result = ToBasic(AnimeFace());

        Assert.AreEqual(FaceStyleRules.BasicEyes, RecordEdits.Find(result, "Eyes").path);
        Assert.AreEqual(FaceStyleRules.BasicPupil, RecordEdits.Find(result, "Pupil").path);
        Assert.AreEqual("#DEFA17", RecordEdits.Find(result, "Eyes").colors[0], "new eyes use their default colors");
    }

    [Test]
    public void Switching_to_a_basic_head_removes_anime_only_parts_and_keeps_the_rest() {
        CustomCharacterRecord result = ToBasic(AnimeFace());

        Assert.IsNull(RecordEdits.Find(result, "EyeShine"));
        Assert.IsNull(RecordEdits.Find(result, "EyeBrows"));
        Assert.IsNull(RecordEdits.Find(result, "EyeLashes"));
        Assert.IsNull(RecordEdits.Find(result, "MakeUpLips"), "make-up is removed, not swapped");
        Assert.AreEqual("Top/Top_Hoodie", RecordEdits.Find(result, "Top").path);
    }

    [Test]
    public void Switching_style_does_not_change_the_original_record() {
        CustomCharacterRecord original = AnimeFace();
        ToBasic(original);
        Assert.AreEqual(FaceStyleRules.AnimeEyes, RecordEdits.Find(original, "Eyes").path);
        Assert.AreEqual(8, original.outfits.Count);
    }

    [Test]
    public void Switching_to_an_anime_head_uses_anime_eyes_and_round_pupil_and_drops_eyeliner() {
        var record = new CustomCharacterRecord {
            outfits = new List<OutfitRecord> {
                Worn(FaceStyleRules.BasicEyes), Worn("Pupil/Pupil_SharpPupil"), Worn("MakeUpEyes/MakeUpEyes_BasicEyeLiner")
            }
        };

        CustomCharacterRecord result = FaceStyleRules.ForHeadStyle(record, animeHead: true, path => FitsFor(path, true),
                                                                   Default, new Dictionary<string, OutfitRecord>());

        Assert.AreEqual(FaceStyleRules.AnimeEyes, RecordEdits.Find(result, "Eyes").path);
        Assert.AreEqual(FaceStyleRules.AnimePupil, RecordEdits.Find(result, "Pupil").path);
        Assert.IsNull(RecordEdits.Find(result, "MakeUpEyes"));
    }

    [Test]
    public void Switching_to_an_anime_head_removes_basic_only_hair_and_keeps_other_hair() {
        var record = new CustomCharacterRecord {
            outfits = new List<OutfitRecord> {
                Worn(FaceStyleRules.BasicEyes), Worn("HairBack/BSMC_HairBack_Flare"), Worn("HairFront/BSMC_HairFront_Fohawk")
            }
        };

        CustomCharacterRecord result = FaceStyleRules.ForHeadStyle(record, animeHead: true, path => FitsFor(path, true),
                                                                   Default, new Dictionary<string, OutfitRecord>());

        Assert.IsNull(RecordEdits.Find(result, "HairBack"));
        Assert.AreEqual("HairFront/BSMC_HairFront_Fohawk", RecordEdits.Find(result, "HairFront").path);
    }

    [Test]
    public void Switching_back_to_anime_restores_remembered_brows_and_lashes() {
        var remembered = new Dictionary<string, OutfitRecord> {
            { "EyeBrows", Worn("EyeBrows/Brows_BasicBrows") }, { "EyeLashes", null } // null = user memilih Kosong
        };
        var record = new CustomCharacterRecord { outfits = new List<OutfitRecord> { Worn(FaceStyleRules.BasicEyes) } };

        CustomCharacterRecord result = FaceStyleRules.ForHeadStyle(record, animeHead: true, path => FitsFor(path, true),
                                                                   Default, remembered);

        Assert.AreEqual("EyeBrows/Brows_BasicBrows", RecordEdits.Find(result, "EyeBrows").path);
        Assert.AreEqual("#123456", RecordEdits.Find(result, "EyeBrows").colors[0], "restored with its own colors");
        Assert.IsNull(RecordEdits.Find(result, "EyeLashes"));
    }

    [Test]
    public void Remembered_brows_are_ignored_on_a_basic_head() {
        var remembered = new Dictionary<string, OutfitRecord> { { "EyeBrows", Worn("EyeBrows/Brows_BasicBrows") } };
        CustomCharacterRecord result = FaceStyleRules.ForHeadStyle(AnimeFace(), animeHead: false, path => FitsFor(path, false),
                                                                   Default, remembered);
        Assert.IsNull(RecordEdits.Find(result, "EyeBrows"));
    }

    [Test]
    public void Remember_snapshots_worn_brows_and_lashes_including_empty_ones() {
        var record = new CustomCharacterRecord { outfits = new List<OutfitRecord> { Worn("EyeBrows/Brows_BasicBrows") } };

        IReadOnlyDictionary<string, OutfitRecord> memory = FaceStyleRules.RememberRestorable(record);

        Assert.AreEqual("EyeBrows/Brows_BasicBrows", memory["EyeBrows"].path);
        Assert.IsTrue(memory.ContainsKey("EyeLashes"));
        Assert.IsNull(memory["EyeLashes"]);
    }

    [Test]
    public void Restorable_memory_falls_back_to_the_template() {
        var template = new CustomCharacterRecord { outfits = new List<OutfitRecord> { Worn("EyeLashes/EyeLashes_ShortLashes") } };

        IReadOnlyDictionary<string, OutfitRecord> memory = FaceStyleRules.RememberRestorable(template);

        Assert.AreEqual("EyeLashes/EyeLashes_ShortLashes", memory["EyeLashes"].path);
        Assert.IsNull(memory["EyeBrows"]);
    }
}

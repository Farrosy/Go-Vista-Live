using System.Collections.Generic;
using NUnit.Framework;

public class RecordEditsTests {
    private static CustomCharacterRecord Sample() {
        return new CustomCharacterRecord {
            templateId = "male",
            outfits = new List<OutfitRecord> {
                new OutfitRecord { path = "Top/BSMC_Top_Hoodie", colors = new List<string> { "#111111", "#222222", "#333333" } },
                new OutfitRecord { path = "Bottom/BSMC_Bottom_Jeans", colors = new List<string> { "#444444" } }
            }
        };
    }

    [Test]
    public void Type_is_the_folder_part_of_the_path() {
        Assert.AreEqual("Top", RecordEdits.TypeOf("Top/BSMC_Top_Hoodie"));
        Assert.AreEqual("", RecordEdits.TypeOf("NoSlash"));
        Assert.AreEqual("", RecordEdits.TypeOf(null));
    }

    [Test]
    public void Find_returns_the_outfit_of_a_type_or_null() {
        CustomCharacterRecord record = Sample();
        Assert.AreEqual("Bottom/BSMC_Bottom_Jeans", RecordEdits.Find(record, "Bottom").path);
        Assert.IsNull(RecordEdits.Find(record, "Hat"));
    }

    [Test]
    public void Setting_an_outfit_replaces_the_same_type_without_changing_the_original() {
        CustomCharacterRecord original = Sample();
        var shirt = new OutfitRecord { path = "Top/BSMC_Top_Shirt", colors = new List<string> { "#AAAAAA" } };

        CustomCharacterRecord edited = RecordEdits.WithOutfit(original, shirt);

        Assert.AreEqual("Top/BSMC_Top_Shirt", RecordEdits.Find(edited, "Top").path);
        Assert.AreEqual(2, edited.outfits.Count);
        Assert.AreEqual("Top/BSMC_Top_Hoodie", RecordEdits.Find(original, "Top").path, "original must not change");
    }

    [Test]
    public void Setting_an_outfit_of_a_new_type_adds_it() {
        var hat = new OutfitRecord { path = "Hat/BSMC_Hat_Beanie" };
        CustomCharacterRecord edited = RecordEdits.WithOutfit(Sample(), hat);
        Assert.AreEqual(3, edited.outfits.Count);
        Assert.IsNotNull(RecordEdits.Find(edited, "Hat"));
    }

    [Test]
    public void Removing_an_outfit_type_keeps_the_others() {
        CustomCharacterRecord original = Sample();
        CustomCharacterRecord edited = RecordEdits.WithoutOutfit(original, "Top");
        Assert.IsNull(RecordEdits.Find(edited, "Top"));
        Assert.IsNotNull(RecordEdits.Find(edited, "Bottom"));
        Assert.IsNotNull(RecordEdits.Find(original, "Top"), "original must not change");
    }

    [Test]
    public void Setting_a_channel_color_changes_only_that_channel() {
        CustomCharacterRecord original = Sample();
        CustomCharacterRecord edited = RecordEdits.WithChannelColor(original, "Top", 1, "#ff0000");

        Assert.AreEqual(new List<string> { "#111111", "#FF0000", "#333333" }, RecordEdits.Find(edited, "Top").colors);
        Assert.AreEqual("#222222", RecordEdits.Find(original, "Top").colors[1], "original must not change");
    }

    [Test]
    public void Setting_a_channel_past_the_list_pads_with_white() {
        CustomCharacterRecord edited = RecordEdits.WithChannelColor(Sample(), "Bottom", 2, "#00FF00");
        Assert.AreEqual(new List<string> { "#444444", HexColor.White, "#00FF00" }, RecordEdits.Find(edited, "Bottom").colors);
    }

    [Test]
    public void Invalid_color_or_missing_outfit_returns_the_same_record() {
        CustomCharacterRecord original = Sample();
        Assert.AreSame(original, RecordEdits.WithChannelColor(original, "Top", 0, "red"));
        Assert.AreSame(original, RecordEdits.WithChannelColor(original, "Hat", 0, "#FFFFFF"));
        Assert.AreSame(original, RecordEdits.WithChannelColor(original, "Top", CustomizationRules.MaxColorChannels, "#FFFFFF"));
    }

    [Test]
    public void Pattern_color_is_set_by_index() {
        CustomCharacterRecord edited = RecordEdits.WithPatternColor(Sample(), "Top", 2, "#123456");
        Assert.AreEqual(new List<string> { HexColor.White, HexColor.White, "#123456" }, RecordEdits.Find(edited, "Top").patternColors);
    }

    [Test]
    public void Pattern_texture_can_be_set_and_cleared() {
        CustomCharacterRecord withPattern = RecordEdits.WithPattern(Sample(), "Top", "Pattern/Pattern_Boxers");
        Assert.AreEqual("Pattern/Pattern_Boxers", RecordEdits.Find(withPattern, "Top").pattern);
        CustomCharacterRecord cleared = RecordEdits.WithPattern(withPattern, "Top", "");
        Assert.AreEqual("", RecordEdits.Find(cleared, "Top").pattern);
    }

    [Test]
    public void Clone_is_deep() {
        CustomCharacterRecord original = Sample();
        CustomCharacterRecord copy = original.Clone();
        copy.outfits[0].colors[0] = "#000000";
        copy.decals.Add(new DecalRecord());
        Assert.AreEqual("#111111", original.outfits[0].colors[0]);
        Assert.AreEqual(0, original.decals.Count);
    }
}

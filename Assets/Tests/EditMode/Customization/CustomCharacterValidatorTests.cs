using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

public class CustomCharacterValidatorTests {
    private static readonly HashSet<string> Allowed = new HashSet<string> {
        "Head/Head_BasicHead", "Top/BSMC_Top_Hoodie", "UnderLower/UnderLower_SimpleBoxers"
    };

    private static bool IsAllowed(string path) => Allowed.Contains(path);

    private static CustomCharacterRecord Sample(string templateId = "male") {
        return new CustomCharacterRecord {
            templateId = templateId,
            outfits = new List<OutfitRecord> {
                new OutfitRecord { path = "Head/Head_BasicHead", colors = new List<string> { "#F7DCC6" } },
                new OutfitRecord { path = "Top/BSMC_Top_Hoodie", colors = new List<string> { "#1546D8", "nope", "#E8ECF4" } },
                new OutfitRecord { path = "UnderLower/UnderLower_SimpleBoxers" },
                new OutfitRecord { path = "Top/Hacked_Item" }
            },
            faceShapes = new List<ShapeRecord> {
                new ShapeRecord { key = "IrisSize", value = 90f },
                new ShapeRecord { key = "Stern", value = 70f },
                new ShapeRecord { key = "Weight", value = 100f }
            },
            decals = new List<DecalRecord> {
                new DecalRecord { decal = "Decal_GoVistaG", position = "Chest", size = 9f, rotation = 400f, color = "#ZZZ" },
                new DecalRecord { decal = "Decal_GoVistaG", position = "Moon", size = 0.2f, color = "#FFFFFF" }
            }
        };
    }

    [Test]
    public void Unknown_template_is_rejected() {
        Assert.IsNull(CustomCharacterValidator.Sanitize(Sample("dragon"), IsAllowed));
        Assert.IsNull(CustomCharacterValidator.Sanitize(null, IsAllowed));
    }

    [Test]
    public void Outfits_outside_the_whitelist_are_dropped() {
        CustomCharacterRecord clean = CustomCharacterValidator.Sanitize(Sample(), IsAllowed);
        Assert.AreEqual(3, clean.outfits.Count);
        Assert.IsFalse(clean.outfits.Exists(o => o.path == "Top/Hacked_Item"));
    }

    [Test]
    public void Invalid_colors_become_white_and_valid_ones_are_kept() {
        CustomCharacterRecord clean = CustomCharacterValidator.Sanitize(Sample(), IsAllowed);
        OutfitRecord hoodie = clean.outfits.Find(o => o.path == "Top/BSMC_Top_Hoodie");
        CollectionAssert.AreEqual(new[] { "#1546D8", "#FFFFFF", "#E8ECF4" }, hoodie.colors);
    }

    [Test]
    public void Face_shapes_are_clamped_and_filtered_for_the_template_gender() {
        CustomCharacterRecord male = CustomCharacterValidator.Sanitize(Sample("male"), IsAllowed);
        Assert.AreEqual(50f, male.faceShapes.Find(s => s.key == "IrisSize").value);
        Assert.AreEqual(70f, male.faceShapes.Find(s => s.key == "Stern").value);
        Assert.IsNull(male.faceShapes.Find(s => s.key == "Weight"));

        CustomCharacterRecord female = CustomCharacterValidator.Sanitize(Sample("female"), IsAllowed);
        Assert.IsNull(female.faceShapes.Find(s => s.key == "Stern"));
    }

    [Test]
    public void Decals_are_clamped_and_unknown_positions_dropped() {
        CustomCharacterRecord clean = CustomCharacterValidator.Sanitize(Sample(), IsAllowed);
        Assert.AreEqual(1, clean.decals.Count);
        DecalRecord decal = clean.decals[0];
        Assert.AreEqual(CustomizationRules.MaxDecalSize, decal.size);
        Assert.AreEqual(180f, decal.rotation);
        Assert.AreEqual("#FFFFFF", decal.color);
    }

    [Test]
    public void Decal_count_is_limited() {
        CustomCharacterRecord record = Sample();
        record.decals.Clear();
        for (int i = 0; i < CustomizationRules.MaxDecals + 3; i++)
            record.decals.Add(new DecalRecord { decal = "Decal_GoVistaG", position = "Back", color = "#FFFFFF" });
        Assert.AreEqual(CustomizationRules.MaxDecals, CustomCharacterValidator.Sanitize(record, IsAllowed).decals.Count);
    }

    [Test]
    public void Sanitize_does_not_mutate_the_input() {
        CustomCharacterRecord original = Sample();
        CustomCharacterValidator.Sanitize(original, IsAllowed);
        Assert.AreEqual(4, original.outfits.Count);
        Assert.AreEqual(90f, original.faceShapes[0].value);
    }

    [Test]
    public void Record_survives_a_json_round_trip() {
        CustomCharacterRecord clean = CustomCharacterValidator.Sanitize(Sample(), IsAllowed);
        string json = JsonConvert.SerializeObject(clean);
        var back = JsonConvert.DeserializeObject<CustomCharacterRecord>(json);

        Assert.AreEqual(clean.templateId, back.templateId);
        Assert.AreEqual(clean.outfits.Count, back.outfits.Count);
        CollectionAssert.AreEqual(clean.outfits[1].colors, back.outfits[1].colors);
        Assert.AreEqual(clean.decals[0].position, back.decals[0].position);
        Assert.AreEqual(clean.faceShapes.Count, back.faceShapes.Count);
    }
}

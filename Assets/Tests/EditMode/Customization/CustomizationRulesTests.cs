using NUnit.Framework;

public class CustomizationRulesTests {
    [TestCase("IrisSize", 80f, 50f)]
    [TestCase("EyeLidHeight", 100f, 50f)]
    [TestCase("IrisSize", 30f, 30f)]
    [TestCase("Roundness", 100f, 100f)]
    [TestCase("Roundness", 150f, 100f)]
    [TestCase("Sharpness", -10f, 0f)]
    public void ClampFaceShape_caps_iris_and_eyelid_at_50_and_others_at_100(string key, float value, float expected) {
        Assert.AreEqual(expected, CustomizationRules.ClampFaceShape(key, value));
    }

    [TestCase("BodyType")]
    [TestCase("Weight")]
    [TestCase("Muscle")]
    [TestCase("NeckThickness")]
    public void Body_shapes_mirrored_on_the_head_are_hidden(string key) {
        Assert.IsFalse(CustomizationRules.IsFaceShapeVisible(key, isMale: true));
        Assert.IsFalse(CustomizationRules.IsFaceShapeVisible(key, isMale: false));
    }

    [Test]
    public void Stern_is_visible_for_male_templates_only() {
        Assert.IsTrue(CustomizationRules.IsFaceShapeVisible("Stern", isMale: true));
        Assert.IsFalse(CustomizationRules.IsFaceShapeVisible("Stern", isMale: false));
    }

    [Test]
    public void Regular_face_shapes_are_visible_for_everyone() {
        Assert.IsTrue(CustomizationRules.IsFaceShapeVisible("IrisSize", isMale: false));
        Assert.IsTrue(CustomizationRules.IsFaceShapeVisible("Squareness", isMale: true));
    }

    [Test]
    public void Lower_underwear_can_never_be_empty() {
        Assert.IsFalse(CustomizationRules.CanBeEmpty("UnderLower", isMale: true));
        Assert.IsFalse(CustomizationRules.CanBeEmpty("UnderLower", isMale: false));
    }

    [Test]
    public void Upper_underwear_can_be_empty_for_male_only() {
        Assert.IsTrue(CustomizationRules.CanBeEmpty("UnderUpper", isMale: true));
        Assert.IsFalse(CustomizationRules.CanBeEmpty("UnderUpper", isMale: false));
    }

    [TestCase("Top")]
    [TestCase("Bottom")]
    [TestCase("Hat")]
    public void Clothes_and_accessories_can_be_empty(string type) {
        Assert.IsTrue(CustomizationRules.CanBeEmpty(type, isMale: false));
    }

    [TestCase("Head")]
    [TestCase("Body")]
    [TestCase("Eyes")]
    public void Base_parts_can_never_be_empty(string type) {
        Assert.IsFalse(CustomizationRules.CanBeEmpty(type, isMale: true));
    }

    [TestCase("Base", 0, "Base")]
    [TestCase("", 1, "Warna 2")]
    [TestCase(null, 2, "Warna 3")]
    [TestCase("  ", 0, "Warna 1")]
    public void Unnamed_color_channels_get_a_numbered_label(string name, int index, string expected) {
        Assert.AreEqual(expected, CustomizationRules.ColorChannelLabel(name, index));
    }

    [Test]
    public void Templates_know_their_gender_and_style() {
        Assert.IsTrue(CharacterTemplates.TryGet("male", out CharacterTemplate male));
        Assert.IsTrue(male.IsMale);
        Assert.IsFalse(male.IsAnime);
        Assert.AreEqual("1DefaultMale", male.BozoId);

        Assert.IsTrue(CharacterTemplates.TryGet("anime_female", out CharacterTemplate animeFemale));
        Assert.IsFalse(animeFemale.IsMale);
        Assert.IsTrue(animeFemale.IsAnime);

        Assert.IsFalse(CharacterTemplates.TryGet("dragon", out _));
        Assert.AreEqual(4, CharacterTemplates.All.Count);
    }
}

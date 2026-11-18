using NUnit.Framework;

public class GenderRulesTests {
    [Test]
    public void Beards_are_male_only() {
        Assert.AreEqual(OutfitGender.Male, GenderRules.GenderOf("LowerFace/BSMC_LowerFace_FullBeard"));
        Assert.IsTrue(GenderRules.Fits("LowerFace/BSMC_LowerFace_FullBeard", isMale: true));
        Assert.IsFalse(GenderRules.Fits("LowerFace/BSMC_LowerFace_FullBeard", isMale: false));
    }

    [Test]
    public void Skirts_dresses_and_bras_are_female_only() {
        foreach (string path in new[] {
                     "Bottom/BSMC_Bottom_Skirt", "Overall/BSMC_Overall_Sundress", "UnderUpper/UnderUpper_SimpleBra",
                     "Top/Outfit_Top_OpenShoulder_Crop", "Feet/BSMC_Feet_SlipOns", "Leggings/Leggings_Stocking"
                 }) {
            Assert.AreEqual(OutfitGender.Female, GenderRules.GenderOf(path), path);
            Assert.IsFalse(GenderRules.Fits(path, isMale: true), path);
            Assert.IsTrue(GenderRules.Fits(path, isMale: false), path);
        }
    }

    [Test]
    public void Unlisted_items_are_universal() {
        foreach (string path in new[] { "Top/BSMC_Top_Hoodie", "Bottom/BSMC_Bottom_CargoPants", "HairBack/BSMC_HairBack_TwinTailLong", "", null }) {
            Assert.AreEqual(OutfitGender.Universal, GenderRules.GenderOf(path), path ?? "null");
            Assert.IsTrue(GenderRules.Fits(path, isMale: true));
            Assert.IsTrue(GenderRules.Fits(path, isMale: false));
        }
    }

    [Test]
    public void Message_names_the_item_and_the_gender() {
        Assert.AreEqual("Skirt hanya untuk karakter cewek", GenderRules.BlockedMessage("Skirt", OutfitGender.Female));
        Assert.AreEqual("Jenggot hanya untuk karakter cowok", GenderRules.BlockedMessage("Jenggot", OutfitGender.Male));
    }

    [Test]
    public void Every_template_item_fits_its_own_template_gender() {
        // Item bawaan template tidak boleh terkunci (data template BoZo, dicek 2026-11-18).
        foreach (string path in new[] { "Bottom/BSMC_Bottom_BigShorts", "Top/BSMC_Top_TankTop", "UnderLower/UnderLower_SimpleBoxers" }) {
            Assert.IsTrue(GenderRules.Fits(path, isMale: true), path);
        }
        foreach (string path in new[] { "Bottom/BSMC_Bottom_ShortShorts", "UnderUpper/UnderUpper_SimpleBra", "UnderLower/UnderLower_SimplePanties" }) {
            Assert.IsTrue(GenderRules.Fits(path, isMale: false), path);
        }
    }
}

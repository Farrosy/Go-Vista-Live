using System.Linq;
using NUnit.Framework;

public class EditorCategoriesTests {
    [Test]
    public void Rail_follows_the_mockup_order() {
        CollectionAssert.AreEqual(
            new[] { "Kulit", "Wajah", "Rambut", "Atasan", "Bawahan", "Kaki", "Aksesori", "Dalaman", "Stiker" },
            EditorCategories.All.Select(c => c.Label).ToArray());
    }

    [Test]
    public void Outfit_categories_have_at_least_one_bozo_type() {
        foreach (EditorCategory category in EditorCategories.All.Where(c => c.Kind == EditorCategoryKind.Outfits))
            Assert.IsNotEmpty(category.SubTabs, category.Label);
    }

    [Test]
    public void Every_bozo_type_appears_once() {
        string[] types = EditorCategories.All.SelectMany(c => c.SubTabs).Select(s => s.OutfitType).ToArray();
        CollectionAssert.AllItemsAreUnique(types);
    }

    [Test]
    public void Top_category_offers_overall_and_accessories_offer_hats_and_glasses() {
        Assert.Contains("Overall", EditorCategories.Get("Atasan").SubTabs.Select(s => s.OutfitType).ToList());
        var accessories = EditorCategories.Get("Aksesori").SubTabs.Select(s => s.OutfitType).ToList();
        Assert.Contains("Hat", accessories);
        Assert.Contains("UpperFace", accessories);
    }

    [Test]
    public void Underwear_tab_covers_upper_and_lower() {
        var types = EditorCategories.Get("Dalaman").SubTabs.Select(s => s.OutfitType).ToList();
        CollectionAssert.AreEquivalent(new[] { "UnderUpper", "UnderLower" }, types);
    }
}

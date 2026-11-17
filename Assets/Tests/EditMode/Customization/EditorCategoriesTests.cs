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
    public void Every_category_has_at_least_one_sub_tab() {
        foreach (EditorCategory category in EditorCategories.All) Assert.IsNotEmpty(category.SubTabs, category.Label);
    }

    [Test]
    public void Outfit_sub_tabs_have_at_least_one_bozo_type() {
        foreach (EditorSubTab tab in EditorCategories.All.SelectMany(c => c.SubTabs)
                                                     .Where(t => t.Panel == EditorPanelKind.Outfits))
            Assert.IsNotEmpty(tab.OutfitTypes, tab.Label);
    }

    [Test]
    public void Every_bozo_type_appears_once() {
        string[] types = EditorCategories.All.SelectMany(c => c.SubTabs).SelectMany(s => s.OutfitTypes).ToArray();
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

    [Test]
    public void Face_tab_has_shapes_expressions_head_eyes_brows_and_makeup() {
        EditorCategory face = EditorCategories.Get("Wajah");
        CollectionAssert.AreEqual(new[] { "Bentuk", "Ekspresi", "Kepala", "Mata", "Alis & Bulu Mata", "Make-up" },
                                  face.SubTabs.Select(t => t.Label).ToArray());
        Assert.AreEqual(EditorPanelKind.FaceShapes, face.SubTabs[0].Panel);
        CollectionAssert.AreEquivalent(new[] { "EyeBrows", "EyeLashes" }, face.SubTabs[4].OutfitTypes.ToArray());
    }

    [Test]
    public void Skin_tab_edits_the_body_colors_only() {
        EditorSubTab skin = EditorCategories.Get("Kulit").SubTabs.Single();
        Assert.AreEqual(EditorPanelKind.Skin, skin.Panel);
        CollectionAssert.AreEqual(new[] { "Body" }, skin.OutfitTypes.ToArray());
    }

    [Test]
    public void Camera_zooms_to_the_face_only_for_face_and_hair() {
        CollectionAssert.AreEquivalent(new[] { "Wajah", "Rambut" },
                                       EditorCategories.All.Where(c => c.FocusFace).Select(c => c.Label).ToArray());
    }

    [Test]
    public void Type_labels_name_the_tab_and_sub_tab() {
        Assert.AreEqual("Atasan · Overall", EditorCategories.LabelOf("Overall"));
        Assert.AreEqual("Atasan", EditorCategories.LabelOf("Top"));
        Assert.AreEqual("Kulit", EditorCategories.LabelOf("Body"));
        Assert.AreEqual("Wajah · Mata", EditorCategories.LabelOf("Pupil"));
        Assert.IsNull(EditorCategories.LabelOf("Decal"));
    }
}

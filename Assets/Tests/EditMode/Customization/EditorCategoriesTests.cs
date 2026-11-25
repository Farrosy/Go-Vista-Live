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

    // ---------------------------------------------------------------- Ruang ganti (clothing shop)

    private static string[] FittingRoomTypes() =>
        EditorCategories.FittingRoom.SelectMany(c => c.SubTabs).SelectMany(t => t.OutfitTypes).ToArray();

    [Test]
    public void Fitting_room_hides_hair_skin_head_shapes_beard_and_stickers() {
        string[] labels = EditorCategories.FittingRoom.Select(c => c.Label).ToArray();
        CollectionAssert.DoesNotContain(labels, "Rambut");
        CollectionAssert.DoesNotContain(labels, "Kulit");
        CollectionAssert.DoesNotContain(labels, "Stiker");
        Assert.IsFalse(EditorCategories.FittingRoom.Any(c => c.Kind == EditorCategoryKind.Stickers));
        string[] types = FittingRoomTypes();
        foreach (string hidden in new[] { "HairFront", "HairBack", "Body", "Head", "LowerFace" })
            CollectionAssert.DoesNotContain(types, hidden);
        EditorCategory face = EditorCategories.FittingRoom.Single(c => c.Label == "Wajah");
        Assert.IsFalse(face.SubTabs.Any(t => t.Panel == EditorPanelKind.FaceShapes));
    }

    [Test]
    public void Fitting_room_keeps_clothes_accessories_and_makeup() {
        string[] types = FittingRoomTypes();
        foreach (string kept in new[] { "Top", "Overall", "Bottom", "Leggings", "Feet", "Socks", "Hat", "UpperFace",
                                        "Gloves", "HeadAcc", "UnderUpper", "UnderLower", "Eyes", "Pupil", "EyeShine",
                                        "EyeBrows", "EyeLashes", "MakeUpLips", "MakeUpCheeks", "MakeUpEyes", "FaceDetails" })
            CollectionAssert.Contains(types, kept);
        Assert.IsTrue(EditorCategories.FittingRoom.All(c => c != null));
    }

    [Test]
    public void IndexOf_finds_a_label_or_falls_back_to_the_first_tab() {
        Assert.AreEqual(1, EditorCategories.IndexOf(EditorCategories.FittingRoom, "Atasan"));
        Assert.AreEqual(0, EditorCategories.IndexOf(EditorCategories.FittingRoom, "Rambut"));
    }

    // ---------------------------------------------------------------- Barbershop (kursi barber)

    [Test]
    public void Barber_offers_only_hair_front_back_and_beard() {
        CollectionAssert.AreEqual(new[] { "Rambut", "Jenggot" }, EditorCategories.Barber.Select(c => c.Label).ToArray());
        string[] types = EditorCategories.Barber.SelectMany(c => c.SubTabs).SelectMany(t => t.OutfitTypes).ToArray();
        CollectionAssert.AreEquivalent(new[] { "HairFront", "HairBack", "LowerFace" }, types);
        Assert.IsTrue(EditorCategories.Barber.All(c => c.Kind == EditorCategoryKind.Outfits));
        Assert.IsTrue(EditorCategories.Barber.SelectMany(c => c.SubTabs).All(t => t.Panel == EditorPanelKind.Outfits));
    }

    [Test]
    public void Barber_keeps_the_fixed_chair_camera() {
        Assert.IsFalse(EditorCategories.Barber.Any(c => c.FocusFace));
    }

    [Test]
    public void Barber_types_keep_their_lobby_labels() {
        Assert.AreEqual("Rambut · Depan", EditorCategories.LabelOf("HairFront"));
        Assert.AreEqual("Aksesori · Jenggot", EditorCategories.LabelOf("LowerFace"));
    }
}

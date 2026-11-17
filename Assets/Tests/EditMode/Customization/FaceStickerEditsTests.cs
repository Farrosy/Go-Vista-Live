using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class FaceStickerEditsTests {
    private static CustomCharacterRecord Sample() {
        return new CustomCharacterRecord {
            templateId = "male",
            outfits = new List<OutfitRecord> {
                new OutfitRecord { path = "Top/BSMC_Top_DressShirt", colors = new List<string> { "#111111" } }
            },
            faceShapes = new List<ShapeRecord> { new ShapeRecord { key = "NoseWidth", value = 20f } }
        };
    }

    // ---------------------------------------------------------------- Bentuk wajah

    [Test]
    public void Face_shape_is_updated_or_added_without_touching_the_original() {
        CustomCharacterRecord original = Sample();

        CustomCharacterRecord edited = RecordEdits.WithFaceShape(RecordEdits.WithFaceShape(original, "NoseWidth", 70f), "Sharpness", 40f);

        Assert.AreEqual(70f, RecordEdits.FaceShape(edited, "NoseWidth", -1f));
        Assert.AreEqual(40f, RecordEdits.FaceShape(edited, "Sharpness", -1f));
        Assert.AreEqual(20f, RecordEdits.FaceShape(original, "NoseWidth", -1f), "original must not change");
    }

    [Test]
    public void Iris_and_eyelid_are_capped_at_fifty() {
        CustomCharacterRecord edited = RecordEdits.WithFaceShape(Sample(), "IrisSize", 90f);
        edited = RecordEdits.WithFaceShape(edited, "EyeLidHeight", 75f);

        Assert.AreEqual(50f, RecordEdits.FaceShape(edited, "IrisSize", -1f));
        Assert.AreEqual(50f, RecordEdits.FaceShape(edited, "EyeLidHeight", -1f));
    }

    [Test]
    public void Same_face_value_returns_the_same_record() {
        CustomCharacterRecord record = Sample();
        Assert.AreSame(record, RecordEdits.WithFaceShape(record, "NoseWidth", 20f));
    }

    [Test]
    public void Missing_face_shape_falls_back() {
        Assert.AreEqual(33f, RecordEdits.FaceShape(Sample(), "EarAngle", 33f));
    }

    [Test]
    public void Face_sliders_start_with_iris_eyelid_and_stern_even_if_the_head_lacks_them() {
        List<FaceShapeEntry> entries = FaceShapeCatalog.Entries(new[] { "Roundness", "MouthWidth" }, isMale: true);

        CollectionAssert.AreEqual(new[] { "IrisSize", "EyeLidHeight", "Stern", "MouthWidth", "Roundness" },
                                  entries.Select(e => e.Key).ToArray());
        Assert.IsFalse(entries[0].Enabled);
        Assert.IsTrue(entries[3].Enabled);
        Assert.AreEqual(50f, entries[0].Max);
        Assert.AreEqual(100f, entries[3].Max);
    }

    [Test]
    public void Female_templates_never_see_stern_and_body_shapes_stay_hidden() {
        List<FaceShapeEntry> entries = FaceShapeCatalog.Entries(new[] { "Stern", "Weight", "IrisSize", "NoseWidth" }, isMale: false);

        CollectionAssert.AreEqual(new[] { "IrisSize", "EyeLidHeight", "NoseWidth" }, entries.Select(e => e.Key).ToArray());
        Assert.IsTrue(entries[0].Enabled);
        Assert.IsFalse(entries[1].Enabled);
    }

    [Test]
    public void Face_labels_are_indonesian_and_unknown_keys_are_split() {
        Assert.AreEqual("Ukuran Iris", FaceShapeCatalog.Label("IrisSize"));
        Assert.AreEqual("Nose Tip Round", FaceShapeCatalog.Label("NoseTipRound"));
    }

    // ---------------------------------------------------------------- Stiker

    [Test]
    public void Adding_a_sticker_puts_it_on_the_chest_with_defaults() {
        CustomCharacterRecord edited = RecordEdits.WithDecalAdded(Sample(), "Decal_GoVistaG");

        DecalRecord decal = edited.decals.Single();
        Assert.AreEqual("Decal_GoVistaG", decal.decal);
        Assert.AreEqual("Chest", decal.position);
        Assert.AreEqual(HexColor.White, decal.color);
    }

    [Test]
    public void Stickers_stop_at_the_maximum() {
        CustomCharacterRecord record = Sample();
        for (int i = 0; i < CustomizationRules.MaxDecals; i++) record = RecordEdits.WithDecalAdded(record, "Decal_GoVistaG");

        Assert.AreSame(record, RecordEdits.WithDecalAdded(record, "Decal_GoVistaG"));
    }

    [Test]
    public void Sticker_edit_clamps_size_and_rotation_and_rejects_unknown_positions() {
        CustomCharacterRecord record = RecordEdits.WithDecalAdded(Sample(), "Decal_GoVistaG");

        CustomCharacterRecord edited = RecordEdits.WithDecal(record, 0, new DecalRecord {
            decal = "Decal_GoVistaG", position = "Back", size = 5f, rotation = 400f, color = "#ff0000"
        });
        Assert.AreEqual("Back", edited.decals[0].position);
        Assert.AreEqual(CustomizationRules.MaxDecalSize, edited.decals[0].size);
        Assert.AreEqual(CustomizationRules.MaxDecalRotation, edited.decals[0].rotation);
        Assert.AreEqual("#FF0000", edited.decals[0].color);

        Assert.AreSame(record, RecordEdits.WithDecal(record, 0, new DecalRecord { decal = "Decal_GoVistaG", position = "Hat" }));
        Assert.AreSame(record, RecordEdits.WithDecal(record, 3, new DecalRecord { decal = "Decal_GoVistaG" }));
    }

    [Test]
    public void Sticker_offsets_are_kept_and_clamped() {
        CustomCharacterRecord record = RecordEdits.WithDecalAdded(Sample(), "Decal_GoVistaG");

        CustomCharacterRecord edited = RecordEdits.WithDecal(record, 0, new DecalRecord {
            decal = "Decal_GoVistaG", position = "Chest", size = 0.15f, color = HexColor.White,
            offsetX = 0.03f, offsetY = -1f
        });

        Assert.AreEqual(0.03f, edited.decals[0].offsetX, 1e-6f);
        Assert.AreEqual(-CustomizationRules.MaxDecalOffset, edited.decals[0].offsetY, 1e-6f);
        Assert.AreEqual(0f, record.decals[0].offsetX, "original must not change");
    }

    [Test]
    public void Validator_clamps_sticker_offsets_from_outside_data() {
        CustomCharacterRecord record = Sample();
        record.decals.Add(new DecalRecord { decal = "Decal_GoVistaG", position = "Back", offsetX = 9f, offsetY = float.NaN });

        DecalRecord clean = CustomCharacterValidator.Sanitize(record, _ => true).decals[0];

        Assert.AreEqual(CustomizationRules.MaxDecalOffset, clean.offsetX, 1e-6f);
        Assert.AreEqual(0f, clean.offsetY);
    }

    [Test]
    public void Removing_a_sticker_keeps_the_others_in_order() {
        CustomCharacterRecord record = RecordEdits.WithDecalAdded(RecordEdits.WithDecalAdded(Sample(), "A"), "B");

        CustomCharacterRecord edited = RecordEdits.WithoutDecal(record, 0);

        CollectionAssert.AreEqual(new[] { "B" }, edited.decals.Select(d => d.decal).ToArray());
        Assert.AreEqual(2, record.decals.Count, "original must not change");
    }

    [Test]
    public void Without_decals_removes_only_matching_stickers_without_touching_the_original() {
        CustomCharacterRecord record = RecordEdits.WithDecalAdded(RecordEdits.WithDecalAdded(Sample(), "A"), "B");

        CustomCharacterRecord edited = RecordEdits.WithoutDecals(record, d => d.decal == "A");

        CollectionAssert.AreEqual(new[] { "B" }, edited.decals.Select(d => d.decal).ToArray());
        Assert.AreEqual(2, record.decals.Count, "original must not change");
    }

    [Test]
    public void Without_decals_returns_the_same_record_when_nothing_matches() {
        CustomCharacterRecord record = RecordEdits.WithDecalAdded(Sample(), "A");

        Assert.AreSame(record, RecordEdits.WithoutDecals(record, d => d.decal == "Z"));
    }

    // ---------------------------------------------------------------- Motif

    [Test]
    public void Pattern_scale_is_clamped_and_applied_to_both_axes() {
        CustomCharacterRecord edited = RecordEdits.WithPatternScale(Sample(), "Top", 99f);

        OutfitRecord top = RecordEdits.Find(edited, "Top");
        Assert.AreEqual(CustomizationRules.MaxPatternScale, top.patternScaleX);
        Assert.AreEqual(CustomizationRules.MaxPatternScale, top.patternScaleY);
    }

    [Test]
    public void Only_pattern_folder_paths_are_valid() {
        Assert.IsTrue(CustomizationRules.IsPatternPath(""));
        Assert.IsTrue(CustomizationRules.IsPatternPath("Pattern/Pattern_Plaid"));
        Assert.IsFalse(CustomizationRules.IsPatternPath("Decal/Decal_GoVistaG"));
        Assert.IsFalse(CustomizationRules.IsPatternPath("Pattern/../Secret"));
        Assert.IsFalse(CustomizationRules.IsPatternPath("Pattern/"));
    }

    [Test]
    public void Validator_drops_patterns_outside_the_pattern_folder() {
        CustomCharacterRecord record = Sample();
        record.outfits[0].pattern = "Top/BSMC_Top_Hoodie";

        CustomCharacterRecord clean = CustomCharacterValidator.Sanitize(record, _ => true);

        Assert.AreEqual("", clean.outfits[0].pattern);
    }
}

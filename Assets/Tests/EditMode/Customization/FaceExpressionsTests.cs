using System.Collections.Generic;
using NUnit.Framework;

public class FaceExpressionsTests {
    private static CustomCharacterRecord Sample(int expression = 0) {
        return new CustomCharacterRecord {
            templateId = "male",
            outfits = new List<OutfitRecord> { new OutfitRecord { path = "Head/Head_BasicHead" } },
            expression = expression
        };
    }

    [Test]
    public void There_are_eight_expressions_and_the_first_is_neutral() {
        Assert.AreEqual(8, FaceExpressions.Count);
        Assert.AreEqual(0, FaceExpressions.Weights(0).Count, "Ekspresi 1 = netral (semua blendshape 0)");
    }

    [Test]
    public void Labels_are_numbered_from_one() {
        Assert.AreEqual("Ekspresi 1", FaceExpressions.Label(0));
        Assert.AreEqual("Ekspresi 8", FaceExpressions.Label(7));
    }

    [Test]
    public void Smile_uses_bozo_clip_weights() {
        IReadOnlyDictionary<string, float> smile = FaceExpressions.Weights(1);

        Assert.AreEqual(52f, smile["Mouth_Happy_R"], 0.01f);
        Assert.AreEqual(80.9f, smile["Mouth_Wide_L"], 0.01f);
    }

    [Test]
    public void Out_of_range_index_falls_back_to_neutral() {
        Assert.AreEqual(0, FaceExpressions.Clamp(-3));
        Assert.AreEqual(0, FaceExpressions.Clamp(99));
        Assert.AreEqual(5, FaceExpressions.Clamp(5));
        Assert.AreEqual(0, FaceExpressions.Weights(99).Count);
    }

    [Test]
    public void Blendshape_names_are_matched_with_or_without_mesh_prefix() {
        Assert.AreEqual("Mouth_Happy_R", FaceExpressions.KeyOf("BS_CombinedSkinnedMesh.Expression_Mouth_Happy_R"));
        Assert.AreEqual("Jaw_Open", FaceExpressions.KeyOf("Expression_Jaw_Open"));
        Assert.IsNull(FaceExpressions.KeyOf("BS_Head.Shape_NoseWidth"));
        Assert.IsNull(FaceExpressions.KeyOf(null));
    }

    [Test]
    public void With_expression_returns_a_copy_and_keeps_the_original() {
        CustomCharacterRecord original = Sample();

        CustomCharacterRecord edited = RecordEdits.WithExpression(original, 3);

        Assert.AreEqual(3, edited.expression);
        Assert.AreEqual(0, original.expression, "original must not change");
        Assert.AreSame(edited, RecordEdits.WithExpression(edited, 3), "same value = no new copy (no extra undo step)");
    }

    [Test]
    public void Clone_copies_the_expression() {
        Assert.AreEqual(6, Sample(6).Clone().expression);
    }

    [Test]
    public void Validator_resets_unknown_expression_to_neutral() {
        CustomCharacterRecord clean = CustomCharacterValidator.Sanitize(Sample(42), _ => true);
        CustomCharacterRecord kept = CustomCharacterValidator.Sanitize(Sample(4), _ => true);

        Assert.AreEqual(0, clean.expression);
        Assert.AreEqual(4, kept.expression);
    }

    [Test]
    public void Face_tab_has_an_expression_sub_tab_after_shape() {
        EditorCategory face = EditorCategories.Get("Wajah");

        Assert.AreEqual("Ekspresi", face.SubTabs[1].Label);
        Assert.AreEqual(EditorPanelKind.Expressions, face.SubTabs[1].Panel);
    }
}

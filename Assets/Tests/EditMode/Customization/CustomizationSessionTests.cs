using System.Collections.Generic;
using NUnit.Framework;

public class CustomizationSessionTests {
    private static CustomCharacterRecord Start() {
        return new CustomCharacterRecord {
            templateId = "male",
            outfits = new List<OutfitRecord> { new OutfitRecord { path = "Top/A", colors = new List<string> { "#111111" } } }
        };
    }

    private static CustomizationSession NewSession() => new CustomizationSession(Start(), slot: 0);

    [Test]
    public void New_session_is_clean_and_cannot_undo() {
        CustomizationSession session = NewSession();
        Assert.IsFalse(session.IsDirty);
        Assert.IsFalse(session.CanUndo);
        Assert.AreEqual(0, session.Slot);
    }

    [Test]
    public void Applying_a_change_makes_it_dirty_and_undoable() {
        CustomizationSession session = NewSession();
        session.Apply(r => RecordEdits.WithChannelColor(r, "Top", 0, "#FF0000"));
        Assert.IsTrue(session.IsDirty);
        Assert.IsTrue(session.CanUndo);
        Assert.AreEqual("#FF0000", RecordEdits.Find(session.Current, "Top").colors[0]);
    }

    [Test]
    public void A_change_that_returns_the_same_record_is_ignored() {
        CustomizationSession session = NewSession();
        session.Apply(r => r);
        Assert.IsFalse(session.IsDirty);
        Assert.IsFalse(session.CanUndo);
    }

    [Test]
    public void Undo_restores_the_previous_record_and_cleans_when_back_at_start() {
        CustomizationSession session = NewSession();
        session.Apply(r => RecordEdits.WithChannelColor(r, "Top", 0, "#FF0000"));
        session.Apply(r => RecordEdits.WithChannelColor(r, "Top", 0, "#00FF00"));

        Assert.IsTrue(session.Undo());
        Assert.AreEqual("#FF0000", RecordEdits.Find(session.Current, "Top").colors[0]);
        Assert.IsTrue(session.IsDirty);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual("#111111", RecordEdits.Find(session.Current, "Top").colors[0]);
        Assert.IsFalse(session.IsDirty);
        Assert.IsFalse(session.Undo());
    }

    [Test]
    public void Saving_marks_clean_and_undo_after_save_makes_it_dirty_again() {
        CustomizationSession session = NewSession();
        session.Apply(r => RecordEdits.WithChannelColor(r, "Top", 0, "#FF0000"));
        session.MarkSaved(slot: 1);
        Assert.IsFalse(session.IsDirty);
        Assert.AreEqual(1, session.Slot);

        session.Undo();
        Assert.IsTrue(session.IsDirty);
    }

    [Test]
    public void Replacing_the_record_is_one_undoable_step() {
        CustomizationSession session = NewSession();
        CustomCharacterRecord random = Start();
        random.outfits[0].path = "Top/B";
        session.Replace(random);
        Assert.AreEqual("Top/B", session.Current.outfits[0].path);
        session.Undo();
        Assert.AreEqual("Top/A", session.Current.outfits[0].path);
    }

    [Test]
    public void Current_is_a_copy_so_callers_cannot_mutate_history() {
        CustomizationSession session = NewSession();
        session.Current.outfits[0].path = "Top/Hacked";
        Assert.AreEqual("Top/A", session.Current.outfits[0].path);
    }

    [Test]
    public void Undo_history_is_bounded() {
        CustomizationSession session = NewSession();
        for (int i = 0; i < CustomizationSession.MaxUndo + 10; i++) {
            string hex = "#" + (i % 256).ToString("X2") + "0000";
            session.Apply(r => RecordEdits.WithChannelColor(r, "Top", 0, hex));
        }
        int undone = 0;
        while (session.Undo()) undone++;
        Assert.AreEqual(CustomizationSession.MaxUndo, undone);
    }
}

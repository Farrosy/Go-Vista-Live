using NUnit.Framework;

public class CustomSlotRulesTests {
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(5, false)]
    public void At_most_two_custom_slots_can_be_created(int existing, bool expected) {
        Assert.AreEqual(expected, CustomSlotRules.CanCreate(existing));
    }

    [Test]
    public void Deleting_the_active_slot_falls_back_to_the_template() {
        Assert.AreEqual(CustomSlotRules.NoActiveSlot, CustomSlotRules.ActiveAfterDelete(activeSlot: 1, deletedSlot: 1));
    }

    [Test]
    public void Deleting_a_slot_before_the_active_one_shifts_the_active_index() {
        Assert.AreEqual(0, CustomSlotRules.ActiveAfterDelete(activeSlot: 1, deletedSlot: 0));
    }

    [Test]
    public void Deleting_a_slot_after_the_active_one_keeps_the_active_index() {
        Assert.AreEqual(0, CustomSlotRules.ActiveAfterDelete(activeSlot: 0, deletedSlot: 1));
    }

    [Test]
    public void Deleting_while_a_template_is_active_keeps_the_template() {
        Assert.AreEqual(CustomSlotRules.NoActiveSlot,
                        CustomSlotRules.ActiveAfterDelete(CustomSlotRules.NoActiveSlot, deletedSlot: 0));
    }

    [TestCase(0, "Karakter 1")]
    [TestCase(1, "Karakter 2")]
    public void Slots_are_named_by_position(int index, string expected) {
        Assert.AreEqual(expected, CustomSlotRules.SlotName(index));
    }
}

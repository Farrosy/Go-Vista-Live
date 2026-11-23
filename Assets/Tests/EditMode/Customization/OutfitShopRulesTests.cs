using System.Collections.Generic;
using NUnit.Framework;

public class OutfitShopRulesTests {
    private static CustomCharacterRecord Record(params string[] paths) {
        var record = new CustomCharacterRecord { templateId = "male" };
        foreach (string path in paths) record.outfits.Add(new OutfitRecord { path = path });
        return record;
    }

    [Test]
    public void Price_grows_with_grid_order() {
        Assert.AreEqual(50, OutfitShopRules.PriceAt(0));
        Assert.AreEqual(75, OutfitShopRules.PriceAt(1));
        Assert.AreEqual(500, OutfitShopRules.PriceAt(18));
    }

    [Test]
    public void Unknown_index_is_free() {
        Assert.AreEqual(0, OutfitShopRules.PriceAt(-1));
    }

    [Test]
    public void Items_include_outfits_and_stickers_once() {
        CustomCharacterRecord record = Record("Top/A", "Bottom/B", "");
        record.decals.Add(new DecalRecord { decal = "Star" });
        record.decals.Add(new DecalRecord { decal = "Star" });

        List<string> items = OutfitShopRules.ItemsOf(record);

        CollectionAssert.AreEquivalent(new[] { "Top/A", "Bottom/B", "sticker:Star" }, items);
    }

    [Test]
    public void Unpaid_skips_owned_items_ignoring_case() {
        CustomCharacterRecord record = Record("Top/A", "Bottom/B");
        var owned = new HashSet<string>(new[] { "top/a" }, System.StringComparer.OrdinalIgnoreCase);

        List<string> unpaid = OutfitShopRules.Unpaid(record, owned.Contains, _ => 100);

        CollectionAssert.AreEqual(new[] { "Bottom/B" }, unpaid);
    }

    [Test]
    public void Free_items_are_never_unpaid() {
        CustomCharacterRecord record = Record("Top/A");

        List<string> unpaid = OutfitShopRules.Unpaid(record, _ => false, _ => 0);

        Assert.IsEmpty(unpaid);
    }

    [Test]
    public void Total_sums_prices() {
        int total = OutfitShopRules.Total(new[] { "a", "b" }, id => id == "a" ? 50 : 75);

        Assert.AreEqual(125, total);
    }

    [Test]
    public void Cannot_afford_when_balance_is_short() {
        Assert.IsTrue(OutfitShopRules.CanAfford(125, 125));
        Assert.IsFalse(OutfitShopRules.CanAfford(124, 125));
    }

    [Test]
    public void Sticker_id_round_trips() {
        string id = OutfitShopRules.StickerId("Heart");

        Assert.IsTrue(OutfitShopRules.IsSticker(id));
        Assert.AreEqual("Heart", OutfitShopRules.StickerName(id));
        Assert.IsFalse(OutfitShopRules.IsSticker("Top/A"));
    }
}

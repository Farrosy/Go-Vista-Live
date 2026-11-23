using NUnit.Framework;

public class CarDealerRulesTests {
    [Test]
    public void NotOwned_AffordableIsBuy_OtherwiseDisabled() {
        Assert.AreEqual(DealerButtonState.Buy, CarDealerRules.StateFor(false, false, 10000, 10000));
        Assert.AreEqual(DealerButtonState.BuyDisabled, CarDealerRules.StateFor(false, false, 10000, 9999));
    }

    [Test]
    public void FreeCar_IsAlwaysBuyable() {
        Assert.AreEqual(DealerButtonState.Buy, CarDealerRules.StateFor(false, false, 0, 0));
    }

    [Test]
    public void Owned_ShowsBoughtOnlyRightAfterPurchase_ThenSpawn() {
        Assert.AreEqual(DealerButtonState.Bought, CarDealerRules.StateFor(true, true, 10000, 0));
        Assert.AreEqual(DealerButtonState.Spawn, CarDealerRules.StateFor(true, false, 10000, 0));
    }

    [Test]
    public void Labels() {
        Assert.AreEqual("Buy", CarDealerRules.ButtonLabel(DealerButtonState.BuyDisabled));
        Assert.AreEqual("Bought!", CarDealerRules.ButtonLabel(DealerButtonState.Bought));
        Assert.AreEqual("Spawn", CarDealerRules.ButtonLabel(DealerButtonState.Spawn));
        Assert.AreEqual("Gratis", CarDealerRules.PriceLabel(0));
        Assert.AreEqual("10,000G", CarDealerRules.PriceLabel(10000));
    }

    [Test]
    public void SpeedFill_ScalesAndClamps() {
        Assert.AreEqual(6, CarDealerRules.SpeedFill(60f));
        Assert.AreEqual(10, CarDealerRules.SpeedFill(250f));
        Assert.AreEqual(1, CarDealerRules.SpeedFill(2f));
        Assert.AreEqual(0, CarDealerRules.SpeedFill(0f));
    }
}

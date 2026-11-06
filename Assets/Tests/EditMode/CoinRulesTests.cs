using NUnit.Framework;

public class CoinRulesTests {
    [Test]
    public void DeathPenalty_TakesFiftyWhenBalanceIsEnough() {
        Assert.AreEqual(50, CoinRules.DeathPenalty(100));
    }

    [Test]
    public void DeathPenalty_TakesOnlyWhatIsLeftSoBalanceNeverGoesNegative() {
        Assert.AreEqual(30, CoinRules.DeathPenalty(30));
    }

    [Test]
    public void DeathPenalty_IsZeroWhenBalanceIsEmpty() {
        Assert.AreEqual(0, CoinRules.DeathPenalty(0));
    }

    [Test]
    public void StartingBalance_IsOneHundred() {
        Assert.AreEqual(100, CoinRules.StartingBalance);
    }

    [TestCase(100, "100")]
    [TestCase(12450, "12.450")]
    public void FormatBalance_UsesIndonesianThousandSeparator(int balance, string expected) {
        Assert.AreEqual(expected, CoinRules.FormatBalance(balance));
    }
}

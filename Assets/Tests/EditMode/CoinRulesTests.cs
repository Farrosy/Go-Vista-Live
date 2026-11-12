using NUnit.Framework;

public class CoinRulesTests {
    [Test]
    public void DeathPenalty_TakesTwentyWhenBalanceIsEnough() {
        Assert.AreEqual(20, CoinRules.DeathPenalty(100));
    }

    [Test]
    public void DeathPenalty_TakesOnlyWhatIsLeftSoBalanceNeverGoesNegative() {
        Assert.AreEqual(15, CoinRules.DeathPenalty(15));
    }

    [Test]
    public void DeathPenalty_IsZeroWhenBalanceIsEmpty() {
        Assert.AreEqual(0, CoinRules.DeathPenalty(0));
    }

    [Test]
    public void StartingBalance_IsOneHundred() {
        Assert.AreEqual(100, CoinRules.StartingBalance);
    }

    [Test]
    public void Add_IncreasesBalanceByReward() {
        Assert.AreEqual(300, CoinRules.Add(100, 200));
    }

    [Test]
    public void Add_IgnoresNegativeAmounts() {
        Assert.AreEqual(100, CoinRules.Add(100, -50));
    }

    [Test]
    public void Add_ClampsInsteadOfOverflowing() {
        Assert.AreEqual(int.MaxValue, CoinRules.Add(int.MaxValue - 5, 200));
    }

    [TestCase(-30, 0)]
    [TestCase(0, 0)]
    [TestCase(250, 250)]
    public void Sanitize_NeverReturnsNegativeBalance(int stored, int expected) {
        Assert.AreEqual(expected, CoinRules.Sanitize(stored));
    }

    [TestCase(100, "100")]
    [TestCase(12450, "12.450")]
    public void FormatBalance_UsesIndonesianThousandSeparator(int balance, string expected) {
        Assert.AreEqual(expected, CoinRules.FormatBalance(balance));
    }
}

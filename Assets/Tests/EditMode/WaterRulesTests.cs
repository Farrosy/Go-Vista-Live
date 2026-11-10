using NUnit.Framework;

public class WaterRulesTests {
    [TestCase(-1f, false)]
    [TestCase(0.39f, false)]
    [TestCase(0.4f, true)]
    [TestCase(5f, true)]
    public void IsWading_StartsAtKneeDepth(float depth, bool expected) {
        Assert.AreEqual(expected, WaterRules.IsWading(depth));
    }

    [TestCase(22f, 21.8f, false)]
    [TestCase(21.8f, 21.8f, false)]
    [TestCase(21.7f, 21.8f, true)]
    public void IsHeadUnderWater_ComparesHeadWithWaterLevel(float headY, float level, bool expected) {
        Assert.AreEqual(expected, WaterRules.IsHeadUnderWater(headY, level));
    }
}

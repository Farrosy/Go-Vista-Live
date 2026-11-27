using NUnit.Framework;

public class GraphicsQualityRulesTests {
    [Test]
    public void EffectsOnByDefault_WhenDeviceHasEnoughMemory() {
        Assert.IsTrue(GraphicsQualityRules.DefaultEffectsOn(GraphicsQualityRules.MinMemoryMbForEffects));
        Assert.IsTrue(GraphicsQualityRules.DefaultEffectsOn(8192));
    }

    [Test]
    public void EffectsOffByDefault_OnLowMemoryDevice() {
        Assert.IsFalse(GraphicsQualityRules.DefaultEffectsOn(GraphicsQualityRules.MinMemoryMbForEffects - 1));
        Assert.IsFalse(GraphicsQualityRules.DefaultEffectsOn(2048));
    }

    [Test]
    public void EffectsOnByDefault_WhenMemoryIsUnknown() {
        Assert.IsTrue(GraphicsQualityRules.DefaultEffectsOn(0));
        Assert.IsTrue(GraphicsQualityRules.DefaultEffectsOn(-1));
    }
}

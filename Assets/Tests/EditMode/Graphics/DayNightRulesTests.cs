using NUnit.Framework;

public class DayNightRulesTests {
    private const double Cycle = DayNightRules.DefaultCycleSeconds;

    [Test]
    public void Hour_StartsAtMidnight_AndAdvancesOneHourPerMinute() {
        Assert.AreEqual(0f, DayNightRules.HourFromUnixSeconds(0, Cycle, 0f), 1e-4f);
        Assert.AreEqual(1f, DayNightRules.HourFromUnixSeconds(60, Cycle, 0f), 1e-4f);
        Assert.AreEqual(12f, DayNightRules.HourFromUnixSeconds(720, Cycle, 0f), 1e-4f);
    }

    [Test]
    public void Hour_WrapsEveryCycle_SoAllPlayersAgree() {
        double now = 1_800_000_000.0;
        Assert.AreEqual(
            DayNightRules.HourFromUnixSeconds(now, Cycle, 0f),
            DayNightRules.HourFromUnixSeconds(now + Cycle * 5, Cycle, 0f), 1e-3f);
    }

    [Test]
    public void Hour_OffsetWrapsAround24() {
        Assert.AreEqual(2f, DayNightRules.HourFromUnixSeconds(22 * 60, Cycle, 4f), 1e-4f);
        Assert.AreEqual(23f, DayNightRules.WrapHour(-1f), 1e-4f);
    }

    [Test]
    public void Hour_RejectsNonPositiveCycle() {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => DayNightRules.HourFromUnixSeconds(0, 0, 0f));
    }

    [Test]
    public void Sun_IsOnHorizonAtSunriseAndSunset_HighestAtNoon() {
        Assert.AreEqual(0f, DayNightRules.SunElevation(6f, 60f), 1e-3f);
        Assert.AreEqual(0f, DayNightRules.SunElevation(18f, 60f), 1e-3f);
        Assert.AreEqual(60f, DayNightRules.SunElevation(12f, 60f), 1e-3f);
        Assert.AreEqual(-60f, DayNightRules.SunElevation(0f, 60f), 1e-3f);
    }

    [Test]
    public void SunAzimuth_SweepsFromSunriseThroughNoonToSunset() {
        Assert.AreEqual(-90f, DayNightRules.SunAzimuthOffset(6f), 1e-3f);
        Assert.AreEqual(0f, DayNightRules.SunAzimuthOffset(12f), 1e-3f);
        Assert.AreEqual(90f, DayNightRules.SunAzimuthOffset(18f), 1e-3f);
    }

    [Test]
    public void StreetLights_OffAtNoon_OnAtNight() {
        Assert.AreEqual(0f, DayNightRules.StreetLightLevel(DayNightRules.SunElevation(12f, 60f)));
        Assert.AreEqual(1f, DayNightRules.StreetLightLevel(DayNightRules.SunElevation(22f, 60f)));
        Assert.AreEqual(1f, DayNightRules.StreetLightLevel(DayNightRules.SunElevation(4f, 60f)));
    }

    [Test]
    public void StreetLights_FadeInAroundSunset() {
        float level = DayNightRules.StreetLightLevel(DayNightRules.SunElevation(18f, 60f));
        Assert.Greater(level, 0f);
        Assert.Less(level, 1f);
    }

    [Test]
    public void NightFactor_IsZeroByDay_OneByNight() {
        Assert.AreEqual(0f, DayNightRules.NightFactor(45f));
        Assert.AreEqual(1f, DayNightRules.NightFactor(-30f));
    }

    [Test]
    public void Golden_PeaksNearHorizon_NotAtNoonOrMidnight() {
        Assert.AreEqual(0f, DayNightRules.GoldenFactor(60f));
        Assert.AreEqual(0f, DayNightRules.GoldenFactor(-60f));
        Assert.Greater(DayNightRules.GoldenFactor(4f), 0.8f);
    }
}

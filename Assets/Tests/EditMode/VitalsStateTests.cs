using NUnit.Framework;

public class VitalsStateTests {
    private const float Tolerance = 0.001f;

    [Test]
    public void Full_StartsAtMaxHealthAndOxygen() {
        VitalsState state = VitalsState.Full;

        Assert.AreEqual(100, state.Health);
        Assert.AreEqual(1f, state.OxygenFraction, Tolerance);
        Assert.IsFalse(state.IsDead);
    }

    [Test]
    public void TickOxygen_LosesOneSegmentPerSecondWhileHeadIsUnderWater() {
        VitalsState state = VitalsState.Full.TickOxygen(1f, headUnderWater: true);

        Assert.AreEqual(9, state.OxygenSegments);
    }

    [Test]
    public void TickOxygen_KillsAfterExactlyTenSecondsUnderWater() {
        VitalsState almost = VitalsState.Full.TickOxygen(9.99f, headUnderWater: true);
        VitalsState dead = almost.TickOxygen(0.01f, headUnderWater: true);

        Assert.IsFalse(almost.IsDead);
        Assert.IsTrue(dead.IsDead);
        Assert.AreEqual(0, dead.OxygenSegments);
    }

    [Test]
    public void TickOxygen_RefillsFromEmptyInTwoSecondsAfterSurfacing() {
        VitalsState empty = VitalsState.Full.TickOxygen(9.9f, headUnderWater: true);

        VitalsState partial = empty.TickOxygen(1f, headUnderWater: false);
        VitalsState full = empty.TickOxygen(2f, headUnderWater: false);

        Assert.Less(partial.OxygenFraction, 1f);
        Assert.AreEqual(1f, full.OxygenFraction, Tolerance);
    }

    [Test]
    public void TickOxygen_DoesNotChangeTheOriginalState() {
        VitalsState original = VitalsState.Full;

        original.TickOxygen(5f, headUnderWater: true);

        Assert.AreEqual(1f, original.OxygenFraction, Tolerance);
    }

    [TestCase(1f, OxygenLevel.Normal)]
    [TestCase(0.51f, OxygenLevel.Normal)]
    [TestCase(0.5f, OxygenLevel.Warning)]
    [TestCase(0.21f, OxygenLevel.Warning)]
    [TestCase(0.2f, OxygenLevel.Danger)]
    [TestCase(0f, OxygenLevel.Danger)]
    public void Level_FollowsWarningAndDangerThresholds(float fraction, OxygenLevel expected) {
        float secondsUnderWater = (1f - fraction) * VitalsRules.OxygenDurationSeconds;

        VitalsState state = VitalsState.Full.TickOxygen(secondsUnderWater, headUnderWater: true);

        Assert.AreEqual(expected, state.Level);
    }

    [Test]
    public void ApplyDamage_ClampsHealthAtZeroAndKills() {
        VitalsState state = VitalsState.Full.ApplyDamage(250, DeathCause.Fall);

        Assert.AreEqual(0, state.Health);
        Assert.IsTrue(state.IsDead);
    }

    [TestCase(20, false)]
    [TestCase(19, true)]
    public void IsHealthCritical_IsTrueBelowTwentyPercent(int health, bool expected) {
        VitalsState state = VitalsState.Full.ApplyDamage(100 - health, DeathCause.Fall);

        Assert.AreEqual(expected, state.IsHealthCritical);
    }

    [TestCase(0f, 0)]
    [TestCase(1.5f, 0)]
    [TestCase(3f, 0)]
    [TestCase(4f, 10)]
    [TestCase(5.5f, 25)]
    [TestCase(13f, 100)]
    public void FallDamage_IsSafeUpToThreeMetresThenTenPerMetre(float metres, int expected) {
        Assert.AreEqual(expected, VitalsRules.FallDamage(metres));
    }

    [TestCase(12.9f, 100, false)]
    [TestCase(13f, 100, true)]
    [TestCase(50f, 100, true)]
    [TestCase(3f, 1, false)]
    [TestCase(3.1f, 1, true)]
    [TestCase(5f, 20, true)]
    [TestCase(4.9f, 20, false)]
    [TestCase(50f, 0, false)]
    public void IsLethalFall_IsTrueOnlyWhenLandingWouldEmptyHealth(float metres, int health, bool expected) {
        Assert.AreEqual(expected, VitalsRules.IsLethalFall(metres, health));
    }

    [Test]
    public void Cause_IsNoneWhileAlive() {
        Assert.AreEqual(DeathCause.None, VitalsState.Full.ApplyDamage(50, DeathCause.Fall).Cause);
    }

    [Test]
    public void Cause_IsFallWhenHealthRunsOut() {
        Assert.AreEqual(DeathCause.Fall, VitalsState.Full.ApplyDamage(100, DeathCause.Fall).Cause);
    }

    [Test]
    public void Cause_IsCarWhenACarLandsTheFinalHit() {
        VitalsState state = VitalsState.Full.ApplyDamage(90, DeathCause.Fall).ApplyDamage(20, DeathCause.Car);

        Assert.AreEqual(DeathCause.Car, state.Cause);
    }

    [Test]
    public void Cause_IsFallWhenAFallLandsTheFinalHitAfterACar() {
        VitalsState state = VitalsState.Full.ApplyDamage(20, DeathCause.Car).ApplyDamage(80, DeathCause.Fall);

        Assert.AreEqual(DeathCause.Fall, state.Cause);
    }

    [Test]
    public void Cause_SurvivesOxygenTicks() {
        VitalsState state = VitalsState.Full.ApplyDamage(100, DeathCause.Car).TickOxygen(1f, headUnderWater: false);

        Assert.AreEqual(DeathCause.Car, state.Cause);
    }

    [Test]
    public void Cause_IsDrownedWhenOxygenRunsOut() {
        VitalsState state = VitalsState.Full.TickOxygen(10f, headUnderWater: true);

        Assert.AreEqual(DeathCause.Drowned, state.Cause);
    }
}

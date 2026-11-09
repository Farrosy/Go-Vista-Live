using NUnit.Framework;

public class CarHitRulesTests {
    private const float PlayerRadius = 0.3f;
    private const float PlayerHeight = 1.8f;
    private static readonly CarExtents Car = new CarExtents(halfWidth: 1f, rear: 2.5f, front: 3f, bottom: 0f, top: 1.6f);

    private static bool TouchesAt(float x, float feetY, float z) {
        return CarHitRules.Touches(Car, x, feetY, z, PlayerRadius, PlayerHeight);
    }

    [TestCase(0f, false)]
    [TestCase(1.99f, false)]
    [TestCase(2f, true)]
    [TestCase(9f, true)]
    public void CanHurt_NeedsCarMovingAtLeastTwoMetresPerSecond(float speed, bool expected) {
        Assert.AreEqual(expected, CarHitRules.CanHurt(speed, now: 10f, vulnerableFrom: 0f));
    }

    [TestCase(11.99f, false)]
    [TestCase(12f, true)]
    public void CanHurt_IgnoresHitsWhileInvulnerable(float now, bool expected) {
        Assert.AreEqual(expected, CarHitRules.CanHurt(speed: 9f, now, vulnerableFrom: 12f));
    }

    [Test]
    public void Damage_IsTwentyAndInvulnerabilityLastsTwoSeconds() {
        Assert.AreEqual(20, CarHitRules.Damage);
        Assert.AreEqual(2f, CarHitRules.InvulnerableSeconds, 0.001f);
    }

    [TestCase(0f, 3.3f)]   // depan
    [TestCase(0f, -2.8f)]  // belakang
    [TestCase(1.3f, 0f)]   // kanan
    [TestCase(-1.3f, 0f)]  // kiri
    public void Touches_IsTrueOnEverySideOfTheCar(float x, float z) {
        Assert.IsTrue(TouchesAt(x, 0f, z));
    }

    [TestCase(0f, 4f)]
    [TestCase(0f, -3.6f)]
    [TestCase(2f, 0f)]
    [TestCase(-2f, 0f)]
    public void Touches_IsFalseAwayFromTheCar(float x, float z) {
        Assert.IsFalse(TouchesAt(x, 0f, z));
    }

    [Test]
    public void Touches_IsFalseJustOutsideACornerEvenWhenBothAxesAreClose() {
        Assert.IsFalse(TouchesAt(1.35f, 0f, 3.35f));
    }

    [TestCase(1.6f)]
    [TestCase(1.4f)]
    [TestCase(3f)]
    public void Touches_IsFalseWhenStandingOnTheRoof(float feetY) {
        Assert.IsFalse(TouchesAt(0f, feetY, 0f));
    }

    [Test]
    public void Touches_IsTrueWhenFeetAreBelowTheRoof() {
        Assert.IsTrue(TouchesAt(0f, 1.2f, 0f));
    }

    [Test]
    public void Touches_IsFalseWhenPlayerIsEntirelyBelowTheCar() {
        Assert.IsFalse(TouchesAt(0f, -3f, 0f));
    }
}

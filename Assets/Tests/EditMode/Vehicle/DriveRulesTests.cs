using NUnit.Framework;

public class DriveRulesTests {
    private static void Pedals(float gas, float brakePedal, float speed, out float drive, out float brake) {
        DriveRules.ResolvePedals(gas, brakePedal, speed, out drive, out brake);
    }

    [Test]
    public void Gas_DrivesForward() {
        Pedals(1f, 0f, 0f, out float drive, out float brake);
        Assert.AreEqual(1f, drive);
        Assert.AreEqual(0f, brake);
    }

    [Test]
    public void BrakePedal_BrakesWhileMovingForward() {
        Pedals(0f, 1f, 5f, out float drive, out float brake);
        Assert.AreEqual(0f, drive);
        Assert.AreEqual(1f, brake);
    }

    [Test]
    public void BrakePedal_ReversesOnceStopped() {
        Pedals(0f, 0.6f, 0.2f, out float drive, out float brake);
        Assert.AreEqual(-0.6f, drive, 1e-5f);
        Assert.AreEqual(0f, brake);
    }

    [Test]
    public void Gas_BrakesWhileStillReversing() {
        Pedals(0.8f, 0f, -3f, out float drive, out float brake);
        Assert.AreEqual(0f, drive);
        Assert.AreEqual(0.8f, brake, 1e-5f);
    }

    [Test]
    public void BothPedals_BrakeWins() {
        Pedals(1f, 0.5f, 4f, out float drive, out float brake);
        Assert.AreEqual(0f, drive);
        Assert.AreEqual(1f, brake);
    }

    [Test]
    public void NoPedals_NoDriveNoBrake() {
        Pedals(0f, 0f, 4f, out float drive, out float brake);
        Assert.AreEqual(0f, drive);
        Assert.AreEqual(0f, brake);
    }

    [TestCase(0f, 1f)]
    [TestCase(8.5f, 1f)]
    [TestCase(9.25f, 0.5f)]
    [TestCase(10f, 0f)]
    [TestCase(12f, 0f)]
    [TestCase(-10f, 0f)]
    public void LimiterFactor_FadesTorqueNearMaxSpeed(float speed, float expected) {
        Assert.AreEqual(expected, DriveRules.LimiterFactor(speed, maxSpeed: 10f), 1e-4f);
    }

    [Test]
    public void SteerAngle_ShrinksWithSpeed() {
        Assert.AreEqual(30f, DriveRules.SteerAngle(1f, 0f, 10f, 30f, 10f), 1e-4f);
        Assert.AreEqual(-20f, DriveRules.SteerAngle(-1f, 5f, 10f, 30f, 10f), 1e-4f);
        Assert.AreEqual(10f, DriveRules.SteerAngle(1f, 50f, 10f, 30f, 10f), 1e-4f);
    }

    [Test]
    public void SteerAngle_ClampsInput() {
        Assert.AreEqual(30f, DriveRules.SteerAngle(3f, 0f, 10f, 30f, 10f), 1e-4f);
    }

    [Test]
    public void WheelTorque_SplitsForceOverWheels() {
        // 1000 kg * 2 m/s² * 0.3 m / 2 roda = 300 Nm per roda.
        Assert.AreEqual(300f, DriveRules.WheelTorque(1000f, 2f, 0.3f, 2), 1e-3f);
        Assert.AreEqual(0f, DriveRules.WheelTorque(1000f, 2f, 0.3f, 0));
    }

    [TestCase(0f, true)]
    [TestCase(0.99f, true)]
    [TestCase(-0.99f, true)]
    [TestCase(1f, false)]
    [TestCase(-5f, false)]
    public void CanExit_OnlyWhenNearlyStopped(float speed, bool expected) {
        Assert.AreEqual(expected, DriveRules.CanExit(speed));
    }

    [Test]
    public void WheelAngleToSteer_ScalesAndClamps() {
        Assert.AreEqual(0.5f, DriveRules.WheelAngleToSteer(DriveRules.WheelMaxAngle * 0.5f), 1e-4f);
        Assert.AreEqual(-1f, DriveRules.WheelAngleToSteer(-400f), 1e-4f);
    }

    [TestCase(170f, -170f, 20f)]
    [TestCase(-170f, 170f, -20f)]
    [TestCase(10f, 40f, 30f)]
    public void DeltaAngle_TakesShortestWay(float a, float b, float expected) {
        Assert.AreEqual(expected, DriveRules.DeltaAngle(a, b), 1e-4f);
    }
}

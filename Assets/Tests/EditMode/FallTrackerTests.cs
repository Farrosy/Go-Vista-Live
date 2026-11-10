using NUnit.Framework;

public class FallTrackerTests {
    private const float Tolerance = 0.001f;

    [Test]
    public void Update_ReturnsZeroWhileStayingOnGround() {
        var tracker = new FallTracker();

        Assert.AreEqual(0f, tracker.Update(10f, grounded: true), Tolerance);
        Assert.AreEqual(0f, tracker.Update(10f, grounded: true), Tolerance);
    }

    [Test]
    public void Update_ReturnsDropFromHighestPointOnLanding() {
        var tracker = new FallTracker();
        tracker.Update(10f, grounded: true);

        tracker.Update(10f, grounded: false);
        tracker.Update(11.5f, grounded: false); // puncak lompatan
        tracker.Update(6f, grounded: false);
        float drop = tracker.Update(4f, grounded: true);

        Assert.AreEqual(7.5f, drop, Tolerance);
    }

    [Test]
    public void Update_ReportsLandingOnlyOnce() {
        var tracker = new FallTracker();
        tracker.Update(10f, grounded: false);
        tracker.Update(2f, grounded: true);

        Assert.AreEqual(0f, tracker.Update(2f, grounded: true), Tolerance);
    }

    [Test]
    public void Reset_ForgetsTheFallInProgress() {
        var tracker = new FallTracker();
        tracker.Update(50f, grounded: false);

        tracker.Reset();

        Assert.AreEqual(0f, tracker.Update(1f, grounded: true), Tolerance);
    }

    [Test]
    public void CurrentDrop_MeasuresFromHighestPointWhileAirborne() {
        var tracker = new FallTracker();

        tracker.Update(10f, grounded: false);
        tracker.Update(11.5f, grounded: false);
        tracker.Update(6.5f, grounded: false);

        Assert.AreEqual(5f, tracker.CurrentDrop, Tolerance);
    }

    [Test]
    public void CurrentDrop_IsZeroWhileRisingOnAJump() {
        var tracker = new FallTracker();

        tracker.Update(10f, grounded: false);
        tracker.Update(11f, grounded: false);

        Assert.AreEqual(0f, tracker.CurrentDrop, Tolerance);
    }

    [Test]
    public void CurrentDrop_ReturnsToZeroAfterLanding() {
        var tracker = new FallTracker();
        tracker.Update(20f, grounded: false);
        tracker.Update(5f, grounded: false);

        tracker.Update(1f, grounded: true);

        Assert.AreEqual(0f, tracker.CurrentDrop, Tolerance);
    }

    [Test]
    public void CurrentDrop_IsZeroAfterReset() {
        var tracker = new FallTracker();
        tracker.Update(20f, grounded: false);
        tracker.Update(5f, grounded: false);

        tracker.Reset();

        Assert.AreEqual(0f, tracker.CurrentDrop, Tolerance);
    }
}

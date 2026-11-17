using NUnit.Framework;

public class AvatarBakeTrackerTests {
    private const int Local = AvatarBakeTracker<string>.LocalKey;

    [Test]
    public void Own_avatar_is_baked_before_other_players() {
        var tracker = new AvatarBakeTracker<string>();
        tracker.Request(7, "a");
        tracker.Request(Local, "me");

        Assert.IsTrue(tracker.TryStartNext(out int key, out string payload));
        Assert.AreEqual(Local, key);
        Assert.AreEqual("me", payload);
    }

    [Test]
    public void Other_players_are_baked_in_arrival_order() {
        var tracker = new AvatarBakeTracker<string>();
        tracker.Request(3, "a");
        tracker.Request(9, "b");

        tracker.TryStartNext(out int first, out _);
        tracker.TryStartNext(out int second, out _);
        Assert.AreEqual(3, first);
        Assert.AreEqual(9, second);
        Assert.IsFalse(tracker.TryStartNext(out _, out _));
    }

    [Test]
    public void Requesting_again_replaces_the_payload_and_moves_to_the_back() {
        var tracker = new AvatarBakeTracker<string>();
        tracker.Request(1, "old");
        tracker.Request(2, "b");
        tracker.Request(1, "new");

        tracker.TryStartNext(out int first, out _);
        tracker.TryStartNext(out int second, out string payload);
        Assert.AreEqual(2, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual("new", payload);
    }

    [Test]
    public void Spawn_waits_until_every_tracked_avatar_is_ready_or_failed() {
        var tracker = new AvatarBakeTracker<string>();
        tracker.Request(Local, "me");
        tracker.Request(4, "x");
        Assert.IsFalse(tracker.AllDone);
        Assert.AreEqual((0, 2), tracker.Progress());

        tracker.TryStartNext(out int a, out _);
        tracker.Complete(a, success: true);
        tracker.TryStartNext(out int b, out _);
        tracker.Complete(b, success: false);

        Assert.IsTrue(tracker.AllDone, "a failed bake shows the template, so it counts as done");
        Assert.AreEqual(AvatarBakeState.Failed, tracker.StateOf(4));
    }

    [Test]
    public void Forgotten_or_rerequested_avatars_ignore_a_late_result() {
        var tracker = new AvatarBakeTracker<string>();
        tracker.Request(5, "a");
        tracker.TryStartNext(out _, out _);
        tracker.Request(5, "b"); // data berubah saat bake lama berjalan

        Assert.IsFalse(tracker.Complete(5, success: true));
        Assert.AreEqual(AvatarBakeState.Queued, tracker.StateOf(5));

        tracker.Forget(5);
        Assert.IsNull(tracker.StateOf(5));
        Assert.IsTrue(tracker.AllDone);
    }

    [Test]
    public void Nothing_tracked_means_ready() {
        Assert.IsTrue(new AvatarBakeTracker<string>().AllDone);
    }
}

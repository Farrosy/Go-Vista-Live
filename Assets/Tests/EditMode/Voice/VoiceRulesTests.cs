using NUnit.Framework;

public class VoiceRulesTests {
    [Test]
    public void ShouldSilenceRemote_IsFalseWhenSpeakerIsAudible() {
        Assert.IsFalse(VoiceRules.ShouldSilenceRemote(sameVenue: true, speakerHidden: false, mutedByListener: false));
    }

    [Test]
    public void ShouldSilenceRemote_IsTrueWhenSpeakerIsInAnotherVenue() {
        Assert.IsTrue(VoiceRules.ShouldSilenceRemote(sameVenue: false, speakerHidden: false, mutedByListener: false));
    }

    [Test]
    public void ShouldSilenceRemote_IsTrueWhileSpeakerIsOnTheDeathScreen() {
        Assert.IsTrue(VoiceRules.ShouldSilenceRemote(sameVenue: true, speakerHidden: true, mutedByListener: false));
    }

    [Test]
    public void ShouldSilenceRemote_IsTrueWhenListenerMutedTheSpeaker() {
        Assert.IsTrue(VoiceRules.ShouldSilenceRemote(sameVenue: true, speakerHidden: false, mutedByListener: true));
    }

    [TestCase(0f, false)]
    [TestCase(0.07f, false)]
    [TestCase(0.08f, true)]
    [TestCase(1f, true)]
    public void IsSpeaking_ComparesVolumeWithThreshold(float volume, bool expected) {
        Assert.AreEqual(expected, VoiceRules.IsSpeaking(volume));
    }

    [Test]
    public void Distances_FadeOutOverAPositiveRange() {
        Assert.Greater(VoiceRules.SilentDistance, VoiceRules.FullVolumeDistance);
        Assert.Greater(VoiceRules.FullVolumeDistance, 0f);
    }
}

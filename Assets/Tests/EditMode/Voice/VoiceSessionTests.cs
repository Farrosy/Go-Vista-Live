using NUnit.Framework;

public class VoiceSessionTests {
    [SetUp]
    public void ResetSession() => VoiceSession.ResetForNewRoom();

    [Test]
    public void Mic_StartsOffInANewRoom() {
        Assert.IsFalse(VoiceSession.MicEnabled);
    }

    [Test]
    public void ToggleMic_SwitchesOnThenOff() {
        VoiceSession.ToggleMic();
        Assert.IsTrue(VoiceSession.MicEnabled);

        VoiceSession.ToggleMic();
        Assert.IsFalse(VoiceSession.MicEnabled);
    }

    [Test]
    public void TogglePlayerMuted_OnlyAffectsThatPlayer() {
        VoiceSession.TogglePlayerMuted(4);

        Assert.IsTrue(VoiceSession.IsPlayerMuted(4));
        Assert.IsFalse(VoiceSession.IsPlayerMuted(5));
    }

    [Test]
    public void TogglePlayerMuted_TwiceUnmutesThePlayer() {
        VoiceSession.TogglePlayerMuted(4);
        VoiceSession.TogglePlayerMuted(4);

        Assert.IsFalse(VoiceSession.IsPlayerMuted(4));
    }

    [Test]
    public void ResetForNewRoom_TurnsMicOffAndForgetsMutedPlayers() {
        VoiceSession.ToggleMic();
        VoiceSession.TogglePlayerMuted(4);

        VoiceSession.ResetForNewRoom();

        Assert.IsFalse(VoiceSession.MicEnabled);
        Assert.IsFalse(VoiceSession.IsPlayerMuted(4));
    }
}

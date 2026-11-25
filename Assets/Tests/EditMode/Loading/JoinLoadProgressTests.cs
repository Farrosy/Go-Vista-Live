using NUnit.Framework;

public class JoinLoadProgressTests {
    [Test]
    public void Fraction_ClampsAndTreatsEmptyStageAsDone() {
        Assert.AreEqual(1f, JoinLoadProgress.Fraction(0, 0));
        Assert.AreEqual(0f, JoinLoadProgress.Fraction(-3, 10));
        Assert.AreEqual(0.5f, JoinLoadProgress.Fraction(5, 10), 1e-5f);
        Assert.AreEqual(1f, JoinLoadProgress.Fraction(15, 10));
    }

    [Test]
    public void DuringCharacters_FillsOnlyTheCharacterShare() {
        Assert.AreEqual(0f, JoinLoadProgress.DuringCharacters(0, 1));
        Assert.AreEqual(JoinLoadProgress.CharacterShare, JoinLoadProgress.DuringCharacters(1, 1), 1e-5f);
    }

    [Test]
    public void DuringCrowd_StartsWhereCharactersEndAndReachesOne() {
        Assert.AreEqual(JoinLoadProgress.CharacterShare, JoinLoadProgress.DuringCrowd(0, 120), 1e-5f);
        Assert.AreEqual(1f, JoinLoadProgress.DuringCrowd(120, 120), 1e-5f);
        Assert.Greater(JoinLoadProgress.DuringCrowd(60, 120), JoinLoadProgress.DuringCharacters(1, 1));
    }

    [Test]
    public void Percent_RoundsDownAndClamps() {
        Assert.AreEqual(0, JoinLoadProgress.Percent(-1f));
        Assert.AreEqual(45, JoinLoadProgress.Percent(0.459f));
        Assert.AreEqual(100, JoinLoadProgress.Percent(1.2f));
    }
}

using NUnit.Framework;

public class NpcCrowdRulesTests {
    private const int None = NpcCrowdRules.NoClient;

    [Test]
    public void IsAuthority_LowestWorldAvatarOwnerWins() {
        Assert.IsTrue(NpcCrowdRules.IsAuthority(2, true, true, 2, 5));
        Assert.IsFalse(NpcCrowdRules.IsAuthority(5, true, true, 2, 5));
    }

    [Test]
    public void IsAuthority_InLobbyWithUnownedDirector_ClaimsWhenWorldLoaded() {
        Assert.IsTrue(NpcCrowdRules.IsAuthority(3, true, true, None, None));
    }

    [Test]
    public void IsAuthority_InLobby_DirectorOwnerKeepsAuthority() {
        Assert.IsTrue(NpcCrowdRules.IsAuthority(3, true, true, None, 3));
        Assert.IsFalse(NpcCrowdRules.IsAuthority(4, true, true, None, 3));
    }

    [Test]
    public void IsAuthority_InLobby_WaitsForWorldSceneNavmesh() {
        Assert.IsFalse(NpcCrowdRules.IsAuthority(3, true, false, None, None));
        Assert.IsFalse(NpcCrowdRules.IsAuthority(3, true, false, None, 3));
    }

    [Test]
    public void IsAuthority_FalseWhenNotConnectedOrOutsideWorld() {
        Assert.IsFalse(NpcCrowdRules.IsAuthority(-1, true, true, None, None));
        Assert.IsFalse(NpcCrowdRules.IsAuthority(1, false, true, 1, 1));
    }

    [Test]
    public void IsCrowdReady_TrueWhenRosterFlaggedOrTargetReached() {
        Assert.IsTrue(NpcCrowdRules.IsCrowdReady(true, 100, 120));
        Assert.IsTrue(NpcCrowdRules.IsCrowdReady(false, 120, 120));
        Assert.IsFalse(NpcCrowdRules.IsCrowdReady(false, 60, 120));
    }
}

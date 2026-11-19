using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class DailyLoginRulesTests {
    private static readonly DateTime Day1 = new DateTime(2026, 11, 19);

    private static DailyLoginProgress Fresh() => new DailyLoginProgress();

    [Test]
    public void Rewards_RiseEachDayAndDaySevenIsBig() {
        CollectionAssert.AreEqual(new[] { 50, 75, 100, 125, 150, 200, 500 },
            new[] { 1, 2, 3, 4, 5, 6, 7 }.Select(DailyLoginRules.Reward));
        Assert.AreEqual(0, DailyLoginRules.Reward(0));
        Assert.AreEqual(0, DailyLoginRules.Reward(8));
    }

    [Test]
    public void FirstLogin_StartsCycleAtDayOneAndSavesReward() {
        DailyLoginProgress p = Fresh();
        DailyLoginCheck check = DailyLoginRules.RegisterLogin(p, Day1);
        Assert.AreEqual(1, check.Today);
        Assert.IsTrue(check.Changed);
        Assert.IsFalse(check.CycleReset);
        Assert.AreEqual("2026-11-19", p.cycleStart);
        Assert.AreEqual(50, DailyLoginRules.SavedGold(p));
    }

    [Test]
    public void SecondLoginSameDay_ChangesNothing() {
        DailyLoginProgress p = Fresh();
        DailyLoginRules.RegisterLogin(p, Day1);
        DailyLoginCheck again = DailyLoginRules.RegisterLogin(p, Day1.AddHours(5));
        Assert.IsFalse(again.Changed);
        Assert.AreEqual(1, p.loggedDays.Count);
    }

    [Test]
    public void UnclaimedRewards_AreSavedAcrossDays() {
        DailyLoginProgress p = Fresh();
        DailyLoginRules.RegisterLogin(p, Day1);
        DailyLoginRules.RegisterLogin(p, Day1.AddDays(1));
        DailyLoginRules.RegisterLogin(p, Day1.AddDays(3));
        Assert.AreEqual(50 + 75 + 125, DailyLoginRules.SavedGold(p));
        CollectionAssert.AreEqual(new[] { 1, 2, 4 }, DailyLoginRules.SavedDays(p));
    }

    [Test]
    public void DayWithoutLogin_IsMissedAndNotSaved() {
        DailyLoginProgress p = Fresh();
        DailyLoginRules.RegisterLogin(p, Day1);
        DailyLoginRules.RegisterLogin(p, Day1.AddDays(3));
        Assert.AreEqual(DailyDayState.Missed, DailyLoginRules.StateOf(p, 3, 4));
        Assert.AreEqual(DailyDayState.Saved, DailyLoginRules.StateOf(p, 4, 4));
        Assert.AreEqual(DailyDayState.Future, DailyLoginRules.StateOf(p, 5, 4));
    }

    [Test]
    public void ClaimAll_ReturnsTotalAndEmptiesSavings() {
        DailyLoginProgress p = Fresh();
        DailyLoginRules.RegisterLogin(p, Day1);
        DailyLoginRules.RegisterLogin(p, Day1.AddDays(1));
        Assert.AreEqual(125, DailyLoginRules.ClaimAll(p));
        Assert.AreEqual(0, DailyLoginRules.SavedGold(p));
        Assert.AreEqual(0, DailyLoginRules.ClaimAll(p));
        Assert.AreEqual(DailyDayState.Claimed, DailyLoginRules.StateOf(p, 1, 2));
    }

    [Test]
    public void DayEight_ResetsCycleAndLosesUnclaimedSavings() {
        DailyLoginProgress p = Fresh();
        DailyLoginRules.RegisterLogin(p, Day1);
        DailyLoginRules.RegisterLogin(p, Day1.AddDays(6));   // Hari 7, tidak diklaim
        DailyLoginCheck check = DailyLoginRules.RegisterLogin(p, Day1.AddDays(7));
        Assert.IsTrue(check.CycleReset);
        Assert.AreEqual(1, check.Today);
        Assert.AreEqual(50 + 500, check.LostGold);
        Assert.AreEqual(50, DailyLoginRules.SavedGold(p));
        Assert.AreEqual(DailyLoginRules.FormatDate(Day1.AddDays(7)), p.cycleStart);
    }

    [Test]
    public void LongAbsence_StartsNewCycleOnReturnDay() {
        DailyLoginProgress p = Fresh();
        DailyLoginRules.RegisterLogin(p, Day1);
        DailyLoginRules.ClaimAll(p);
        DailyLoginCheck check = DailyLoginRules.RegisterLogin(p, Day1.AddDays(30));
        Assert.IsTrue(check.CycleReset);
        Assert.AreEqual(0, check.LostGold);
        Assert.AreEqual(1, check.Today);
    }

    [Test]
    public void ClockMovedBeforeCycleStart_StartsNewCycle() {
        DailyLoginProgress p = Fresh();
        DailyLoginRules.RegisterLogin(p, Day1);
        DailyLoginCheck check = DailyLoginRules.RegisterLogin(p, Day1.AddDays(-2));
        Assert.AreEqual(1, check.Today);
        Assert.IsTrue(check.CycleReset);
    }

    [Test]
    public void Sanitize_RemovesInvalidDaysAndClaimsWithoutLogin() {
        var p = new DailyLoginProgress {
            cycleStart = null,
            loggedDays = new List<int> { 1, 1, 9, 0, 3 },
            claimedDays = new List<int> { 3, 5 },
        };
        Assert.IsTrue(DailyLoginRules.Sanitize(p));
        Assert.AreEqual("", p.cycleStart);
        CollectionAssert.AreEqual(new[] { 1, 3 }, p.loggedDays);
        CollectionAssert.AreEqual(new[] { 3 }, p.claimedDays);
    }
}


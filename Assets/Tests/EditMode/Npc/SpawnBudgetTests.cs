using NUnit.Framework;

public class SpawnBudgetTests {
    private const float BudgetMs = 4f;
    private const int MaxPerFrame = 4;

    [Test]
    public void CanSpawnMore_AlwaysAllowsFirstSpawnOfFrameEvenOverBudget() {
        // Satu NPC per frame minimum, supaya roster tetap terisi walau satu instantiate lebih lama dari budget.
        Assert.IsTrue(SpawnBudget.CanSpawnMore(0, 50.0, BudgetMs, MaxPerFrame));
    }

    [Test]
    public void CanSpawnMore_AllowsAnotherSpawnWhileUnderBudget() {
        Assert.IsTrue(SpawnBudget.CanSpawnMore(1, 1.5, BudgetMs, MaxPerFrame));
    }

    [Test]
    public void CanSpawnMore_StopsOnceBudgetIsUsedUp() {
        Assert.IsFalse(SpawnBudget.CanSpawnMore(1, 4.0, BudgetMs, MaxPerFrame));
        Assert.IsFalse(SpawnBudget.CanSpawnMore(2, 9.0, BudgetMs, MaxPerFrame));
    }

    [Test]
    public void CanSpawnMore_StopsAtMaxPerFrameEvenUnderBudget() {
        Assert.IsFalse(SpawnBudget.CanSpawnMore(MaxPerFrame, 0.5, BudgetMs, MaxPerFrame));
    }

    [Test]
    public void CanSpawnMore_TreatsNonPositiveMaxAsOnePerFrame() {
        Assert.IsTrue(SpawnBudget.CanSpawnMore(0, 0.0, BudgetMs, 0));
        Assert.IsFalse(SpawnBudget.CanSpawnMore(1, 0.0, BudgetMs, 0));
    }
}

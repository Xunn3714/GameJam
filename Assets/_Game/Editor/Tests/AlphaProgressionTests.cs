using NUnit.Framework;

public sealed class AlphaProgressionTests
{
    private static FlockGrowthStage[] Stages() => new[]
    {
        new FlockGrowthStage("孤羊", 1, 1, 1, 5f),
        new FlockGrowthStage("小群", 5, 2, 3, 7f),
        new FlockGrowthStage("狼群来袭", 20, 5, 7, 10f),
        new FlockGrowthStage("暴力扩张", 50, 10, 15, 14f),
        new FlockGrowthStage("羊潮", 100, 20, 30, 18f)
    };

    [Test]
    public void StartsAtFirstStageWithOneSheep()
    {
        AlphaProgression progression = new AlphaProgression(Stages(), 100, 1);
        Assert.AreEqual(0, progression.StageIndex);
        Assert.AreEqual(1, progression.HighestFlockSize);
        Assert.IsFalse(progression.ExitUnlocked);
    }

    [Test]
    public void StageNeverDowngradesWhenFlockShrinks()
    {
        AlphaProgression progression = new AlphaProgression(Stages(), 100, 1);
        AlphaProgression.Change up = progression.Observe(22);
        Assert.IsTrue(up.StageChanged);
        Assert.AreEqual(2, progression.StageIndex);

        AlphaProgression.Change down = progression.Observe(3);
        Assert.IsFalse(down.StageChanged);
        Assert.IsFalse(down.HighestChanged);
        Assert.AreEqual(2, progression.StageIndex);
        Assert.AreEqual(22, progression.HighestFlockSize);
    }

    [Test]
    public void ExitUnlocksOnceAtConfiguredThresholdAndStaysUnlocked()
    {
        AlphaProgression progression = new AlphaProgression(Stages(), 100, 1);
        Assert.IsFalse(progression.Observe(99).ExitJustUnlocked);
        Assert.IsFalse(progression.ExitUnlocked);

        AlphaProgression.Change unlock = progression.Observe(100);
        Assert.IsTrue(unlock.ExitJustUnlocked);
        Assert.IsTrue(progression.ExitUnlocked);

        Assert.IsFalse(progression.Observe(120).ExitJustUnlocked, "解锁只触发一次");
        progression.Observe(0);
        Assert.IsTrue(progression.ExitUnlocked, "羊数归零也不会撤销解锁");
    }

    [Test]
    public void ExitThresholdIsConfigurable()
    {
        AlphaProgression progression = new AlphaProgression(Stages(), 30, 1);
        Assert.IsTrue(progression.Observe(30).ExitJustUnlocked);
    }

    [Test]
    public void SkipsStraightToHighestQualifiedStage()
    {
        AlphaProgression progression = new AlphaProgression(Stages(), 100, 1);
        progression.Observe(60);
        Assert.AreEqual(3, progression.StageIndex);
    }
}

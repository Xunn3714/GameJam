using NUnit.Framework;

public sealed class AlphaProgressionTests
{
    private static FlockGrowthStage[] Stages() => new[]
    {
        new FlockGrowthStage("孤羊", 1, 1, 1, 5f),
        new FlockGrowthStage("小群", 12, 2, 3, 7f),
        new FlockGrowthStage("狼群来袭", 20, 5, 7, 10f),
        new FlockGrowthStage("暴力扩张", 50, 10, 15, 14f),
        new FlockGrowthStage("羊潮", 90, 20, 30, 18f)
    };

    [Test]
    public void SheepTideUnlocksAtNinetyBeforeExitAtOneHundred()
    {
        AlphaProgression progression = new AlphaProgression(Stages(), 100, 1);
        progression.Observe(89);
        Assert.AreEqual(3, progression.StageIndex);
        Assert.IsTrue(progression.Observe(90).StageChanged);
        Assert.AreEqual(4, progression.StageIndex);
        Assert.IsFalse(progression.ExitUnlocked);
        progression.Observe(99);
        Assert.IsFalse(progression.ExitUnlocked);
        Assert.IsTrue(progression.Observe(100).ExitJustUnlocked);
    }

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

    [Test]
    public void SequentialTaskReachesSixAfterFiveRecruitedPartners()
    {
        MvpObjectiveSnapshot first = CurrentTask(newRecruitCount: 0);
        Assert.AreEqual("alpha.touch_first", first.Id);

        MvpObjectiveSnapshot afterFirst = CurrentTask(newRecruitCount: 1);
        Assert.AreEqual("alpha.recruit_five", afterFirst.Id);
        Assert.AreEqual(1, afterFirst.Progress);
        Assert.AreEqual(5, afterFirst.Target);

        MvpObjectiveSnapshot beforeSixTotal = CurrentTask(newRecruitCount: 4);
        Assert.AreEqual("alpha.recruit_five", beforeSixTotal.Id);
        Assert.AreEqual(4, beforeSixTotal.Progress);

        MvpObjectiveSnapshot atSixTotal = CurrentTask(newRecruitCount: 5);
        Assert.AreEqual("alpha.break_pen", atSixTotal.Id);
        Assert.AreEqual("撞开羊圈！", atSixTotal.Title);
    }

    [Test]
    public void SequentialTaskAdvancesThroughPastureWolfAndExitStages()
    {
        Assert.AreEqual("alpha.expand_pasture", CurrentTask(6, penOpened: true, highestFlockSize: 7).Id);
        Assert.AreEqual("alpha.survive_wolf", CurrentTask(6, true, 20).Id);
        Assert.AreEqual("alpha.gather_army", CurrentTask(6, true, 20, firstWolfEventCompleted: true).Id);
        Assert.AreEqual("alpha.fill_pasture", CurrentTask(6, true, 50, true).Id);
        Assert.AreEqual("alpha.reach_exit", CurrentTask(6, true, 90, true).Id);

        MvpObjectiveSnapshot restore = CurrentTask(6, true, 100, true, currentMemberCount: 96);
        Assert.AreEqual("alpha.restore_for_exit", restore.Id);
        Assert.AreEqual(96, restore.Progress);

        Assert.AreEqual(
            "alpha.break_border",
            CurrentTask(6, true, 100, true, currentMemberCount: 100).Id);
        Assert.AreEqual(
            "alpha.escape",
            CurrentTask(6, true, 100, true, borderBroken: true, currentMemberCount: 100).Id);
    }

    [Test]
    public void PoopCounterIsAStatLineAndDoesNotComplete()
    {
        MvpObjectiveSnapshot counter = AlphaTaskSequence.PoopCounter(7);

        Assert.AreEqual("alpha.poop_counter", counter.Id);
        Assert.AreEqual("Space 拉屎", counter.Title);
        Assert.AreEqual(7, counter.Progress);
        Assert.AreEqual(0, counter.Target);
        Assert.IsTrue(counter.IsCounter);
        Assert.IsFalse(counter.IsRequired);
        Assert.IsFalse(counter.IsComplete);
    }

    private static MvpObjectiveSnapshot CurrentTask(
        int newRecruitCount,
        bool penOpened = false,
        int highestFlockSize = 1,
        bool firstWolfEventCompleted = false,
        bool borderBroken = false,
        bool escaped = false,
        int currentMemberCount = 1)
    {
        return AlphaTaskSequence.Current(
            newRecruitCount,
            penOpened,
            highestFlockSize,
            firstWolfEventCompleted,
            borderBroken,
            escaped,
            currentMemberCount,
            5,
            20,
            50,
            90,
            100);
    }
}

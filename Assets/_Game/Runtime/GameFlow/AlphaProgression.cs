using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 羊群扩张的阶段推进（纯 C#，可测）：按本局历史最高羊数解锁阶段，只升不降；
/// 历史最高达到出口门槛后永久解锁“冲出地图”。
/// </summary>
public sealed class AlphaProgression
{
    public readonly struct Change
    {
        public Change(bool stageChanged, bool exitJustUnlocked, bool highestChanged)
        {
            StageChanged = stageChanged;
            ExitJustUnlocked = exitJustUnlocked;
            HighestChanged = highestChanged;
        }

        public bool StageChanged { get; }
        public bool ExitJustUnlocked { get; }
        public bool HighestChanged { get; }
    }

    private readonly IReadOnlyList<FlockGrowthStage> stages;

    public AlphaProgression(IReadOnlyList<FlockGrowthStage> stages, int exitUnlockFlockSize, int initialFlockSize = 1)
    {
        this.stages = stages ?? new List<FlockGrowthStage>();
        ExitUnlockFlockSize = Mathf.Max(1, exitUnlockFlockSize);
        HighestFlockSize = 0;
        StageIndex = 0;
        Observe(Mathf.Max(1, initialFlockSize));
    }

    public int ExitUnlockFlockSize { get; }
    public int HighestFlockSize { get; private set; }
    public int StageIndex { get; private set; }
    public bool ExitUnlocked { get; private set; }
    public int StageCount => stages.Count;

    public FlockGrowthStage CurrentStage =>
        stages.Count == 0 ? null : stages[Mathf.Clamp(StageIndex, 0, stages.Count - 1)];

    /// <summary>喂入当前羊数；羊数下降不会改变任何已解锁状态。</summary>
    public Change Observe(int memberCount)
    {
        if (memberCount <= HighestFlockSize)
            return new Change(false, false, false);

        HighestFlockSize = memberCount;

        int unlockedIndex = StageIndex;
        for (int index = 0; index < stages.Count; index++)
        {
            if (stages[index] != null && HighestFlockSize >= stages[index].MinimumFlockSize)
                unlockedIndex = Mathf.Max(unlockedIndex, index);
        }

        bool stageChanged = unlockedIndex != StageIndex;
        StageIndex = unlockedIndex;

        bool exitJustUnlocked = false;
        if (!ExitUnlocked && HighestFlockSize >= ExitUnlockFlockSize)
        {
            ExitUnlocked = true;
            exitJustUnlocked = true;
        }

        return new Change(stageChanged, exitJustUnlocked, true);
    }
}

/// <summary>
/// 把 Alpha 的运行状态翻译成单条、顺序推进的玩家任务。
/// 第一次接触羊单独教学；随后累计寻找五个伙伴，其中包含第一次接触的羊。
/// </summary>
public static class AlphaTaskSequence
{
    public static MvpObjectiveSnapshot Current(
        int newRecruitCount,
        bool penOpened,
        int highestFlockSize,
        bool firstWolfEventCompleted,
        bool borderBroken,
        bool escaped,
        int currentMemberCount,
        int recruitTarget,
        int wolfStageTarget,
        int armyTarget,
        int sheepTideTarget,
        int exitTarget)
    {
        newRecruitCount = Mathf.Max(0, newRecruitCount);
        recruitTarget = Mathf.Max(1, recruitTarget);
        wolfStageTarget = Mathf.Max(1, wolfStageTarget);
        armyTarget = Mathf.Max(wolfStageTarget, armyTarget);
        sheepTideTarget = Mathf.Max(armyTarget, sheepTideTarget);
        exitTarget = Mathf.Max(sheepTideTarget, exitTarget);

        if (newRecruitCount == 0)
            return Objective("alpha.touch_first", "去触碰另一只羊！", 0, 1);

        if (newRecruitCount < recruitTarget)
        {
            return Objective(
                "alpha.recruit_five",
                "找五个新伙伴",
                newRecruitCount,
                recruitTarget);
        }

        if (!penOpened)
            return Objective("alpha.break_pen", "撞开羊圈！", 0, 1);

        if (highestFlockSize < wolfStageTarget)
            return Objective("alpha.expand_pasture", "壮大羊群！", highestFlockSize, wolfStageTarget);

        if (!firstWolfEventCompleted)
            return Objective("alpha.survive_wolf", "撑过狼群袭击！", 0, 1);

        if (highestFlockSize < armyTarget)
            return Objective("alpha.gather_army", "集结羊群大军！", highestFlockSize, armyTarget);

        if (highestFlockSize < sheepTideTarget)
            return Objective("alpha.fill_pasture", "让牧场挤满羊！", highestFlockSize, sheepTideTarget);

        if (highestFlockSize < exitTarget)
            return Objective("alpha.reach_exit", "集结一百只羊！", highestFlockSize, exitTarget);

        if (!borderBroken)
        {
            if (currentMemberCount < exitTarget)
                return Objective("alpha.restore_for_exit", $"让当前羊群恢复到 {exitTarget} 只！", currentMemberCount, exitTarget);

            return Objective("alpha.break_border", "撞开外围围栏！", 0, 1);
        }

        return Objective("alpha.escape", "冲出草原！", escaped ? 1 : 0, 1, escaped);
    }

    /// <summary>撞过宝通寺但羊不够时解锁的支线：寻找？？</summary>
    public static MvpObjectiveSnapshot Pagoda(int currentMemberCount, int requiredCount, bool smashed)
    {
        requiredCount = Mathf.Max(1, requiredCount);
        return new MvpObjectiveSnapshot(
            "alpha.pagoda",
            "寻找？？",
            false,
            !smashed,
            smashed,
            Mathf.Clamp(currentMemberCount, 0, requiredCount),
            requiredCount);
    }

    /// <summary>常驻统计行：只记录本局成功生成粪便的次数，不参与任务完成判断。</summary>
    public static MvpObjectiveSnapshot PoopCounter(int useCount)
    {
        return new MvpObjectiveSnapshot(
            "alpha.poop_counter",
            "Space 拉屎",
            false,
            false,
            false,
            Mathf.Max(0, useCount),
            0,
            true);
    }

    private static MvpObjectiveSnapshot Objective(
        string id,
        string title,
        int progress,
        int target,
        bool complete = false)
    {
        return new MvpObjectiveSnapshot(
            id,
            title,
            true,
            false,
            complete,
            Mathf.Clamp(progress, 0, Mathf.Max(0, target)),
            Mathf.Max(0, target));
    }
}

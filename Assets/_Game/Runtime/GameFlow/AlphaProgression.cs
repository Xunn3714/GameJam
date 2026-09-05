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

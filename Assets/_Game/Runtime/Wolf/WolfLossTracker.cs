using System;
using System.Collections.Generic;

/// <summary>
/// 单局统计（纯 C#，可测）：每种攻击方式叼走了多少羊、长狼 / 单狼各叼走多少，
/// 以及"本场损失最多羊的攻击方式"（不计五角星；这个数据只用于抽取，不展示）。
/// </summary>
public sealed class WolfLossTracker
{
    private readonly Dictionary<WolfAttackType, int> lossByType = new Dictionary<WolfAttackType, int>();

    public int TotalTaken { get; private set; }
    /// <summary>被长条狼叼走（任何含长狼的攻击方式里，由长狼叼走的）。</summary>
    public int TakenByLongWolves { get; private set; }
    /// <summary>被单只普通狼叼走（直冲 / 聪明的狼）。</summary>
    public int TakenBySingleWolves { get; private set; }

    public IReadOnlyDictionary<WolfAttackType, int> LossByType => lossByType;

    public void RecordTaken(WolfAttackType attackType, bool byLongWolf, int count = 1)
    {
        if (count <= 0)
            return;

        TotalTaken += count;
        if (byLongWolf)
            TakenByLongWolves += count;
        else if (!WolfAttackTypes.IsPack(attackType))
            TakenBySingleWolves += count;

        lossByType.TryGetValue(attackType, out int value);
        lossByType[attackType] = value + count;
    }

    public int GetLoss(WolfAttackType type)
    {
        lossByType.TryGetValue(type, out int value);
        return value;
    }

    /// <summary>本场损失最多羊的攻击方式（不含五角星）；一只都没丢时返回 null。</summary>
    public WolfAttackType? MostLossType
    {
        get
        {
            WolfAttackType? best = null;
            int bestLoss = 0;
            foreach (KeyValuePair<WolfAttackType, int> pair in lossByType)
            {
                if (pair.Key == WolfAttackType.Pentagram || pair.Value <= 0)
                    continue;
                if (pair.Value > bestLoss)
                {
                    bestLoss = pair.Value;
                    best = pair.Key;
                }
            }
            return best;
        }
    }

    public void Clear()
    {
        lossByType.Clear();
        TotalTaken = 0;
        TakenByLongWolves = 0;
        TakenBySingleWolves = 0;
    }
}

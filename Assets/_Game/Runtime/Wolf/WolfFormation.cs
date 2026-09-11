using System;
using UnityEngine;

/// <summary>多狼协作的进攻方式。</summary>
public enum WolfFormationType
{
    /// <summary>一只普通狼（原有行为）。</summary>
    Single,
    /// <summary>一条长狼单独从镜头外扫过羊群。</summary>
    SingleLong,
    /// <summary>N 条长狼平行同时出击。</summary>
    ParallelSimultaneous,
    /// <summary>N 条长狼平行、随机顺序依次出击：先攻击的一定先出现预警。</summary>
    ParallelSequential,
    /// <summary>一条长狼正面扫过；生成那一刻看玩家在路线哪一侧，三只普通狼呈扇形从同侧（70%）或对侧（30%）冲向玩家。</summary>
    LongWolfWithEscorts,
    /// <summary>五条狼沿五角星的五条边依次冲锋（每条从上一条的终点出发）。</summary>
    Pentagram,
    /// <summary>N 条长狼依次出现，方向彼此垂直（每条比上一条转 90°）；上一条消失的一瞬间生成下一条。</summary>
    PerpendicularChain
}

/// <summary>一种编队的参数。放在 WolfEventDirector 的列表里，或直接交给 WolfFormationRunner.Play。</summary>
[Serializable]
public sealed class WolfFormation
{
    [Tooltip("给 HUD / 日志看的名字，可留空。")]
    public string displayName;
    public WolfFormationType type = WolfFormationType.Single;

    [Header("Prefabs")]
    [Tooltip("普通狼；留空用 WolfSpawner 的默认狼。")]
    public Wolf wolfPrefab;
    [Tooltip("长狼；平行编队 / 包夹主攻用。留空时退回普通狼。")]
    public Wolf longWolfPrefab;

    [Header("Parallel (平行出击)")]
    [Tooltip("平行编队的狼数。")]
    [Min(1)] public int count = 3;
    [Tooltip("相邻两条路线的间距（世界单位）。")]
    [Min(0f)] public float laneSpacing = 2.6f;

    [Header("Timing")]
    [Tooltip("依次出击时，相邻两只狼预警出现的间隔（秒）。")]
    [Min(0f)] public float sequentialDelay = 0.7f;
    [Tooltip("包夹编队里，三只普通狼比长狼晚多少秒出现。")]
    [Min(0f)] public float escortDelay = 0.8f;
    [Tooltip("包夹编队里长狼的预警时长（0 = 用 prefab 的值）。")]
    [Min(0f)] public float escortLongWolfWarningDuration = 2.4f;
    [Tooltip("包夹编队：三只普通狼出现在玩家同一侧的概率（其余概率在另一侧）。")]
    [Range(0f, 1f)] public float escortSameSideChance = 0.7f;
    [Tooltip("包夹编队：扇形里相邻两只狼的夹角（度）。")]
    [Range(0f, 80f)] public float escortFanSpread = 35f;

    [Header("Pentagram (五角星)")]
    [Tooltip("五角星核心路线的固定世界半径；镜头缩放不会改变它。狼会沿路线反向延伸到画外生成。")]
    [Min(1f)] public float pentagramRadius = 16f;
    [Tooltip("五角星用长狼还是普通狼。")]
    public bool pentagramUsesLongWolves = true;
    [Tooltip("五角星里每条狼的预警时长（0 = 用 prefab 的值）。")]
    [Min(0f)] public float pentagramWarningDuration = 1.5f;
    [Tooltip("五角星里相邻两条狼预警出现的间隔。要让五条预警同时可见 0.2~0.3 s：间隔 ≈ (预警时长 - 0.3) / 4。")]
    [Min(0f)] public float pentagramStagger = 0.3f;
    [Tooltip("五角星里狼的冲锋速度（0 = 用 prefab 的值）。慢一点，五角星在场上停留更久。")]
    [Min(0f)] public float pentagramChargeSpeed = 9f;

    [Header("Perpendicular Chain (直角连击)")]
    [Tooltip("上一条狼冲出多远时，下一条狼的预警刚好结束、开始冲锋。场上始终由相邻两条狼构成一个直角。")]
    [Min(0f)] public float chainHandoffDistance = 14f;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? DefaultName(type) : displayName;

    /// <summary>复制一份参数、换成另一种类型（节奏表按阶段动态生成编队时用）。</summary>
    public WolfFormation CloneAs(WolfFormationType newType)
    {
        return new WolfFormation
        {
            displayName = string.Empty,
            type = newType,
            wolfPrefab = wolfPrefab,
            longWolfPrefab = longWolfPrefab,
            count = count,
            laneSpacing = laneSpacing,
            sequentialDelay = sequentialDelay,
            escortDelay = escortDelay,
            escortLongWolfWarningDuration = escortLongWolfWarningDuration,
            escortSameSideChance = escortSameSideChance,
            escortFanSpread = escortFanSpread,
            pentagramRadius = pentagramRadius,
            pentagramUsesLongWolves = pentagramUsesLongWolves,
            pentagramWarningDuration = pentagramWarningDuration,
            pentagramStagger = pentagramStagger,
            pentagramChargeSpeed = pentagramChargeSpeed,
            chainHandoffDistance = chainHandoffDistance,
        };
    }

    public static string DefaultName(WolfFormationType type)
    {
        switch (type)
        {
            case WolfFormationType.SingleLong: return "长狼";
            case WolfFormationType.ParallelSimultaneous: return "长狼并排齐冲";
            case WolfFormationType.ParallelSequential: return "长狼并排轮冲";
            case WolfFormationType.LongWolfWithEscorts: return "长狼包夹";
            case WolfFormationType.Pentagram: return "五角星围猎";
            case WolfFormationType.PerpendicularChain: return "长狼直角连击";
            default: return "独狼";
        }
    }
}

/// <summary>WolfEventDirector 里的编队条目：满足回合数 / 羊数门槛后才会被抽到。</summary>
[Serializable]
public sealed class WolfFormationEntry
{
    public WolfFormation formation = new WolfFormation();
    [Tooltip("从第几轮狼来开始允许（1 = 第一轮就允许）。")]
    [Min(1)] public int minRound = 1;
    [Tooltip("羊群至少多少只才允许。")]
    [Min(0)] public int minMemberCount;
    [Tooltip("抽取权重，0 = 不抽。")]
    [Min(0f)] public float weight = 1f;
}

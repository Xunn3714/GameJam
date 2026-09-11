using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 正式关卡的狼袭配置。阶段只控制节奏与强度倾向，攻击条目独立声明解锁阶段和基础权重。
/// 运行时由 <see cref="WolfAttackSelectionState"/> 根据新鲜度调整权重，避免为每个阶段重复维护整张概率表。
/// </summary>
[CreateAssetMenu(fileName = "WolfAttackSchedule", menuName = "Game/Wolf Attack Schedule")]
public sealed class WolfAttackSchedule : ScriptableObject
{
    [Serializable]
    public sealed class Stage
    {
        public string displayName = "阶段";
        [Tooltip("羊群至少多少只进入这个阶段。狼袭只会升级，不会因暂时减员降级。")]
        [Min(0)] public int minMemberCount;

        [Header("Rhythm (seconds)")]
        [Min(0f)] public float calmDurationMin;
        [Min(0f)] public float calmDurationMax;

        [Header("Long Wolf")]
        [Tooltip("长狼身体宽度倍率。")]
        [Min(0.01f)] public float longWolfWidthMultiplier = 1f;

        [Header("Threat intensity weights")]
        [Tooltip("基础袭击 / 阵型袭击 / 大型袭击的相对抽取权重。")]
        [Min(0f)] public float basicIntensityWeight = 1f;
        [Min(0f)] public float formationIntensityWeight;
        [Min(0f)] public float majorIntensityWeight;

        public float[] IntensityWeights => new[]
        {
            basicIntensityWeight,
            formationIntensityWeight,
            majorIntensityWeight,
        };
    }

    [Serializable]
    public sealed class AttackDefinition
    {
        public WolfAttackType type;
        [Tooltip("达到哪个阶段下标后解锁。")]
        [Min(0)] public int unlockStageIndex;
        public WolfAttackIntensity intensity;
        [Tooltip("同一强度档内的基础权重；实际权重还会受新鲜度影响。")]
        [Min(0f)] public float baseWeight = 1f;
        [Tooltip("关闭后保留实现与配置，但运行时不会抽到。")]
        public bool enabled = true;
    }

    [SerializeField] private Stage[] stages = CreateDefaultStages();
    [SerializeField] private AttackDefinition[] attacks = CreateDefaultAttacks();

    [Header("Freshness")]
    [Tooltip("刚在上一波出现的攻击仍可再次出现，但会乘以这个权重，避免两种攻击变成固定交替。")]
    [SerializeField, Range(0f, 1f)] private float repeatedAttackWeightMultiplier = 0.3f;
    [Tooltip("一种攻击每缺席一波增加的权重倍率。")]
    [SerializeField, Min(0f)] private float freshnessWeightPerMiss = 0.3f;
    [Tooltip("新鲜度最多累计多少波。")]
    [SerializeField, Min(0)] private int maxFreshnessRounds = 4;
    [Tooltip("大型袭击后，下一波只从已解锁的基础袭击中抽取。")]
    [SerializeField] private bool forceBasicAfterMajor = true;

    [Header("Scared Wolf (被吓跑的狼)")]
    [Tooltip("羊群超过这么多只后，抽到直冲狼时狼只会预警、露面、掉头逃跑。")]
    [SerializeField, Min(0)] private int scareThreshold = 50;

    [Header("Wolf Speed")]
    [Tooltip("狼速度随羊群倍率增长的指数：狼倍率 = 羊群倍率 ^ 指数。")]
    [SerializeField, Min(0f)] private float wolfSpeedExponent = 1.3f;

    public Stage[] Stages => stages;
    public AttackDefinition[] Attacks => attacks;
    public float RepeatedAttackWeightMultiplier => repeatedAttackWeightMultiplier;
    public float FreshnessWeightPerMiss => freshnessWeightPerMiss;
    public int MaxFreshnessRounds => maxFreshnessRounds;
    public bool ForceBasicAfterMajor => forceBasicAfterMajor;
    public int ScareThreshold => scareThreshold;
    public float WolfSpeedExponent => wolfSpeedExponent;

    public int GetStageIndex(int memberCount)
    {
        return WolfAttackPlanner.GetStageIndex(stages, memberCount);
    }

    public Stage GetStage(int memberCount)
    {
        return GetStageByIndex(GetStageIndex(memberCount));
    }

    public Stage GetStageByIndex(int stageIndex)
    {
        return stages != null && stageIndex >= 0 && stageIndex < stages.Length ? stages[stageIndex] : null;
    }

    public Vector2 GetCalmDurationRange(int memberCount)
    {
        return GetCalmDurationRangeForStage(GetStageIndex(memberCount));
    }

    public Vector2 GetCalmDurationRangeForStage(int stageIndex)
    {
        Stage stage = GetStageByIndex(stageIndex);
        if (stage == null)
            return Vector2.zero;

        return new Vector2(
            Mathf.Max(0f, stage.calmDurationMin),
            Mathf.Max(stage.calmDurationMin, stage.calmDurationMax));
    }

    public float GetLongWolfWidthMultiplier(int memberCount)
    {
        return GetLongWolfWidthMultiplierForStage(GetStageIndex(memberCount));
    }

    public float GetLongWolfWidthMultiplierForStage(int stageIndex)
    {
        Stage stage = GetStageByIndex(stageIndex);
        return stage != null ? Mathf.Max(0.01f, stage.longWolfWidthMultiplier) : 1f;
    }

    public static Stage[] CreateDefaultStages()
    {
        return new[]
        {
            new Stage
            {
                displayName = "蛰伏", minMemberCount = 0,
                calmDurationMin = 0f, calmDurationMax = 0f,
                basicIntensityWeight = 0f,
            },
            new Stage
            {
                displayName = "基础袭击", minMemberCount = 20,
                calmDurationMin = 5f, calmDurationMax = 8f,
                longWolfWidthMultiplier = 1f,
                basicIntensityWeight = 1f,
            },
            new Stage
            {
                displayName = "阵型袭击", minMemberCount = 50,
                calmDurationMin = 5f, calmDurationMax = 7f,
                longWolfWidthMultiplier = 1.35f,
                basicIntensityWeight = 3f, formationIntensityWeight = 4f,
            },
            new Stage
            {
                displayName = "大型袭击", minMemberCount = 90,
                calmDurationMin = 5f, calmDurationMax = 7f,
                longWolfWidthMultiplier = 1.5f,
                basicIntensityWeight = 2f, formationIntensityWeight = 4f, majorIntensityWeight = 3f,
            },
            new Stage
            {
                displayName = "强化袭击", minMemberCount = 130,
                calmDurationMin = 5f, calmDurationMax = 5f,
                longWolfWidthMultiplier = 1.65f,
                basicIntensityWeight = 2f, formationIntensityWeight = 3f, majorIntensityWeight = 4f,
            },
        };
    }

    public static AttackDefinition[] CreateDefaultAttacks()
    {
        return new[]
        {
            Attack(WolfAttackType.StraightWolf, 1, WolfAttackIntensity.Basic, 5f),
            // 暂时下线：保留 SmartWolf 的代码、Prefab 与配置入口，启用后即可重新加入抽取池。
            Attack(WolfAttackType.SmartWolf, 1, WolfAttackIntensity.Basic, 4f, false),
            // 开局基础袭击只放普通单狼；长条身体作为阵型阶段的新威胁再引入。
            Attack(WolfAttackType.LongWolf, 2, WolfAttackIntensity.Basic, 3f),
            Attack(WolfAttackType.ParallelSequential, 2, WolfAttackIntensity.Formation, 4f),
            Attack(WolfAttackType.PerpendicularChain, 2, WolfAttackIntensity.Formation, 3f),
            Attack(WolfAttackType.ParallelSimultaneous, 3, WolfAttackIntensity.Major, 4f),
            Attack(WolfAttackType.LongWolfWithEscorts, 3, WolfAttackIntensity.Major, 3f),
            Attack(WolfAttackType.Pentagram, 3, WolfAttackIntensity.Major, 1f),
        };
    }

    private static AttackDefinition Attack(
        WolfAttackType type,
        int unlockStageIndex,
        WolfAttackIntensity intensity,
        float baseWeight,
        bool enabled = true)
    {
        return new AttackDefinition
        {
            type = type,
            unlockStageIndex = unlockStageIndex,
            intensity = intensity,
            baseWeight = baseWeight,
            enabled = enabled,
        };
    }

    private void OnValidate()
    {
        if (stages == null || stages.Length == 0)
            stages = CreateDefaultStages();
        if (attacks == null || attacks.Length == 0)
            attacks = CreateDefaultAttacks();

        foreach (Stage stage in stages)
        {
            if (stage == null)
                continue;
            stage.calmDurationMax = Mathf.Max(stage.calmDurationMin, stage.calmDurationMax);
            stage.longWolfWidthMultiplier = Mathf.Max(0.01f, stage.longWolfWidthMultiplier);
        }
    }
}

/// <summary>狼袭抽取状态：阶段只升不降，并记录最近使用情况以形成自然的强弱起伏。</summary>
public sealed class WolfAttackSelectionState
{
    private readonly Dictionary<WolfAttackType, int> missedRounds = new Dictionary<WolfAttackType, int>();
    private int pendingDebutStageIndex = -1;

    public int HighestStageIndex { get; private set; } = -1;
    public WolfAttackType? LastAttack { get; private set; }
    public WolfAttackIntensity? LastIntensity { get; private set; }

    public void Reset()
    {
        missedRounds.Clear();
        pendingDebutStageIndex = -1;
        HighestStageIndex = -1;
        LastAttack = null;
        LastIntensity = null;
    }

    public void ObserveStage(WolfAttackSchedule schedule, int stageIndex)
    {
        if (schedule == null || schedule.Stages == null || stageIndex <= HighestStageIndex)
            return;

        HighestStageIndex = Mathf.Min(stageIndex, schedule.Stages.Length - 1);
        pendingDebutStageIndex = HighestStageIndex;
    }

    public WolfAttackType? Pick(WolfAttackSchedule schedule, Func<float> roll01)
    {
        if (schedule == null || roll01 == null || HighestStageIndex < 0)
            return null;

        List<WolfAttackSchedule.AttackDefinition> unlocked = GetUnlocked(schedule);
        if (unlocked.Count == 0)
            return null;

        WolfAttackSchedule.AttackDefinition selected = PickDebut(schedule, unlocked, roll01);
        if (selected == null)
            selected = PickRegular(schedule, unlocked, roll01);
        if (selected == null)
            return null;

        RegisterSelection(unlocked, selected);
        return selected.type;
    }

    private WolfAttackSchedule.AttackDefinition PickDebut(
        WolfAttackSchedule schedule,
        List<WolfAttackSchedule.AttackDefinition> unlocked,
        Func<float> roll01)
    {
        if (pendingDebutStageIndex < 0)
            return null;

        int debutStage = pendingDebutStageIndex;
        pendingDebutStageIndex = -1;
        List<WolfAttackSchedule.AttackDefinition> candidates = unlocked.FindAll(
            attack => attack.unlockStageIndex == debutStage);
        return PickAttack(schedule, candidates, roll01());
    }

    private WolfAttackSchedule.AttackDefinition PickRegular(
        WolfAttackSchedule schedule,
        List<WolfAttackSchedule.AttackDefinition> unlocked,
        Func<float> roll01)
    {
        List<WolfAttackSchedule.AttackDefinition> candidates = unlocked;
        if (schedule.ForceBasicAfterMajor && LastIntensity == WolfAttackIntensity.Major)
        {
            List<WolfAttackSchedule.AttackDefinition> basic = unlocked.FindAll(
                attack => attack.intensity == WolfAttackIntensity.Basic);
            if (basic.Count > 0)
                candidates = basic;
        }

        WolfAttackSchedule.Stage stage = schedule.GetStageByIndex(HighestStageIndex);
        if (stage == null)
            return null;

        float[] intensityWeights = stage.IntensityWeights;
        for (int intensityIndex = 0; intensityIndex < intensityWeights.Length; intensityIndex++)
        {
            WolfAttackIntensity intensity = (WolfAttackIntensity)intensityIndex;
            if (!candidates.Exists(attack => attack.intensity == intensity))
                intensityWeights[intensityIndex] = 0f;
        }

        int pickedIntensity = WolfAttackPlanner.WeightedIndex(intensityWeights, roll01());
        if (pickedIntensity < 0)
            return null;

        List<WolfAttackSchedule.AttackDefinition> sameIntensity = candidates.FindAll(
            attack => attack.intensity == (WolfAttackIntensity)pickedIntensity);
        return PickAttack(schedule, sameIntensity, roll01());
    }

    private WolfAttackSchedule.AttackDefinition PickAttack(
        WolfAttackSchedule schedule,
        List<WolfAttackSchedule.AttackDefinition> candidates,
        float roll)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        float[] weights = new float[candidates.Count];
        for (int index = 0; index < candidates.Count; index++)
        {
            WolfAttackSchedule.AttackDefinition candidate = candidates[index];
            missedRounds.TryGetValue(candidate.type, out int misses);
            float freshness = 1f + Mathf.Min(misses, schedule.MaxFreshnessRounds) * schedule.FreshnessWeightPerMiss;
            float repeatPenalty = LastAttack == candidate.type ? schedule.RepeatedAttackWeightMultiplier : 1f;
            weights[index] = candidate.baseWeight * freshness * repeatPenalty;
        }

        int pickedIndex = WolfAttackPlanner.WeightedIndex(weights, roll);
        return pickedIndex >= 0 ? candidates[pickedIndex] : null;
    }

    private List<WolfAttackSchedule.AttackDefinition> GetUnlocked(WolfAttackSchedule schedule)
    {
        List<WolfAttackSchedule.AttackDefinition> result = new List<WolfAttackSchedule.AttackDefinition>();
        if (schedule.Attacks == null)
            return result;

        foreach (WolfAttackSchedule.AttackDefinition attack in schedule.Attacks)
        {
            if (attack != null
                && attack.enabled
                && attack.baseWeight > 0f
                && attack.unlockStageIndex <= HighestStageIndex)
            {
                result.Add(attack);
            }
        }
        return result;
    }

    private void RegisterSelection(
        List<WolfAttackSchedule.AttackDefinition> unlocked,
        WolfAttackSchedule.AttackDefinition selected)
    {
        foreach (WolfAttackSchedule.AttackDefinition attack in unlocked)
        {
            missedRounds.TryGetValue(attack.type, out int misses);
            missedRounds[attack.type] = attack.type == selected.type ? 0 : misses + 1;
        }

        LastAttack = selected.type;
        LastIntensity = selected.intensity;
    }
}

/// <summary>不持有运行时状态的基础抽取工具。</summary>
public static class WolfAttackPlanner
{
    public static int GetStageIndex(WolfAttackSchedule.Stage[] stages, int memberCount)
    {
        if (stages == null || stages.Length == 0)
            return -1;

        int result = 0;
        for (int index = 0; index < stages.Length; index++)
        {
            if (stages[index] != null && memberCount >= stages[index].minMemberCount)
                result = index;
        }
        return result;
    }

    public static int WeightedIndex(float[] weights, float roll)
    {
        if (weights == null)
            return -1;

        float total = 0f;
        foreach (float weight in weights)
            total += Math.Max(0f, weight);
        if (total <= 0f)
            return -1;

        float target = Math.Min(Math.Max(roll, 0f), 0.999999f) * total;
        for (int index = 0; index < weights.Length; index++)
        {
            float weight = Math.Max(0f, weights[index]);
            if (weight <= 0f)
                continue;
            target -= weight;
            if (target < 0f)
                return index;
        }

        for (int index = weights.Length - 1; index >= 0; index--)
        {
            if (weights[index] > 0f)
                return index;
        }
        return -1;
    }
}

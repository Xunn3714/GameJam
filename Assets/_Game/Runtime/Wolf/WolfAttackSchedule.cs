using System;
using UnityEngine;

/// <summary>
/// 正式关卡的狼群节奏表：按羊群规模分阶段，每次攻击前先抽"一只狼 / 多只狼 / 本场损失最多的攻击"，
/// 再按阶段抽具体的攻击方式。默认数值来自策划表，可在 Inspector 调整。
/// </summary>
[CreateAssetMenu(fileName = "WolfAttackSchedule", menuName = "Game/Wolf Attack Schedule")]
public sealed class WolfAttackSchedule : ScriptableObject
{
    [Serializable]
    public sealed class Stage
    {
        public string displayName = "阶段";
        [Tooltip("羊群至少多少只进入这个阶段（按当前羊数）。")]
        [Min(0)] public int minMemberCount;

        [Header("大类权重（一只狼 / 多只狼 / 本场损失最多的攻击）")]
        [Min(0f)] public float singleWeight = 100f;
        [Min(0f)] public float packWeight;
        [Min(0f)] public float mostLossWeight;

        [Header("一只狼：直冲 / 聪明 / 长狼")]
        [Min(0f)] public float straightWolfWeight = 100f;
        [Min(0f)] public float smartWolfWeight;
        [Min(0f)] public float longWolfWeight;

        [Header("多只狼：并排轮冲 / 并排齐冲 / 直角连击 / 包夹 / 五角星")]
        [Min(0f)] public float parallelSequentialWeight;
        [Min(0f)] public float parallelSimultaneousWeight;
        [Min(0f)] public float perpendicularChainWeight;
        [Min(0f)] public float escortsWeight;
        [Min(0f)] public float pentagramWeight;

        public bool HasAnyAttack => singleWeight + packWeight + mostLossWeight > 0f;

        public float[] CategoryWeights => new[] { singleWeight, packWeight, mostLossWeight };
        public float[] SingleWeights => new[] { straightWolfWeight, smartWolfWeight, longWolfWeight };
        public float[] PackWeights => new[]
        {
            parallelSequentialWeight, parallelSimultaneousWeight, perpendicularChainWeight, escortsWeight, pentagramWeight,
        };
    }

    [SerializeField] private Stage[] stages = CreateDefaultStages();

    [Header("Scared Wolf (被吓跑的狼)")]
    [Tooltip("羊群超过这么多只后，抽到普通狼（直冲 / 聪明）时狼只会预警、露面、掉头逃跑。")]
    [SerializeField, Min(0)] private int scareThreshold = 50;

    [Header("Pentagram Trigger")]
    [Tooltip("进入第几个阶段（下标）时立刻触发一次五角星围猎；-1 = 不触发。")]
    [SerializeField] private int pentagramOnEnterStage = 4;

    [Header("Wolf Speed")]
    [Tooltip("狼速度随羊群倍率增长的指数：>1 表示狼比羊涨得快。狼倍率 = 羊群倍率 ^ 指数。")]
    [SerializeField, Min(0f)] private float wolfSpeedExponent = 1.3f;

    public Stage[] Stages => stages;
    public int ScareThreshold => scareThreshold;
    public int PentagramOnEnterStage => pentagramOnEnterStage;
    public float WolfSpeedExponent => wolfSpeedExponent;

    /// <summary>羊数对应的阶段下标（取 minMemberCount 不大于羊数的最后一个）。</summary>
    public int GetStageIndex(int memberCount)
    {
        return WolfAttackPlanner.GetStageIndex(stages, memberCount);
    }

    public Stage GetStage(int memberCount)
    {
        int index = GetStageIndex(memberCount);
        return index >= 0 && index < stages.Length ? stages[index] : null;
    }

    public static Stage[] CreateDefaultStages()
    {
        return new[]
        {
            new Stage
            {
                displayName = "阶段0 教学", minMemberCount = 0,
                singleWeight = 0f, packWeight = 0f, mostLossWeight = 0f,
            },
            new Stage
            {
                displayName = "阶段一", minMemberCount = 6,
                singleWeight = 100f, packWeight = 0f, mostLossWeight = 0f,
                straightWolfWeight = 100f, smartWolfWeight = 0f, longWolfWeight = 0f,
            },
            new Stage
            {
                displayName = "阶段二", minMemberCount = 20,
                singleWeight = 60f, packWeight = 40f, mostLossWeight = 0f,
                straightWolfWeight = 10f, smartWolfWeight = 45f, longWolfWeight = 45f,
                parallelSequentialWeight = 40f, parallelSimultaneousWeight = 40f,
                perpendicularChainWeight = 15f, escortsWeight = 5f, pentagramWeight = 0f,
            },
            new Stage
            {
                displayName = "阶段三", minMemberCount = 50,
                singleWeight = 20f, packWeight = 80f, mostLossWeight = 0f,
                straightWolfWeight = 0f, smartWolfWeight = 50f, longWolfWeight = 50f,
                parallelSequentialWeight = 30f, parallelSimultaneousWeight = 30f,
                perpendicularChainWeight = 25f, escortsWeight = 15f, pentagramWeight = 0f,
            },
            new Stage
            {
                displayName = "阶段四", minMemberCount = 90,
                singleWeight = 20f, packWeight = 65f, mostLossWeight = 15f,
                straightWolfWeight = 0f, smartWolfWeight = 45f, longWolfWeight = 55f,
                parallelSequentialWeight = 20f, parallelSimultaneousWeight = 20f,
                perpendicularChainWeight = 30f, escortsWeight = 30f, pentagramWeight = 5f,
            },
        };
    }

    private void OnValidate()
    {
        if (stages == null || stages.Length == 0)
            stages = CreateDefaultStages();
    }
}

/// <summary>抽取逻辑（纯 C#，可测）。</summary>
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

    /// <summary>
    /// 先抽大类，再抽具体攻击。<paramref name="mostLoss"/> 为 null（本场还没丢过羊）时，
    /// "损失最多"这一类的权重按比例并回一只狼 / 多只狼。抽不到任何攻击返回 null。
    /// </summary>
    public static WolfAttackType? Pick(WolfAttackSchedule.Stage stage, WolfAttackType? mostLoss, Func<float> roll01)
    {
        if (stage == null || roll01 == null)
            return null;

        WolfAttackCategory? category = PickCategory(stage, mostLoss.HasValue, roll01);
        if (!category.HasValue)
            return null;

        switch (category.Value)
        {
            case WolfAttackCategory.MostLoss:
                return mostLoss;
            case WolfAttackCategory.Single:
            {
                int index = WeightedIndex(stage.SingleWeights, roll01());
                return index < 0 ? null : WolfAttackTypes.SingleTypes[index];
            }
            default:
            {
                int index = WeightedIndex(stage.PackWeights, roll01());
                return index < 0 ? null : WolfAttackTypes.PackTypes[index];
            }
        }
    }

    public static WolfAttackCategory? PickCategory(WolfAttackSchedule.Stage stage, bool hasMostLoss, Func<float> roll01)
    {
        float[] weights = stage.CategoryWeights;
        if (!hasMostLoss)
            weights[2] = 0f;

        int index = WeightedIndex(weights, roll01());
        return index < 0 ? null : (WolfAttackCategory)index;
    }

    /// <summary>按权重抽下标；roll 为 [0,1)。总权重为 0 返回 -1。</summary>
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

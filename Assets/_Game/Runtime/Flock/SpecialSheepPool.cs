using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public enum SheepQuality
{
    Common = 0,
    Green = 1,
    Blue = 2,
    Purple = 3,
    Gold = 4,
    EasterEgg = 5,
}

public readonly struct SpecialSheepSelection
{
    public SpecialSheepSelection(
        RecruitableSheep prefab,
        string typeId,
        string typeName,
        SheepQuality quality,
        string visualEffectId,
        string abilityId)
    {
        Prefab = prefab;
        TypeId = typeId;
        TypeName = typeName;
        Quality = quality;
        VisualEffectId = visualEffectId;
        AbilityId = abilityId;
    }

    public RecruitableSheep Prefab { get; }
    public string TypeId { get; }
    public string TypeName { get; }
    public SheepQuality Quality { get; }
    public string VisualEffectId { get; }
    public string AbilityId { get; }
}

[CreateAssetMenu(fileName = "SpecialSheepPool", menuName = "Sheep MVP/Special Sheep Pool")]
public sealed class SpecialSheepPool : ScriptableObject
{
    [Serializable]
    private sealed class Entry
    {
        [SerializeField] private string sheepTypeId;
        [SerializeField] private string displayName;
        [SerializeField] private RecruitableSheep prefab;
        [SerializeField] private string visualEffectId;
        [SerializeField] private string abilityId;

        public string SheepTypeId => sheepTypeId?.Trim();
        public string DisplayName => displayName?.Trim();
        public RecruitableSheep Prefab => prefab;
        public string VisualEffectId => visualEffectId?.Trim();
        public string AbilityId => abilityId?.Trim();
    }

    [Serializable]
    private sealed class QualityTier
    {
        [SerializeField] private SheepQuality quality;
        [SerializeField, Range(0f, 100f)] private float probabilityPercent;
        [SerializeField] private List<Entry> entries = new();

        public SheepQuality Quality => quality;
        public float ProbabilityPercent => Mathf.Max(0f, probabilityPercent);
        public IReadOnlyList<Entry> Entries => entries;
    }

    private static readonly SheepQuality[] RollOrder =
    {
        SheepQuality.EasterEgg,
        SheepQuality.Gold,
        SheepQuality.Purple,
        SheepQuality.Blue,
        SheepQuality.Green,
    };

    [SerializeField] private List<QualityTier> tiers = new();

    /// <summary>
    /// 为一个生成位抽取特殊羊。命中空品质或该品质所有羊均已出现时返回 false，
    /// 调用方应直接生成普通羊，不继续降级抽取。
    /// </summary>
    public bool TryPick(
        Random random,
        ISet<string> consumedTypeIds,
        out SpecialSheepSelection selection)
    {
        selection = default;
        if (random == null)
            return false;

        SheepQuality rolledQuality = RollQuality(random.NextDouble() * 100d);
        return rolledQuality != SheepQuality.Common
            && TryPickFromQuality(rolledQuality, random, consumedTypeIds, out selection);
    }

    /// <summary>把 0～100 的随机值映射到表格品质概率，供验证和未来幸运值修正复用。</summary>
    public SheepQuality RollQuality(double rollPercent)
    {
        double threshold = 0d;
        foreach (SheepQuality quality in RollOrder)
        {
            QualityTier tier = FindTier(quality);
            if (tier == null)
                continue;

            threshold += tier.ProbabilityPercent;
            if (rollPercent < threshold)
                return quality;
        }

        return SheepQuality.Common;
    }

    /// <summary>
    /// 从指定品质中均匀选择一个本局未出现的羊。可供未来剧情点或条件解锁复用。
    /// </summary>
    public bool TryPickFromQuality(
        SheepQuality quality,
        Random random,
        ISet<string> consumedTypeIds,
        out SpecialSheepSelection selection)
    {
        selection = default;
        QualityTier tier = FindTier(quality);
        if (tier == null || random == null)
            return false;

        List<Entry> candidates = new();
        foreach (Entry entry in tier.Entries)
        {
            if (entry == null
                || entry.Prefab == null
                || string.IsNullOrWhiteSpace(entry.SheepTypeId)
                || (consumedTypeIds != null && consumedTypeIds.Contains(entry.SheepTypeId)))
            {
                continue;
            }

            candidates.Add(entry);
        }

        if (candidates.Count == 0)
            return false;

        Entry picked = candidates[random.Next(candidates.Count)];
        consumedTypeIds?.Add(picked.SheepTypeId);
        selection = new SpecialSheepSelection(
            picked.Prefab,
            picked.SheepTypeId,
            picked.DisplayName,
            quality,
            picked.VisualEffectId,
            picked.AbilityId);
        return true;
    }

    public float GetProbabilityPercent(SheepQuality quality)
    {
        QualityTier tier = FindTier(quality);
        return tier != null ? tier.ProbabilityPercent : 0f;
    }

    private QualityTier FindTier(SheepQuality quality)
    {
        foreach (QualityTier tier in tiers)
            if (tier != null && tier.Quality == quality) return tier;
        return null;
    }

    private void OnValidate()
    {
        HashSet<SheepQuality> seenQualities = new();
        HashSet<string> seenTypeIds = new(StringComparer.Ordinal);
        foreach (QualityTier tier in tiers)
        {
            if (tier == null)
                continue;
            if (tier.Quality == SheepQuality.Common)
                Debug.LogWarning("普通羊不应配置在特殊品质池中；普通羊由未命中回退生成。", this);
            if (!seenQualities.Add(tier.Quality))
                Debug.LogWarning($"品质 {tier.Quality} 重复配置，只会读取第一项。", this);

            foreach (Entry entry in tier.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.SheepTypeId))
                    continue;
                if (!seenTypeIds.Add(entry.SheepTypeId))
                    Debug.LogWarning($"特殊羊类型 id 重复：{entry.SheepTypeId}", this);
            }
        }
    }
}

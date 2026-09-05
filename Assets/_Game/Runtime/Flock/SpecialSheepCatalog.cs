using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpecialSheepCatalog", menuName = "Sheep Alpha/Special Sheep Catalog")]
public sealed class SpecialSheepCatalog : ScriptableObject
{
    public const string DefaultSourceRootFolder = "Assets/SpecialSheep";
    private const string LegacySourceRootFolder = "Assets/羊 程序";

    [Serializable]
    public sealed class Entry
    {
        [SerializeField, HideInInspector] private string assetGuid;
        [SerializeField, HideInInspector] private string typeId;
        [SerializeField, HideInInspector] private string lastImportedFileName;
        [SerializeField] private string displayName;
        [SerializeField, HideInInspector] private Sprite sprite;
        [SerializeField, TextArea(2, 4)] private string codexDescription;
        [SerializeField, TextArea(2, 4)] private string visualEffectDescription;
        [SerializeField, TextArea(2, 4)] private string gameplayEffectDescription;
        [SerializeField, TextArea(1, 3)] private string unlockHint;
        [SerializeField] private bool spawnEnabled = true;

        public string AssetGuid => assetGuid?.Trim();
        public string TypeId => typeId?.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? TypeId : displayName.Trim();
        public Sprite Sprite => sprite;
        public string CodexDescription => codexDescription?.Trim();
        public string VisualEffectDescription => visualEffectDescription?.Trim();
        public string GameplayEffectDescription => gameplayEffectDescription?.Trim();
        public string UnlockHint => unlockHint?.Trim();
        public bool SpawnEnabled => spawnEnabled;
        public bool CanSpawn => spawnEnabled && sprite != null && !string.IsNullOrWhiteSpace(TypeId);

#if UNITY_EDITOR
        public void EditorBindImportedSprite(string guid, string stableTypeId, string defaultDisplayName, Sprite importedSprite)
        {
            assetGuid = guid?.Trim();
            typeId = stableTypeId?.Trim();
            sprite = importedSprite;

            string nextImportedFileName = defaultDisplayName?.Trim();
            string previousDisplayName = displayName?.Trim();
            string previousPlaceholder = string.IsNullOrWhiteSpace(previousDisplayName)
                ? null
                : $"一只独特的{previousDisplayName}。";
            bool hasGeneratedDescription = string.IsNullOrWhiteSpace(codexDescription)
                || string.Equals(codexDescription.Trim(), previousPlaceholder, StringComparison.Ordinal);
            bool hasGeneratedDisplayName = string.IsNullOrWhiteSpace(previousDisplayName)
                || string.Equals(previousDisplayName, lastImportedFileName?.Trim(), StringComparison.Ordinal)
                || (string.IsNullOrWhiteSpace(lastImportedFileName) && hasGeneratedDescription);

            if (hasGeneratedDisplayName)
                displayName = nextImportedFileName;
            if (hasGeneratedDescription && !string.IsNullOrWhiteSpace(displayName))
                codexDescription = $"一只独特的{displayName.Trim()}。";
            lastImportedFileName = nextImportedFileName;
        }

        public void EditorMarkMissing()
        {
            sprite = null;
        }
#endif
    }

    [Serializable]
    public sealed class Tier
    {
        [SerializeField] private SheepQuality quality;
        [SerializeField] private string folderName;
        [SerializeField, Range(0f, 100f)] private float probabilityPercent;
        [SerializeField] private List<Entry> entries = new();

        public Tier(SheepQuality quality, string folderName, float probabilityPercent)
        {
            this.quality = quality;
            this.folderName = folderName;
            this.probabilityPercent = probabilityPercent;
        }

        public SheepQuality Quality => quality;
        public string FolderName => folderName?.Trim();
        public float ProbabilityPercent => Mathf.Max(0f, probabilityPercent);
        public IReadOnlyList<Entry> Entries => entries;

#if UNITY_EDITOR
        public List<Entry> EditorEntries => entries;
#endif
    }

    private static readonly SheepQuality[] RollOrder =
    {
        SheepQuality.EasterEgg,
        SheepQuality.Gold,
        SheepQuality.Purple,
        SheepQuality.Blue,
        SheepQuality.Green,
    };

    [SerializeField] private string sourceRootFolder = DefaultSourceRootFolder;
    [SerializeField] private RecruitableSheep baseSheepPrefab;
    [SerializeField] private List<Tier> tiers = new()
    {
        new Tier(SheepQuality.Green, "绿色羊", 20f),
        new Tier(SheepQuality.Blue, "蓝色羊", 5f),
        new Tier(SheepQuality.Purple, "紫色羊", 1f),
        new Tier(SheepQuality.Gold, "金色羊", 0.3f),
        new Tier(SheepQuality.EasterEgg, "彩色羊", 0.1f),
    };

    public string SourceRootFolder => string.IsNullOrWhiteSpace(sourceRootFolder)
        ? DefaultSourceRootFolder
        : sourceRootFolder.Trim().TrimEnd('/', '\\');
    public RecruitableSheep BaseSheepPrefab => baseSheepPrefab;
    public IReadOnlyList<Tier> Tiers => tiers;
    public float SpecialProbabilityPercent
    {
        get
        {
            float total = 0f;
            foreach (Tier tier in tiers)
                if (tier != null) total += tier.ProbabilityPercent;
            return total;
        }
    }
    public float CommonProbabilityPercent => Mathf.Max(0f, 100f - SpecialProbabilityPercent);

    public SheepQuality RollQuality(double rollPercent)
    {
        double threshold = 0d;
        foreach (SheepQuality quality in RollOrder)
        {
            Tier tier = FindTier(quality);
            if (tier == null)
                continue;

            threshold += tier.ProbabilityPercent;
            if (rollPercent < threshold)
                return quality;
        }

        return SheepQuality.Common;
    }

    public bool TryPickAvailable(
        SheepQuality quality,
        System.Random random,
        Func<string, bool> isAvailable,
        out Entry selection)
    {
        selection = null;
        Tier tier = FindTier(quality);
        if (tier == null || random == null)
            return false;

        List<Entry> candidates = new();
        foreach (Entry entry in tier.Entries)
        {
            if (entry == null || !entry.CanSpawn)
                continue;
            if (isAvailable != null && !isAvailable(entry.TypeId))
                continue;
            candidates.Add(entry);
        }

        if (candidates.Count == 0)
            return false;

        selection = candidates[random.Next(candidates.Count)];
        return true;
    }

    public Entry Find(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return null;

        string normalized = typeId.Trim();
        foreach (Tier tier in tiers)
        {
            if (tier == null)
                continue;
            foreach (Entry entry in tier.Entries)
                if (entry != null && string.Equals(entry.TypeId, normalized, StringComparison.Ordinal)) return entry;
        }

        return null;
    }

    public string GetDisplayName(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId) || string.Equals(typeId, MvpSheepCatalog.DefaultTypeId, StringComparison.Ordinal))
            return "普通羊";

        Entry entry = Find(typeId);
        return entry != null ? entry.DisplayName : typeId.Trim();
    }

    public float GetProbabilityPercent(SheepQuality quality)
    {
        Tier tier = FindTier(quality);
        return tier != null ? tier.ProbabilityPercent : 0f;
    }

#if UNITY_EDITOR
    public void EditorSetBaseSheepPrefab(RecruitableSheep prefab)
    {
        baseSheepPrefab = prefab;
    }

    public void EditorEnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(sourceRootFolder)
            || string.Equals(
                sourceRootFolder.Trim().TrimEnd('/', '\\'),
                LegacySourceRootFolder,
                StringComparison.OrdinalIgnoreCase))
        {
            sourceRootFolder = DefaultSourceRootFolder;
        }
        tiers ??= new List<Tier>();
        if (tiers.Count > 0)
            return;

        tiers.Add(new Tier(SheepQuality.Green, "绿色羊", 20f));
        tiers.Add(new Tier(SheepQuality.Blue, "蓝色羊", 5f));
        tiers.Add(new Tier(SheepQuality.Purple, "紫色羊", 1f));
        tiers.Add(new Tier(SheepQuality.Gold, "金色羊", 0.3f));
        tiers.Add(new Tier(SheepQuality.EasterEgg, "彩色羊", 0.1f));
    }
#endif

    private Tier FindTier(SheepQuality quality)
    {
        foreach (Tier tier in tiers)
            if (tier != null && tier.Quality == quality) return tier;
        return null;
    }

    private void OnValidate()
    {
        sourceRootFolder = SourceRootFolder.Replace('\\', '/');

        if (baseSheepPrefab == null)
            Debug.LogWarning("特殊羊目录缺少共用的基础羊 Prefab，运行时将无法生成羊。", this);

        HashSet<SheepQuality> qualities = new();
        HashSet<string> typeIds = new(StringComparer.Ordinal);
        foreach (Tier tier in tiers)
        {
            if (tier == null)
                continue;
            if (tier.Quality == SheepQuality.Common)
                Debug.LogWarning("普通羊不应作为特殊羊目录品质。", this);
            if (!qualities.Add(tier.Quality))
                Debug.LogWarning($"特殊羊品质重复：{tier.Quality}", this);

            foreach (Entry entry in tier.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.TypeId))
                    continue;
                if (!typeIds.Add(entry.TypeId))
                    Debug.LogWarning($"特殊羊类型 ID 重复：{entry.TypeId}", this);
            }
        }

        if (SpecialProbabilityPercent > 100.0001f)
            Debug.LogError($"特殊羊品质概率总和为 {SpecialProbabilityPercent:0.###}%，不能超过 100%。", this);
    }
}

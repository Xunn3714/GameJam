using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 一局的按类型统计（纯 C#，可测）：当前构成、累计招募、被狼抓走、峰值构成、生存时间、历史最高。
/// 初始羊计入当前 / 峰值，但不计入累计招募（由调用方决定何时调用 RecordRecruit）。
/// </summary>
public sealed class AlphaRunStats
{
    public sealed class DestructionEntry
    {
        internal DestructionEntry(string obstacleId, string displayName)
        {
            ObstacleId = obstacleId;
            DisplayName = displayName;
        }

        public string ObstacleId { get; }
        public string DisplayName { get; }
        public int Count { get; internal set; }
        public int Score { get; internal set; }
    }

    private readonly Dictionary<string, int> recruitedByType = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> takenByType = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> peakComposition = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> currentComposition = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, DestructionEntry> destroyedByType =
        new Dictionary<string, DestructionEntry>(StringComparer.Ordinal);

    public int HighestFlockSize { get; private set; }
    public int TotalRecruited { get; private set; }
    public int TotalTaken { get; private set; }
    public float SurvivalSeconds { get; set; }
    public int CurrentFlockSize { get; private set; }
    public int DestructionScore { get; private set; }
    public int TotalDestroyed { get; private set; }
    /// <summary>被长条狼叼走的数量（由狼群节奏控制器统计后写入）。</summary>
    public int TakenByLongWolves { get; private set; }
    /// <summary>被单只普通狼叼走的数量。</summary>
    public int TakenBySingleWolves { get; private set; }

    public void SetWolfBreakdown(int byLongWolves, int bySingleWolves)
    {
        TakenByLongWolves = Math.Max(0, byLongWolves);
        TakenBySingleWolves = Math.Max(0, bySingleWolves);
    }

    public IReadOnlyDictionary<string, int> RecruitedByType => recruitedByType;
    public IReadOnlyDictionary<string, int> TakenByType => takenByType;
    public IReadOnlyDictionary<string, int> PeakComposition => peakComposition;
    public IReadOnlyDictionary<string, int> CurrentComposition => currentComposition;
    public IReadOnlyDictionary<string, DestructionEntry> DestroyedByType => destroyedByType;

    public void RecordRecruit(string typeId)
    {
        Increment(recruitedByType, Normalize(typeId));
        TotalRecruited++;
    }

    public void RecordTaken(string typeId)
    {
        Increment(takenByType, Normalize(typeId));
        TotalTaken++;
    }

    /// <summary>记录一个已经完全破坏的物件。score 由物件的破坏羊数门槛派生。</summary>
    public void RecordDestruction(string obstacleId, string displayName, int score)
    {
        string normalizedId = NormalizeObstacleId(obstacleId, displayName);
        string normalizedName = NormalizeObstacleName(displayName);
        int awardedScore = Math.Max(1, score);

        if (!destroyedByType.TryGetValue(normalizedId, out DestructionEntry entry))
        {
            entry = new DestructionEntry(normalizedId, normalizedName);
            destroyedByType.Add(normalizedId, entry);
        }

        entry.Count++;
        entry.Score += awardedScore;
        TotalDestroyed++;
        DestructionScore += awardedScore;
    }

    /// <summary>喂入当前羊群所有成员的类型；羊数创新高时记录峰值构成。</summary>
    public void ObserveComposition(IEnumerable<string> memberTypeIds)
    {
        currentComposition.Clear();
        int count = 0;
        if (memberTypeIds != null)
        {
            foreach (string typeId in memberTypeIds)
            {
                Increment(currentComposition, Normalize(typeId));
                count++;
            }
        }

        CurrentFlockSize = count;
        if (count > HighestFlockSize)
        {
            HighestFlockSize = count;
            peakComposition.Clear();
            foreach (KeyValuePair<string, int> pair in currentComposition)
                peakComposition[pair.Key] = pair.Value;
        }
    }

    // ---- 真结局：踩出来的大洞和找到的洪山菜薹 ----
    /// <summary>踩出来的洞：每只羊 2 平方米。</summary>
    public const float HoleSquareMetersPerSheep = 2f;
    public const float SquareMetersPerMu = 2000f / 3f;
    public const float CaitaiJinPerMu = 300f;
    /// <summary>每只羊对应的菜薹：2 平方米 × 300 斤/亩 ÷ (2000/3 平方米/亩) = 0.9 斤。</summary>
    public const float CaitaiJinPerSheep = HoleSquareMetersPerSheep * CaitaiJinPerMu / SquareMetersPerMu;

    /// <summary>大于 0 表示这局走的是真结局；数值是踩塌宝通寺时的羊数。</summary>
    public int TrueEndingFlockSize { get; private set; }
    public bool IsTrueEnding => TrueEndingFlockSize > 0;

    public void RecordTrueEnding(int flockSize)
    {
        TrueEndingFlockSize = Math.Max(0, flockSize);
    }

    public string BuildTrueEndingHighlights()
    {
        if (!IsTrueEnding)
            return string.Empty;

        float holeSquareMeters = TrueEndingFlockSize * HoleSquareMetersPerSheep;
        float caitaiJin = TrueEndingFlockSize * CaitaiJinPerSheep;
        return $"踩出了 {holeSquareMeters:0.#} 平方米的大洞\n" +
               $"找到了 {caitaiJin:0.#} 斤的美味洪山菜薹";
    }

    public string BuildReport(Func<string, string> displayName)
    {
        displayName ??= id => id;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"生存时间：{FormatTime(SurvivalSeconds)}　历史最高：{HighestFlockSize} 只");
        builder.AppendLine($"当前羊群（{CurrentFlockSize}）：{Describe(currentComposition, displayName)}");
        builder.AppendLine($"累计招募（{TotalRecruited}）：{Describe(recruitedByType, displayName)}");
        builder.AppendLine($"被狼抓走（{TotalTaken}）：{Describe(takenByType, displayName)}");
        builder.AppendLine($"　其中长条狼叼走 {TakenByLongWolves} 只，单只狼叼走 {TakenBySingleWolves} 只");
        builder.AppendLine(BuildDestructionSummary());
        builder.Append($"峰值构成（{HighestFlockSize}）：{Describe(peakComposition, displayName)}");
        return builder.ToString();
    }

    public string BuildDestructionSummary()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append($"破坏得分：{DestructionScore}　破坏物品：{TotalDestroyed} 件");
        builder.Append("\n破坏清单：");

        if (destroyedByType.Count == 0)
        {
            builder.Append("无");
            return builder.ToString();
        }

        Dictionary<string, DestructionEntry> mergedByName =
            new Dictionary<string, DestructionEntry>(StringComparer.Ordinal);
        foreach (DestructionEntry source in destroyedByType.Values)
        {
            if (!mergedByName.TryGetValue(source.DisplayName, out DestructionEntry merged))
            {
                merged = new DestructionEntry(source.DisplayName, source.DisplayName);
                mergedByName.Add(source.DisplayName, merged);
            }

            merged.Count += source.Count;
            merged.Score += source.Score;
        }

        List<DestructionEntry> ordered = new List<DestructionEntry>(mergedByName.Values);
        ordered.Sort((a, b) =>
            b.Score != a.Score
                ? b.Score.CompareTo(a.Score)
                : b.Count != a.Count
                    ? b.Count.CompareTo(a.Count)
                    : string.CompareOrdinal(a.DisplayName, b.DisplayName));

        for (int index = 0; index < ordered.Count; index++)
        {
            if (index > 0)
                builder.Append('，');
            builder.Append(ordered[index].DisplayName).Append(" ×").Append(ordered[index].Count);
        }

        return builder.ToString();
    }

    public static string Describe(IReadOnlyDictionary<string, int> composition, Func<string, string> displayName)
    {
        if (composition == null || composition.Count == 0)
            return "无";

        List<KeyValuePair<string, int>> ordered = new List<KeyValuePair<string, int>>(composition);
        ordered.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));

        StringBuilder builder = new StringBuilder();
        for (int index = 0; index < ordered.Count; index++)
        {
            if (index > 0) builder.Append("，");
            builder.Append(displayName(ordered[index].Key)).Append(' ').Append(ordered[index].Value);
        }

        return builder.ToString();
    }

    public static string FormatTime(float seconds)
    {
        int total = Math.Max(0, (int)seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    private static string Normalize(string typeId)
    {
        return string.IsNullOrWhiteSpace(typeId) ? MvpSheepCatalog.DefaultTypeId : typeId.Trim();
    }

    private static string NormalizeObstacleId(string obstacleId, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(obstacleId))
            return obstacleId.Trim();
        if (!string.IsNullOrWhiteSpace(displayName))
            return "obstacle.unknown." + displayName.Trim();
        return "obstacle.unknown";
    }

    private static string NormalizeObstacleName(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? "未知物品" : displayName.Trim();
    }

    private static void Increment(Dictionary<string, int> map, string key)
    {
        map.TryGetValue(key, out int value);
        map[key] = value + 1;
    }
}

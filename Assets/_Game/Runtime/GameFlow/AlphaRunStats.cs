using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 一局的按类型统计（纯 C#，可测）：当前构成、累计招募、被狼抓走、峰值构成、生存时间、历史最高。
/// 初始羊计入当前 / 峰值，但不计入累计招募（由调用方决定何时调用 RecordRecruit）。
/// </summary>
public sealed class AlphaRunStats
{
    private readonly Dictionary<string, int> recruitedByType = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> takenByType = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> peakComposition = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> currentComposition = new Dictionary<string, int>(StringComparer.Ordinal);

    public int HighestFlockSize { get; private set; }
    public int TotalRecruited { get; private set; }
    public int TotalTaken { get; private set; }
    public float SurvivalSeconds { get; set; }
    public int CurrentFlockSize { get; private set; }
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

    public string BuildReport(Func<string, string> displayName)
    {
        displayName ??= id => id;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"生存时间：{FormatTime(SurvivalSeconds)}　历史最高：{HighestFlockSize} 只");
        builder.AppendLine($"当前羊群（{CurrentFlockSize}）：{Describe(currentComposition, displayName)}");
        builder.AppendLine($"累计招募（{TotalRecruited}）：{Describe(recruitedByType, displayName)}");
        builder.AppendLine($"被狼抓走（{TotalTaken}）：{Describe(takenByType, displayName)}");
        builder.AppendLine($"　其中长条狼叼走 {TakenByLongWolves} 只，单只狼叼走 {TakenBySingleWolves} 只");
        builder.Append($"峰值构成（{HighestFlockSize}）：{Describe(peakComposition, displayName)}");
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

    private static void Increment(Dictionary<string, int> map, string key)
    {
        map.TryGetValue(key, out int value);
        map[key] = value + 1;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatValueType
{
    Integer,
    Decimal,
    TimeSeconds
}


[Serializable]
public class GameStatEntry
{
    public string key;
    public string displayName;

    public float value;

    public string suffix;

    public StatValueType valueType;

    public int order;

    // 用于最小值等统计，判断是否已经真的记录过一次
    public bool hasValue;


    public string GetFormattedValue()
    {
        switch (valueType)
        {
            case StatValueType.TimeSeconds:
                return FormatTime(value);

            case StatValueType.Decimal:
                return value.ToString("0.##") + suffix;

            case StatValueType.Integer:
            default:
                return Mathf.RoundToInt(value) + suffix;
        }
    }


    private string FormatTime(float seconds)
    {
        if (!hasValue)
            return "--:--";

        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));

        int minutes = totalSeconds / 60;
        int remainSeconds = totalSeconds % 60;

        return $"{minutes:00}:{remainSeconds:00}";
    }
}


[Serializable]
public class GameStatsSaveData
{
    public List<GameStatEntry> stats = new List<GameStatEntry>();
}


public class GameStatsManager : MonoBehaviour
{
    public static GameStatsManager Instance { get; private set; }

    private const string SAVE_KEY = "GameStatsData";

    [SerializeField]
    private List<GameStatEntry> stats = new List<GameStatEntry>();

    private AlphaRunStats lastRecordedRun;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        LoadStats();
        RegisterAlphaStats();
    }

    private void RegisterAlphaStats()
    {
        RegisterStat("wins", "成功冲出草原", suffix: " 次", order: 0);
        RegisterStat("fastest_win", "最快通关时间", StatValueType.TimeSeconds, order: 1);
        RegisterStat("slowest_win", "最慢通关时间", StatValueType.TimeSeconds, order: 2);
        RegisterStat("largest_win_flock", "通关时最多羊数", suffix: " 只", order: 3);
        RegisterStat("smallest_win_flock", "通关时最少羊数", suffix: " 只", order: 4);
        RegisterStat("sheep_collected", "累计收集的羊", suffix: " 只", order: 10);
        RegisterStat("largest_flock", "单局最大羊群", suffix: " 只", order: 20);
        RegisterStat("most_recruits", "单局最多招募", suffix: " 只", order: 21);
        RegisterStat("sheep_taken", "累计被狼叼走", suffix: " 只", order: 30);
        RegisterStat("long_wolf_taken", "其中：长条狼叼走", suffix: " 只", order: 31);
        RegisterStat("other_wolf_taken", "其中：其他狼叼走", suffix: " 只", order: 32);
        RegisterStat("runs_completed", "已结算的旅程", suffix: " 局", order: 40);
        RegisterStat("runs_lost", "未能逃出的旅程", suffix: " 局", order: 41);
        RegisterStat("finished_play_time", "累计游玩时间（已结算）", StatValueType.TimeSeconds, order: 42);
    }

    // Called after the final frame's loss/composition events have completed.
    // Recruitment already accumulates in FlockController, so it is not added again here.
    public void RecordRunResult(AlphaRunStats run, bool victory)
    {
        if (run == null || ReferenceEquals(lastRecordedRun, run))
            return;

        RegisterAlphaStats();
        float duration = Mathf.Max(0f, run.SurvivalSeconds);
        AddStat("runs_completed");
        AddStat("finished_play_time", duration);
        UpdateMaxStat("largest_flock", run.HighestFlockSize);
        UpdateMaxStat("most_recruits", run.TotalRecruited);
        AddStat("sheep_taken", run.TotalTaken);
        int longWolfLosses = Mathf.Clamp(run.TakenByLongWolves, 0, run.TotalTaken);
        AddStat("long_wolf_taken", longWolfLosses);
        AddStat("other_wolf_taken", run.TotalTaken - longWolfLosses);

        if (victory)
        {
            AddStat("wins");
            UpdateMinStat("fastest_win", duration);
            UpdateMaxStat("slowest_win", duration);
            UpdateMaxStat("largest_win_flock", run.CurrentFlockSize);
            UpdateMinStat("smallest_win_flock", run.CurrentFlockSize);
        }
        else
        {
            AddStat("runs_lost");
        }

        lastRecordedRun = run;
        SaveStats();
    }


    // 注册统计项
    public void RegisterStat(
        string key,
        string displayName,
        StatValueType valueType = StatValueType.Integer,
        string suffix = "",
        int order = 0)
    {
        GameStatEntry existing = FindStat(key);

        if (existing != null)
        {
            // 如果已经存在，只更新显示信息
            existing.displayName = displayName;
            existing.valueType = valueType;
            existing.suffix = suffix;
            existing.order = order;

            return;
        }

        GameStatEntry newStat = new GameStatEntry
        {
            key = key,
            displayName = displayName,
            value = 0f,
            suffix = suffix,
            valueType = valueType,
            order = order,
            hasValue = false
        };

        stats.Add(newStat);
    }

    // 累加统计
    public void AddStat(string key, float amount = 1f)
    {
        GameStatEntry stat = FindStat(key);

        if (stat == null)
        {
            Debug.LogWarning(
                $"GameStatsManager: Stat '{key}' has not been registered.");

            return;
        }

        stat.value += amount;
        stat.hasValue = true;
    }

    // 直接设置数值
    public void SetStat(string key, float value)
    {
        GameStatEntry stat = FindStat(key);

        if (stat == null)
        {
            Debug.LogWarning(
                $"GameStatsManager: Stat '{key}' has not been registered.");

            return;
        }

        stat.value = value;
        stat.hasValue = true;
    }

    // 更新最大值
    public void UpdateMaxStat(string key, float value)
    {
        GameStatEntry stat = FindStat(key);

        if (stat == null)
        {
            Debug.LogWarning(
                $"GameStatsManager: Stat '{key}' has not been registered.");

            return;
        }

        if (!stat.hasValue || value > stat.value)
        {
            stat.value = value;
            stat.hasValue = true;
        }
    }

    // 更新最小值
    public void UpdateMinStat(string key, float value)
    {
        GameStatEntry stat = FindStat(key);

        if (stat == null)
        {
            Debug.LogWarning(
                $"GameStatsManager: Stat '{key}' has not been registered.");

            return;
        }

        if (!stat.hasValue || value < stat.value)
        {
            stat.value = value;
            stat.hasValue = true;
        }
    }

    // 获取单个统计
    public GameStatEntry GetStat(string key)
    {
        return FindStat(key);
    }

    // 提供给 StatisticsPanel
    public List<GameStatEntry> GetAllStats()
    {
        List<GameStatEntry> result =
            new List<GameStatEntry>(stats);

        result.Sort(
            (a, b) => a.order.CompareTo(b.order)
        );

        return result;
    }

    // 保存
    public void SaveStats()
    {
        GameStatsSaveData data =
            new GameStatsSaveData();

        data.stats = stats;

        string json =
            JsonUtility.ToJson(data);

        PlayerPrefs.SetString(
            SAVE_KEY,
            json);

        PlayerPrefs.Save();
    }


    // 读取

    private void LoadStats()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
            return;

        string json =
            PlayerPrefs.GetString(SAVE_KEY);

        GameStatsSaveData data =
            JsonUtility.FromJson<GameStatsSaveData>(json);

        if (data != null &&
            data.stats != null)
        {
            stats = data.stats;
        }
    }

    // 清空统计
    public void ResetAllStats()
    {
        stats.Clear();
        lastRecordedRun = null;
        RegisterAlphaStats();

        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
    }


    private GameStatEntry FindStat(string key)
    {
        return stats.Find(
            stat => stat.key == key);
    }


    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveStats();
    }


    private void OnApplicationQuit()
    {
        SaveStats();
    }
}

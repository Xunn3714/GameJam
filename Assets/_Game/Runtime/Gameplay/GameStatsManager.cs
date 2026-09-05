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

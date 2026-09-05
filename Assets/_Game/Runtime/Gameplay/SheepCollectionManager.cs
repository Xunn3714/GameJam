using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class SheepCollectionProgress
{
    public string sheepId;
    public bool unlocked;
    public int encounterCount;
}


[Serializable]
public class SheepCollectionSaveData
{
    public List<SheepCollectionProgress> progressList =
        new List<SheepCollectionProgress>();
}


public class SheepCollectionManager : MonoBehaviour
{
    public static SheepCollectionManager Instance { get; private set; }

    private const string SAVE_KEY = "SheepCollectionProgress";

    [Header("Database")]
    public SheepCollectionDatabase database;

    [Tooltip("当前主玩法的唯一特殊羊目录；存在时图鉴直接从这里生成，不再维护第二份羊列表。")]
    [SerializeField] private SpecialSheepCatalog specialSheepCatalog;

    [Header("Runtime Progress")]
    [SerializeField]
        private List<SheepCollectionProgress> progressList =
        new List<SheepCollectionProgress>();

    private readonly List<SheepCollectionEntry> catalogEntries =
        new List<SheepCollectionEntry>();


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        RebuildCatalogEntries();
        LoadProgress();
        EnsureDatabaseEntries();
    }

    // 遇到一只羊
    public void EncounterSheep(string sheepId)
    {
        SheepCollectionProgress progress =
            GetOrCreateProgress(sheepId);

        if (progress == null)
            return;

        progress.unlocked = true;
        progress.encounterCount++;

        SaveProgress();
    }

    // 是否已经解锁
    public bool IsUnlocked(string sheepId)
    {
        SheepCollectionProgress progress =
            FindProgress(sheepId);

        return progress != null &&
               progress.unlocked;
    }


    // 获取遇到次数
    public int GetEncounterCount(string sheepId)
    {
        SheepCollectionProgress progress =
            FindProgress(sheepId);

        if (progress == null)
            return 0;

        return progress.encounterCount;
    }

    // 已解锁数量
    public int GetUnlockedCount()
    {
        int count = 0;
        HashSet<string> configuredIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SheepCollectionEntry entry in GetConfiguredEntries())
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.sheepId))
                configuredIds.Add(entry.sheepId);
        }

        foreach (SheepCollectionProgress progress
                 in progressList)
        {
            if (progress != null && progress.unlocked && configuredIds.Contains(progress.sheepId))
                count++;
        }

        return count;
    }


    // 图鉴总羊数量
    public int GetTotalCount()
    {
        return GetConfiguredEntries().Count;
    }


    // 获取所有羊资料
    public List<SheepCollectionEntry> GetAllSheep()
    {
        return new List<SheepCollectionEntry>(GetConfiguredEntries());
    }


    // 获取某只羊资料
    public SheepCollectionEntry GetSheepData(
        string sheepId)
    {
        return GetConfiguredEntries().Find(sheep => sheep.sheepId == sheepId);
    }


    // 确保 Database 中的羊
    // 都有对应进度记录
    private void EnsureDatabaseEntries()
    {
        List<SheepCollectionEntry> entries = GetConfiguredEntries();
        if (entries.Count == 0)
        {
            Debug.LogWarning(
                "SheepCollectionManager: collection catalog is missing or empty.");

            return;
        }

        foreach (SheepCollectionEntry sheep in entries)
        {
            if (FindProgress(sheep.sheepId) == null)
            {
                progressList.Add(
                    new SheepCollectionProgress
                    {
                        sheepId = sheep.sheepId,
                        unlocked = false,
                        encounterCount = 0
                    }
                );
            }
        }
    }


    private SheepCollectionProgress GetOrCreateProgress(
        string sheepId)
    {
        SheepCollectionProgress progress =
            FindProgress(sheepId);

        if (progress != null)
            return progress;

        if (GetSheepData(sheepId) == null)
        {
            Debug.LogWarning(
                $"SheepCollectionManager: Sheep '{sheepId}' does not exist in database.");

            return null;
        }

        progress = new SheepCollectionProgress
        {
            sheepId = sheepId,
            unlocked = false,
            encounterCount = 0
        };

        progressList.Add(progress);

        return progress;
    }


    private SheepCollectionProgress FindProgress(
        string sheepId)
    {
        return progressList.Find(
            progress =>
                progress.sheepId == sheepId);
    }


    private List<SheepCollectionEntry> GetConfiguredEntries()
    {
        if (catalogEntries.Count > 0)
            return catalogEntries;

        return database != null
            ? database.GetAllSheep()
            : new List<SheepCollectionEntry>();
    }


    private void RebuildCatalogEntries()
    {
        catalogEntries.Clear();
        if (specialSheepCatalog == null)
            return;

        Sprite commonSprite = null;
        if (specialSheepCatalog.BaseSheepPrefab != null)
        {
            SpriteRenderer renderer =
                specialSheepCatalog.BaseSheepPrefab.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
                commonSprite = renderer.sprite;
        }

        catalogEntries.Add(new SheepCollectionEntry
        {
            sheepId = MvpSheepCatalog.DefaultTypeId,
            displayName = "普通羊",
            icon = commonSprite,
            description = "最常见、也最可靠的羊群伙伴。",
            abilityName = "群体行动",
            abilityDescription = "会跟随羊群一起移动、收拢与冲刺。",
            order = 0
        });

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal)
        {
            MvpSheepCatalog.DefaultTypeId
        };
        int order = 1;
        foreach (SpecialSheepCatalog.Tier tier in specialSheepCatalog.Tiers)
        {
            if (tier == null)
                continue;

            foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.TypeId) ||
                    entry.Sprite == null || !ids.Add(entry.TypeId))
                {
                    continue;
                }

                string effect = !string.IsNullOrWhiteSpace(entry.GameplayEffectDescription)
                    ? entry.GameplayEffectDescription
                    : entry.VisualEffectDescription;
                catalogEntries.Add(new SheepCollectionEntry
                {
                    sheepId = entry.TypeId,
                    displayName = entry.DisplayName,
                    icon = entry.Sprite,
                    description = string.IsNullOrWhiteSpace(entry.CodexDescription)
                        ? $"一只独特的{entry.DisplayName}。"
                        : entry.CodexDescription,
                    abilityName = string.IsNullOrWhiteSpace(effect) ? "外观特征" : "特殊效果",
                    abilityDescription = string.IsNullOrWhiteSpace(effect)
                        ? "暂无额外能力说明。"
                        : effect,
                    order = order++
                });
            }
        }
    }

    // 保存
    public void SaveProgress()
    {
        SheepCollectionSaveData data =
            new SheepCollectionSaveData();

        data.progressList = progressList;

        string json =
            JsonUtility.ToJson(data);

        PlayerPrefs.SetString(
            SAVE_KEY,
            json);

        PlayerPrefs.Save();
    }

    // 读取
    private void LoadProgress()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
            return;

        string json =
            PlayerPrefs.GetString(SAVE_KEY);

        SheepCollectionSaveData data =
            JsonUtility.FromJson
            <SheepCollectionSaveData>(json);

        if (data != null &&
            data.progressList != null)
        {
            progressList =
                data.progressList;
        }
    }

    // 清空图鉴进度
    public void ResetProgress()
    {
        progressList.Clear();

        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();

        EnsureDatabaseEntries();
    }


    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveProgress();
    }


    private void OnApplicationQuit()
    {
        SaveProgress();
    }
}

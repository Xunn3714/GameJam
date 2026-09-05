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

    private const string SAVE_KEY =
        "SheepCollectionProgress";


    [Header("Database")]
    public SheepCollectionDatabase database;


    [Header("Sheep Catalog")]
    [Tooltip(
        "图鉴主数据源。普通羊来自 BaseSheepPrefab，" +
        "特殊羊及其品阶来自 SpecialSheepCatalog 的 Tiers。")]
    [SerializeField]
    private SpecialSheepCatalog specialSheepCatalog;


    [Header("Runtime Progress")]
    [SerializeField]
    private List<SheepCollectionProgress> progressList =
        new List<SheepCollectionProgress>();


    private readonly List<SheepCollectionEntry> catalogEntries =
        new List<SheepCollectionEntry>();


    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
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


    // ============================================================
    // ENCOUNTER
    // ============================================================

    public void EncounterSheep(string sheepId)
    {
        if (string.IsNullOrWhiteSpace(sheepId))
            return;


        SheepCollectionProgress progress =
            GetOrCreateProgress(sheepId);


        if (progress == null)
            return;


        progress.unlocked = true;
        progress.encounterCount++;


        SaveProgress();
    }


    // ============================================================
    // QUERY
    // ============================================================

    public bool IsUnlocked(string sheepId)
    {
        SheepCollectionProgress progress =
            FindProgress(sheepId);


        return progress != null &&
               progress.unlocked;
    }


    public int GetEncounterCount(string sheepId)
    {
        SheepCollectionProgress progress =
            FindProgress(sheepId);


        if (progress == null)
            return 0;


        return progress.encounterCount;
    }


    public int GetUnlockedCount()
    {
        int count = 0;


        HashSet<string> configuredIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );


        foreach (SheepCollectionEntry entry
                 in GetConfiguredEntries())
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(
                    entry.sheepId))
            {
                continue;
            }


            configuredIds.Add(
                entry.sheepId
            );
        }


        foreach (SheepCollectionProgress progress
                 in progressList)
        {
            if (progress == null ||
                !progress.unlocked)
            {
                continue;
            }


            if (configuredIds.Contains(
                progress.sheepId))
            {
                count++;
            }
        }


        return count;
    }


    public int GetTotalCount()
    {
        return GetConfiguredEntries().Count;
    }


    public List<SheepCollectionEntry> GetAllSheep()
    {
        return new List<SheepCollectionEntry>(
            GetConfiguredEntries()
        );
    }


    public SheepCollectionEntry GetSheepData(
        string sheepId)
    {
        return GetConfiguredEntries().Find(
            sheep =>
                sheep != null &&
                sheep.sheepId == sheepId
        );
    }


    // ============================================================
    // PROGRESS
    // ============================================================

    private void EnsureDatabaseEntries()
    {
        List<SheepCollectionEntry> entries =
            GetConfiguredEntries();


        if (entries.Count == 0)
        {
            Debug.LogWarning(
                "SheepCollectionManager: collection catalog is missing or empty."
            );

            return;
        }


        foreach (SheepCollectionEntry sheep
                 in entries)
        {
            if (sheep == null ||
                string.IsNullOrWhiteSpace(
                    sheep.sheepId))
            {
                continue;
            }


            if (FindProgress(
                sheep.sheepId) != null)
            {
                continue;
            }


            progressList.Add(
                new SheepCollectionProgress
                {
                    sheepId =
                        sheep.sheepId,

                    unlocked =
                        false,

                    encounterCount =
                        0
                }
            );
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
                $"SheepCollectionManager: Sheep '{sheepId}' does not exist in catalog."
            );

            return null;
        }


        progress =
            new SheepCollectionProgress
            {
                sheepId =
                    sheepId,

                unlocked =
                    false,

                encounterCount =
                    0
            };


        progressList.Add(progress);

        return progress;
    }


    private SheepCollectionProgress FindProgress(
        string sheepId)
    {
        return progressList.Find(
            progress =>
                progress != null &&
                progress.sheepId == sheepId
        );
    }


    // ============================================================
    // CATALOG
    // ============================================================

    private List<SheepCollectionEntry> GetConfiguredEntries()
    {
        if (catalogEntries.Count > 0)
        {
            return catalogEntries;
        }


        return database != null
            ? database.GetAllSheep()
            : new List<SheepCollectionEntry>();
    }


    private void RebuildCatalogEntries()
    {
        catalogEntries.Clear();


        if (specialSheepCatalog == null)
            return;


        // ========================================================
        // COMMON SHEEP
        // ========================================================

        Sprite commonSprite = null;


        if (specialSheepCatalog.BaseSheepPrefab != null)
        {
            SpriteRenderer renderer =
                specialSheepCatalog
                    .BaseSheepPrefab
                    .GetComponentInChildren
                        <SpriteRenderer>();


            if (renderer != null)
            {
                commonSprite =
                    renderer.sprite;
            }
        }


        catalogEntries.Add(
            new SheepCollectionEntry
            {
                sheepId =
                    MvpSheepCatalog.DefaultTypeId,

                displayName =
                    "普通羊",

                icon =
                    commonSprite,

                quality =
                    SheepQuality.Common,

                rarityName =
                    "普通羊",

                description =
                    "最常见、也最可靠的羊群伙伴。",

                abilityName =
                    "群体行动",

                abilityDescription =
                    "会跟随羊群一起移动、收拢与冲刺。",

                order =
                    0
            }
        );


        // ========================================================
        // SPECIAL SHEEP
        // ========================================================

        HashSet<string> ids =
            new HashSet<string>(
                StringComparer.Ordinal
            )
            {
                MvpSheepCatalog.DefaultTypeId
            };


        int order = 1;


        foreach (SpecialSheepCatalog.Tier tier
                 in specialSheepCatalog.Tiers)
        {
            if (tier == null)
                continue;


            foreach (SpecialSheepCatalog.Entry entry
                     in tier.Entries)
            {
                if (entry == null)
                    continue;


                if (string.IsNullOrWhiteSpace(
                    entry.TypeId))
                {
                    continue;
                }


                if (entry.Sprite == null)
                    continue;


                if (!ids.Add(entry.TypeId))
                    continue;


                string effect =
                    !string.IsNullOrWhiteSpace(
                        entry.GameplayEffectDescription)
                        ? entry.GameplayEffectDescription
                        : entry.VisualEffectDescription;


                string rarityName =
                    !string.IsNullOrWhiteSpace(
                        tier.FolderName)
                        ? tier.FolderName
                        : GetDefaultRarityName(
                            tier.Quality
                        );


                catalogEntries.Add(
                    new SheepCollectionEntry
                    {
                        sheepId =
                            entry.TypeId,

                        displayName =
                            entry.DisplayName,

                        icon =
                            entry.Sprite,

                        quality =
                            tier.Quality,

                        rarityName =
                            rarityName,

                        description =
                            string.IsNullOrWhiteSpace(
                                entry.CodexDescription)
                                ? $"一只独特的{entry.DisplayName}。"
                                : entry.CodexDescription,

                        abilityName =
                            string.IsNullOrWhiteSpace(effect)
                                ? "外观特征"
                                : "特殊效果",

                        abilityDescription =
                            string.IsNullOrWhiteSpace(effect)
                                ? "暂无额外能力说明。"
                                : effect,

                        order =
                            order++
                    }
                );
            }
        }
    }


    private static string GetDefaultRarityName(
        SheepQuality quality)
    {
        switch (quality)
        {
            case SheepQuality.Green:
                return "绿色羊";

            case SheepQuality.Blue:
                return "蓝色羊";

            case SheepQuality.Purple:
                return "紫色羊";

            case SheepQuality.Gold:
                return "金色羊";

            case SheepQuality.EasterEgg:
                return "彩色羊";

            case SheepQuality.Common:
            default:
                return "普通羊";
        }
    }


    // ============================================================
    // SAVE
    // ============================================================

    public void SaveProgress()
    {
        SheepCollectionSaveData data =
            new SheepCollectionSaveData
            {
                progressList =
                    progressList
            };


        string json =
            JsonUtility.ToJson(data);


        PlayerPrefs.SetString(
            SAVE_KEY,
            json
        );


        PlayerPrefs.Save();
    }


    private void LoadProgress()
    {
        if (!PlayerPrefs.HasKey(
            SAVE_KEY))
        {
            return;
        }


        string json =
            PlayerPrefs.GetString(
                SAVE_KEY
            );


        SheepCollectionSaveData data =
            JsonUtility.FromJson
                <SheepCollectionSaveData>(
                    json
                );


        if (data != null &&
            data.progressList != null)
        {
            progressList =
                data.progressList;
        }
    }


    public void ResetProgress()
    {
        progressList.Clear();


        PlayerPrefs.DeleteKey(
            SAVE_KEY
        );


        PlayerPrefs.Save();


        EnsureDatabaseEntries();
    }


    private void OnApplicationPause(
        bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveProgress();
        }
    }


    private void OnApplicationQuit()
    {
        SaveProgress();
    }
}

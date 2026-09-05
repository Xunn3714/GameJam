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

    [Header("Runtime Progress")]
    [SerializeField]
    private List<SheepCollectionProgress> progressList =
        new List<SheepCollectionProgress>();


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

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

        foreach (SheepCollectionProgress progress
                 in progressList)
        {
            if (progress.unlocked)
                count++;
        }

        return count;
    }


    // 图鉴总羊数量
    public int GetTotalCount()
    {
        if (database == null)
            return 0;

        return database.sheepList.Count;
    }


    // 获取所有羊资料
    public List<SheepCollectionEntry> GetAllSheep()
    {
        if (database == null)
            return new List<SheepCollectionEntry>();

        return database.GetAllSheep();
    }


    // 获取某只羊资料
    public SheepCollectionEntry GetSheepData(
        string sheepId)
    {
        if (database == null)
            return null;

        return database.GetSheep(sheepId);
    }


    // 确保 Database 中的羊
    // 都有对应进度记录
    private void EnsureDatabaseEntries()
    {
        if (database == null)
        {
            Debug.LogWarning(
                "SheepCollectionManager: Database is missing.");

            return;
        }

        foreach (SheepCollectionEntry sheep
                 in database.sheepList)
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

        if (database != null &&
            database.GetSheep(sheepId) == null)
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

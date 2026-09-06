using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GameStatisticsTests
{
    private const string SaveKey = "GameStatsData";
    private GameObject root;
    private GameStatsManager manager;
    private bool hadSave;
    private string originalSave;

    [SetUp]
    public void SetUp()
    {
        hadSave = PlayerPrefs.HasKey(SaveKey);
        originalSave = PlayerPrefs.GetString(SaveKey, string.Empty);
        root = new GameObject("GameStatisticsTests");
        root.SetActive(false); // Do not replace the scene's singleton or load the player's save.
        manager = root.AddComponent<GameStatsManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        if (hadSave) PlayerPrefs.SetString(SaveKey, originalSave);
        else PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    [Test]
    public void VictoryUsesFinalFlockAndDoesNotCountRecruitsTwice()
    {
        manager.RegisterStat("sheep_collected", "Existing count");
        manager.SetStat("sheep_collected", 326);
        AlphaRunStats run = CreateRun(120, 118, 108, 119, 2, 2);
        manager.RecordRunResult(run, true);
        manager.RecordRunResult(run, true);
        Assert.AreEqual(1, Value("wins"));
        Assert.AreEqual(1, Value("runs_completed"));
        Assert.AreEqual(118, Value("largest_win_flock"));
        Assert.AreEqual(120, Value("largest_flock"));
        Assert.AreEqual(119, Value("most_recruits"));
        Assert.AreEqual(326, Value("sheep_collected"));
        Assert.AreEqual(2, Value("sheep_taken"));
        Assert.AreEqual(2, Value("long_wolf_taken"));
        Assert.AreEqual(0, Value("other_wolf_taken"));
        Assert.AreEqual("01:48", manager.GetStat("fastest_win").GetFormattedValue());
    }

    [Test]
    public void DefeatDoesNotCreateAZeroSecondWinOrZeroSheepWinRecord()
    {
        manager.RecordRunResult(CreateRun(12, 0, 75, 11, 4, 1), false);
        Assert.AreEqual(1, Value("runs_lost"));
        Assert.AreEqual(0, Value("wins"));
        Assert.AreEqual(75, Value("finished_play_time"));
        Assert.AreEqual(3, Value("other_wolf_taken"));
        Assert.IsFalse(manager.GetStat("fastest_win").hasValue);
        Assert.IsFalse(manager.GetStat("smallest_win_flock").hasValue);
        Assert.AreEqual("--:--", manager.GetStat("fastest_win").GetFormattedValue());
    }

    [Test]
    public void MultipleJourneysAccumulateAndKeepIndependentWinExtremes()
    {
        manager.RecordRunResult(CreateRun(60, 8, 300, 59, 12, 5), true);
        manager.RecordRunResult(CreateRun(50, 47, 90, 49, 2, 1), true);
        manager.RecordRunResult(CreateRun(80, 0, 20, 79, 8, 0), false);
        Assert.AreEqual(2, Value("wins"));
        Assert.AreEqual(3, Value("runs_completed"));
        Assert.AreEqual(90, Value("fastest_win"));
        Assert.AreEqual(300, Value("slowest_win"));
        Assert.AreEqual(47, Value("largest_win_flock"));
        Assert.AreEqual(8, Value("smallest_win_flock"));
        Assert.AreEqual(80, Value("largest_flock"));
        Assert.AreEqual(410, Value("finished_play_time"));
        Assert.AreEqual(22, Value("sheep_taken"));
        Assert.AreEqual(Value("sheep_taken"), Value("long_wolf_taken") + Value("other_wolf_taken"));
    }

    [Test]
    public void LoadingLegacyCollectionCountPreservesItWithoutInventingOldRuns()
    {
        PlayerPrefs.SetString(SaveKey,
            "{\"stats\":[{\"key\":\"sheep_collected\",\"displayName\":\"Old\",\"value\":326,\"hasValue\":true}]}");
        InvokePrivate("LoadStats");
        InvokePrivate("RegisterAlphaStats");
        Assert.AreEqual(326, Value("sheep_collected"));
        Assert.AreEqual(0, Value("runs_completed"));
        Assert.IsFalse(manager.GetStat("fastest_win").hasValue);
        Assert.AreEqual(14, manager.GetAllStats().Select(stat => stat.key).Distinct().Count());
    }

    [Test]
    public void SavedRecordsSurviveReloadAndContinueAccumulating()
    {
        manager.RecordRunResult(CreateRun(120, 110, 150, 119, 3, 1), true);
        GameStatsSaveData saved = JsonUtility.FromJson<GameStatsSaveData>(PlayerPrefs.GetString(SaveKey));
        Assert.AreEqual(1, saved.stats.Single(stat => stat.key == "wins").value);
        Assert.IsTrue(saved.stats.Single(stat => stat.key == "smallest_win_flock").hasValue);
        InvokePrivate("LoadStats");
        InvokePrivate("RegisterAlphaStats");
        manager.RecordRunResult(CreateRun(130, 105, 100, 129, 5, 2), true);
        Assert.AreEqual(2, Value("wins"));
        Assert.AreEqual(100, Value("fastest_win"));
        Assert.AreEqual(150, Value("slowest_win"));
        Assert.AreEqual(105, Value("smallest_win_flock"));
    }

    [Test]
    public void ResetRestoresEmptyRecordsAndAllowsTheNextJourney()
    {
        manager.RecordRunResult(CreateRun(20, 12, 200, 19, 4, 0), true);
        manager.ResetAllStats();
        Assert.AreEqual(0, Value("wins"));
        Assert.IsFalse(manager.GetStat("smallest_win_flock").hasValue);
        manager.RecordRunResult(CreateRun(30, 15, 100, 29, 2, 0), true);
        Assert.AreEqual(1, Value("wins"));
        Assert.AreEqual(15, Value("smallest_win_flock"));
    }

    private float Value(string key) => manager.GetStat(key).value;

    private void InvokePrivate(string method)
    {
        typeof(GameStatsManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
    }

    private static AlphaRunStats CreateRun(int peak, int final, float duration, int recruited, int taken, int longLosses)
    {
        AlphaRunStats run = new AlphaRunStats { SurvivalSeconds = duration };
        run.ObserveComposition(Enumerable.Repeat("sheep.mvp.common", peak));
        run.ObserveComposition(Enumerable.Repeat("sheep.mvp.common", final));
        for (int i = 0; i < recruited; i++) run.RecordRecruit("sheep.mvp.common");
        for (int i = 0; i < taken; i++) run.RecordTaken("sheep.mvp.common");
        run.SetWolfBreakdown(longLosses, taken - longLosses);
        return run;
    }
}

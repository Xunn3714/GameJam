using System.Collections.Generic;
using NUnit.Framework;

public sealed class AlphaRunStatsTests
{
    private const string Common = "sheep.mvp.common";
    private const string Black = "sheep.special.black";

    [Test]
    public void InitialSheepCountsTowardCurrentAndPeakButNotRecruited()
    {
        AlphaRunStats stats = new AlphaRunStats();
        stats.ObserveComposition(new[] { Common });

        Assert.AreEqual(1, stats.CurrentFlockSize);
        Assert.AreEqual(1, stats.HighestFlockSize);
        Assert.AreEqual(1, stats.PeakComposition[Common]);
        Assert.AreEqual(0, stats.TotalRecruited);
    }

    [Test]
    public void RecruitAndTakenAreTrackedByType()
    {
        AlphaRunStats stats = new AlphaRunStats();
        stats.RecordRecruit(Common);
        stats.RecordRecruit(Common);
        stats.RecordRecruit(Black);
        stats.RecordTaken(Black);

        Assert.AreEqual(3, stats.TotalRecruited);
        Assert.AreEqual(2, stats.RecruitedByType[Common]);
        Assert.AreEqual(1, stats.RecruitedByType[Black]);
        Assert.AreEqual(1, stats.TotalTaken);
        Assert.AreEqual(1, stats.TakenByType[Black]);
    }

    [Test]
    public void PeakCompositionIsSnapshotAtHighestCount()
    {
        AlphaRunStats stats = new AlphaRunStats();
        stats.ObserveComposition(new[] { Common, Common, Black });
        stats.ObserveComposition(new[] { Common });

        Assert.AreEqual(3, stats.HighestFlockSize);
        Assert.AreEqual(2, stats.PeakComposition[Common]);
        Assert.AreEqual(1, stats.PeakComposition[Black]);
        Assert.AreEqual(1, stats.CurrentFlockSize);
        Assert.IsFalse(stats.CurrentComposition.ContainsKey(Black));
    }

    [Test]
    public void NullOrEmptyTypeFallsBackToCommon()
    {
        AlphaRunStats stats = new AlphaRunStats();
        stats.RecordRecruit(null);
        stats.RecordRecruit("  ");
        Assert.AreEqual(2, stats.RecruitedByType[Common]);
    }

    [Test]
    public void ReportListsEveryCategory()
    {
        AlphaRunStats stats = new AlphaRunStats();
        stats.ObserveComposition(new List<string> { Common, Black });
        stats.RecordRecruit(Black);
        stats.SurvivalSeconds = 75f;

        string report = stats.BuildReport(id => id == Black ? "黑羊" : "普通羊");
        StringAssert.Contains("01:15", report);
        StringAssert.Contains("黑羊 1", report);
        StringAssert.Contains("普通羊 1", report);
        StringAssert.Contains("被狼抓走（0）：无", report);
    }

    [Test]
    public void TrueEndingHighlightsUseHoleAreaAndMuYieldFormula()
    {
        AlphaRunStats stats = new AlphaRunStats();
        stats.RecordTrueEnding(150);

        Assert.That(AlphaRunStats.CaitaiJinPerSheep, Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That(stats.BuildTrueEndingHighlights(), Is.EqualTo(
            "踩出了 300 平方米的大洞\n找到了 135 斤的美味洪山菜薹"));
    }
}

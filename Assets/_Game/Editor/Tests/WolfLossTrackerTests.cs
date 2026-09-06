using NUnit.Framework;

public sealed class WolfLossTrackerTests
{
    [Test]
    public void TracksTotalsByWolfKind()
    {
        WolfLossTracker tracker = new WolfLossTracker();
        tracker.RecordTaken(WolfAttackType.SmartWolf, byLongWolf: false);
        tracker.RecordTaken(WolfAttackType.LongWolf, byLongWolf: true);
        tracker.RecordTaken(WolfAttackType.LongWolfWithEscorts, byLongWolf: true, count: 3);
        tracker.RecordTaken(WolfAttackType.LongWolfWithEscorts, byLongWolf: false);

        Assert.AreEqual(6, tracker.TotalTaken);
        Assert.AreEqual(4, tracker.TakenByLongWolves);
        // 包夹里的普通狼不算"单只狼"。
        Assert.AreEqual(1, tracker.TakenBySingleWolves);
        Assert.AreEqual(4, tracker.GetLoss(WolfAttackType.LongWolfWithEscorts));
    }

    [Test]
    public void MostLossIgnoresPentagramAndEmptyRuns()
    {
        WolfLossTracker tracker = new WolfLossTracker();
        Assert.IsNull(tracker.MostLossType);

        tracker.RecordTaken(WolfAttackType.Pentagram, true, 10);
        Assert.IsNull(tracker.MostLossType);

        tracker.RecordTaken(WolfAttackType.SmartWolf, false, 2);
        tracker.RecordTaken(WolfAttackType.ParallelSequential, true, 3);
        Assert.AreEqual(WolfAttackType.ParallelSequential, tracker.MostLossType);

        tracker.Clear();
        Assert.IsNull(tracker.MostLossType);
        Assert.AreEqual(0, tracker.TotalTaken);
    }
}

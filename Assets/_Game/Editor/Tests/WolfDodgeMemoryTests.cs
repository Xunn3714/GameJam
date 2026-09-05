using NUnit.Framework;

public sealed class WolfDodgeMemoryTests
{
    [Test]
    public void AverageOfRecentSamples()
    {
        WolfDodgeMemory memory = new WolfDodgeMemory(20);
        memory.Record(10f);
        memory.Record(20f);
        memory.Record(-6f);
        Assert.AreEqual(3, memory.Count);
        Assert.AreEqual(8f, memory.AverageDegrees, 0.001f);
        Assert.AreEqual(-6f, memory.LatestDegrees, 0.001f);
    }

    [Test]
    public void KeepsOnlyTheLastCapacitySamples()
    {
        WolfDodgeMemory memory = new WolfDodgeMemory(3);
        memory.Record(100f);
        memory.Record(1f);
        memory.Record(2f);
        memory.Record(3f);
        Assert.AreEqual(3, memory.Count);
        Assert.AreEqual(2f, memory.AverageDegrees, 0.001f, "最早的 100 应该被挤出去");
    }

    [Test]
    public void IgnoresInvalidSamplesAndReportsReadiness()
    {
        WolfDodgeMemory memory = new WolfDodgeMemory(20);
        memory.Record(float.NaN);
        Assert.AreEqual(0, memory.Count);
        Assert.IsFalse(memory.HasEnoughSamples(1));
        memory.Record(5f);
        Assert.IsTrue(memory.HasEnoughSamples(1));
        Assert.IsFalse(memory.HasEnoughSamples(3));
        memory.Clear();
        Assert.AreEqual(0, memory.Count);
        Assert.AreEqual(0f, memory.AverageDegrees);
    }
}

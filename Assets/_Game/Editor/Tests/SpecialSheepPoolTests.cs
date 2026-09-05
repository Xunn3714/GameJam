using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public sealed class SpecialSheepPoolTests
{
    private const string PoolPath =
        "Assets/_Game/Content/Data/SheepMvp/DefaultSpecialSheepPool.asset";

    private SpecialSheepPool pool;

    [SetUp]
    public void SetUp()
    {
        pool = AssetDatabase.LoadAssetAtPath<SpecialSheepPool>(PoolPath);
        Assert.IsNotNull(pool, $"Missing pool asset: {PoolPath}");
    }

    [Test]
    public void ProbabilitiesMatchTheDesignWorkbook()
    {
        Assert.That(pool.GetProbabilityPercent(SheepQuality.Green), Is.EqualTo(50f).Within(0.0001f));
        Assert.That(pool.GetProbabilityPercent(SheepQuality.Blue), Is.EqualTo(5f).Within(0.0001f));
        Assert.That(pool.GetProbabilityPercent(SheepQuality.Purple), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(pool.GetProbabilityPercent(SheepQuality.Gold), Is.EqualTo(0.3f).Within(0.0001f));
        Assert.That(pool.GetProbabilityPercent(SheepQuality.EasterEgg), Is.EqualTo(0.1f).Within(0.0001f));
    }

    [TestCase(0.05d, SheepQuality.EasterEgg)]
    [TestCase(0.2d, SheepQuality.Gold)]
    [TestCase(0.8d, SheepQuality.Purple)]
    [TestCase(2d, SheepQuality.Blue)]
    [TestCase(10d, SheepQuality.Green)]
    [TestCase(80d, SheepQuality.Common)]
    public void RollMapsToExpectedQuality(double roll, SheepQuality expected)
    {
        Assert.AreEqual(expected, pool.RollQuality(roll));
    }

    [Test]
    public void GreenSheepAppearOnceThenTheTierIsExhausted()
    {
        HashSet<string> consumed = new(StringComparer.Ordinal);
        Random random = new(12345);

        for (int index = 0; index < 4; index++)
        {
            Assert.IsTrue(pool.TryPickFromQuality(
                SheepQuality.Green,
                random,
                consumed,
                out SpecialSheepSelection selection));
            Assert.AreEqual(SheepQuality.Green, selection.Quality);
            Assert.IsTrue(consumed.Contains(selection.TypeId));
        }

        Assert.AreEqual(4, consumed.Count);
        Assert.IsFalse(pool.TryPickFromQuality(
            SheepQuality.Green,
            random,
            consumed,
            out _));
    }

    [Test]
    public void EmptyQualityDoesNotFallBackToAnotherQuality()
    {
        Assert.IsFalse(pool.TryPickFromQuality(
            SheepQuality.Blue,
            new Random(54321),
            new HashSet<string>(StringComparer.Ordinal),
            out _));
    }
}

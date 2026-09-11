using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class WolfAttackPlannerTests
{
    private static WolfAttackSchedule.Stage[] Stages() => WolfAttackSchedule.CreateDefaultStages();

    [Test]
    public void StageIndexFollowsThreatUnlockThresholds()
    {
        WolfAttackSchedule.Stage[] stages = Stages();
        Assert.AreEqual(0, WolfAttackPlanner.GetStageIndex(stages, 1));
        Assert.AreEqual(0, WolfAttackPlanner.GetStageIndex(stages, 19));
        Assert.AreEqual(1, WolfAttackPlanner.GetStageIndex(stages, 20));
        Assert.AreEqual(1, WolfAttackPlanner.GetStageIndex(stages, 49));
        Assert.AreEqual(2, WolfAttackPlanner.GetStageIndex(stages, 50));
        Assert.AreEqual(3, WolfAttackPlanner.GetStageIndex(stages, 90));
        Assert.AreEqual(4, WolfAttackPlanner.GetStageIndex(stages, 130));
        Assert.AreEqual(4, WolfAttackPlanner.GetStageIndex(stages, 500));
    }

    [Test]
    public void DefaultStagesControlOnlyRhythmAndIntensity()
    {
        WolfAttackSchedule.Stage[] stages = Stages();
        Assert.AreEqual(5, stages.Length);
        Assert.AreEqual(new[] { 0f, 0f, 0f }, stages[0].IntensityWeights);
        Assert.AreEqual(new[] { 1f, 0f, 0f }, stages[1].IntensityWeights);
        Assert.AreEqual(new[] { 3f, 4f, 0f }, stages[2].IntensityWeights);
        Assert.AreEqual(new[] { 2f, 4f, 3f }, stages[3].IntensityWeights);
        Assert.AreEqual(new[] { 2f, 3f, 4f }, stages[4].IntensityWeights);
    }

    [Test]
    public void ScheduleUsesFiveSecondRhythmAndWiderLongWolfAtOneHundredThirtySheep()
    {
        WolfAttackSchedule schedule = ScriptableObject.CreateInstance<WolfAttackSchedule>();
        try
        {
            Assert.AreEqual(new Vector2(5f, 5f), schedule.GetCalmDurationRange(130));
            Assert.AreEqual(1.65f, schedule.GetLongWolfWidthMultiplier(130));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(schedule);
        }
    }

    [Test]
    public void RuntimeLongWolfWidthMultiplierChangesTheEffectiveWidth()
    {
        GameObject wolfObject = new GameObject("TestLongWolf");
        GameObject visualObject = new GameObject("BodyVisual");
        try
        {
            visualObject.transform.SetParent(wolfObject.transform, false);
            LongWolfSweep sweep = wolfObject.AddComponent<LongWolfSweep>();
            typeof(LongWolfSweep)
                .GetField("bodyVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(sweep, visualObject.transform);
            sweep.SetDirection(Vector2.right);
            float originalWidth = sweep.BodyWidth;
            sweep.SetRuntimeWidthMultiplier(1.5f);

            Assert.AreEqual(originalWidth * 1.5f, sweep.BodyWidth, 0.0001f);
            Assert.AreEqual(sweep.BodyWidth, visualObject.transform.localScale.y, 0.0001f,
                "Changing stage width must immediately refresh the long-wolf visual.");
            Assert.IsTrue(LongWolfSweep.TouchesSweep(
                new Vector2(0f, sweep.BodyWidth * 0.45f),
                0f,
                Vector2.zero,
                Vector2.right,
                Vector2.right,
                2f,
                sweep.BodyWidth));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(wolfObject);
        }
    }

    [Test]
    public void DormantStageNeverAttacks()
    {
        WolfAttackSchedule schedule = ScriptableObject.CreateInstance<WolfAttackSchedule>();
        try
        {
            WolfAttackSelectionState state = new WolfAttackSelectionState();
            state.ObserveStage(schedule, 0);
            Assert.IsNull(state.Pick(schedule, () => 0.5f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(schedule);
        }
    }

    [Test]
    public void OpeningAttackStageUsesOnlyNormalSingleWolves()
    {
        WolfAttackSchedule schedule = ScriptableObject.CreateInstance<WolfAttackSchedule>();
        try
        {
            WolfAttackSchedule.AttackDefinition longWolf = Array.Find(
                schedule.Attacks,
                attack => attack.type == WolfAttackType.LongWolf);
            Assert.IsNotNull(longWolf);
            Assert.AreEqual(2, longWolf.unlockStageIndex,
                "A long body must not appear during the opening single-wolf stage.");

            WolfAttackSelectionState state = new WolfAttackSelectionState();
            state.ObserveStage(schedule, 1);
            float[] rolls = { 0f, 0.25f, 0.5f, 0.75f, 0.999f };
            foreach (float roll in rolls)
                Assert.AreEqual(WolfAttackType.StraightWolf, state.Pick(schedule, () => roll));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(schedule);
        }
    }

    [Test]
    public void SmartWolfStaysConfiguredButCannotBeSelected()
    {
        WolfAttackSchedule schedule = ScriptableObject.CreateInstance<WolfAttackSchedule>();
        try
        {
            WolfAttackSchedule.AttackDefinition smart = Array.Find(
                schedule.Attacks,
                attack => attack.type == WolfAttackType.SmartWolf);
            Assert.IsNotNull(smart);
            Assert.IsFalse(smart.enabled);

            WolfAttackSelectionState state = new WolfAttackSelectionState();
            state.ObserveStage(schedule, 4);
            System.Random random = new System.Random(42);
            for (int index = 0; index < 500; index++)
            {
                WolfAttackType? picked = state.Pick(schedule, () => (float)random.NextDouble());
                Assert.AreNotEqual(WolfAttackType.SmartWolf, picked);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(schedule);
        }
    }

    [Test]
    public void NewlyUnlockedIntensityGetsTheNextAttackWithoutInterruptingTheCurrentOne()
    {
        WolfAttackSchedule schedule = ScriptableObject.CreateInstance<WolfAttackSchedule>();
        try
        {
            WolfAttackSelectionState state = new WolfAttackSelectionState();
            state.ObserveStage(schedule, 1);
            Assert.That(state.Pick(schedule, () => 0f), Is.EqualTo(WolfAttackType.StraightWolf));

            state.ObserveStage(schedule, 2);
            WolfAttackType? debut = state.Pick(schedule, () => 0.99f);
            Assert.That(debut, Is.EqualTo(WolfAttackType.PerpendicularChain));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(schedule);
        }
    }

    [Test]
    public void MajorAttackIsFollowedByBasicRecovery()
    {
        WolfAttackSchedule schedule = ScriptableObject.CreateInstance<WolfAttackSchedule>();
        try
        {
            WolfAttackSelectionState state = new WolfAttackSelectionState();
            state.ObserveStage(schedule, 3);
            WolfAttackType? major = state.Pick(schedule, () => 0.99f);
            Assert.AreEqual(WolfAttackIntensity.Major, WolfAttackTypes.Intensity(major.Value));

            Queue<float> rolls = new Queue<float>(new[] { 0.5f, 0.5f });
            WolfAttackType? recovery = state.Pick(schedule, rolls.Dequeue);
            Assert.AreEqual(WolfAttackIntensity.Basic, WolfAttackTypes.Intensity(recovery.Value));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(schedule);
        }
    }

    [Test]
    public void UnlockedStageNeverDropsAfterFlockLosses()
    {
        WolfAttackSchedule schedule = ScriptableObject.CreateInstance<WolfAttackSchedule>();
        try
        {
            WolfAttackSelectionState state = new WolfAttackSelectionState();
            state.ObserveStage(schedule, 3);
            state.ObserveStage(schedule, 1);
            Assert.AreEqual(3, state.HighestStageIndex);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(schedule);
        }
    }

    [Test]
    public void WeightedIndexSkipsZeroWeightsAndHandlesEdges()
    {
        Assert.AreEqual(-1, WolfAttackPlanner.WeightedIndex(new[] { 0f, 0f }, 0.5f));
        Assert.AreEqual(1, WolfAttackPlanner.WeightedIndex(new[] { 0f, 1f, 0f }, 0.999f));
        Assert.AreEqual(0, WolfAttackPlanner.WeightedIndex(new[] { 1f, 1f }, 0f));
        Assert.AreEqual(1, WolfAttackPlanner.WeightedIndex(new[] { 1f, 1f }, 1f));
    }
}

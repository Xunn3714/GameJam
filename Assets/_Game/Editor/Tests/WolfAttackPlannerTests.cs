using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class WolfAttackPlannerTests
{
    private static WolfAttackSchedule.Stage[] Stages() => WolfAttackSchedule.CreateDefaultStages();

    [Test]
    public void StageIndexFollowsMemberCountThresholds()
    {
        WolfAttackSchedule.Stage[] stages = Stages();
        Assert.AreEqual(0, WolfAttackPlanner.GetStageIndex(stages, 1));
        Assert.AreEqual(0, WolfAttackPlanner.GetStageIndex(stages, 5));
        Assert.AreEqual(1, WolfAttackPlanner.GetStageIndex(stages, 6));
        Assert.AreEqual(1, WolfAttackPlanner.GetStageIndex(stages, 19));
        Assert.AreEqual(2, WolfAttackPlanner.GetStageIndex(stages, 20));
        Assert.AreEqual(3, WolfAttackPlanner.GetStageIndex(stages, 50));
        Assert.AreEqual(3, WolfAttackPlanner.GetStageIndex(stages, 89));
        Assert.AreEqual(4, WolfAttackPlanner.GetStageIndex(stages, 90));
        Assert.AreEqual(4, WolfAttackPlanner.GetStageIndex(stages, 500));
    }

    [Test]
    public void DefaultStagesUseTheRequestedRhythmAndLongWolfGrowth()
    {
        WolfAttackSchedule.Stage[] stages = Stages();
        Assert.AreEqual(8f, stages[1].calmDurationMin);
        Assert.AreEqual(10f, stages[1].calmDurationMax);
        Assert.AreEqual(6f, stages[2].calmDurationMin);
        Assert.AreEqual(10f, stages[2].calmDurationMax);
        Assert.AreEqual(6f, stages[3].calmDurationMin);
        Assert.AreEqual(8f, stages[3].calmDurationMax);
        Assert.Greater(stages[3].longWolfWidthMultiplier, 1f);
        Assert.Greater(stages[4].longWolfWidthMultiplier, stages[3].longWolfWidthMultiplier);
    }

    [Test]
    public void ScheduleUsesFiveSecondRhythmAtOneHundredThirtySheep()
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
        try
        {
            LongWolfSweep sweep = wolfObject.AddComponent<LongWolfSweep>();
            float originalWidth = sweep.BodyWidth;
            sweep.SetRuntimeWidthMultiplier(1.5f);

            Assert.AreEqual(originalWidth * 1.5f, sweep.BodyWidth, 0.0001f);
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
    public void LongWolfWidthCompensatesForCameraZoomAndStillGrowsByStage()
    {
        WolfAttackSchedule schedule = ScriptableObject.CreateInstance<WolfAttackSchedule>();
        try
        {
            float stageTwo = schedule.GetLongWolfWidthMultiplier(20, 10f, 5f);
            float stageThree = schedule.GetLongWolfWidthMultiplier(50, 14f, 5f);
            float stageFour = schedule.GetLongWolfWidthMultiplier(90, 18f, 5f);

            Assert.AreEqual(2f, stageTwo, 0.0001f);
            Assert.AreEqual(3.78f, stageThree, 0.0001f);
            Assert.AreEqual(5.4f, stageFour, 0.0001f);
            Assert.Greater(stageThree / 14f, stageTwo / 10f);
            Assert.Greater(stageFour / 18f, stageThree / 14f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(schedule);
        }
    }

    [Test]
    public void TutorialStageNeverAttacks()
    {
        WolfAttackSchedule.Stage stage = Stages()[0];
        for (int index = 0; index < 20; index++)
        {
            float roll = index / 20f;
            Assert.IsNull(WolfAttackPlanner.Pick(stage, null, () => roll));
        }
    }

    [Test]
    public void StageOneIsAlwaysStraightWolf()
    {
        WolfAttackSchedule.Stage stage = Stages()[1];
        System.Random random = new System.Random(7);
        for (int index = 0; index < 200; index++)
        {
            Assert.AreEqual(WolfAttackType.StraightWolf, WolfAttackPlanner.Pick(stage, null, () => (float)random.NextDouble()));
        }
    }

    [Test]
    public void StageTwoRollsCategoryThenAttack()
    {
        WolfAttackSchedule.Stage stage = Stages()[2];
        // 第一次抽大类：0.5 落在 60% 的"一只狼"里；第二次抽 0.05 → 10% 的直冲狼。
        Queue<float> rolls = new Queue<float>(new[] { 0.5f, 0.05f });
        Assert.AreEqual(WolfAttackType.StraightWolf, WolfAttackPlanner.Pick(stage, null, rolls.Dequeue));

        // 0.2 → 直冲(10%) 之后的聪明狼(45%)。
        rolls = new Queue<float>(new[] { 0.1f, 0.2f });
        Assert.AreEqual(WolfAttackType.SmartWolf, WolfAttackPlanner.Pick(stage, null, rolls.Dequeue));

        // 0.7 落在 40% 的"多只狼"里；第二次 0.0 → 并排轮冲(40%)。
        rolls = new Queue<float>(new[] { 0.7f, 0.0f });
        Assert.AreEqual(WolfAttackType.ParallelSequential, WolfAttackPlanner.Pick(stage, null, rolls.Dequeue));

        // 0.99 → 多只狼里最后一个有权重的：包夹(5%)，五角星在阶段二是 0%。
        rolls = new Queue<float>(new[] { 0.7f, 0.99f });
        Assert.AreEqual(WolfAttackType.LongWolfWithEscorts, WolfAttackPlanner.Pick(stage, null, rolls.Dequeue));
    }

    [Test]
    public void StageFourMostLossUsesRecordedAttackAndFallsBackWithoutIt()
    {
        WolfAttackSchedule.Stage stage = Stages()[4];
        // 大类权重 20 / 65 / 15：0.9 落在"损失最多"里。
        Queue<float> rolls = new Queue<float>(new[] { 0.9f });
        Assert.AreEqual(WolfAttackType.PerpendicularChain,
            WolfAttackPlanner.Pick(stage, WolfAttackType.PerpendicularChain, rolls.Dequeue));

        // 没有损失记录时"损失最多"权重归零，0.9 落在多只狼里（20/65），再抽 0.0 → 并排轮冲。
        rolls = new Queue<float>(new[] { 0.9f, 0.0f });
        Assert.AreEqual(WolfAttackType.ParallelSequential, WolfAttackPlanner.Pick(stage, null, rolls.Dequeue));
    }

    [Test]
    public void DistributionRoughlyMatchesWeights()
    {
        WolfAttackSchedule.Stage stage = Stages()[3];
        System.Random random = new System.Random(42);
        int single = 0, pack = 0;
        const int samples = 20000;
        for (int index = 0; index < samples; index++)
        {
            WolfAttackType? type = WolfAttackPlanner.Pick(stage, null, () => (float)random.NextDouble());
            Assert.IsTrue(type.HasValue);
            if (WolfAttackTypes.IsPack(type.Value)) pack++; else single++;
        }
        // 阶段三：一只狼 20% / 多只狼 80%。
        Assert.That(single / (float)samples, Is.EqualTo(0.2f).Within(0.02f));
        Assert.That(pack / (float)samples, Is.EqualTo(0.8f).Within(0.02f));
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

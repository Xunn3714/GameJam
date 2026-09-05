using System;
using System.Collections.Generic;

/// <summary>
/// 狼群的“记性”（纯 C#，可测）：记录最近若干次进攻时玩家的躲避角度——
/// 羊群中心相对于狼预警直线的带符号夹角（度，正 = 直线左侧 / 逆时针）。
/// 聪明狼在预警最后一瞬用平均角度做预判。
/// </summary>
public sealed class WolfDodgeMemory
{
    private readonly Queue<float> samples = new Queue<float>();

    public WolfDodgeMemory(int capacity = 20)
    {
        Capacity = Math.Max(1, capacity);
    }

    public int Capacity { get; }
    public int Count => samples.Count;

    /// <summary>最近 Capacity 次躲避角度的平均值（度）；没有样本时为 0。</summary>
    public float AverageDegrees
    {
        get
        {
            if (samples.Count == 0)
                return 0f;

            float sum = 0f;
            foreach (float sample in samples)
                sum += sample;
            return sum / samples.Count;
        }
    }

    /// <summary>最近一次的躲避角度；没有样本时为 0。</summary>
    public float LatestDegrees { get; private set; }

    public IEnumerable<float> Samples => samples;

    public bool HasEnoughSamples(int minimum) => samples.Count >= Math.Max(1, minimum);

    public void Record(float signedDegrees)
    {
        if (float.IsNaN(signedDegrees) || float.IsInfinity(signedDegrees))
            return;

        LatestDegrees = signedDegrees;
        samples.Enqueue(signedDegrees);
        while (samples.Count > Capacity)
            samples.Dequeue();
    }

    public void Clear()
    {
        samples.Clear();
        LatestDegrees = 0f;
    }
}

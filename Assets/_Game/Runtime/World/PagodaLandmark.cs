using System;
using UnityEngine;

/// <summary>
/// 洪山宝通寺：全图唯一的一座，需要 150 只羊用 E 整群冲刺才撞得动。
/// 数量不够时撞一下会解锁「寻找？？」任务；撞碎了就走真结局。
/// 判定完全复用围栏那一套（FenceObstacle + BreakableObstacle），这里只负责把两个时机抛出去。
/// </summary>
[DisallowMultipleComponent]
public sealed class PagodaLandmark : MonoBehaviour
{
    public static PagodaLandmark Current { get; private set; }

    [SerializeField] private BreakableObstacle breakable;
    [SerializeField] private FenceObstacle fence;

    /// <summary>羊不够却来撞了一下。</summary>
    public event Action<PagodaLandmark> AttemptRejected;
    /// <summary>撞碎了，该走真结局了。</summary>
    public event Action<PagodaLandmark> Smashed;

    public bool HasBeenAttempted { get; private set; }
    public bool IsSmashed => breakable != null && breakable.IsBroken;
    public int RequiredFlockCount => fence != null ? fence.RequiredFlockCount : 150;

    public void Configure(BreakableObstacle obstacle, FenceObstacle fenceObstacle)
    {
        breakable = obstacle;
        fence = fenceObstacle;
    }

    private void Awake()
    {
        if (breakable == null) breakable = GetComponent<BreakableObstacle>();
        if (fence == null) fence = GetComponent<FenceObstacle>();
        // 只能被 E 整群冲刺撞碎：羊够了走过去蹭一下不算。
        fence?.SetBreakOnContact(false);
    }

    private void OnEnable()
    {
        Current = this;
        if (fence != null) fence.DashRejected += HandleDashRejected;
        if (breakable != null) breakable.Broken += HandleBroken;
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
        if (fence != null) fence.DashRejected -= HandleDashRejected;
        if (breakable != null) breakable.Broken -= HandleBroken;
    }

    private void HandleDashRejected(FenceObstacle source)
    {
        HasBeenAttempted = true;
        AttemptRejected?.Invoke(this);
    }

    private void HandleBroken(BreakableObstacle source)
    {
        Smashed?.Invoke(this);
    }
}

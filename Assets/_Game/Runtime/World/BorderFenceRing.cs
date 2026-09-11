using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图最外圈的围栏。门槛由关卡控制器统一下发，冲刺时按当前羊数判定；任意一段被撞开后羊群即可冲出地图。
/// </summary>
[DisallowMultipleComponent]
public sealed class BorderFenceRing : MonoBehaviour
{
    [SerializeField] private FenceObstacle[] fences = Array.Empty<FenceObstacle>();
    [SerializeField] private Rect worldRect = new Rect(-120f, -70f, 240f, 140f);

    public IReadOnlyList<FenceObstacle> Fences => fences;
    public Rect WorldRect => worldRect;
    public int BrokenCount { get; private set; }
    public bool AnyBroken => BrokenCount > 0;

    public event Action<FenceObstacle> FenceBroken;

    public void Configure(Rect rect, FenceObstacle[] ringFences)
    {
        worldRect = rect;
        fences = ringFences ?? Array.Empty<FenceObstacle>();
    }

    private bool hasBreakableSpan;
    private Rect breakableSpan;

    /// <summary>只有落在这个矩形里的围栏段才接受门槛，其余段锁死（出口区块用）。要在 ApplyRequiredCount 之前调用。</summary>
    public void SetBreakableSpan(Rect span)
    {
        hasBreakableSpan = true;
        breakableSpan = span;
    }

    /// <summary>把冲出地图的门槛下发到每一段围栏；出口范围之外的段用 int.MaxValue 锁死。</summary>
    public void ApplyRequiredCount(int count)
    {
        foreach (FenceObstacle fence in fences)
        {
            if (fence == null)
                continue;

            bool breakable = !hasBreakableSpan || breakableSpan.Contains((Vector2)fence.transform.position);
            fence.SetRequiredCountOverride(breakable ? count : int.MaxValue);
        }
    }

    private void OnEnable()
    {
        foreach (FenceObstacle fence in fences)
        {
            if (fence != null && fence.Breakable != null)
                fence.Breakable.Broken += HandleBroken;
        }
    }

    private void OnDisable()
    {
        foreach (FenceObstacle fence in fences)
        {
            if (fence != null && fence.Breakable != null)
                fence.Breakable.Broken -= HandleBroken;
        }
    }

    private void HandleBroken(BreakableObstacle obstacle)
    {
        BrokenCount++;
        FenceObstacle fence = obstacle != null ? obstacle.GetComponent<FenceObstacle>() : null;
        FenceBroken?.Invoke(fence);
    }
}

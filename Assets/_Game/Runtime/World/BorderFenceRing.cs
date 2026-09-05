using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图最外圈的围栏。门槛由关卡控制器统一下发（按本局历史最高羊数），任意一段被撞开后羊群即可冲出地图。
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

    /// <summary>把冲出地图的门槛下发到每一段围栏。</summary>
    public void ApplyRequiredCount(int count)
    {
        foreach (FenceObstacle fence in fences)
        {
            if (fence != null)
                fence.SetRequiredCountOverride(count);
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

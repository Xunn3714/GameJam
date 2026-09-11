using System.Collections.Generic;
using UnityEngine;

/// 羊群中心点和每只成员在 MovePosition 之前调用，检查目标位置是否被 Blocking 层挡住。
/// 被挡时尝试只走 X 或只走 Y（贴墙滑动），都不行才原地不动。
public static class MovementBlocking
{
    public const string BlockingLayerName = "Blocking";

    private static readonly List<Collider2D> overlapResults = new List<Collider2D>(8);
    private static readonly RaycastHit2D[] lineHits = new RaycastHit2D[1];
    private static readonly RaycastHit2D[] sweepHits = new RaycastHit2D[8];

    public static LayerMask DefaultMask()
    {
        int layer = LayerMask.NameToLayer(BlockingLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"Layer \"{BlockingLayerName}\" 不存在，请在 Tags and Layers 中创建；当前不会阻挡移动。");
            return 0;
        }

        return 1 << layer;
    }

    public static Vector2 ResolveMove(Vector2 from, Vector2 to, float radius, LayerMask blockingMask)
    {
        return ResolveMove(from, to, radius, blockingMask, out _);
    }

    public static Vector2 ResolveMove(
        Vector2 from,
        Vector2 to,
        float radius,
        LayerMask blockingMask,
        out MovementBlockResult result)
    {
        result = default;
        if (blockingMask.value == 0 || from == to)
            return to;

        if (TryFindBlocker(to, radius, blockingMask, out Collider2D directBlocker) == false)
            return to;

        result = new MovementBlockResult(true, false, directBlocker);

        Vector2 xOnly = new Vector2(to.x, from.y);
        if (xOnly != from && TryFindBlocker(xOnly, radius, blockingMask, out _) == false)
            return xOnly;

        Vector2 yOnly = new Vector2(from.x, to.y);
        if (yOnly != from && TryFindBlocker(yOnly, radius, blockingMask, out _) == false)
            return yOnly;

        result = new MovementBlockResult(true, true, directBlocker);
        return from;
    }

    /// <summary>
    /// 冲刺使用的连续碰撞检查。沿整段位移做圆形扫掠，不尝试贴墙滑动，
    /// 避免高速移动越过较薄的围栏。
    /// </summary>
    public static Vector2 ResolveDashMove(
        Vector2 from,
        Vector2 to,
        float radius,
        LayerMask blockingMask,
        out MovementBlockResult result)
    {
        result = default;
        Vector2 displacement = to - from;
        float distance = displacement.magnitude;
        if (blockingMask.value == 0 || distance <= 0.000001f)
            return to;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = blockingMask,
            useTriggers = false,
        };

        int hitCount = Physics2D.CircleCast(
            from,
            Mathf.Max(0f, radius),
            displacement / distance,
            filter,
            sweepHits,
            distance);
        if (hitCount <= 0)
            return to;

        RaycastHit2D nearest = default;
        float nearestDistance = float.MaxValue;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = sweepHits[index];
            if (hit.collider == null || hit.distance >= nearestDistance)
                continue;

            nearest = hit;
            nearestDistance = hit.distance;
        }

        if (nearest.collider == null)
            return to;

        result = new MovementBlockResult(true, true, nearest.collider);
        float safeDistance = Mathf.Max(0f, nearestDistance - 0.01f);
        return from + displacement / distance * safeDistance;
    }

    /// <summary>两点之间是否隔着实体阻挡（围栏）；只看非 Trigger 碰撞体。</summary>
    public static bool IsLineBlocked(Vector2 from, Vector2 to, LayerMask blockingMask)
    {
        if (blockingMask.value == 0)
            return false;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = blockingMask,
            useTriggers = false,
        };

        return Physics2D.Linecast(from, to, filter, lineHits) > 0;
    }

    public static bool IsFree(Vector2 position, float radius, LayerMask blockingMask)
    {
        return TryFindBlocker(position, radius, blockingMask, out _) == false;
    }

    /// <summary>返回当前位置与指定阻挡层重叠的第一个实体碰撞体。</summary>
    internal static bool TryGetBlocker(
        Vector2 position,
        float radius,
        LayerMask blockingMask,
        out Collider2D blocker)
    {
        return TryFindBlocker(position, radius, blockingMask, out blocker);
    }

    private static bool TryFindBlocker(
        Vector2 position,
        float radius,
        LayerMask blockingMask,
        out Collider2D blocker)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = blockingMask,
            useTriggers = false, // 只看实体碰撞体，围栏自己的交互 Trigger 不算
        };

        overlapResults.Clear();
        int count = Physics2D.OverlapCircle(position, radius, filter, overlapResults);
        blocker = count > 0 ? overlapResults[0] : null;
        return count > 0;
    }
}

public readonly struct MovementBlockResult
{
    public MovementBlockResult(bool wasBlocked, bool fullyBlocked, Collider2D blocker)
    {
        WasBlocked = wasBlocked;
        FullyBlocked = fullyBlocked;
        Blocker = blocker;
    }

    public bool WasBlocked { get; }
    public bool FullyBlocked { get; }
    public Collider2D Blocker { get; }
}

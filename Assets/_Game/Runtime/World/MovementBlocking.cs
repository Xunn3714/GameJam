using System.Collections.Generic;
using UnityEngine;

/// 羊群中心点和每只成员在 MovePosition 之前调用，检查目标位置是否被 Blocking 层挡住。
/// 被挡时尝试只走 X 或只走 Y（贴墙滑动），都不行才原地不动。
public static class MovementBlocking
{
    public const string BlockingLayerName = "Blocking";

    private static readonly List<Collider2D> overlapResults = new List<Collider2D>(8);

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

    public static bool IsFree(Vector2 position, float radius, LayerMask blockingMask)
    {
        return TryFindBlocker(position, radius, blockingMask, out _) == false;
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

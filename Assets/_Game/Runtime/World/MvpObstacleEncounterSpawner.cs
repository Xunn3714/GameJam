using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 根据本局已经生成的 2～3 只羊群组布置规则化障碍，让障碍天然指向可见奖励。
/// </summary>
public static class MvpObstacleEncounterSpawner
{
    private const float ClusterLinkDistance = 2.35f;
    private const float BoundsMargin = 4.25f;
    private const float FenceHalfWidth = 2.65f;
    private const float FenceDepth = 1.85f;
    private const float FenceSpacing = 1.65f;
    private const float BarrelSpacing = 0.92f;

    public static int Generate(
        Rect worldBounds,
        IReadOnlyList<RecruitableSheep> sheep,
        int maximumEncounterCount,
        Sprite fenceSprite,
        ObstacleDefinition fenceDefinition,
        Sprite barrelSprite,
        ObstacleDefinition barrelDefinition,
        Sprite flowerSprite)
    {
        if (sheep == null || sheep.Count == 0 || maximumEncounterCount <= 0)
            return 0;
        if (fenceSprite == null && barrelSprite == null)
        {
            Debug.LogWarning("障碍遭遇已启用，但没有配置栅栏或木桶 Sprite。");
            return 0;
        }

        List<List<RecruitableSheep>> rewardClusters = FindRewardClusters(sheep, worldBounds);
        if (rewardClusters.Count == 0)
            return 0;

        Transform root = new GameObject("RuntimeObstacleEncounters").transform;
        int encounterCount = Mathf.Min(maximumEncounterCount, rewardClusters.Count);
        int generated = 0;

        for (int index = 0; index < encounterCount; index++)
        {
            List<RecruitableSheep> rewardSheep = rewardClusters[index];
            Vector2 center = AveragePosition(rewardSheep);
            Vector2 openDirection = worldBounds.center - center;
            if (openDirection.sqrMagnitude < 0.01f)
                openDirection = index % 2 == 0 ? Vector2.right : Vector2.down;
            openDirection.Normalize();
            Vector2 sideDirection = new(-openDirection.y, openDirection.x);

            GameObject encounter = new($"Encounter_{index + 1:00}");
            encounter.transform.SetParent(root, false);
            encounter.transform.position = center;

            bool useFence = index % 2 == 0 ? fenceSprite != null : barrelSprite == null;
            if (useFence)
            {
                encounter.name += "_FenceCorral";
                CreateFenceCorral(
                    encounter.transform,
                    center,
                    openDirection,
                    sideDirection,
                    fenceSprite,
                    fenceDefinition);
            }
            else
            {
                encounter.name += "_BarrelGate";
                CreateBarrelGate(
                    encounter.transform,
                    center,
                    openDirection,
                    sideDirection,
                    barrelSprite,
                    barrelDefinition);
            }

            CreateFlowerPatch(
                encounter.transform,
                center + openDirection * 2.6f,
                openDirection,
                sideDirection,
                flowerSprite,
                index);

            for (int sheepIndex = 0; sheepIndex < rewardSheep.Count; sheepIndex++)
                rewardSheep[sheepIndex]?.ConfigureWanderBounds(worldBounds, 1.2f);

            generated++;
        }

        Debug.Log($"生成 {generated} 组障碍奖励遭遇，障碍后共有 {CountRewards(rewardClusters, generated)} 只羊。");
        return generated;
    }

    private static List<List<RecruitableSheep>> FindRewardClusters(
        IReadOnlyList<RecruitableSheep> sheep,
        Rect bounds)
    {
        List<List<RecruitableSheep>> clusters = new();
        bool[] used = new bool[sheep.Count];
        float linkDistanceSquared = ClusterLinkDistance * ClusterLinkDistance;

        for (int seedIndex = 0; seedIndex < sheep.Count; seedIndex++)
        {
            RecruitableSheep seed = sheep[seedIndex];
            if (used[seedIndex] || seed == null || !IsInsideMargin(seed.transform.position, bounds))
                continue;

            List<(float distance, int index)> neighbors = new();
            Vector2 seedPosition = seed.transform.position;
            for (int candidateIndex = seedIndex + 1; candidateIndex < sheep.Count; candidateIndex++)
            {
                RecruitableSheep candidate = sheep[candidateIndex];
                if (used[candidateIndex] || candidate == null)
                    continue;

                float squaredDistance = ((Vector2)candidate.transform.position - seedPosition).sqrMagnitude;
                if (squaredDistance <= linkDistanceSquared)
                    neighbors.Add((squaredDistance, candidateIndex));
            }

            if (neighbors.Count == 0)
                continue;

            neighbors.Sort((left, right) => left.distance.CompareTo(right.distance));
            List<RecruitableSheep> cluster = new() { seed };
            used[seedIndex] = true;
            int appended = Mathf.Min(2, neighbors.Count);
            for (int neighborIndex = 0; neighborIndex < appended; neighborIndex++)
            {
                int sheepIndex = neighbors[neighborIndex].index;
                used[sheepIndex] = true;
                cluster.Add(sheep[sheepIndex]);
            }

            if (IsInsideMargin(AveragePosition(cluster), bounds))
                clusters.Add(cluster);
        }

        return clusters;
    }

    private static bool IsInsideMargin(Vector2 position, Rect bounds)
    {
        return position.x >= bounds.xMin + BoundsMargin
            && position.x <= bounds.xMax - BoundsMargin
            && position.y >= bounds.yMin + BoundsMargin
            && position.y <= bounds.yMax - BoundsMargin;
    }

    private static void CreateFenceCorral(
        Transform parent,
        Vector2 center,
        Vector2 forward,
        Vector2 side,
        Sprite sprite,
        ObstacleDefinition definition)
    {
        Vector2 backCenter = center - forward * FenceDepth;
        for (int index = -1; index <= 1; index++)
        {
            Vector2 position = backCenter + side * (index * FenceSpacing);
            CreateFenceSegment(parent, position, side, sprite, definition);
        }

        for (int sideSign = -1; sideSign <= 1; sideSign += 2)
        {
            for (int depthIndex = 0; depthIndex < 3; depthIndex++)
            {
                float depth = -FenceDepth + depthIndex * FenceSpacing;
                Vector2 position = center + side * (sideSign * FenceHalfWidth) + forward * depth;
                CreateFenceSegment(parent, position, forward, sprite, definition);
            }
        }
    }

    private static void CreateBarrelGate(
        Transform parent,
        Vector2 center,
        Vector2 forward,
        Vector2 side,
        Sprite sprite,
        ObstacleDefinition definition)
    {
        Vector2 gateCenter = center + forward * 2.25f;
        for (int index = -2; index <= 2; index++)
        {
            Vector2 position = gateCenter + side * (index * BarrelSpacing);
            CreateBarrel(parent, position, index * 7f, sprite, definition);
        }
    }

    private static void CreateFenceSegment(
        Transform parent,
        Vector2 position,
        Vector2 longAxis,
        Sprite sprite,
        ObstacleDefinition definition)
    {
        GameObject obstacle = new("Fence");
        obstacle.SetActive(false);
        obstacle.layer = ResolveBlockingLayer();
        obstacle.transform.SetParent(parent, true);
        obstacle.transform.position = position;
        obstacle.transform.rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(longAxis.y, longAxis.x) * Mathf.Rad2Deg);

        SpriteRenderer renderer = obstacle.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 2;

        BoxCollider2D solid = obstacle.AddComponent<BoxCollider2D>();
        Vector2 spriteSize = sprite != null ? sprite.bounds.size : new Vector2(2f, 1f);
        solid.size = new Vector2(Mathf.Max(0.7f, spriteSize.x * 0.86f), Mathf.Max(0.22f, spriteSize.y * 0.42f));
        solid.offset = new Vector2(0f, -spriteSize.y * 0.13f);
        solid.isTrigger = false;

        GameObject range = new("InteractRange");
        range.layer = obstacle.layer;
        range.transform.SetParent(obstacle.transform, false);
        BoxCollider2D trigger = range.AddComponent<BoxCollider2D>();
        trigger.size = new Vector2(solid.size.x + 0.75f, Mathf.Max(1.25f, solid.size.y + 1f));
        trigger.offset = solid.offset;
        trigger.isTrigger = true;

        Rigidbody2D body = obstacle.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        BreakableObstacle breakable = obstacle.AddComponent<BreakableObstacle>();
        breakable.Configure(definition, renderer);
        obstacle.AddComponent<FenceObstacle>();
        obstacle.SetActive(true);
    }

    private static void CreateBarrel(
        Transform parent,
        Vector2 position,
        float rotation,
        Sprite sprite,
        ObstacleDefinition definition)
    {
        GameObject obstacle = new("Barrel");
        obstacle.SetActive(false);
        obstacle.transform.SetParent(parent, true);
        obstacle.transform.position = position;
        obstacle.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        obstacle.transform.localScale = Vector3.one * 0.9f;

        SpriteRenderer renderer = obstacle.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 2;

        CircleCollider2D trigger = obstacle.AddComponent<CircleCollider2D>();
        trigger.radius = 0.42f;
        trigger.isTrigger = true;
        Rigidbody2D body = obstacle.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        BreakableObstacle breakable = obstacle.AddComponent<BreakableObstacle>();
        breakable.Configure(definition, renderer);
        obstacle.SetActive(true);
    }

    private static void CreateFlowerPatch(
        Transform parent,
        Vector2 center,
        Vector2 forward,
        Vector2 side,
        Sprite sprite,
        int patternIndex)
    {
        if (sprite == null)
            return;

        for (int index = 0; index < 5; index++)
        {
            float sideOffset = (index - 2) * 0.62f;
            float forwardOffset = (index % 2 == 0 ? -0.22f : 0.28f);
            Vector2 position = center + side * sideOffset + forward * forwardOffset;

            GameObject flower = new($"Flower_{index + 1:00}");
            flower.SetActive(false);
            flower.transform.SetParent(parent, true);
            flower.transform.position = position;
            flower.transform.rotation = Quaternion.Euler(0f, 0f, patternIndex * 17f + index * 31f);
            flower.transform.localScale = Vector3.one * (0.62f + (index % 3) * 0.09f);

            SpriteRenderer renderer = flower.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 1;
            CircleCollider2D trigger = flower.AddComponent<CircleCollider2D>();
            trigger.radius = 0.36f;
            trigger.isTrigger = true;
            Rigidbody2D body = flower.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            VegetationTrampleEffect effect = flower.AddComponent<VegetationTrampleEffect>();
            effect.Configure(renderer);
            flower.SetActive(true);
        }
    }

    private static Vector2 AveragePosition(IReadOnlyList<RecruitableSheep> sheep)
    {
        Vector2 total = Vector2.zero;
        int count = 0;
        for (int index = 0; index < sheep.Count; index++)
        {
            if (sheep[index] == null)
                continue;
            total += (Vector2)sheep[index].transform.position;
            count++;
        }
        return count > 0 ? total / count : Vector2.zero;
    }

    private static int CountRewards(IReadOnlyList<List<RecruitableSheep>> clusters, int count)
    {
        int total = 0;
        for (int index = 0; index < Mathf.Min(count, clusters.Count); index++)
            total += clusters[index].Count;
        return total;
    }

    private static int ResolveBlockingLayer()
    {
        int layer = LayerMask.NameToLayer(MovementBlocking.BlockingLayerName);
        return layer >= 0 ? layer : 0;
    }
}

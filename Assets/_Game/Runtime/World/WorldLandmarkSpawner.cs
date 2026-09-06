using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单局固定地标：10 个红箱子、房屋，以及 2×2/2×3 成片农田。它们只在开局生成一次（Spawn 有一次性保护），
/// 不参与普通散布物的补充；被摧毁后换成"坏"图沉到 Background 层，残骸永久留在原地。
/// 箱子和房屋使用整张 Sprite（Single 模式），所以完好图和坏图共用同一张画布、天然对齐。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(30)]
public sealed class WorldLandmarkSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldSeed worldSeed;
    [SerializeField] private ProgressiveSheepSpawner sheepSpawner;
    [SerializeField] private GameObject riceFieldPrefab;
    [SerializeField] private GameObject haystackPrefab;
    [SerializeField] private GameObject barrelPrefab;
    [SerializeField] private GameObject fencePrefab;
    [SerializeField] private ObstacleDefinition penFenceDefinition;
    [SerializeField] private ObstacleDefinition redChestDefinition;
    [SerializeField] private ObstacleDefinition houseDefinition;
    [SerializeField] private Sprite redChestSprite;
    [SerializeField] private Sprite houseSprite;

    [Header("Area")]
    [SerializeField] private Rect area = new(-120f, -70f, 240f, 140f);
    [SerializeField, Min(0f)] private float borderPadding = 7f;
    [SerializeField] private Rect[] exclusionZones = Array.Empty<Rect>();

    [Header("Counts")]
    [SerializeField, Min(1)] private int redChestCount = 10;
    [SerializeField, Min(1)] private int minimumHouseCount = 3;
    [SerializeField, Min(1)] private int maximumHouseCount = 4;
    [SerializeField, Min(0)] private int farmClusterCount = 10;

    [Header("Red Chest")]
    [Tooltip("整张贴图（含透明边）缩放到世界单位的倍率；完好图和坏图共用，破坏后残骸自然落在原地。")]
    [SerializeField, Min(0.001f)] private float redChestScale = 0.2f;
    [Tooltip("箱子的实体碰撞盒（世界单位），只包住画面上看得见的箱子。")]
    [SerializeField] private Vector2 redChestColliderSize = new(2.2f, 2.2f);
    [SerializeField] private Vector2 redChestColliderOffset = new(0.06f, 0.36f);
    [Tooltip("摆放红箱子时与其他地标 / 已有碰撞体的最小间距。")]
    [SerializeField, Min(0.5f)] private float redChestClearance = 6f;
    [Tooltip("每个红箱子摔碎后掉落的紫色及以上品质羊的最少数量。")]
    [SerializeField, Min(1)] private int rewardSheepMinimum = 3;
    [Tooltip("每个红箱子摔碎后掉落的紫色及以上品质羊的最多数量。")]
    [SerializeField, Min(1)] private int rewardSheepMaximum = 5;

    [Header("House")]
    [SerializeField, Min(0.001f)] private float houseScale = 1.25f;
    [SerializeField] private Vector2 houseColliderSize = new(2.8f, 3f);
    [SerializeField] private Vector2 houseColliderOffset = new(-0.02f, 0.13f);

    private readonly List<Vector2> occupied = new();
    private Transform root;
    private bool hasSpawned;

    private void Start() => Spawn();

    /// <summary>整局只生成一次：地标被摧毁后不再补刷，残骸永久保留。</summary>
    public void Spawn()
    {
        if (hasSpawned)
            return;

        Clear();
        if (worldSeed == null || redChestDefinition == null || houseDefinition == null || fencePrefab == null)
        {
            Debug.LogWarning("地标刷新器缺少必要引用。", this);
            return;
        }

        hasSpawned = true;
        System.Random random = worldSeed.CreateRandom(3);
        root = new GameObject("WorldLandmarks").transform;
        root.SetParent(transform, false);

        List<GameObject> createdChests = new();
        for (int index = 0; index < redChestCount; index++)
        {
            if (TryFindPosition(random, redChestClearance, out Vector2 position))
                createdChests.Add(CreateRedChest(position));
        }
        if (createdChests.Count < redChestCount)
        {
            Debug.LogWarning($"红箱子只放下了 {createdChests.Count}/{redChestCount} 个，地图可能太挤或排除区太大。", this);
        }
        // 每个红箱子都装着奖励：摔碎后掉 3~5 只紫色及以上品质的羊。
        foreach (GameObject chest in createdChests)
        {
            LandmarkChestReward reward = chest.AddComponent<LandmarkChestReward>();
            reward.Configure(sheepSpawner, rewardSheepMinimum, rewardSheepMaximum);
        }

        int houseCount = random.Next(
            Mathf.Max(1, minimumHouseCount),
            Mathf.Max(minimumHouseCount, maximumHouseCount) + 1);
        for (int index = 0; index < houseCount; index++)
        {
            if (TryFindPosition(random, 9f, out Vector2 position))
                CreateHouseCompound(position, random);
        }

        for (int index = 0; index < farmClusterCount; index++)
        {
            if (TryFindPosition(random, 5f, out Vector2 position))
                CreateFarmCluster(position, random);
        }
    }

    private GameObject CreateRedChest(Vector2 position)
    {
        return CreateBlockedObject(
            "RedChest", position, redChestScale, redChestColliderSize, redChestColliderOffset,
            redChestDefinition, redChestSprite);
    }

    private void CreateHouseCompound(Vector2 position, System.Random random)
    {
        CreateBlockedObject(
            "House", position, houseScale, houseColliderSize, houseColliderOffset,
            houseDefinition, houseSprite);

        // 随机选一个角，围一段 90° 羊圈栅栏；不注册 TutorialPen，因此没有教程提示。
        int corner = random.Next(4);
        Vector2 horizontal = corner < 2 ? Vector2.up : Vector2.down;
        Vector2 vertical = corner % 2 == 0 ? Vector2.left : Vector2.right;
        Vector2 cornerPosition = position + horizontal * 3.4f + vertical * 3.2f;
        List<Vector2> fencePositions = new();
        for (int index = 0; index < 3; index++)
        {
            Vector2 horizontalPosition = cornerPosition - vertical * index * 2f;
            Vector2 verticalPosition = cornerPosition - horizontal * index * 2f;
            CreateFence(horizontalPosition, 0f, "HousePen_H");
            CreateFence(verticalPosition, 90f, "HousePen_V");
            fencePositions.Add(horizontalPosition);
            fencePositions.Add(verticalPosition);
        }

        // 家具从房屋四周的离散位置中抽取，并避开本栋围栏和已放置家具。
        // 这样仍保留随机感，但不会因随机角度相近而叠在一起。
        List<Vector2> furnitureSlots = CreateFurnitureSlots(position);
        RemoveSlotsNear(furnitureSlots, fencePositions, 1.65f);
        PlaceFurniture(haystackPrefab, "HouseHaystack", random.Next(1, 3), 2.25f, furnitureSlots, random);
        PlaceFurniture(barrelPrefab, "HouseBarrel", random.Next(0, 4), 1.8f, furnitureSlots, random);
    }

    private static List<Vector2> CreateFurnitureSlots(Vector2 center) => new()
    {
        center + new Vector2(-3.8f, -2.7f),
        center + new Vector2(0f, -3.1f),
        center + new Vector2(3.8f, -2.7f),
        center + new Vector2(-3.6f, 0f),
        center + new Vector2(3.6f, 0f),
        center + new Vector2(-3.8f, 2.7f),
        center + new Vector2(0f, 3.1f),
        center + new Vector2(3.8f, 2.7f)
    };

    private void PlaceFurniture(
        GameObject prefab,
        string objectName,
        int count,
        float minimumSpacing,
        List<Vector2> availableSlots,
        System.Random random)
    {
        for (int index = 0; index < count && availableSlots.Count > 0; index++)
        {
            int slotIndex = random.Next(availableSlots.Count);
            Vector2 selected = availableSlots[slotIndex];
            CreatePrefab(prefab, selected, objectName);
            RemoveSlotsNear(availableSlots, new[] { selected }, minimumSpacing);
        }
    }

    private static void RemoveSlotsNear(List<Vector2> slots, IEnumerable<Vector2> blockedPositions, float minimumSpacing)
    {
        float minimumSqrDistance = minimumSpacing * minimumSpacing;
        slots.RemoveAll(slot =>
        {
            foreach (Vector2 blocked in blockedPositions)
                if ((slot - blocked).sqrMagnitude < minimumSqrDistance)
                    return true;
            return false;
        });
    }

    private void CreateFarmCluster(Vector2 position, System.Random random)
    {
        int columns = 2;
        int rows = random.Next(2) == 0 ? 2 : 3;
        CreateFarmGrid(position, columns, rows);
    }

    private void CreateFarmGrid(Vector2 center, int columns, int rows)
    {
        const float spacing = 1.45f;
        for (int x = 0; x < columns; x++)
        for (int y = 0; y < rows; y++)
        {
            Vector2 offset = new((x - (columns - 1) * 0.5f) * spacing, (y - (rows - 1) * 0.5f) * spacing);
            CreatePrefab(riceFieldPrefab, center + offset, "RiceField");
        }
    }

    private void CreateFence(Vector2 position, float rotation, string name)
    {
        GameObject fence = CreatePrefab(fencePrefab, position, name);
        if (fence == null)
            return;
        fence.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        BreakableObstacle breakable = fence.GetComponent<BreakableObstacle>();
        if (breakable != null)
            breakable.Configure(penFenceDefinition, fence.GetComponent<SpriteRenderer>());
    }

    private GameObject CreatePrefab(GameObject prefab, Vector2 position, string name)
    {
        if (prefab == null)
            return null;
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, root);
        instance.name = name;
        return instance;
    }

    /// <summary>
    /// 生成一个挡路的地标。缩放对两个轴一致（不拉伸美术），碰撞盒单独给，
    /// 这样换成"坏"图时不需要改 transform，残骸就落在美术画好的位置上。
    /// </summary>
    private GameObject CreateBlockedObject(
        string name,
        Vector2 position,
        float scale,
        Vector2 colliderSize,
        Vector2 colliderOffset,
        ObstacleDefinition definition,
        Sprite sprite)
    {
        GameObject result = new(name);
        result.transform.SetParent(root, false);
        result.transform.position = position;
        result.transform.localScale = new Vector3(scale, scale, 1f);
        result.layer = LayerMask.NameToLayer(MovementBlocking.BlockingLayerName);
        SpriteRenderer renderer = result.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 2;

        Rigidbody2D body = result.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;

        // 碰撞盒按世界单位给，除以缩放换算回本地单位。
        float inverseScale = 1f / Mathf.Max(0.001f, scale);
        BoxCollider2D solid = result.AddComponent<BoxCollider2D>();
        solid.size = colliderSize * inverseScale;
        solid.offset = colliderOffset * inverseScale;
        BoxCollider2D trigger = result.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = (colliderSize + Vector2.one * 0.45f) * inverseScale;
        trigger.offset = solid.offset;

        BreakableObstacle breakable = result.AddComponent<BreakableObstacle>();
        breakable.Configure(definition, renderer);
        return result;
    }

    private bool TryFindPosition(System.Random random, float clearance, out Vector2 position)
    {
        Rect inner = new(area.xMin + borderPadding, area.yMin + borderPadding, area.width - borderPadding * 2f, area.height - borderPadding * 2f);
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Vector2 candidate = new(inner.xMin + (float)random.NextDouble() * inner.width, inner.yMin + (float)random.NextDouble() * inner.height);
            if (IsExcluded(candidate) || Physics2D.OverlapCircle(candidate, clearance) != null)
                continue;
            bool tooClose = false;
            foreach (Vector2 existing in occupied)
                if ((candidate - existing).sqrMagnitude < clearance * clearance) { tooClose = true; break; }
            if (tooClose)
                continue;
            occupied.Add(candidate);
            position = candidate;
            return true;
        }
        position = default;
        return false;
    }

    private bool IsExcluded(Vector2 position)
    {
        foreach (Rect zone in exclusionZones)
            if (zone.Contains(position)) return true;
        return false;
    }

    private void Clear()
    {
        occupied.Clear();
        if (root != null)
            Destroy(root.gameObject);
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BreakableObstacle))]
public sealed class LandmarkChestReward : MonoBehaviour
{
    private ProgressiveSheepSpawner sheepSpawner;
    private BreakableObstacle obstacle;
    private bool rewarded;
    private int minimumCount = 3;
    private int maximumCount = 5;

    public void Configure(ProgressiveSheepSpawner spawner, int minimum, int maximum)
    {
        sheepSpawner = spawner;
        minimumCount = Mathf.Max(1, minimum);
        maximumCount = Mathf.Max(minimumCount, maximum);
    }

    private void Awake()
    {
        obstacle = GetComponent<BreakableObstacle>();
        obstacle.Broken += HandleBroken;
    }

    private void OnDestroy()
    {
        if (obstacle != null) obstacle.Broken -= HandleBroken;
    }

    private void HandleBroken(BreakableObstacle broken)
    {
        if (rewarded || broken != obstacle)
            return;
        rewarded = true;
        // 3~5 只同一种紫色及以上品质的羊，成簇落在箱子旁边。
        sheepSpawner?.TrySpawnRewardSpecialGroup(
            (Vector2)transform.position + Vector2.right * 2f, minimumCount, maximumCount);
    }
}

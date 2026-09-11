using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 单局固定地标：7 个红箱子、房屋，以及 2×2/2×3 成片农田。它们只在开局生成一次（Spawn 有一次性保护），
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
    [FormerlySerializedAs("penFenceDefinition")]
    [SerializeField] private ObstacleDefinition fenceDefinition;
    [SerializeField] private ObstacleDefinition redChestDefinition;
    [SerializeField] private ObstacleDefinition houseDefinition;
    [SerializeField] private Sprite redChestSprite;
    [SerializeField] private Sprite houseSprite;
    [Tooltip("洪山宝通寺：全图唯一一座，150 只羊用 E 冲刺才撞得动。")]
    [SerializeField] private ObstacleDefinition pagodaDefinition;
    [SerializeField] private Sprite pagodaSprite;

    [Header("Area")]
    [SerializeField] private Rect area = new(-120f, -70f, 240f, 140f);
    [SerializeField, Min(0f)] private float borderPadding = 7f;
    [SerializeField] private Rect[] exclusionZones = Array.Empty<Rect>();

    [Header("Counts")]
    [SerializeField, Min(1)] private int redChestCount = 7;
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

    [Header("Pagoda (洪山宝通寺)")]
    [Tooltip("贴图 23.04 x 17.28 单位、塔身可见部分 7.46 x 14.90 单位（100 PPU）。")]
    [SerializeField, Min(0.001f)] private float pagodaScale = 0.6f;
    [SerializeField] private Vector2 pagodaColliderSize = new(4.2f, 2.4f);
    [SerializeField] private Vector2 pagodaColliderOffset = new(0.23f, -2.91f);
    [SerializeField, Min(0.5f)] private float pagodaClearance = 14f;
    [Tooltip("宝塔落位后清掉这个半径内的花草石头，让它站得干净。")]
    [SerializeField, Min(0f)] private float pagodaClearRadius = 7f;

    [Header("House")]
    [SerializeField, Min(0.001f)] private float houseScale = 1.25f;
    [SerializeField] private Vector2 houseColliderSize = new(2.8f, 3f);
    [SerializeField] private Vector2 houseColliderOffset = new(-0.02f, 0.13f);

    /// <summary>找不到位置时依次放宽到的间距倍率。</summary>
    private static readonly float[] RelaxSteps = { 1f, 0.75f, 0.55f, 0.4f, 0.28f, 0.18f };

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

        // 全图唯一的宝通寺，最先挑位置。
        CreatePagoda(random);

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
        // 每个红箱子都立即预留一个不同的奖励羊类型，确保撞碎任意箱子都会掉羊。
        SheepCollectionManager collection = SheepCollectionManager.Instance;
        Func<string, bool> isDiscovered = collection != null ? collection.IsUnlocked : null;
        int configuredRewardChests = 0;
        if (sheepSpawner != null)
        {
            foreach (GameObject rewardChest in createdChests)
            {
                if (!sheepSpawner.TryReserveRewardSpecialGroup(
                        isDiscovered,
                        out ProgressiveSheepSpawner.RewardSpecialGroupReservation reservation))
                {
                    break;
                }

                LandmarkChestReward reward = rewardChest.AddComponent<LandmarkChestReward>();
                reward.Configure(sheepSpawner, reservation);
                configuredRewardChests++;
            }
        }

        if (configuredRewardChests < createdChests.Count)
        {
            Debug.LogWarning(
                $"只有 {configuredRewardChests}/{createdChests.Count} 个红箱子成功预留奖励羊，请检查彩色、紫色和金色羊池。",
                this);
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

    /// <summary>洪山宝通寺：全图有且仅有一座。复用围栏的 FenceObstacle 判定，只是门槛是 150 只羊。</summary>
    private void CreatePagoda(System.Random random)
    {
        if (pagodaDefinition == null || pagodaSprite == null)
        {
            Debug.LogWarning("宝通寺缺少 ObstacleDefinition 或贴图，这局不生成。", this);
            return;
        }

        if (!TryFindPositionRelaxed(random, pagodaClearance, out Vector2 position))
        {
            Debug.LogWarning("找不到摆宝通寺的位置。", this);
            return;
        }

        // 塔脚下的花草石头清掉，免得这座唯一地标插在一堆杂物里。
        ClearDebrisAround(position, pagodaClearRadius);

        GameObject pagoda = CreateBlockedObject(
            "HongshanPagoda", position, pagodaScale, pagodaColliderSize, pagodaColliderOffset,
            pagodaDefinition, pagodaSprite);

        FenceObstacle fence = pagoda.AddComponent<FenceObstacle>();
        pagoda.AddComponent<PagodaLandmark>().Configure(pagoda.GetComponent<BreakableObstacle>(), fence);
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
            breakable.Configure(fenceDefinition, fence.GetComponent<SpriteRenderer>());
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

    /// <summary>
    /// 散布物是先于地标生成的（WorldDebrisSpawner 没有 DefaultExecutionOrder，默认 0，比这里的 30 早），
    /// 整张图早就铺满了小碰撞体，间距要求一大就一个位置都找不到。所以从想要的间距开始逐级放宽。
    /// </summary>
    private bool TryFindPositionRelaxed(System.Random random, float clearance, out Vector2 position)
    {
        foreach (float factor in RelaxSteps)
        {
            if (TryFindPosition(random, Mathf.Max(2.5f, clearance * factor), out position))
                return true;
        }

        position = default;
        return false;
    }

    /// <summary>清掉一圈散布物；围栏（外围 / 羊圈）和已经放好的地标不动。</summary>
    private void ClearDebrisAround(Vector2 center, float radius)
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, radius))
        {
            if (hit == null)
                continue;

            BreakableObstacle obstacle = hit.GetComponentInParent<BreakableObstacle>();
            if (obstacle == null)
                continue;

            // 围栏别拆（外圈拆了羊就跑出去了），已经摆好的地标也别拆。
            if (obstacle.GetComponent<FenceObstacle>() != null)
                continue;
            if (root != null && obstacle.transform.IsChildOf(root))
                continue;

            Destroy(obstacle.gameObject);
        }
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
    private ProgressiveSheepSpawner.RewardSpecialGroupReservation reservation;
    private BreakableObstacle obstacle;
    private bool rewarded;

    public void Configure(
        ProgressiveSheepSpawner spawner,
        ProgressiveSheepSpawner.RewardSpecialGroupReservation rewardReservation)
    {
        sheepSpawner = spawner;
        reservation = rewardReservation;
        EnsureObstacleSubscription();
    }

    private void Awake()
    {
        EnsureObstacleSubscription();
    }

    private void OnDestroy()
    {
        if (obstacle != null) obstacle.Broken -= HandleBroken;
    }

    private void EnsureObstacleSubscription()
    {
        BreakableObstacle current = GetComponent<BreakableObstacle>();
        if (current == obstacle)
            return;

        if (obstacle != null)
            obstacle.Broken -= HandleBroken;
        obstacle = current;
        if (obstacle != null)
            obstacle.Broken += HandleBroken;
    }

    private void HandleBroken(BreakableObstacle broken)
    {
        if (rewarded || broken != obstacle)
            return;
        rewarded = true;
        // 同一种彩色、紫色或金色羊，数量沿用撞碎时当前成长阶段的普通刷新批次范围。
        sheepSpawner?.TrySpawnRewardSpecialGroup(
            reservation,
            (Vector2)transform.position + Vector2.right * 2f);
    }
}

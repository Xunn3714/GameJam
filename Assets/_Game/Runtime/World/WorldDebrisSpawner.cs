using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按世界种子在地图上散布可破坏物（花 / 石头 / 木桶……）。
/// 位置、种类都由 <see cref="WorldSeed"/> 派生的随机源决定，同一个种子得到同一张地图。
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldDebrisSpawner : MonoBehaviour
{
    [Serializable]
    public sealed class DebrisEntry
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0f)] private float weight = 1f;
        [Tooltip("放置时周围这个半径内不能有其他碰撞体。")]
        [SerializeField, Min(0.1f)] private float clearance = 1.2f;

        public DebrisEntry() { }

        public DebrisEntry(GameObject prefab, float weight, float clearance)
        {
            this.prefab = prefab;
            this.weight = weight;
            this.clearance = clearance;
        }

        public GameObject Prefab => prefab;
        public float Weight => Mathf.Max(0f, weight);
        public float Clearance => Mathf.Max(0.1f, clearance);
    }

    [Header("References")]
    [SerializeField] private WorldSeed worldSeed;
    [Tooltip("有区块布局时按每格的定义撒；为空则按下面的 entries 在整张 area 里撒。")]
    [SerializeField] private MapLayoutBuilder layout;

    [Header("Debris")]
    [SerializeField] private DebrisEntry[] entries = Array.Empty<DebrisEntry>();

    [Header("Area")]
    [SerializeField] private Rect area = new Rect(-120f, -70f, 240f, 140f);
    [Tooltip("离地图边缘留多宽不放东西（围栏内侧）。")]
    [SerializeField, Min(0f)] private float borderPadding = 4f;
    [Tooltip("这些矩形里不放（例如出生羊圈）。")]
    [SerializeField] private Rect[] exclusionZones = Array.Empty<Rect>();

    [Header("Amount")]
    [Tooltip("每 100 平方单位放多少个。")]
    [SerializeField, Min(0f)] private float densityPer100SquareUnits = 0.8f;
    [Tooltip("全图散布物总数上限；0 = 不限制。")]
    [SerializeField, Min(0)] private int maximumCount = 300;
    [SerializeField, Min(1)] private int placementAttemptsPerItem = 8;
    [Tooltip("任意两个散布物之间的最小距离（世界单位），和各自的 clearance 取大者。防止同一帧放下的东西挤成一团。")]
    [SerializeField, Min(0f)] private float minimumSpacing = 2.2f;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Forest Shape")]
    [Tooltip("森林格不按方形撒：以格中心为圆心的椭圆 + 噪声扰动，边缘是弧形。1 = 椭圆刚好内切格子；小于 1 让整个椭圆留在格内，不会被格边切成直线。")]
    [SerializeField, Min(0.5f)] private float forestBlobRadius = 0.9f;
    [Tooltip("边缘软化宽度（椭圆归一化半径）：从这个距离到 1 之间树逐渐变稀，而不是一刀切。")]
    [SerializeField, Range(0f, 0.9f)] private float forestEdgeSoftness = 0.35f;
    [Tooltip("噪声频率（每世界单位）；越小森林边缘的起伏越大块。")]
    [SerializeField, Min(0.001f)] private float forestNoiseScale = 0.05f;
    [Tooltip("噪声对边缘的推拉幅度（0 = 纯椭圆）。")]
    [SerializeField, Min(0f)] private float forestNoiseStrength = 0.55f;
    [Tooltip("椭圆之外（格子四角）仍按这个比例撒同一套散布物，形成稀疏林缘。")]
    [SerializeField, Range(0f, 1f)] private float forestFringeDensityScale = 0.06f;

    [Header("Regrowth")]
    [Tooltip("开局之后每隔这么多秒在相机外补撒散布物；0 = 不补。")]
    [SerializeField, Min(0f)] private float regrowIntervalSeconds = 6f;
    [SerializeField, Min(0)] private int regrowPerTick = 2;
    [Tooltip("补撒点离相机可见范围的最小距离，避免在玩家眼前凭空长出来。")]
    [SerializeField, Min(0f)] private float regrowCameraPadding = 4f;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Vector2> placedPositions = new List<Vector2>();
    private readonly List<float> placedClearances = new List<float>();
    private Transform debrisRoot;
    private System.Random regrowRandom;
    private float regrowTimer;
    private float noiseOffsetX;
    private float noiseOffsetY;
    private int targetPopulation;

    public int SpawnedCount
    {
        get
        {
            PruneDestroyedInstances();
            return spawned.Count;
        }
    }
    public Rect Area => area;

    public void SetArea(Rect newArea)
    {
        area = newArea;
    }

    public void SetExclusionZones(params Rect[] zones)
    {
        exclusionZones = zones ?? Array.Empty<Rect>();
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            Spawn();
        }
    }

    public void Spawn()
    {
        Clear();

        System.Random random = worldSeed != null ? worldSeed.CreateRandom(2) : new System.Random();
        regrowRandom = worldSeed != null ? worldSeed.CreateRandom(5) : new System.Random();
        regrowTimer = regrowIntervalSeconds;
        noiseOffsetX = (float)random.NextDouble() * 1000f;
        noiseOffsetY = (float)random.NextDouble() * 1000f;
        GameObject root = new GameObject("WorldDebris");
        root.transform.SetParent(transform, false);
        debrisRoot = root.transform;

        // 留边只对整张图的外圈生效；格子之间不留空带。
        Rect inner = new Rect(
            area.xMin + borderPadding,
            area.yMin + borderPadding,
            Mathf.Max(0f, area.width - borderPadding * 2f),
            Mathf.Max(0f, area.height - borderPadding * 2f));

        Physics2D.SyncTransforms();
        placedPositions.Clear();
        placedClearances.Clear();

        int placed = 0;
        int target = 0;
        if (layout != null && layout.Cells.Count > 0)
        {
            foreach (MapCell cell in layout.Cells)
            {
                MapBlockDefinition definition = cell.Definition;
                if (definition == null || definition.Debris.Length == 0)
                    continue;

                Rect region = Intersect(cell.Rect, inner);
                target += SpawnInArea(region, definition.Debris, definition.DebrisDensityPer100SquareUnits, random, ref placed,
                    cell.Role == MapBlockRole.Forest ? cell.Rect : (Rect?)null);
            }
        }
        else
        {
            target = SpawnInArea(inner, entries, densityPer100SquareUnits, random, ref placed, null);
        }

        targetPopulation = maximumCount > 0 ? Mathf.Min(target, maximumCount) : target;
        Physics2D.SyncTransforms();
        // 开局同一物理帧内靠这两张表防重叠；同步进物理世界后，后续补撒只需做物理查询。
        placedPositions.Clear();
        placedClearances.Clear();
        Debug.Log($"地图散布了 {placed} 个可破坏物（目标 {targetPopulation}）。", this);
    }

    /// <summary>
    /// 在一个矩形里按给定条目和密度撒；返回本区域的目标数量。
    /// blobCell 非空时按森林弧形处理：椭圆 + 噪声之内全密度，之外只撒 forestFringeDensityScale 的稀疏林缘。
    /// </summary>
    private int SpawnInArea(Rect inner, DebrisEntry[] set, float density, System.Random random, ref int placed, Rect? blobCell)
    {
        float totalWeight = 0f;
        foreach (DebrisEntry entry in set)
        {
            if (entry != null && entry.Prefab != null)
                totalWeight += entry.Weight;
        }

        if (totalWeight <= 0f || inner.width <= 0f || inner.height <= 0f)
            return 0;

        // 弧形森林的有效面积用蒙特卡洛估一下，否则目标数会把整格的数量挤进椭圆里。
        float coverage = 1f;
        if (blobCell.HasValue)
        {
            const int samples = 256;
            float weightSum = 0f;
            for (int sample = 0; sample < samples; sample++)
                weightSum += ForestBlobWeight(RandomPoint(inner, random), blobCell.Value);
            coverage = weightSum / samples;
        }

        int targetCount = Mathf.RoundToInt(inner.width * inner.height / 100f * density * coverage);
        for (int index = 0; index < targetCount && (maximumCount <= 0 || placed < maximumCount); index++)
        {
            DebrisEntry entry = PickEntry(set, random, totalWeight);
            if (entry == null)
                continue;

            for (int attempt = 0; attempt < placementAttemptsPerItem; attempt++)
            {
                Vector2 position = RandomPoint(inner, random);
                if (blobCell.HasValue && random.NextDouble() >= ForestBlobWeight(position, blobCell.Value))
                    continue;

                if (TryPlace(entry, position, random, ref placed, checkPlacedList: true))
                    break;
            }
        }

        return targetCount;
    }

    private bool TryPlace(DebrisEntry entry, Vector2 position, System.Random random, ref int placed, bool checkPlacedList)
    {
        if (IsInsideExclusionZone(position))
            return false;
        // 场景里原有的碰撞体（围栏、羊圈……）用物理查询避开。
        if (Physics2D.OverlapCircle(position, entry.Clearance) != null)
            return false;
        // 同一次散布里已经放下的东西，用记录的位置和间距判断（新生成的碰撞体这一帧还查不到）。
        if (checkPlacedList && IsTooCloseToPlaced(position, entry.Clearance))
            return false;

        GameObject instance = Instantiate(entry.Prefab, position, Quaternion.identity, debrisRoot);
        instance.name = $"{entry.Prefab.name}_{placed + 1:000}";
        spawned.Add(instance);
        if (checkPlacedList)
        {
            placedPositions.Add(position);
            placedClearances.Add(entry.Clearance);
        }
        placed++;
        return true;
    }

    private static Vector2 RandomPoint(Rect rect, System.Random random)
    {
        return new Vector2(
            rect.xMin + (float)random.NextDouble() * rect.width,
            rect.yMin + (float)random.NextDouble() * rect.height);
    }

    /// <summary>
    /// 以格中心为圆心的椭圆，边缘用 Perlin 噪声推拉，得到弧形而不是方形的林子。
    /// 返回该点的接受概率：椭圆内核为 1，往外经 forestEdgeSoftness 渐变到林缘密度 forestFringeDensityScale。
    /// </summary>
    private float ForestBlobWeight(Vector2 position, Rect cell)
    {
        Vector2 center = cell.center;
        float nx = (position.x - center.x) / (cell.width * 0.5f * forestBlobRadius);
        float ny = (position.y - center.y) / (cell.height * 0.5f * forestBlobRadius);
        float distance = Mathf.Sqrt(nx * nx + ny * ny);
        float noise = Mathf.PerlinNoise(
            position.x * forestNoiseScale + noiseOffsetX,
            position.y * forestNoiseScale + noiseOffsetY);
        float radial = distance + (noise - 0.5f) * forestNoiseStrength;
        float core = Mathf.Max(0f, 1f - forestEdgeSoftness);
        float t = Mathf.InverseLerp(1f, core, radial); // 1 = 内核，0 = 椭圆之外
        return Mathf.Lerp(forestFringeDensityScale, 1f, t * t * (3f - 2f * t));
    }

    // ---------------------------------------------------------------- regrowth

    private void Update()
    {
        if (regrowIntervalSeconds <= 0f || regrowPerTick <= 0 || debrisRoot == null || regrowRandom == null)
            return;

        regrowTimer -= Time.deltaTime;
        if (regrowTimer > 0f)
            return;

        regrowTimer = regrowIntervalSeconds;
        Regrow(regrowPerTick);
    }

    /// <summary>
    /// 随时间补撒：随机挑一格（按面积加权），在相机看不到的地方按该格的散布物表再放几个。
    /// 复用开局的同一套挑选 / 间距 / 弧形规则，只是不再查开局的位置记录（被撞碎的东西早就不在了）。
    /// </summary>
    public void Regrow(int count)
    {
        if (count <= 0 || regrowRandom == null || layout == null || layout.Cells.Count == 0)
            return;

        PruneDestroyedInstances();
        int cap = maximumCount > 0 ? Mathf.Min(targetPopulation, maximumCount) : targetPopulation;
        int budget = Mathf.Min(count, Mathf.Max(0, cap - spawned.Count));
        if (budget <= 0)
            return;

        Physics2D.SyncTransforms();
        Camera camera = Camera.main;
        Rect inner = new Rect(
            area.xMin + borderPadding, area.yMin + borderPadding,
            Mathf.Max(0f, area.width - borderPadding * 2f), Mathf.Max(0f, area.height - borderPadding * 2f));

        int placed = spawned.Count;
        for (int index = 0; index < budget; index++)
        {
            MapCell cell = layout.Cells[regrowRandom.Next(layout.Cells.Count)];
            MapBlockDefinition definition = cell.Definition;
            if (definition == null || definition.Debris.Length == 0)
                continue;

            float totalWeight = 0f;
            foreach (DebrisEntry entry in definition.Debris)
            {
                if (entry != null && entry.Prefab != null)
                    totalWeight += entry.Weight;
            }

            DebrisEntry pick = PickEntry(definition.Debris, regrowRandom, totalWeight);
            if (pick == null)
                continue;

            Rect region = Intersect(cell.Rect, inner);
            bool forest = cell.Role == MapBlockRole.Forest;
            for (int attempt = 0; attempt < placementAttemptsPerItem; attempt++)
            {
                Vector2 position = RandomPoint(region, regrowRandom);
                if (forest && regrowRandom.NextDouble() >= ForestBlobWeight(position, cell.Rect))
                    continue;
                if (IsVisibleToCamera(camera, position))
                    continue;
                if (TryPlace(pick, position, regrowRandom, ref placed, checkPlacedList: false))
                {
                    // 同一轮可能补多个；立即同步，避免下一项落到刚生成物的碰撞体上。
                    Physics2D.SyncTransforms();
                    break;
                }
            }
        }
    }

    private void PruneDestroyedInstances()
    {
        spawned.RemoveAll(instance => instance == null);
    }

    private bool IsVisibleToCamera(Camera camera, Vector2 position)
    {
        if (camera == null)
            return false;

        float halfHeight = camera.orthographic ? camera.orthographicSize : 10f;
        float halfWidth = halfHeight * camera.aspect;
        Vector2 delta = position - (Vector2)camera.transform.position;
        return Mathf.Abs(delta.x) < halfWidth + regrowCameraPadding
            && Mathf.Abs(delta.y) < halfHeight + regrowCameraPadding;
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        return new Rect(xMin, yMin, Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
    }

    public void Clear()
    {
        foreach (GameObject instance in spawned)
        {
            if (instance != null)
                Destroy(instance);
        }

        spawned.Clear();
        placedPositions.Clear();
        placedClearances.Clear();
        targetPopulation = 0;
        regrowRandom = null;
        if (debrisRoot != null)
        {
            Destroy(debrisRoot.gameObject);
            debrisRoot = null;
        }
    }

    private static DebrisEntry PickEntry(DebrisEntry[] set, System.Random random, float totalWeight)
    {
        float roll = (float)random.NextDouble() * totalWeight;
        foreach (DebrisEntry entry in set)
        {
            if (entry == null || entry.Prefab == null)
                continue;

            roll -= entry.Weight;
            if (roll <= 0f)
                return entry;
        }

        return null;
    }

    private bool IsTooCloseToPlaced(Vector2 position, float clearance)
    {
        for (int index = 0; index < placedPositions.Count; index++)
        {
            float required = Mathf.Max(minimumSpacing, clearance, placedClearances[index]);
            if ((placedPositions[index] - position).sqrMagnitude < required * required)
                return true;
        }

        return false;
    }

    private bool IsInsideExclusionZone(Vector2 position)
    {
        foreach (Rect zone in exclusionZones)
        {
            if (zone.Contains(position))
                return true;
        }

        return false;
    }
}

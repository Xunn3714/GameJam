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
    [SerializeField, Min(0)] private int maximumCount = 300;
    [SerializeField, Min(1)] private int placementAttemptsPerItem = 8;
    [Tooltip("任意两个散布物之间的最小距离（世界单位），和各自的 clearance 取大者。防止同一帧放下的东西挤成一团。")]
    [SerializeField, Min(0f)] private float minimumSpacing = 2.2f;
    [SerializeField] private bool spawnOnStart = true;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Vector2> placedPositions = new List<Vector2>();
    private readonly List<float> placedClearances = new List<float>();
    private Transform debrisRoot;

    public int SpawnedCount => spawned.Count;
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
        float totalWeight = 0f;
        foreach (DebrisEntry entry in entries)
        {
            if (entry != null && entry.Prefab != null)
                totalWeight += entry.Weight;
        }

        if (totalWeight <= 0f)
            return;

        GameObject root = new GameObject("WorldDebris");
        root.transform.SetParent(transform, false);
        debrisRoot = root.transform;

        Rect inner = new Rect(
            area.xMin + borderPadding,
            area.yMin + borderPadding,
            Mathf.Max(0f, area.width - borderPadding * 2f),
            Mathf.Max(0f, area.height - borderPadding * 2f));

        int targetCount = Mathf.Min(
            maximumCount,
            Mathf.RoundToInt(inner.width * inner.height / 100f * densityPer100SquareUnits));

        Physics2D.SyncTransforms();
        placedPositions.Clear();
        placedClearances.Clear();

        int placed = 0;
        for (int index = 0; index < targetCount; index++)
        {
            DebrisEntry entry = PickEntry(random, totalWeight);
            if (entry == null)
                continue;

            for (int attempt = 0; attempt < placementAttemptsPerItem; attempt++)
            {
                Vector2 position = new Vector2(
                    inner.xMin + (float)random.NextDouble() * inner.width,
                    inner.yMin + (float)random.NextDouble() * inner.height);

                if (IsInsideExclusionZone(position))
                    continue;
                // 场景里原有的碰撞体（围栏、羊圈……）用物理查询避开。
                if (Physics2D.OverlapCircle(position, entry.Clearance) != null)
                    continue;
                // 同一次散布里已经放下的东西，用记录的位置和间距判断（新生成的碰撞体这一帧还查不到）。
                if (IsTooCloseToPlaced(position, entry.Clearance))
                    continue;

                GameObject instance = Instantiate(entry.Prefab, position, Quaternion.identity, debrisRoot);
                instance.name = $"{entry.Prefab.name}_{placed + 1:000}";
                spawned.Add(instance);
                placedPositions.Add(position);
                placedClearances.Add(entry.Clearance);
                placed++;
                break;
            }
        }

        Physics2D.SyncTransforms();
        Debug.Log($"地图散布了 {placed} 个可破坏物（目标 {targetCount}）。", this);
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
        if (debrisRoot != null)
        {
            Destroy(debrisRoot.gameObject);
            debrisRoot = null;
        }
    }

    private DebrisEntry PickEntry(System.Random random, float totalWeight)
    {
        float roll = (float)random.NextDouble() * totalWeight;
        foreach (DebrisEntry entry in entries)
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

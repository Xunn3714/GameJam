using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class ProgressiveSheepSpawner : MonoBehaviour
{
    private const float GoldenAngle = 2.39996323f;

    /// <summary>一种羊的刷新条目：预制体 + 类型 id + 显示名 + 权重。</summary>
    [Serializable]
    public sealed class SheepTypeEntry
    {
        [SerializeField] private RecruitableSheep prefab;
        [SerializeField] private string typeId = MvpSheepCatalog.DefaultTypeId;
        [SerializeField] private string displayName = "普通羊";
        [SerializeField, Min(0f)] private float weight = 90f;

        public SheepTypeEntry() { }

        public SheepTypeEntry(RecruitableSheep prefab, string typeId, string displayName, float weight)
        {
            this.prefab = prefab;
            this.typeId = typeId;
            this.displayName = displayName;
            this.weight = weight;
        }

        public RecruitableSheep Prefab => prefab;
        public string TypeId => string.IsNullOrWhiteSpace(typeId) ? MvpSheepCatalog.DefaultTypeId : typeId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? TypeId : displayName;
        public float Weight => Mathf.Max(0f, weight);
    }

    [Header("References")]
    [SerializeField] private FlockController flock;
    [Tooltip("类型表为空时使用的默认羊。")]
    [SerializeField] private RecruitableSheep sheepPrefab;
    [SerializeField] private SheepNamePool namePool;
    [SerializeField] private Camera gameplayCamera;
    [Tooltip("可选：提供后刷新位置与羊的类型都由种子决定，可复现。")]
    [SerializeField] private WorldSeed worldSeed;

    [Header("Sheep Types")]
    [Tooltip("按权重随机的羊类型表。建议：普通 90，四种特殊各 2.5。为空则只刷 sheepPrefab。")]
    [SerializeField] private SheepTypeEntry[] sheepTypes = System.Array.Empty<SheepTypeEntry>();

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero;
    [SerializeField] private Vector2 spawnAreaSize = new(240f, 140f);
    [SerializeField, Min(0f)] private float cameraEdgePadding = 3f;
    [SerializeField, Min(0.1f)] private float sheepSpacing = 1.15f;
    [SerializeField, Min(1)] private int placementAttempts = 80;
    [Tooltip("不在这些矩形里刷羊（例如出生羊圈）。")]
    [SerializeField] private Rect[] exclusionZones = System.Array.Empty<Rect>();

    private readonly List<RecruitableSheep> activeBatch = new();
    private readonly List<string> availableNames = new();
    private readonly HashSet<string> usedNames = new(StringComparer.Ordinal);
    private Transform spawnRoot;
    private System.Random random;
    private int fallbackNameIndex = 1;
    private int groupSequence;
    private float spawnAngleOffset;
    private bool initialized;

    /// <summary>某种类型的羊被刷出时触发（类型 id, 羊）。</summary>
    public event Action<string, RecruitableSheep> SheepSpawned;

    public Rect SpawnBounds => new(spawnAreaCenter - spawnAreaSize * 0.5f, spawnAreaSize);
    public int ActiveBatchCount
    {
        get
        {
            activeBatch.RemoveAll(sheep => sheep == null || sheep.IsRecruited);
            return activeBatch.Count;
        }
    }

    public int TotalSpawned { get; private set; }
    public int ActiveWildSheepCount => ActiveBatchCount;
    public IReadOnlyList<SheepTypeEntry> SheepTypes => sheepTypes;

    /// <summary>类型 id 对应的显示名；表里没有则返回 id 本身。</summary>
    public string GetTypeDisplayName(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            typeId = MvpSheepCatalog.DefaultTypeId;

        foreach (SheepTypeEntry entry in sheepTypes)
        {
            if (entry != null && string.Equals(entry.TypeId, typeId, StringComparison.Ordinal))
                return entry.DisplayName;
        }

        return typeId == MvpSheepCatalog.DefaultTypeId ? "普通羊" : typeId;
    }

    public void SetExclusionZones(params Rect[] zones)
    {
        exclusionZones = zones ?? System.Array.Empty<Rect>();
    }

    public void SetSpawnArea(Rect area)
    {
        spawnAreaCenter = area.center;
        spawnAreaSize = area.size;
    }

    public void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        random = worldSeed != null ? worldSeed.CreateRandom(1) : new System.Random();
        BuildNamePool();
        AssignNamesToCurrentFlock();
        spawnAngleOffset = NextFloat() * Mathf.PI * 2f;

        GameObject root = new("RuntimeRecruitableSheep");
        spawnRoot = root.transform;
        spawnRoot.SetParent(transform, false);
    }

    public bool SpawnBatch(int minimumCount, int maximumCount)
    {
        if (!initialized)
            Initialize();

        if (flock == null || (sheepPrefab == null && sheepTypes.Length == 0))
            return false;

        minimumCount = Mathf.Max(1, minimumCount);
        maximumCount = Mathf.Max(minimumCount, maximumCount);
        int count = random.Next(minimumCount, maximumCount + 1);

        if (!TryFindClusterCenter(count, out Vector2 clusterCenter))
        {
            Debug.LogWarning($"无法为下一批 {count} 只羊找到镜头外的安全位置。", this);
            return false;
        }

        groupSequence++;

        for (int index = 0; index < count; index++)
        {
            Vector2 offset = GetClusterOffset(index);
            SheepTypeEntry type = PickType();
            RecruitableSheep prefab = type != null && type.Prefab != null ? type.Prefab : sheepPrefab;
            if (prefab == null)
                continue;

            RecruitableSheep sheep = Instantiate(
                prefab,
                new Vector3(
                    clusterCenter.x + offset.x,
                    clusterCenter.y + offset.y,
                    prefab.transform.position.z),
                prefab.transform.rotation,
                spawnRoot);

            TotalSpawned++;
            sheep.name = $"AlphaWildSheep_{TotalSpawned:000}";
            SheepIdentity identity = sheep.GetComponent<SheepIdentity>();
            if (identity == null)
                identity = sheep.gameObject.AddComponent<SheepIdentity>();
            if (type != null)
                identity.AssignType(type.TypeId);
            AssignName(identity);
            sheep.gameObject.SetActive(true);
            activeBatch.Add(sheep);
            SheepSpawned?.Invoke(identity.SheepTypeId, sheep);
        }

        Debug.Log($"Alpha 刷新了一批 {count} 只羊；累计刷新 {TotalSpawned} 只。", this);
        return true;
    }

    public bool MarkRecruited(RecruitableSheep sheep)
    {
        return sheep != null && activeBatch.Remove(sheep);
    }

    public int CountWildSheepNear(Vector2 center, float radius)
    {
        activeBatch.RemoveAll(sheep => sheep == null || sheep.IsRecruited);
        float radiusSquared = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
        int count = 0;
        foreach (RecruitableSheep sheep in activeBatch)
        {
            if (((Vector2)sheep.transform.position - center).sqrMagnitude <= radiusSquared)
                count++;
        }

        return count;
    }

    public int DespawnWildSheepFartherThan(Vector2 center, float distance)
    {
        float distanceSquared = Mathf.Max(0f, distance) * Mathf.Max(0f, distance);
        int removed = 0;
        for (int index = activeBatch.Count - 1; index >= 0; index--)
        {
            RecruitableSheep sheep = activeBatch[index];
            if (sheep == null || sheep.IsRecruited)
            {
                activeBatch.RemoveAt(index);
                continue;
            }

            if (((Vector2)sheep.transform.position - center).sqrMagnitude <= distanceSquared)
                continue;

            activeBatch.RemoveAt(index);
            Destroy(sheep.gameObject);
            removed++;
        }

        return removed;
    }

    /// <summary>按权重挑一种羊；权重全为 0 或表为空时返回 null（用默认预制体）。</summary>
    private SheepTypeEntry PickType()
    {
        float total = 0f;
        foreach (SheepTypeEntry entry in sheepTypes)
        {
            if (entry != null && entry.Prefab != null)
                total += entry.Weight;
        }

        if (total <= 0f)
            return null;

        float roll = NextFloat() * total;
        foreach (SheepTypeEntry entry in sheepTypes)
        {
            if (entry == null || entry.Prefab == null)
                continue;

            roll -= entry.Weight;
            if (roll <= 0f)
                return entry;
        }

        return sheepTypes[sheepTypes.Length - 1];
    }

    private float NextFloat()
    {
        return random != null ? (float)random.NextDouble() : Random.value;
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

    private bool TryFindClusterCenter(int count, out Vector2 center)
    {
        Rect bounds = SpawnBounds;
        float clusterRadius = sheepSpacing * Mathf.Sqrt(Mathf.Max(1, count - 1)) + 0.75f;
        Vector2 focus = flock != null ? flock.Center : spawnAreaCenter;
        Vector2 cameraCenter = gameplayCamera != null
            ? (Vector2)gameplayCamera.transform.position
            : focus;

        float halfHeight = gameplayCamera != null && gameplayCamera.orthographic
            ? gameplayCamera.orthographicSize
            : 5f;
        float halfWidth = gameplayCamera != null
            ? halfHeight * gameplayCamera.aspect
            : halfHeight * (16f / 9f);

        Physics2D.SyncTransforms();

        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            float angle = spawnAngleOffset + (groupSequence + attempt) * GoldenAngle;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));

            float distance = Mathf.Max(halfWidth, halfHeight)
                + cameraEdgePadding
                + clusterRadius
                + NextFloat() * 8f;
            Vector2 candidate = cameraCenter + direction * distance;

            candidate.x = Mathf.Clamp(candidate.x, bounds.xMin + clusterRadius, bounds.xMax - clusterRadius);
            candidate.y = Mathf.Clamp(candidate.y, bounds.yMin + clusterRadius, bounds.yMax - clusterRadius);

            if (!IsOutsideCamera(candidate, cameraCenter, halfWidth, halfHeight, clusterRadius))
                continue;
            if (!CanPlaceCluster(candidate, count, bounds))
                continue;

            center = candidate;
            return true;
        }

        center = default;
        return false;
    }

    private bool CanPlaceCluster(Vector2 center, int count, Rect bounds)
    {
        for (int index = 0; index < count; index++)
        {
            Vector2 position = center + GetClusterOffset(index);
            if (!bounds.Contains(position) || IsInsideExclusionZone(position))
                return false;

            Collider2D[] hits = Physics2D.OverlapCircleAll(position, sheepSpacing * 0.4f);
            foreach (Collider2D hit in hits)
            {
                if (hit != null)
                    return false;
            }
        }

        return true;
    }

    private bool IsOutsideCamera(
        Vector2 candidate,
        Vector2 focus,
        float halfWidth,
        float halfHeight,
        float clusterRadius)
    {
        Vector2 delta = candidate - focus;
        return Mathf.Abs(delta.x) > halfWidth + cameraEdgePadding + clusterRadius
            || Mathf.Abs(delta.y) > halfHeight + cameraEdgePadding + clusterRadius;
    }

    private Vector2 GetClusterOffset(int index)
    {
        if (index <= 0)
            return Vector2.zero;

        float radius = sheepSpacing * Mathf.Sqrt(index);
        float angle = index * GoldenAngle;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private void BuildNamePool()
    {
        availableNames.Clear();
        usedNames.Clear();

        if (namePool != null)
        {
            foreach (string candidate in namePool.Names)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                string trimmed = candidate.Trim();
                if (!availableNames.Contains(trimmed))
                    availableNames.Add(trimmed);
            }
        }

        for (int index = availableNames.Count - 1; index > 0; index--)
        {
            int randomIndex = random != null ? random.Next(0, index + 1) : Random.Range(0, index + 1);
            (availableNames[index], availableNames[randomIndex]) =
                (availableNames[randomIndex], availableNames[index]);
        }
    }

    private void AssignNamesToCurrentFlock()
    {
        if (flock == null)
            return;

        foreach (SheepMember member in flock.Members)
        {
            if (member == null)
                continue;

            SheepIdentity identity = member.GetComponent<SheepIdentity>();
            if (identity == null)
                identity = member.gameObject.AddComponent<SheepIdentity>();
            AssignName(identity);
        }
    }

    private void AssignName(SheepIdentity identity)
    {
        if (identity == null)
            return;

        if (!string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            usedNames.Add(identity.DisplayName);
            availableNames.Remove(identity.DisplayName);
            return;
        }

        string assignedName = null;
        while (availableNames.Count > 0 && assignedName == null)
        {
            string candidate = availableNames[0];
            availableNames.RemoveAt(0);
            if (usedNames.Add(candidate))
                assignedName = candidate;
        }

        while (assignedName == null)
        {
            string candidate = $"小羊 {fallbackNameIndex:000}";
            fallbackNameIndex++;
            if (usedNames.Add(candidate))
                assignedName = candidate;
        }

        identity.AssignName(assignedName);
    }

    private void OnValidate()
    {
        spawnAreaSize.x = Mathf.Max(20f, spawnAreaSize.x);
        spawnAreaSize.y = Mathf.Max(20f, spawnAreaSize.y);
        placementAttempts = Mathf.Max(1, placementAttempts);
    }
}

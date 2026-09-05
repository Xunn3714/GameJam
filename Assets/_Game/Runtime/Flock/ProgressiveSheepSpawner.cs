using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class ProgressiveSheepSpawner : MonoBehaviour
{
    private const float GoldenAngle = 2.39996323f;

    [Header("References")]
    [SerializeField] private FlockController flock;
    [SerializeField] private SheepNamePool namePool;
    [SerializeField] private Camera gameplayCamera;
    [Tooltip("可选：提供后刷新位置与羊的类型都由种子决定，可复现。")]
    [SerializeField] private WorldSeed worldSeed;

    [Header("Special Sheep Catalog")]
    [Tooltip("当前主流程唯一的羊刷新配置：包含共用 Prefab、品质概率和文件夹登记的特殊羊。")]
    [SerializeField] private SpecialSheepCatalog specialSheepCatalog;

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
    private readonly SpecialSheepRunState specialRunState = new();
    private readonly Dictionary<RecruitableSheep, ActiveSpecialGroup> specialGroupBySheep = new();
    private Transform spawnRoot;
    private System.Random random;
    private int fallbackNameIndex = 1;
    private int groupSequence;
    private float spawnAngleOffset;
    private bool initialized;

    private sealed class ActiveSpecialGroup
    {
        public ActiveSpecialGroup(string typeId)
        {
            TypeId = typeId;
        }

        public string TypeId { get; }
        public bool Collected { get; set; }
        public HashSet<RecruitableSheep> WildMembers { get; } = new();
    }

    /// <summary>某种类型的羊被刷出时触发（类型 id, 羊）。</summary>
    public event Action<string, RecruitableSheep> SheepSpawned;

    public Rect SpawnBounds => new(spawnAreaCenter - spawnAreaSize * 0.5f, spawnAreaSize);
    public int ActiveBatchCount
    {
        get
        {
            PruneInactiveSheep();
            return activeBatch.Count;
        }
    }

    public int TotalSpawned { get; private set; }
    public int ActiveWildSheepCount => ActiveBatchCount;
    public SpecialSheepCatalog Catalog => specialSheepCatalog;
    public SpecialSheepRunStatus GetSpecialSheepStatus(string typeId) => specialRunState.GetStatus(typeId);

    /// <summary>类型 id 对应的显示名；表里没有则返回 id 本身。</summary>
    public string GetTypeDisplayName(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            typeId = MvpSheepCatalog.DefaultTypeId;

        return specialSheepCatalog != null
            ? specialSheepCatalog.GetDisplayName(typeId)
            : typeId == MvpSheepCatalog.DefaultTypeId ? "普通羊" : typeId;
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
        specialRunState.Reset();
        specialGroupBySheep.Clear();
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

        RecruitableSheep prefab = specialSheepCatalog != null
            ? specialSheepCatalog.BaseSheepPrefab
            : null;
        if (flock == null || prefab == null)
        {
            Debug.LogWarning("羊刷新器缺少 Flock 或 Special Sheep Catalog 的基础羊 Prefab。", this);
            return false;
        }

        minimumCount = Mathf.Max(1, minimumCount);
        maximumCount = Mathf.Max(minimumCount, maximumCount);
        int count = random.Next(minimumCount, maximumCount + 1);

        if (!TryFindClusterCenter(count, out Vector2 clusterCenter))
        {
            Debug.LogWarning($"无法为下一批 {count} 只羊找到镜头外的安全位置。", this);
            return false;
        }

        groupSequence++;
        int groupId = groupSequence;
        SpecialSheepCatalog.Entry catalogEntry = null;
        SheepQuality quality = specialSheepCatalog.RollQuality(random.NextDouble() * 100d);
        if (quality != SheepQuality.Common)
        {
            specialSheepCatalog.TryPickAvailable(
                quality,
                random,
                specialRunState.IsAvailable,
                out catalogEntry);
        }

        string typeId = catalogEntry != null
            ? catalogEntry.TypeId
            : MvpSheepCatalog.DefaultTypeId;
        List<RecruitableSheep> created = new(count);
        if (!TryCreateGroupInstances(
                prefab,
                count,
                clusterCenter,
                groupId,
                typeId,
                catalogEntry,
                quality,
                created))
        {
            if (catalogEntry == null)
                return false;

            catalogEntry = null;
            quality = SheepQuality.Common;
            typeId = MvpSheepCatalog.DefaultTypeId;
            if (!TryCreateGroupInstances(
                    prefab,
                    count,
                    clusterCenter,
                    groupId,
                    typeId,
                    catalogEntry,
                    quality,
                    created))
                return false;
        }

        ActiveSpecialGroup specialGroup = null;
        if (catalogEntry != null)
        {
            if (!specialRunState.TryActivate(typeId))
            {
                DestroyGroupInstances(created);
                created.Clear();
                catalogEntry = null;
                quality = SheepQuality.Common;
                typeId = MvpSheepCatalog.DefaultTypeId;
                if (!TryCreateGroupInstances(
                        prefab,
                        count,
                        clusterCenter,
                        groupId,
                        typeId,
                        catalogEntry,
                        quality,
                        created))
                    return false;
            }
            else
            {
                specialGroup = new ActiveSpecialGroup(typeId);
            }
        }

        foreach (RecruitableSheep sheep in created)
        {
            TotalSpawned++;
            activeBatch.Add(sheep);
            if (specialGroup != null)
            {
                specialGroup.WildMembers.Add(sheep);
                specialGroupBySheep[sheep] = specialGroup;
            }

            SheepIdentity identity = sheep.GetComponent<SheepIdentity>();
            SheepSpawned?.Invoke(identity.SheepTypeId, sheep);
        }

        Debug.Log(
            $"Alpha 刷新了第 {groupId} 组，共 {count} 只{GetTypeDisplayName(typeId)}；" +
            $"累计刷新 {TotalSpawned} 只。",
            this);
        return true;
    }

    private bool TryCreateGroupInstances(
        RecruitableSheep prefab,
        int count,
        Vector2 clusterCenter,
        int groupId,
        string typeId,
        SpecialSheepCatalog.Entry catalogEntry,
        SheepQuality quality,
        List<RecruitableSheep> created)
    {
        try
        {
            for (int index = 0; index < count; index++)
            {
                Vector2 offset = GetClusterOffset(index);
                RecruitableSheep sheep = Instantiate(
                    prefab,
                    new Vector3(
                        clusterCenter.x + offset.x,
                        clusterCenter.y + offset.y,
                        prefab.transform.position.z),
                    prefab.transform.rotation,
                    spawnRoot);
                created.Add(sheep);

                sheep.name = $"AlphaWildSheep_G{groupId:000}_{index + 1:00}";
                SheepIdentity identity = sheep.GetComponent<SheepIdentity>();
                identity ??= sheep.gameObject.AddComponent<SheepIdentity>();
                identity.AssignType(typeId);

                if (catalogEntry != null)
                {
                    sheep.ConfigureSprite(catalogEntry.Sprite);
                    SpecialSheepMarker marker = sheep.GetComponent<SpecialSheepMarker>();
                    marker ??= sheep.gameObject.AddComponent<SpecialSheepMarker>();
                    marker.Configure(typeId, catalogEntry.DisplayName, quality);
                }

                AssignName(identity);
                sheep.gameObject.SetActive(true);
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            DestroyGroupInstances(created);
            created.Clear();
            return false;
        }
    }

    private static void DestroyGroupInstances(IEnumerable<RecruitableSheep> sheepGroup)
    {
        foreach (RecruitableSheep sheep in sheepGroup)
            DestroySheepInstance(sheep);
    }

    private static void DestroySheepInstance(RecruitableSheep sheep)
    {
        if (sheep == null)
            return;

        sheep.gameObject.SetActive(false);
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(sheep.gameObject);
        else
#endif
            Destroy(sheep.gameObject);
    }

    public bool MarkRecruited(RecruitableSheep sheep)
    {
        if (sheep == null || !activeBatch.Remove(sheep))
            return false;

        if (specialGroupBySheep.TryGetValue(sheep, out ActiveSpecialGroup group))
        {
            group.Collected = true;
            specialRunState.MarkCollected(group.TypeId);
            RemoveSpecialGroupMember(sheep, knownGroup: group);
        }

        return true;
    }

    public int CountWildSheepNear(Vector2 center, float radius)
    {
        PruneInactiveSheep();
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
                RemoveSpecialGroupMember(sheep, sheep != null && sheep.IsRecruited);
                continue;
            }

            if (((Vector2)sheep.transform.position - center).sqrMagnitude <= distanceSquared)
                continue;

            activeBatch.RemoveAt(index);
            RemoveSpecialGroupMember(sheep);
            DestroySheepInstance(sheep);
            removed++;
        }

        return removed;
    }

    private void PruneInactiveSheep()
    {
        for (int index = activeBatch.Count - 1; index >= 0; index--)
        {
            RecruitableSheep sheep = activeBatch[index];
            if (sheep != null && !sheep.IsRecruited)
                continue;

            activeBatch.RemoveAt(index);
            RemoveSpecialGroupMember(sheep, sheep != null && sheep.IsRecruited);
        }
    }

    private void RemoveSpecialGroupMember(
        RecruitableSheep sheep,
        bool recruited = false,
        ActiveSpecialGroup knownGroup = null)
    {
        if (ReferenceEquals(sheep, null))
            return;

        ActiveSpecialGroup group = knownGroup;
        if (group == null && !specialGroupBySheep.TryGetValue(sheep, out group))
            return;

        if (recruited)
        {
            group.Collected = true;
            specialRunState.MarkCollected(group.TypeId);
        }

        specialGroupBySheep.Remove(sheep);
        group.WildMembers.Remove(sheep);
        if (group.WildMembers.Count == 0 && !group.Collected)
            specialRunState.ReleaseIfActive(group.TypeId);
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

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
    [SerializeField] private RecruitableSheep sheepPrefab;
    [SerializeField] private SheepNamePool namePool;
    [SerializeField] private Camera gameplayCamera;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero;
    [SerializeField] private Vector2 spawnAreaSize = new(240f, 140f);
    [SerializeField, Min(0f)] private float cameraEdgePadding = 3f;
    [SerializeField, Min(0.1f)] private float sheepSpacing = 1.15f;
    [SerializeField, Min(1)] private int placementAttempts = 80;

    private readonly List<RecruitableSheep> activeBatch = new();
    private readonly List<string> availableNames = new();
    private readonly HashSet<string> usedNames = new(StringComparer.Ordinal);
    private Transform spawnRoot;
    private int fallbackNameIndex = 1;
    private int groupSequence;
    private float spawnAngleOffset;
    private bool initialized;

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

    public void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        BuildNamePool();
        AssignNamesToCurrentFlock();
        spawnAngleOffset = Random.value * Mathf.PI * 2f;

        GameObject root = new("RuntimeRecruitableSheep");
        spawnRoot = root.transform;
        spawnRoot.SetParent(transform, false);
    }

    public bool SpawnBatch(int minimumCount, int maximumCount)
    {
        if (!initialized)
            Initialize();

        if (sheepPrefab == null || flock == null)
            return false;

        minimumCount = Mathf.Max(1, minimumCount);
        maximumCount = Mathf.Max(minimumCount, maximumCount);
        int count = Random.Range(minimumCount, maximumCount + 1);

        if (!TryFindClusterCenter(count, out Vector2 clusterCenter))
        {
            Debug.LogWarning($"无法为下一批 {count} 只羊找到镜头外的安全位置。", this);
            return false;
        }

        groupSequence++;

        for (int index = 0; index < count; index++)
        {
            Vector2 offset = GetClusterOffset(index);
            RecruitableSheep sheep = Instantiate(
                sheepPrefab,
                new Vector3(
                    clusterCenter.x + offset.x,
                    clusterCenter.y + offset.y,
                    sheepPrefab.transform.position.z),
                sheepPrefab.transform.rotation,
                spawnRoot);

            TotalSpawned++;
            sheep.name = $"AlphaWildSheep_{TotalSpawned:000}";
            SheepIdentity identity = sheep.GetComponent<SheepIdentity>();
            if (identity == null)
                identity = sheep.gameObject.AddComponent<SheepIdentity>();
            AssignName(identity);
            sheep.gameObject.SetActive(true);
            activeBatch.Add(sheep);
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
                + Random.Range(0f, 8f);
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
            if (!bounds.Contains(position))
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
            int randomIndex = Random.Range(0, index + 1);
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

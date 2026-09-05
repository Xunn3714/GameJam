using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

public static class MvpSheepSpawnDistributor
{
    private readonly struct SpawnSlot
    {
        public SpawnSlot(Vector2 position, bool isSpecial, RecruitableSheep prefabOverride = null)
        {
            Position = position;
            IsSpecial = isSpecial;
            PrefabOverride = prefabOverride;
        }

        public Vector2 Position { get; }
        public bool IsSpecial { get; }
        public RecruitableSheep PrefabOverride { get; }
    }

    public static IReadOnlyList<RecruitableSheep> PrepareAndDistribute(
        RecruitableSheep normalPrefab,
        SpecialSheepPool specialPool,
        RecruitableSheep fallbackSpecialPrefab,
        IReadOnlyList<SpecialSheepSpawnPoint> fixedSpecialSpawnPoints,
        int targetCount,
        int specialCount,
        float groupOfOneWeight,
        float groupOfTwoWeight,
        float groupOfThreeWeight,
        Rect spawnArea,
        Vector2 playerPosition,
        float playerSafeRadius,
        float clusterRadius,
        float minimumClusterDistance,
        float localDensityRadius,
        float sheepClearance,
        int fixedSeed)
    {
        targetCount = Mathf.Max(1, targetCount);
        specialCount = Mathf.Clamp(specialCount, 0, targetCount);

        List<RecruitableSheep> authoredSheep = new(Object.FindObjectsByType<RecruitableSheep>(
            FindObjectsInactive.Include));

        if (normalPrefab == null && authoredSheep.Count > 0)
            normalPrefab = authoredSheep[0];

        if (normalPrefab == null)
        {
            Debug.LogError("没有配置 RecruitableSheep Prefab，无法生成野生羊。");
            return Array.Empty<RecruitableSheep>();
        }

        // Scene sheep are authoring references only. Every run creates a fresh
        // population so the requested count never depends on pre-placed objects.
        foreach (RecruitableSheep sheep in authoredSheep)
            if (sheep != null) sheep.gameObject.SetActive(false);

        int seed = fixedSeed != 0
            ? fixedSeed
            : unchecked(Environment.TickCount * 397 ^ DateTime.UtcNow.Ticks.GetHashCode());
        Random random = new(seed);
        List<SpawnSlot> slots = new(targetCount);

        bool generated = TryGenerateRandomLayout(
            random,
            targetCount - specialCount,
            specialCount,
            fixedSpecialSpawnPoints,
            groupOfOneWeight,
            groupOfTwoWeight,
            groupOfThreeWeight,
            spawnArea,
            playerPosition,
            playerSafeRadius,
            clusterRadius,
            minimumClusterDistance,
            localDensityRadius,
            sheepClearance,
            slots,
            out int fixedSpecialsUsed,
            out int regularGroupCount);

        if (!generated)
        {
            slots.Clear();
            generated = TryGenerateGridFallback(
                random,
                targetCount - specialCount,
                specialCount,
                fixedSpecialSpawnPoints,
                groupOfOneWeight,
                groupOfTwoWeight,
                groupOfThreeWeight,
                spawnArea,
                playerPosition,
                playerSafeRadius,
                clusterRadius,
                minimumClusterDistance,
                localDensityRadius,
                sheepClearance,
                slots,
                out fixedSpecialsUsed,
                out regularGroupCount);
        }

        if (!generated)
        {
            Debug.LogError(
                $"无法在当前区域安全放置 {targetCount} 只羊。请扩大生成区域或降低数量。");
            return Array.Empty<RecruitableSheep>();
        }

        Transform spawnRoot = new GameObject("RuntimeRecruitableSheep").transform;
        List<RecruitableSheep> activeSheep = new(targetCount);
        int regularIndex = 0;
        int specialIndex = 0;

        foreach (SpawnSlot slot in slots)
        {
            RecruitableSheep prefab = normalPrefab;
            if (slot.IsSpecial)
            {
                prefab = slot.PrefabOverride != null
                    ? slot.PrefabOverride
                    : specialPool != null
                        ? specialPool.Pick(random, fallbackSpecialPrefab)
                        : fallbackSpecialPrefab;
                prefab ??= normalPrefab;
            }

            RecruitableSheep sheep = Object.Instantiate(
                prefab,
                new Vector3(slot.Position.x, slot.Position.y, prefab.transform.position.z),
                prefab.transform.rotation,
                spawnRoot);

            if (slot.IsSpecial)
            {
                specialIndex++;
                sheep.name = $"SpecialSheep_{specialIndex:00}";
                if (sheep.GetComponent<SpecialSheepMarker>() == null)
                    sheep.gameObject.AddComponent<SpecialSheepMarker>();
            }
            else
            {
                regularIndex++;
                sheep.name = $"RecruitableSheep_{regularIndex:00}";
            }

            sheep.gameObject.SetActive(true);
            activeSheep.Add(sheep);
        }

        Debug.Log(
            $"使用随机种子 {seed} 生成 {activeSheep.Count} 只羊：普通羊 {regularIndex} 只/{regularGroupCount} 群，" +
            $"特殊羊 {specialIndex} 只（固定点 {fixedSpecialsUsed} 只）。");
        return activeSheep;
    }

    private static bool TryGenerateRandomLayout(
        Random random,
        int regularCount,
        int specialCount,
        IReadOnlyList<SpecialSheepSpawnPoint> fixedPoints,
        float groupOfOneWeight,
        float groupOfTwoWeight,
        float groupOfThreeWeight,
        Rect area,
        Vector2 playerPosition,
        float playerSafeRadius,
        float clusterRadius,
        float minimumClusterDistance,
        float localDensityRadius,
        float clearance,
        List<SpawnSlot> slots,
        out int fixedSpecialsUsed,
        out int regularGroupCount)
    {
        fixedSpecialsUsed = 0;
        regularGroupCount = 0;

        for (int layoutAttempt = 0; layoutAttempt < 60; layoutAttempt++)
        {
            slots.Clear();
            List<Vector2> positions = new();
            List<Vector2> centers = new();
            fixedSpecialsUsed = AppendValidFixedSpecials(
                specialCount,
                fixedPoints,
                area,
                playerPosition,
                playerSafeRadius,
                minimumClusterDistance,
                localDensityRadius,
                clearance,
                positions,
                centers,
                slots);

            int remainingSpecial = specialCount - fixedSpecialsUsed;
            while (remainingSpecial > 0)
            {
                if (!TryAppendRandomSingleton(
                        random,
                        true,
                        area,
                        playerPosition,
                        playerSafeRadius,
                        clusterRadius,
                        minimumClusterDistance,
                        localDensityRadius,
                        clearance,
                        positions,
                        centers,
                        slots))
                {
                    break;
                }

                remainingSpecial--;
            }

            if (remainingSpecial > 0)
                continue;

            int remainingRegular = regularCount;
            regularGroupCount = 0;
            while (remainingRegular > 0)
            {
                int groupSize = ChooseGroupSize(
                    random,
                    remainingRegular,
                    groupOfOneWeight,
                    groupOfTwoWeight,
                    groupOfThreeWeight);
                bool placed = false;

                for (int centerAttempt = 0; centerAttempt < 200 && !placed; centerAttempt++)
                {
                    Vector2 center = RandomPoint(random, area, clusterRadius + clearance);
                    if (Vector2.Distance(center, playerPosition) < playerSafeRadius + clusterRadius)
                        continue;
                    if (!FarEnoughFromCenters(center, centers, minimumClusterDistance))
                        continue;

                    float angle = (float)(random.NextDouble() * Math.PI * 2d);
                    Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                    placed = TryAppendRegularCluster(
                        center,
                        direction,
                        groupSize,
                        area,
                        playerPosition,
                        playerSafeRadius,
                        localDensityRadius,
                        clearance,
                        positions,
                        slots);

                    if (placed)
                        centers.Add(center);
                }

                if (!placed)
                    break;

                regularGroupCount++;
                remainingRegular -= groupSize;
            }

            if (slots.Count == regularCount + specialCount)
                return true;
        }

        slots.Clear();
        fixedSpecialsUsed = 0;
        regularGroupCount = 0;
        return false;
    }

    private static bool TryGenerateGridFallback(
        Random random,
        int regularCount,
        int specialCount,
        IReadOnlyList<SpecialSheepSpawnPoint> fixedPoints,
        float groupOfOneWeight,
        float groupOfTwoWeight,
        float groupOfThreeWeight,
        Rect area,
        Vector2 playerPosition,
        float playerSafeRadius,
        float clusterRadius,
        float minimumClusterDistance,
        float localDensityRadius,
        float clearance,
        List<SpawnSlot> slots,
        out int fixedSpecialsUsed,
        out int regularGroupCount)
    {
        List<Vector2> positions = new();
        List<Vector2> centers = new();
        fixedSpecialsUsed = AppendValidFixedSpecials(
            specialCount,
            fixedPoints,
            area,
            playerPosition,
            playerSafeRadius,
            minimumClusterDistance,
            localDensityRadius,
            clearance,
            positions,
            centers,
            slots);

        int remainingSpecial = specialCount - fixedSpecialsUsed;
        int remainingRegular = regularCount;
        regularGroupCount = 0;
        float margin = clusterRadius + clearance;
        float step = Mathf.Max(minimumClusterDistance, localDensityRadius + clusterRadius);
        int gridIndex = 0;

        for (float y = area.yMin + margin; y <= area.yMax - margin; y += step)
        {
            for (float x = area.xMin + margin; x <= area.xMax - margin; x += step)
            {
                if (remainingSpecial <= 0 && remainingRegular <= 0)
                    return true;

                Vector2 center = new(x, y);
                if (Vector2.Distance(center, playerPosition) < playerSafeRadius + clusterRadius)
                    continue;
                if (!FarEnoughFromCenters(center, centers, minimumClusterDistance))
                    continue;

                if (remainingSpecial > 0)
                {
                    if (!IsValidPosition(
                            center,
                            area,
                            playerPosition,
                            playerSafeRadius,
                            localDensityRadius,
                            clearance,
                            positions))
                    {
                        continue;
                    }

                    positions.Add(center);
                    centers.Add(center);
                    slots.Add(new SpawnSlot(center, true));
                    remainingSpecial--;
                    continue;
                }

                int groupSize = ChooseGroupSize(
                    random,
                    remainingRegular,
                    groupOfOneWeight,
                    groupOfTwoWeight,
                    groupOfThreeWeight);
                Vector2 direction = gridIndex++ % 2 == 0 ? Vector2.right : Vector2.up;
                if (!TryAppendRegularCluster(
                        center,
                        direction,
                        groupSize,
                        area,
                        playerPosition,
                        playerSafeRadius,
                        localDensityRadius,
                        clearance,
                        positions,
                        slots))
                {
                    continue;
                }

                centers.Add(center);
                regularGroupCount++;
                remainingRegular -= groupSize;
            }
        }

        return remainingSpecial == 0 && remainingRegular == 0;
    }

    private static int AppendValidFixedSpecials(
        int maximumCount,
        IReadOnlyList<SpecialSheepSpawnPoint> fixedPoints,
        Rect area,
        Vector2 playerPosition,
        float playerSafeRadius,
        float minimumClusterDistance,
        float localDensityRadius,
        float clearance,
        List<Vector2> positions,
        List<Vector2> centers,
        List<SpawnSlot> slots)
    {
        if (fixedPoints == null || maximumCount <= 0)
            return 0;

        int appended = 0;
        foreach (SpecialSheepSpawnPoint point in fixedPoints)
        {
            if (appended >= maximumCount)
                break;
            if (point == null || !point.isActiveAndEnabled)
                continue;

            Vector2 candidate = point.Position;
            if (!FarEnoughFromCenters(candidate, centers, minimumClusterDistance))
                continue;
            if (!IsValidPosition(
                    candidate,
                    area,
                    playerPosition,
                    playerSafeRadius,
                    localDensityRadius,
                    clearance,
                    positions))
            {
                continue;
            }

            positions.Add(candidate);
            centers.Add(candidate);
            slots.Add(new SpawnSlot(candidate, true, point.PrefabOverride));
            appended++;
        }

        return appended;
    }

    private static bool TryAppendRandomSingleton(
        Random random,
        bool isSpecial,
        Rect area,
        Vector2 playerPosition,
        float playerSafeRadius,
        float clusterRadius,
        float minimumClusterDistance,
        float localDensityRadius,
        float clearance,
        List<Vector2> positions,
        List<Vector2> centers,
        List<SpawnSlot> slots)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            Vector2 candidate = RandomPoint(random, area, clusterRadius + clearance);
            if (!FarEnoughFromCenters(candidate, centers, minimumClusterDistance))
                continue;
            if (!IsValidPosition(
                    candidate,
                    area,
                    playerPosition,
                    playerSafeRadius,
                    localDensityRadius,
                    clearance,
                    positions))
            {
                continue;
            }

            positions.Add(candidate);
            centers.Add(candidate);
            slots.Add(new SpawnSlot(candidate, isSpecial));
            return true;
        }

        return false;
    }

    private static bool TryAppendRegularCluster(
        Vector2 center,
        Vector2 direction,
        int groupSize,
        Rect area,
        Vector2 playerPosition,
        float playerSafeRadius,
        float localDensityRadius,
        float clearance,
        List<Vector2> positions,
        List<SpawnSlot> slots)
    {
        int originalPositionCount = positions.Count;
        int originalSlotCount = slots.Count;
        float spacing = clearance * 2f;
        List<Vector2> candidates = BuildClusterPositions(center, direction, groupSize, spacing);

        foreach (Vector2 candidate in candidates)
        {
            if (!IsValidPosition(
                    candidate,
                    area,
                    playerPosition,
                    playerSafeRadius,
                    localDensityRadius,
                    clearance,
                    positions))
            {
                positions.RemoveRange(originalPositionCount, positions.Count - originalPositionCount);
                slots.RemoveRange(originalSlotCount, slots.Count - originalSlotCount);
                return false;
            }

            positions.Add(candidate);
            slots.Add(new SpawnSlot(candidate, false));
        }

        return true;
    }

    private static List<Vector2> BuildClusterPositions(
        Vector2 center,
        Vector2 direction,
        int groupSize,
        float spacing)
    {
        List<Vector2> result = new(groupSize);
        if (groupSize <= 1)
        {
            result.Add(center);
            return result;
        }

        if (groupSize == 2)
        {
            result.Add(center - direction * spacing * 0.5f);
            result.Add(center + direction * spacing * 0.5f);
            return result;
        }

        float startingAngle = Mathf.Atan2(direction.y, direction.x);
        float radius = spacing / Mathf.Sqrt(3f);
        for (int i = 0; i < 3; i++)
        {
            float angle = startingAngle + i * Mathf.PI * 2f / 3f;
            result.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        return result;
    }

    private static int ChooseGroupSize(
        Random random,
        int remaining,
        float groupOfOneWeight,
        float groupOfTwoWeight,
        float groupOfThreeWeight)
    {
        float weightOne = Mathf.Max(0f, groupOfOneWeight);
        float weightTwo = remaining >= 2 ? Mathf.Max(0f, groupOfTwoWeight) : 0f;
        float weightThree = remaining >= 3 ? Mathf.Max(0f, groupOfThreeWeight) : 0f;
        float totalWeight = weightOne + weightTwo + weightThree;

        if (totalWeight <= 0f)
            return 1;

        double choice = random.NextDouble() * totalWeight;
        if (choice < weightOne)
            return 1;
        if (choice < weightOne + weightTwo)
            return 2;
        return 3;
    }

    private static bool IsValidPosition(
        Vector2 candidate,
        Rect area,
        Vector2 playerPosition,
        float playerSafeRadius,
        float localDensityRadius,
        float clearance,
        List<Vector2> placed)
    {
        if (!area.Contains(candidate) || Vector2.Distance(candidate, playerPosition) < playerSafeRadius)
            return false;

        int nearby = 0;
        foreach (Vector2 existing in placed)
        {
            float distance = Vector2.Distance(candidate, existing);
            if (distance < clearance * 2f)
                return false;
            if (distance <= localDensityRadius)
                nearby++;
        }

        if (nearby >= 3)
            return false;

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(candidate, clearance))
        {
            if (hit == null || hit.GetComponentInParent<RecruitableSheep>() != null)
                continue;

            return false;
        }

        return true;
    }

    private static bool FarEnoughFromCenters(
        Vector2 candidate,
        List<Vector2> centers,
        float minimumDistance)
    {
        foreach (Vector2 center in centers)
            if (Vector2.Distance(candidate, center) < minimumDistance) return false;

        return true;
    }

    private static Vector2 RandomPoint(Random random, Rect area, float margin)
    {
        float x = Mathf.Lerp(area.xMin + margin, area.xMax - margin, (float)random.NextDouble());
        float y = Mathf.Lerp(area.yMin + margin, area.yMax - margin, (float)random.NextDouble());
        return new Vector2(x, y);
    }
}

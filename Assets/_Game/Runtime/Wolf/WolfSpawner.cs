using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 以固定间隔在羊群外围随机方向生成一只狼并让它进攻羊群。
/// </summary>
[DisallowMultipleComponent]
public sealed class WolfSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlockController flock;
    [SerializeField] private Wolf wolfPrefab;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float firstSpawnDelay = 3f;
    [SerializeField, Min(0.1f)] private float spawnInterval = 6f;
    [Tooltip("勾选后必须等上一只狼离开才会生成下一只。")]
    [SerializeField] private bool waitForPreviousWolf;
    [SerializeField, Min(1)] private int maxAliveWolves = 3;
    [Tooltip("策划可调：每只狼生成后额外增加的冲锋前预警秒数；只在本次运行时读取。")]
    [SerializeField, Min(0f)] private float additionalWarningLeadTime = 0.8f;

    [Header("Placement")]
    [Tooltip("狼出现的位置离羊群中心的最小距离。")]
    [SerializeField, Min(1f)] private float spawnDistance = 11f;
    [Tooltip("勾选后狼一定在镜头外生成：沿出发方向推到镜头矩形之外再加 offscreenMargin。")]
    [SerializeField] private bool spawnOffscreen = true;
    [Tooltip("镜头外再多留多少世界单位，保证狼身和预警条起点都在画面外。")]
    [SerializeField, Min(0f)] private float offscreenMargin = 2.5f;
    [Tooltip("用来判断画面范围的镜头；留空用 Camera.main。")]
    [SerializeField] private Camera viewCamera;

    [Header("Smart Wolves")]
    [Tooltip("狼群共享的躲避记忆：记最近多少次进攻。")]
    [SerializeField, Min(1)] private int dodgeMemoryCapacity = 20;

    private readonly List<Wolf> aliveWolves = new List<Wolf>();
    private WolfDodgeMemory dodgeMemory;
    private float timer;

    /// <summary>这一局所有狼共享的躲避记忆。</summary>
    public WolfDodgeMemory DodgeMemory => dodgeMemory ??= new WolfDodgeMemory(dodgeMemoryCapacity);

    public FlockController Flock => flock;
    public Wolf WolfPrefab => wolfPrefab;
    public float AdditionalWarningLeadTime => Mathf.Max(0f, additionalWarningLeadTime);

    /// <summary>之后生成的每只狼都会乘这个速度倍率（随羊群规模增长，由节奏控制器设置）。</summary>
    public float SpeedScale { get; private set; } = 1f;

    public void SetSpeedScale(float scale)
    {
        SpeedScale = Mathf.Max(0.1f, scale);
    }
    public bool IsSpawning { get; private set; } = true;
    public int AliveCount => aliveWolves.Count;
    public int SpawnedCount { get; private set; }
    public IReadOnlyList<Wolf> AliveWolves => aliveWolves;

    public event Action<Wolf> WolfSpawned;

    private void Awake()
    {
        timer = firstSpawnDelay;
    }

    private void Update()
    {
        if (!IsSpawning || flock == null || wolfPrefab == null || Time.timeScale == 0f)
            return;

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        if (waitForPreviousWolf && aliveWolves.Count > 0)
            return;

        if (aliveWolves.Count >= maxAliveWolves)
            return;

        timer = spawnInterval;
        SpawnWolf();
    }

    public void StartSpawning()
    {
        IsSpawning = true;
    }

    public void StopSpawning()
    {
        IsSpawning = false;
    }

    public float SpawnDistance => spawnDistance;

    /// <summary>
    /// 从 <paramref name="from"/> 沿 <paramref name="outward"/> 走多远才算"在镜头外"：
    /// 至少 spawnDistance；开启 spawnOffscreen 时还要越过镜头矩形再加 offscreenMargin。
    /// </summary>
    public float GetSpawnDistance(Vector2 from, Vector2 outward)
    {
        if (!spawnOffscreen)
            return spawnDistance;

        Camera camera = viewCamera != null ? viewCamera : Camera.main;
        if (camera == null || !camera.orthographic || outward.sqrMagnitude < 0.0001f)
            return spawnDistance;

        outward.Normalize();
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        Vector2 center = camera.transform.position;

        float exitX = float.PositiveInfinity;
        if (Mathf.Abs(outward.x) > 0.0001f)
        {
            float edge = outward.x > 0f ? center.x + halfWidth : center.x - halfWidth;
            exitX = (edge - from.x) / outward.x;
        }
        float exitY = float.PositiveInfinity;
        if (Mathf.Abs(outward.y) > 0.0001f)
        {
            float edge = outward.y > 0f ? center.y + halfHeight : center.y - halfHeight;
            exitY = (edge - from.y) / outward.y;
        }

        // 起点已经在某个轴的画面外时对应距离为负，取 0。
        float exit = Mathf.Max(0f, Mathf.Min(exitX, exitY));
        return Mathf.Max(spawnDistance, exit + offscreenMargin);
    }

    public Wolf SpawnWolf()
    {
        return SpawnWolf(true, false);
    }

    /// <param name="allowPrediction">false = 只会直线攻击的狼。</param>
    /// <param name="scared">true = 被吓跑的狼：露面后掉头逃跑，碰到羊群会被踹飞。</param>
    public Wolf SpawnWolf(bool allowPrediction, bool scared)
    {
        if (wolfPrefab == null || flock == null)
            return null;

        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector2.right;
        }

        Vector2 position = flock.Center + direction * GetSpawnDistance(flock.Center, direction);
        Wolf wolf = Register(Instantiate(wolfPrefab, position, Quaternion.identity));
        if (scared)
            wolf.LaunchScared(flock, DodgeMemory);
        else
            wolf.Launch(flock, DodgeMemory, allowPrediction);
        WolfSpawned?.Invoke(wolf);
        return wolf;
    }

    /// <summary>
    /// 编队用：在 <paramref name="origin"/> 生成一只狼，沿 <paramref name="direction"/> 直冲 <paramref name="travelDistance"/>。
    /// <paramref name="prefab"/> 为空时用默认狼。
    /// </summary>
    public Wolf SpawnWolfAlong(
        Wolf prefab,
        Vector2 origin,
        Vector2 direction,
        float travelDistance,
        float warningDurationOverride = 0f,
        float chargeSpeedOverride = 0f)
    {
        Wolf source = prefab != null ? prefab : wolfPrefab;
        if (source == null || flock == null)
            return null;

        Wolf wolf = Register(Instantiate(source, origin, Quaternion.identity));
        wolf.LaunchAlong(flock, direction, travelDistance, DodgeMemory, warningDurationOverride, chargeSpeedOverride);
        WolfSpawned?.Invoke(wolf);
        return wolf;
    }

    private Wolf Register(Wolf wolf)
    {
        wolf.name = $"Wolf_{SpawnedCount + 1:00}";
        wolf.SetSpeedScale(SpeedScale);
        wolf.SetAdditionalWarningLeadTime(AdditionalWarningLeadTime);
        wolf.Finished += HandleWolfFinished;
        aliveWolves.Add(wolf);
        SpawnedCount++;
        return wolf;
    }

    private void HandleWolfFinished(Wolf wolf)
    {
        aliveWolves.Remove(wolf);
    }
}

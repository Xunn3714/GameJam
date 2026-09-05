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

    [Header("Placement")]
    [Tooltip("狼出现的位置离羊群中心的距离。")]
    [SerializeField, Min(1f)] private float spawnDistance = 11f;

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

    public Wolf SpawnWolf()
    {
        if (wolfPrefab == null || flock == null)
            return null;

        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector2.right;
        }

        Vector2 position = flock.Center + direction * spawnDistance;
        Wolf wolf = Instantiate(wolfPrefab, position, Quaternion.identity);
        wolf.name = $"Wolf_{SpawnedCount + 1:00}";
        wolf.Finished += HandleWolfFinished;
        aliveWolves.Add(wolf);
        SpawnedCount++;

        wolf.Launch(flock, DodgeMemory);
        WolfSpawned?.Invoke(wolf);
        return wolf;
    }

    private void HandleWolfFinished(Wolf wolf)
    {
        aliveWolves.Remove(wolf);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 执行一次多狼协作进攻：按编队类型算出每只狼的出发点、方向和出场时间，通过 WolfSpawner 放出来，
/// 直到所有狼离场才算结束。狼的行为本身仍由 Wolf 负责。
/// </summary>
[DisallowMultipleComponent]
public sealed class WolfFormationRunner : MonoBehaviour
{
    [SerializeField] private WolfSpawner spawner;

    private readonly List<Wolf> activeWolves = new List<Wolf>();
    private Coroutine running;
    private bool launching;

    public bool IsRunning
    {
        get
        {
            PruneDestroyedWolves();
            return running != null || launching || activeWolves.Count > 0;
        }
    }
    public int ActiveCount
    {
        get
        {
            PruneDestroyedWolves();
            return activeWolves.Count;
        }
    }
    public WolfFormation Current { get; private set; }

    /// <summary>编队里每只狼被放出来时触发。</summary>
    public event Action<Wolf> WolfLaunched;
    /// <summary>整个编队的狼全部离场后触发。</summary>
    public event Action<WolfFormation> Completed;

    private void Awake()
    {
        if (spawner == null)
        {
            spawner = GetComponent<WolfSpawner>();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play(WolfFormation formation)
    {
        if (formation == null || spawner == null || spawner.Flock == null)
        {
            Debug.LogWarning("WolfFormationRunner.Play needs a formation, a WolfSpawner and a flock.", this);
            return;
        }

        Stop();
        Current = formation;
        running = StartCoroutine(Run(formation));
    }

    /// <summary>停止继续放狼；已经在场上的狼不受影响。</summary>
    public void Stop()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }
        launching = false;
        Current = null;
    }

    /// <summary>停止继续放狼，并让已经生成的狼停止命中后安全离场。</summary>
    public void AbortAndRetreat()
    {
        Stop();
        for (int index = activeWolves.Count - 1; index >= 0; index--)
        {
            Wolf wolf = activeWolves[index];
            if (wolf == null)
                activeWolves.RemoveAt(index);
            else
                wolf.ForceRetreat();
        }
    }

    private IEnumerator Run(WolfFormation formation)
    {
        launching = true;
        switch (formation.type)
        {
            case WolfFormationType.ParallelSimultaneous:
                yield return RunParallel(formation, false);
                break;
            case WolfFormationType.ParallelSequential:
                yield return RunParallel(formation, true);
                break;
            case WolfFormationType.LongWolfWithEscorts:
                yield return RunEscorts(formation);
                break;
            case WolfFormationType.Pentagram:
                yield return RunPentagram(formation);
                break;
            case WolfFormationType.PerpendicularChain:
                yield return RunPerpendicularChain(formation);
                break;
            case WolfFormationType.SingleLong:
            {
                Wolf longPrefab = formation.longWolfPrefab != null ? formation.longWolfPrefab : formation.wolfPrefab;
                Track(SpawnLane(longPrefab, RandomDirection(), 0f));
                break;
            }
            default:
                Track(spawner.SpawnWolf());
                break;
        }
        launching = false;

        // 等最后一只狼离场。
        while (activeWolves.Count > 0)
        {
            PruneDestroyedWolves();
            yield return null;
        }

        if (Current == formation)
            Current = null;
        running = null;
        Completed?.Invoke(formation);
    }

    // ------------------------------------------------------------ formations

    /// <summary>N 条平行路线，全部同向；同时或按随机顺序依次放出。</summary>
    private IEnumerator RunParallel(WolfFormation formation, bool sequential)
    {
        Vector2 direction = RandomDirection();
        Wolf prefab = formation.longWolfPrefab != null ? formation.longWolfPrefab : formation.wolfPrefab;
        int count = Mathf.Max(1, formation.count);

        int[] order = new int[count];
        for (int index = 0; index < count; index++)
            order[index] = index;
        if (sequential)
            Shuffle(order);

        for (int step = 0; step < count; step++)
        {
            float offset = (order[step] - (count - 1) * 0.5f) * formation.laneSpacing;
            Track(SpawnLane(prefab, direction, offset));
            if (sequential && step < count - 1 && formation.sequentialDelay > 0f)
                yield return new WaitForSeconds(formation.sequentialDelay);
        }
    }

    /// <summary>
    /// 长狼包夹：长狼生成那一刻记下羊群中心，并根据羊群移动方向判断玩家在长狼路线的哪一侧。
    /// 稍后三只普通狼呈扇形从"玩家那一侧"（escortSameSideChance）或"另一侧"冲向记下的玩家位置。
    /// </summary>
    private IEnumerator RunEscorts(WolfFormation formation)
    {
        FlockController flock = spawner.Flock;
        Vector2 direction = RandomDirection();
        Wolf longPrefab = formation.longWolfPrefab != null ? formation.longWolfPrefab : formation.wolfPrefab;
        Track(SpawnLane(longPrefab, direction, 0f, formation.escortLongWolfWarningDuration));

        // 当前玩法没有固定头羊；玩家位置就是羊群中心。
        Vector2 playerPosition = flock.Center;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float playerSide = DeterminePlayerSide(flock, perpendicular);
        bool sameSide = UnityEngine.Random.value < formation.escortSameSideChance;
        float chosenSide = sameSide ? playerSide : -playerSide;

        if (formation.escortDelay > 0f)
            yield return new WaitForSeconds(formation.escortDelay);

        // 扇形：三只狼从所选侧出发，冲向玩家位置，方向以"垂直于长狼路线"为中心左右各错开一个夹角。
        Vector2 inward = -perpendicular * chosenSide;
        float[] fanAngles = { -formation.escortFanSpread, 0f, formation.escortFanSpread };
        foreach (float angle in fanAngles)
        {
            Vector2 chargeDirection = Rotate(inward, angle);
            float approachDistance = spawner.GetSpawnDistance(playerPosition, -chargeDirection);
            float departureDistance = spawner.GetSpawnDistance(playerPosition, chargeDirection);
            Vector2 origin = playerPosition - chargeDirection * approachDistance;
            Track(spawner.SpawnWolfAlong(
                formation.wolfPrefab,
                origin,
                chargeDirection,
                approachDistance + departureDistance));
        }
    }

    /// <summary>玩家在路线哪一侧（+1 = perpendicular 那一侧）。先看羊群运动方向，不明显时随机。</summary>
    private static float DeterminePlayerSide(FlockController flock, Vector2 perpendicular)
    {
        Vector2 velocity = flock.MovementVelocity;
        float moving = Vector2.Dot(velocity, perpendicular);
        if (Mathf.Abs(moving) > 0.3f)
            return Mathf.Sign(moving);

        return UnityEngine.Random.value < 0.5f ? -1f : 1f;
    }

    /// <summary>
    /// 五角星：核心五个顶点始终使用固定世界半径，不随镜头缩放。
    /// 狼的预警实体先放在画外以提供方向 UI；正式冲锋从核心顶点开始，镜头不改变进场时机或星形尺寸。
    /// </summary>
    private IEnumerator RunPentagram(WolfFormation formation)
    {
        Vector2 center = spawner.Flock.Center;
        float baseAngle = UnityEngine.Random.Range(0f, 360f);
        Vector2[] vertices = CreatePentagramVertices(center, formation.pentagramRadius, baseAngle);

        int[] path = { 0, 2, 4, 1, 3 };
        Wolf prefab = formation.pentagramUsesLongWolves && formation.longWolfPrefab != null
            ? formation.longWolfPrefab
            : formation.wolfPrefab;

        for (int step = 0; step < 5; step++)
        {
            Vector2 from = vertices[path[step]];
            Vector2 to = vertices[path[(step + 1) % 5]];
            Vector2 edge = to - from;
            Vector2 direction = edge.normalized;
            float approachDistance = spawner.GetSpawnDistance(from, -direction);
            Vector2 offscreenOrigin = from - direction * approachDistance;
            Track(spawner.SpawnWolfAlong(
                prefab, offscreenOrigin, direction, approachDistance + edge.magnitude,
                formation.pentagramWarningDuration, formation.pentagramChargeSpeed,
                approachDistance, edge.magnitude));
            if (step < 4 && formation.pentagramStagger > 0f)
                yield return new WaitForSeconds(formation.pentagramStagger);
        }
    }

    /// <summary>
    /// 直角连击：N 条长狼依次出现，每条比上一条转 90°（转向随机取左或右，整段保持一致），都穿过当时的羊群中心。
    /// 下一条的预警提前出现，使它恰好在上一条冲出 chainHandoffDistance 时结束预警开始冲锋——
    /// 场上始终由相邻两条狼构成一个直角。
    /// </summary>
    private IEnumerator RunPerpendicularChain(WolfFormation formation)
    {
        Wolf prefab = formation.longWolfPrefab != null ? formation.longWolfPrefab : formation.wolfPrefab;
        Vector2 direction = RandomDirection();
        float turn = UnityEngine.Random.value < 0.5f ? 90f : -90f;
        int count = Mathf.Max(1, formation.count);

        for (int step = 0; step < count; step++)
        {
            Vector2 stepDirection = Rotate(direction, turn * step);
            Wolf wolf = SpawnLane(prefab, stepDirection, 0f);
            Track(wolf);
            if (wolf == null || step == count - 1)
                continue;

            // 上一条冲锋开始时刻 + 冲出 handoff 距离所需时间 = 下一条冲锋开始时刻；再减去下一条的预警时长就是它的生成时刻。
            float chargeStart = Time.time + wolf.WarningDuration;
            float handoffSeconds = wolf.ChargeSpeed > 0f ? formation.chainHandoffDistance / wolf.ChargeSpeed : 0f;
            // 下一条使用同一 prefab、阶段速度与统一提前量，直接沿用本条的有效预警时长；
            // 这样阶段速度补偿后直角交接仍落在同一时刻。
            float nextWarning = wolf.WarningDuration;
            float nextSpawnTime = chargeStart + handoffSeconds - nextWarning;

            bool finished = false;
            wolf.Finished += _ => finished = true;
            while (Time.time < nextSpawnTime && !finished && wolf != null)
                yield return null;
        }
    }

    // ------------------------------------------------------------ helpers

    /// <summary>
    /// 沿 direction 冲向羊群的一条路线，lateralOffset 为相对羊群中心的横向偏移。
    /// 出发点沿反方向推到镜头外（见 WolfSpawner.GetSpawnDistance），狼身和预警条起点都不会在画面里凭空出现。
    /// </summary>
    private Wolf SpawnLane(Wolf prefab, Vector2 direction, float lateralOffset, float warningDurationOverride = 0f)
    {
        Vector2 center = spawner.Flock.Center;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        Vector2 laneCenter = center + perpendicular * lateralOffset;
        float approachDistance = spawner.GetSpawnDistance(laneCenter, -direction);
        float departureDistance = spawner.GetSpawnDistance(laneCenter, direction);
        Vector2 origin = laneCenter - direction * approachDistance;
        return spawner.SpawnWolfAlong(
            prefab,
            origin,
            direction,
            approachDistance + departureDistance,
            warningDurationOverride);
    }

    private void Track(Wolf wolf)
    {
        if (wolf == null)
            return;

        activeWolves.Add(wolf);
        wolf.Finished += HandleWolfFinished;
        WolfLaunched?.Invoke(wolf);
    }

    private void HandleWolfFinished(Wolf wolf)
    {
        activeWolves.Remove(wolf);
    }

    private void PruneDestroyedWolves()
    {
        for (int index = activeWolves.Count - 1; index >= 0; index--)
        {
            if (activeWolves[index] == null)
                activeWolves.RemoveAt(index);
        }
    }

    private static Vector2 RandomDirection()
    {
        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
        return direction.sqrMagnitude < 0.001f ? Vector2.right : direction;
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    public static Vector2[] CreatePentagramVertices(Vector2 center, float radius, float baseAngle)
    {
        radius = Mathf.Max(1f, radius);
        Vector2[] vertices = new Vector2[5];
        for (int index = 0; index < vertices.Length; index++)
        {
            float radians = (baseAngle + index * 72f) * Mathf.Deg2Rad;
            vertices[index] = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }
        return vertices;
    }

    private static void Shuffle(int[] values)
    {
        for (int index = values.Length - 1; index > 0; index--)
        {
            int swap = UnityEngine.Random.Range(0, index + 1);
            (values[index], values[swap]) = (values[swap], values[index]);
        }
    }
}

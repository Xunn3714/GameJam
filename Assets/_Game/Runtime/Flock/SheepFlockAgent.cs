using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class SheepFlockAgent : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float maximumSpeed = 5.2f;
    [SerializeField, Min(0f)] private float acceleration = 14f;
    [SerializeField, Min(0f)] private float idleBraking = 18f;

    [Header("Flock Shape")]
    [Tooltip("羊群只有 1 只羊时的舒适半径；羊越多半径按 radiusGrowthPerSheep * sqrt(羊数-1) 变大。")]
    [SerializeField, Min(0f)] private float comfortableRadius = 1f;
    [SerializeField, Min(0f)] private float radiusGrowthPerSheep = 0.8f;
    [Tooltip("两只羊之间开始互相推开的距离，建议 ≈ 羊贴图的宽度。")]
    [SerializeField, Min(0f)] private float separationRadius = 1.9f;
    [SerializeField, Min(0f)] private float cohesionWeight = 2.2f;
    [SerializeField, Min(0f)] private float separationWeight = 4f;
    [SerializeField, Min(0f)] private float alignmentWeight = 0.35f;

    [Header("Leader Ripple")]
    [Tooltip("领头羊贴着羊群中心走的半径。")]
    [SerializeField, Min(0f)] private float leaderRadius = 0.1f;
    [Tooltip("离领头羊每远 1 单位，跟随输入就晚多少秒。")]
    [SerializeField, Min(0f)] private float followDelayPerUnit = 0.14f;
    [SerializeField, Min(0f)] private float maximumFollowDelay = 0.8f;
    [Tooltip("延迟值的变化速度（秒/秒），避免羊在群里换位置时突然抖动。")]
    [SerializeField, Min(0f)] private float followDelayAdjustSpeed = 2f;
    [SerializeField, Range(0f, 1f)] private float leaderWanderScale = 0.2f;

    [Header("Organic Wander")]
    [SerializeField, Min(0f)] private float wanderStrength = 0.7f;
    [SerializeField, Min(0f)] private float wanderTurnSpeed = 1.6f;
    [SerializeField, Min(0.1f)] private float minimumWanderDuration = 0.55f;
    [SerializeField, Min(0.1f)] private float maximumWanderDuration = 1.25f;

    [Header("Settling")]
    [SerializeField, Min(0f)] private float idleCorrectionMargin = 0.18f;
    [SerializeField, Min(0f)] private float stopSpeed = 0.025f;

    [Header("Blocking")]
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField, Min(0f)] private float blockingRadius = 0f;
    [Tooltip("被围栏挡住且离羊群中心较远时，多少秒后直接“翻过去”回到羊群旁。")]
    [SerializeField, Min(0.2f)] private float stuckTimeout = 1.2f;
    [Tooltip("离中心超过舒适半径的这个倍数才算“掉队被卡住”。")]
    [SerializeField, Min(1f)] private float stuckDistanceFactor = 1.5f;

    private Rigidbody2D body;
    private SheepVisualAnimator visualAnimator;
    private FlockController flock;
    private Vector2 velocity;
    private Vector2 wanderDirection;
    private Vector2 targetWanderDirection;
    private Vector2 fallbackSeparationDirection;
    private float wanderTimer;
    private float followDelay;
    private float stuckTimer;
    private float nextImpactFeedbackTime;

    public Vector2 Velocity => velocity;
    public bool IsLeader => flock != null && flock.Leader != null && flock.Leader.gameObject == gameObject;
    public float FollowDelay => followDelay;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        visualAnimator = SheepVisualAnimator.Ensure(gameObject);
        ResetWander();
        if (blockingLayers.value == 0)
            blockingLayers = MovementBlocking.DefaultMask();
        if (blockingRadius <= 0f)
        {
            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            blockingRadius = circle != null ? circle.radius : 0.3f;
        }
    }

    public void SetFlock(FlockController owner)
    {
        flock = owner;
        velocity = Vector2.zero;
        followDelay = 0f;
        ResetWander();
        enabled = flock != null;
    }

    private void FixedUpdate()
    {
        if (flock == null || body == null || Time.timeScale == 0f)
            return;

        float deltaTime = Time.fixedDeltaTime;
        bool isLeader = IsLeader;

        // 离领头羊越远，跟随玩家输入就越晚：移动从领头羊开始一圈圈向外扩散。
        float targetDelay = isLeader
            ? 0f
            : Mathf.Min((flock.Center - body.position).magnitude * followDelayPerUnit, maximumFollowDelay);
        followDelay = Mathf.MoveTowards(followDelay, targetDelay, followDelayAdjustSpeed * deltaTime);

        Vector2 driveVelocity = flock.GetMovementVelocity(followDelay);
        bool flockIsMoving = driveVelocity.sqrMagnitude > 0.0001f;
        float speedScale = flock.SpeedMultiplier;
        Vector2 steeringVelocity = CalculateSteeringVelocity(driveVelocity, flockIsMoving, isLeader, deltaTime);
        float response = (flockIsMoving ? acceleration : idleBraking) * speedScale;
        velocity = Vector2.MoveTowards(velocity, steeringVelocity, response * deltaTime);

        if (!flockIsMoving
            && steeringVelocity.sqrMagnitude <= stopSpeed * stopSpeed
            && velocity.sqrMagnitude <= stopSpeed * stopSpeed)
        {
            velocity = Vector2.zero;
            return;
        }

        Vector2 from = body.position;
        Vector2 desiredStep = velocity * deltaTime;
        Vector2 target = MovementBlocking.ResolveMove(
            from,
            from + desiredStep,
            blockingRadius,
            blockingLayers,
            out MovementBlockResult blockResult);

        bool recoilFromHardImpact = false;
        Vector2 impactVelocity = velocity;
        if (blockResult.WasBlocked && Time.time >= nextImpactFeedbackTime)
        {
            bool hardImpact = !CanBreakOnContact(blockResult.Blocker);
            visualAnimator?.PlayObstacleImpact(hardImpact, velocity);
            nextImpactFeedbackTime = Time.time + (hardImpact ? 0.48f : 0.2f);
            recoilFromHardImpact = hardImpact && velocity.sqrMagnitude > 0.001f;
        }

        // 轴向滑动都不行时，沿垂直于前进方向、更靠近羊群中心的那一侧贴着障碍走。
        if (target == from && desiredStep.sqrMagnitude > 0.000001f)
        {
            target = TrySlideAroundObstacle(from, desiredStep);
        }

        UpdateStuckState(from, desiredStep, target, deltaTime);

        // 把实际走出去的位移反算回速度，避免贴墙的羊把"想走但没走成"的速度
        // 通过 alignment 传染给邻居，导致整群往墙里挤。
        velocity = (target - from) / deltaTime;
        if (recoilFromHardImpact)
            velocity = -impactVelocity.normalized * Mathf.Min(0.85f, maximumSpeed * 0.18f);

        if (target == from)
            return;

        body.MovePosition(target);
    }

    /// <summary>撞上的障碍是不是羊群现在就能撞碎的（决定播软 / 硬撞击动画）。</summary>
    private bool CanBreakOnContact(Collider2D blocker)
    {
        if (blocker == null)
            return false;

        BreakableObstacle obstacle = blocker.GetComponentInParent<BreakableObstacle>();
        if (obstacle == null || obstacle.Definition == null)
            return false;

        if (obstacle.Definition.BreakRule == ObstacleBreakRule.OnAnyContact)
            return true;

        if (flock == null)
            return false;

        FenceObstacle fence = obstacle.GetComponent<FenceObstacle>();
        int required = fence != null ? fence.RequiredFlockCount : obstacle.Definition.RequiredFlockCount;
        int count = obstacle.Definition.CountSource == ObstacleCountSource.HighestFlockCountThisRun
            ? flock.HighestMemberCount
            : flock.MemberCount;
        return count >= required;
    }

    private Vector2 TrySlideAroundObstacle(Vector2 from, Vector2 desiredStep)
    {
        Vector2 direction = desiredStep.normalized;
        float length = desiredStep.magnitude * 0.8f;
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        Vector2 toCenter = flock.Center - from;
        if (Vector2.Dot(tangent, toCenter) < 0f)
            tangent = -tangent;

        Vector2 candidate = from + tangent * length;
        if (MovementBlocking.IsFree(candidate, blockingRadius, blockingLayers))
            return candidate;

        candidate = from - tangent * length;
        if (MovementBlocking.IsFree(candidate, blockingRadius, blockingLayers))
            return candidate;

        return from;
    }

    /// <summary>
    /// 被围栏卡住又离羊群太远（例如羊群穿过缺口后它留在另一边）时，
    /// 等一小会儿直接把它挪到羊群中心附近的空位——当作它自己翻过了栏杆。
    /// </summary>
    private void UpdateStuckState(Vector2 from, Vector2 desiredStep, Vector2 target, float deltaTime)
    {
        float wanted = desiredStep.magnitude;
        float achieved = (target - from).magnitude;
        bool blocked = wanted > 0.01f && achieved < wanted * 0.15f;
        float farDistance = GetComfortableRadius() * stuckDistanceFactor + blockingRadius;
        bool farFromFlock = (flock.Center - from).sqrMagnitude > farDistance * farDistance;
        // 离得不远但中间隔着围栏（例如卡在角落、羊群在栏杆另一侧）也算掉队。
        bool separatedByWall = !farFromFlock
            && MovementBlocking.IsLineBlocked(from, flock.Center, blockingLayers);

        if (!blocked || !(farFromFlock || separatedByWall))
        {
            stuckTimer = 0f;
            return;
        }

        stuckTimer += deltaTime;
        if (stuckTimer < stuckTimeout)
            return;

        stuckTimer = 0f;
        if (TryFindFreeSpotNearCenter(out Vector2 spot))
        {
            body.position = spot;
            velocity = Vector2.zero;
        }
    }

    private bool TryFindFreeSpotNearCenter(out Vector2 spot)
    {
        Vector2 center = flock.Center;
        float radius = Mathf.Max(blockingRadius * 2f, GetComfortableRadius() * 0.6f);
        for (int ring = 0; ring < 3; ring++)
        {
            float ringRadius = radius * (0.5f + ring * 0.5f);
            for (int index = 0; index < 8; index++)
            {
                float angle = index * Mathf.PI * 0.25f + ring * 0.3f;
                Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                if (MovementBlocking.IsFree(candidate, blockingRadius, blockingLayers))
                {
                    spot = candidate;
                    return true;
                }
            }
        }

        spot = center;
        return MovementBlocking.IsFree(center, blockingRadius, blockingLayers);
    }

    private Vector2 CalculateSteeringVelocity(
        Vector2 driveVelocity,
        bool flockIsMoving,
        bool isLeader,
        float deltaTime)
    {
        Vector2 position = body.position;
        Vector2 desiredVelocity = driveVelocity;
        Vector2 centerOffset = flock.Center - position;
        float centerDistance = centerOffset.magnitude;

        float activeRadius = isLeader ? leaderRadius : GetComfortableRadius();
        if (!flockIsMoving)
        {
            activeRadius += idleCorrectionMargin;
        }
        if (centerDistance > activeRadius)
        {
            float outsideDistance = centerDistance - activeRadius;
            desiredVelocity += centerOffset.normalized
                * Mathf.Min(outsideDistance * cohesionWeight, maximumSpeed * flock.SpeedMultiplier);
        }

        Vector2 separation = Vector2.zero;
        Vector2 averageNeighborVelocity = Vector2.zero;
        int velocityNeighborCount = 0;
        IReadOnlyList<SheepMember> members = flock.Members;
        // 抱团时允许羊挨得更近一些（最多缩到 70%）。
        float activeSeparationRadius = separationRadius * Mathf.Lerp(0.7f, 1f, flock.Compactness);
        float separationRadiusSquared = activeSeparationRadius * activeSeparationRadius;

        for (int index = 0; index < members.Count; index++)
        {
            SheepMember member = members[index];
            if (member == null || member.gameObject == gameObject)
                continue;

            SheepFlockAgent neighbor = member.Agent;
            if (neighbor != null)
            {
                averageNeighborVelocity += neighbor.Velocity;
                velocityNeighborCount++;
            }

            Vector2 away = position - (Vector2)member.transform.position;
            float squaredDistance = away.sqrMagnitude;
            if (squaredDistance >= separationRadiusSquared)
                continue;

            if (squaredDistance <= 0.0001f)
            {
                separation += fallbackSeparationDirection;
                continue;
            }

            float distance = Mathf.Sqrt(squaredDistance);
            float closeness = 1f - distance / activeSeparationRadius;
            float avoidance = closeness + closeness * closeness;
            separation += away / distance * avoidance;
        }

        desiredVelocity += separation * separationWeight;

        if (flockIsMoving)
        {
            if (velocityNeighborCount > 0)
            {
                Vector2 averageVelocity = averageNeighborVelocity / velocityNeighborCount;
                desiredVelocity += (averageVelocity - velocity) * alignmentWeight;
            }

            UpdateWander(deltaTime);
            desiredVelocity += wanderDirection * wanderStrength * (isLeader ? leaderWanderScale : 1f);
        }
        else
        {
            wanderDirection = Vector2.MoveTowards(
                wanderDirection,
                Vector2.zero,
                wanderTurnSpeed * deltaTime);
        }

        return Vector2.ClampMagnitude(desiredVelocity, maximumSpeed * flock.SpeedMultiplier);
    }

    /// <summary>舒适半径随羊数增长，保证羊多了也有足够空间彼此分开。</summary>
    private float GetComfortableRadius()
    {
        int others = Mathf.Max(0, flock.MemberCount - 1);
        return (comfortableRadius + radiusGrowthPerSheep * Mathf.Sqrt(others)) * flock.Compactness;
    }

    private void UpdateWander(float deltaTime)
    {
        wanderTimer -= deltaTime;
        if (wanderTimer <= 0f)
        {
            targetWanderDirection = RandomDirection();
            wanderTimer = Random.Range(minimumWanderDuration, maximumWanderDuration);
        }

        wanderDirection = Vector2.MoveTowards(
            wanderDirection,
            targetWanderDirection,
            wanderTurnSpeed * deltaTime);
    }

    private void ResetWander()
    {
        fallbackSeparationDirection = RandomDirection();
        wanderDirection = RandomDirection();
        targetWanderDirection = RandomDirection();
        wanderTimer = Random.Range(minimumWanderDuration, maximumWanderDuration);
    }

    private static Vector2 RandomDirection()
    {
        Vector2 direction = Random.insideUnitCircle;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }

    private void OnValidate()
    {
        maximumWanderDuration = Mathf.Max(maximumWanderDuration, minimumWanderDuration);
    }
}

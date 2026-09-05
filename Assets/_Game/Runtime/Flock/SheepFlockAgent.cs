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

    private Rigidbody2D body;
    private FlockController flock;
    private Vector2 velocity;
    private Vector2 wanderDirection;
    private Vector2 targetWanderDirection;
    private Vector2 fallbackSeparationDirection;
    private float wanderTimer;
    private float followDelay;

    public Vector2 Velocity => velocity;
    public bool IsLeader => flock != null && flock.Leader != null && flock.Leader.gameObject == gameObject;
    public float FollowDelay => followDelay;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
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
        Vector2 steeringVelocity = CalculateSteeringVelocity(driveVelocity, flockIsMoving, isLeader, deltaTime);
        float response = flockIsMoving ? acceleration : idleBraking;
        velocity = Vector2.MoveTowards(velocity, steeringVelocity, response * deltaTime);

        if (!flockIsMoving
            && steeringVelocity.sqrMagnitude <= stopSpeed * stopSpeed
            && velocity.sqrMagnitude <= stopSpeed * stopSpeed)
        {
            velocity = Vector2.zero;
            return;
        }

        Vector2 from = body.position;
        Vector2 target = MovementBlocking.ResolveMove(
            from,
            from + velocity * deltaTime,
            blockingRadius,
            blockingLayers);

        // 把实际走出去的位移反算回速度，避免贴墙的羊把"想走但没走成"的速度
        // 通过 alignment 传染给邻居，导致整群往墙里挤。
        velocity = (target - from) / deltaTime;

        if (target == from)
            return;

        body.MovePosition(target);
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
                * Mathf.Min(outsideDistance * cohesionWeight, maximumSpeed);
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

        return Vector2.ClampMagnitude(desiredVelocity, maximumSpeed);
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

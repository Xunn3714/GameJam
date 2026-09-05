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
    [Tooltip("只与这个范围内的邻居进行速度对齐，避免大羊群全量互相影响。")]
    [SerializeField, Min(0f)] private float alignmentRadius = 3.2f;
    [Tooltip("小羊群也略微拉长，避免静止时形成标准圆。")]
    [SerializeField, Range(1f, 2f)] private float minimumFlockAspectRatio = 1.12f;
    [Tooltip("大羊群沿移动方向的最大长宽比；占地面积会大致保持不变。")]
    [SerializeField, Range(1f, 2.5f)] private float maximumFlockAspectRatio = 1.55f;
    [SerializeField, Min(2)] private int maximumAspectRatioMemberCount = 100;
    [Tooltip("在统一椭圆上叠加低频边缘起伏，避免轮廓像规则几何图形。")]
    [SerializeField, Range(0f, 0.3f)] private float outlineIrregularity = 0.14f;
    [Tooltip("每只羊允许略微不同的归群边界，形成松散的外沿。")]
    [SerializeField, Range(0f, 0.2f)] private float individualRadiusVariation = 0.08f;
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

    [Header("Facing Response")]
    [Tooltip("持续转向确认后，每只羊用不同的短延迟翻面，避免按中心距离形成圆形波纹。")]
    [SerializeField, Min(0f)] private float minimumFacingReactionDelay = 0.03f;
    [SerializeField, Min(0f)] private float maximumFacingReactionDelay = 0.15f;

    [Header("Organic Wander")]
    [SerializeField, Min(0f)] private float wanderStrength = 0.7f;
    [SerializeField, Min(0f)] private float wanderTurnSpeed = 1.6f;
    [SerializeField, Min(0.1f)] private float minimumWanderDuration = 0.55f;
    [SerializeField, Min(0.1f)] private float maximumWanderDuration = 1.25f;

    [Header("Settling")]
    [SerializeField, Min(0f)] private float idleCorrectionMargin = 0.18f;
    [SerializeField, Min(0f)] private float stopSpeed = 0.025f;

    [Header("Recruited Idle Pacing")]
    [SerializeField, Min(0.1f)] private float minimumIdleStartDelay = 1.2f;
    [SerializeField, Min(0.1f)] private float maximumIdleStartDelay = 3f;
    [SerializeField, Min(0.1f)] private float minimumIdlePauseDuration = 0.7f;
    [SerializeField, Min(0.1f)] private float maximumIdlePauseDuration = 1.8f;
    [SerializeField, Min(0.1f)] private float minimumIdlePaceDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float maximumIdlePaceDuration = 1.8f;
    [SerializeField, Min(0f)] private float idlePaceRadius = 0.8f;
    [SerializeField, Min(0f)] private float idlePaceSpeed = 0.42f;

    [Header("Blocking")]
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField, Min(0f)] private float blockingRadius = 0f;
    [Tooltip("被围栏挡住且离羊群中心较远时，多少秒后直接“翻过去”回到羊群旁。")]
    [SerializeField, Min(0.2f)] private float stuckTimeout = 1.2f;
    [Tooltip("离中心超过舒适半径的这个倍数才算“掉队被卡住”。")]
    [SerializeField, Min(1f)] private float stuckDistanceFactor = 1.5f;

    private Rigidbody2D body;
    private SheepMember member;
    private SheepVisualAnimator visualAnimator;
    private FlockController flock;
    private readonly List<SheepMember> neighborBuffer = new List<SheepMember>(24);
    private Vector2 velocity;
    private Vector2 cachedSteeringVelocity;
    private Vector2 wanderDirection;
    private Vector2 targetWanderDirection;
    private Vector2 fallbackSeparationDirection;
    private float wanderTimer;
    private float followDelay;
    private float stuckTimer;
    private float nextImpactFeedbackTime;
    private Vector2 idleAnchor;
    private Vector2 idleTarget;
    private float idleStateTimer;
    private int simulationSlot;
    private bool idlePaceTowardLeft;
    private bool idleWasAllowed;
    private bool hasCachedSteering;
    private bool wasControllerMoving;
    private bool wasHuddling;
    private float shapeRadiusBias;
    private float facingReactionDelay;
    private float facingReactionCountdown;
    private int observedFacingIntentRevision = -1;
    private bool hasPendingFacingIntent;
    private bool hasAppliedFacingIntent;

    private enum IdlePaceState
    {
        Settling,
        Pausing,
        Pacing,
    }

    private IdlePaceState idlePaceState;

    public Vector2 Velocity => velocity;
    public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
    public bool IsLeader => flock != null && flock.Leader != null && flock.Leader.gameObject == gameObject;
    public float FollowDelay => followDelay;
    public int SimulationSlot => simulationSlot;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        member = GetComponent<SheepMember>();
        visualAnimator = SheepVisualAnimator.Ensure(gameObject);
        shapeRadiusBias = Random.Range(-1f, 1f);
        maximumFacingReactionDelay = Mathf.Max(maximumFacingReactionDelay, minimumFacingReactionDelay);
        facingReactionDelay = Random.Range(minimumFacingReactionDelay, maximumFacingReactionDelay);
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
        cachedSteeringVelocity = Vector2.zero;
        followDelay = 0f;
        hasCachedSteering = false;
        idleWasAllowed = false;
        observedFacingIntentRevision = -1;
        hasPendingFacingIntent = false;
        hasAppliedFacingIntent = false;
        visualAnimator?.ClearFlockFacingIntent();
        wasControllerMoving = owner != null && owner.IsMoving;
        wasHuddling = owner != null && owner.IsHuddling;
        ResetWander();
        BeginIdleSettling();
        enabled = flock != null;
    }

    internal void SetSimulationSlot(int slot)
    {
        simulationSlot = Mathf.Max(0, slot);
    }

    private void FixedUpdate()
    {
        if (flock == null || body == null || Time.timeScale == 0f)
            return;

        float deltaTime = Time.fixedDeltaTime;
        bool isLeader = IsLeader;
        UpdateFacingIntent(deltaTime);

        // 离领头羊越远，跟随玩家输入就越晚：移动从领头羊开始一圈圈向外扩散。
        float targetDelay = isLeader
            ? 0f
            : Mathf.Min((flock.Center - body.position).magnitude * followDelayPerUnit, maximumFollowDelay);
        followDelay = Mathf.MoveTowards(followDelay, targetDelay, followDelayAdjustSpeed * deltaTime);

        Vector2 driveVelocity = flock.GetDelayedDriveVelocity(followDelay);
        bool flockIsMoving = driveVelocity.sqrMagnitude > 0.0001f;
        float speedScale = flock.MemberSpeedMultiplier;
        bool controllerMovementChanged = wasControllerMoving != flock.IsMoving;
        bool huddleChanged = wasHuddling != flock.IsHuddling;
        int steeringInterval = flock.GetSteeringUpdateInterval();
        bool shouldUpdateSteering = !hasCachedSteering
            || controllerMovementChanged
            || huddleChanged
            || flock.ShouldUpdateSteering(simulationSlot, isLeader);
        if (shouldUpdateSteering)
        {
            float steeringDeltaTime = (controllerMovementChanged || huddleChanged)
                ? deltaTime
                : deltaTime * steeringInterval;
            cachedSteeringVelocity = CalculateSteeringVelocity(
                driveVelocity,
                flockIsMoving,
                isLeader,
                steeringDeltaTime);
            hasCachedSteering = true;
        }

        wasControllerMoving = flock.IsMoving;
        wasHuddling = flock.IsHuddling;
        Vector2 steeringVelocity = cachedSteeringVelocity;
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

    private void UpdateFacingIntent(float deltaTime)
    {
        if (!flock.HasActiveFacingIntent)
        {
            hasPendingFacingIntent = false;
            if (hasAppliedFacingIntent)
            {
                visualAnimator?.ClearFlockFacingIntent();
                hasAppliedFacingIntent = false;
            }
            return;
        }

        if (observedFacingIntentRevision != flock.FacingIntentRevision)
        {
            observedFacingIntentRevision = flock.FacingIntentRevision;
            facingReactionCountdown = IsLeader ? 0f : facingReactionDelay;
            hasPendingFacingIntent = true;
        }

        if (!hasPendingFacingIntent)
            return;

        facingReactionCountdown -= deltaTime;
        if (facingReactionCountdown > 0f)
            return;

        hasPendingFacingIntent = false;
        hasAppliedFacingIntent = true;
        visualAnimator?.SetFlockFacingIntent(flock.FacingIntentLeft);
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

        float activeRadius = isLeader ? leaderRadius : GetComfortableRadius();
        if (!flockIsMoving)
        {
            activeRadius += idleCorrectionMargin;
        }
        Vector2 cohesionOffset = CalculateCohesionOffset(
            centerOffset,
            activeRadius,
            isLeader,
            out bool isInsideComfortableShape);
        if (cohesionOffset.sqrMagnitude > 0.0001f)
        {
            float outsideDistance = cohesionOffset.magnitude;
            desiredVelocity += cohesionOffset.normalized
                * Mathf.Min(outsideDistance * cohesionWeight, maximumSpeed * flock.MemberSpeedMultiplier);
        }

        if (!flockIsMoving)
        {
            desiredVelocity += CalculateIdlePacingVelocity(
                isLeader,
                isInsideComfortableShape,
                deltaTime);
        }
        else
        {
            DisableIdlePacing();
        }

        Vector2 separation = Vector2.zero;
        Vector2 averageNeighborVelocity = Vector2.zero;
        int velocityNeighborCount = 0;
        // 抱团或手动收拢时允许羊挨得更近，使 Q 的间距变化清晰可见。
        float activeSeparationRadius = separationRadius * Mathf.Lerp(0.5f, 1f, flock.Compactness);
        float separationRadiusSquared = activeSeparationRadius * activeSeparationRadius;
        float activeAlignmentRadius = Mathf.Max(activeSeparationRadius, alignmentRadius);
        float alignmentRadiusSquared = activeAlignmentRadius * activeAlignmentRadius;
        flock.CollectNearbyMembers(position, activeAlignmentRadius, neighborBuffer);

        for (int index = 0; index < neighborBuffer.Count; index++)
        {
            SheepMember neighborMember = neighborBuffer[index];
            if (neighborMember == null || neighborMember == member)
                continue;

            Vector2 away = position - (Vector2)neighborMember.transform.position;
            float squaredDistance = away.sqrMagnitude;
            SheepFlockAgent neighbor = neighborMember.Agent;
            if (neighbor != null && squaredDistance <= alignmentRadiusSquared)
            {
                averageNeighborVelocity += neighbor.Velocity;
                velocityNeighborCount++;
            }

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

        return Vector2.ClampMagnitude(desiredVelocity, maximumSpeed * flock.MemberSpeedMultiplier);
    }

    private Vector2 CalculateCohesionOffset(
        Vector2 centerOffset,
        float activeRadius,
        bool isLeader,
        out bool isInsideComfortableShape)
    {
        float centerDistance = centerOffset.magnitude;
        if (isLeader || centerDistance <= 0.0001f)
        {
            isInsideComfortableShape = centerDistance <= activeRadius;
            if (isInsideComfortableShape)
                return Vector2.zero;

            return centerOffset.normalized * (centerDistance - activeRadius);
        }

        float shapeStrength = Mathf.InverseLerp(
            4f,
            Mathf.Max(5, maximumAspectRatioMemberCount),
            flock.MemberCount);
        float aspectRatio = Mathf.Lerp(
            minimumFlockAspectRatio,
            Mathf.Max(minimumFlockAspectRatio, maximumFlockAspectRatio),
            shapeStrength);
        float aspectRoot = Mathf.Sqrt(Mathf.Max(1f, aspectRatio));

        Vector2 forward = flock.ShapeForward.sqrMagnitude > 0.0001f
            ? flock.ShapeForward.normalized
            : Vector2.right;
        Vector2 side = new Vector2(-forward.y, forward.x);
        Vector2 fromCenter = -centerOffset;
        float longitudinal = Vector2.Dot(fromCenter, forward);
        float lateral = Vector2.Dot(fromCenter, side);
        float angle = Mathf.Atan2(lateral, longitudinal);

        float sharedOutlineWave =
            Mathf.Sin(angle * 3f + 0.65f) * 0.65f
            + Mathf.Sin(angle * 5f - 1.2f) * 0.35f;
        float huddleShapeScale = Mathf.Lerp(0.35f, 1f, flock.Compactness);
        float outlineScale = 1f
            + sharedOutlineWave * outlineIrregularity * shapeStrength * huddleShapeScale
            + shapeRadiusBias * individualRadiusVariation;
        outlineScale = Mathf.Max(0.7f, outlineScale);

        float longitudinalRadius = Mathf.Max(0.01f, activeRadius * aspectRoot * outlineScale);
        float lateralRadius = Mathf.Max(0.01f, activeRadius / aspectRoot * outlineScale);
        float normalizedDistance = Mathf.Sqrt(
            longitudinal * longitudinal / (longitudinalRadius * longitudinalRadius)
            + lateral * lateral / (lateralRadius * lateralRadius));
        isInsideComfortableShape = normalizedDistance <= 1f;
        if (isInsideComfortableShape)
            return Vector2.zero;

        Vector2 nearestRadialBoundary = fromCenter / normalizedDistance;
        return nearestRadialBoundary - fromCenter;
    }

    private Vector2 CalculateIdlePacingVelocity(
        bool isLeader,
        bool isInsideComfortableShape,
        float deltaTime)
    {
        bool allowed = !isLeader
            && !flock.IsMoving
            && !flock.IsCompressed
            && isInsideComfortableShape
            && flock.CanIdlePace(simulationSlot, false);
        if (!allowed)
        {
            DisableIdlePacing();
            return Vector2.zero;
        }

        if (!idleWasAllowed)
        {
            idleWasAllowed = true;
            BeginIdleSettling();
        }

        idleStateTimer -= deltaTime;
        if (idlePaceState == IdlePaceState.Settling && idleStateTimer <= 0f)
            BeginIdlePacing();
        else if (idlePaceState == IdlePaceState.Pausing && idleStateTimer <= 0f)
            BeginIdlePacing();

        if (idlePaceState != IdlePaceState.Pacing)
            return Vector2.zero;

        Vector2 toTarget = idleTarget - body.position;
        if (idleStateTimer <= 0f || toTarget.sqrMagnitude <= 0.01f)
        {
            BeginIdlePause();
            return Vector2.zero;
        }

        return toTarget.normalized * idlePaceSpeed;
    }

    private void DisableIdlePacing()
    {
        if (!idleWasAllowed && idlePaceState == IdlePaceState.Settling)
            return;

        idleWasAllowed = false;
        BeginIdleSettling();
    }

    private void BeginIdleSettling()
    {
        idlePaceState = IdlePaceState.Settling;
        idleAnchor = body != null ? body.position : (Vector2)transform.position;
        idleTarget = idleAnchor;
        maximumIdleStartDelay = Mathf.Max(maximumIdleStartDelay, minimumIdleStartDelay);
        idleStateTimer = Random.Range(minimumIdleStartDelay, maximumIdleStartDelay);
    }

    private void BeginIdlePacing()
    {
        idlePaceState = IdlePaceState.Pacing;
        idlePaceTowardLeft = !idlePaceTowardLeft;
        float horizontalDistance = Random.Range(idlePaceRadius * 0.55f, idlePaceRadius);
        float horizontalSign = idlePaceTowardLeft ? -1f : 1f;
        idleTarget = idleAnchor + new Vector2(
            horizontalDistance * horizontalSign,
            Random.Range(-idlePaceRadius * 0.25f, idlePaceRadius * 0.25f));
        maximumIdlePaceDuration = Mathf.Max(maximumIdlePaceDuration, minimumIdlePaceDuration);
        idleStateTimer = Random.Range(minimumIdlePaceDuration, maximumIdlePaceDuration);
    }

    private void BeginIdlePause()
    {
        idlePaceState = IdlePaceState.Pausing;
        maximumIdlePauseDuration = Mathf.Max(maximumIdlePauseDuration, minimumIdlePauseDuration);
        idleStateTimer = Random.Range(minimumIdlePauseDuration, maximumIdlePauseDuration);
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
        maximumFacingReactionDelay = Mathf.Max(maximumFacingReactionDelay, minimumFacingReactionDelay);
        maximumFlockAspectRatio = Mathf.Max(minimumFlockAspectRatio, maximumFlockAspectRatio);
        maximumAspectRatioMemberCount = Mathf.Max(5, maximumAspectRatioMemberCount);
        maximumIdleStartDelay = Mathf.Max(maximumIdleStartDelay, minimumIdleStartDelay);
        maximumIdlePauseDuration = Mathf.Max(maximumIdlePauseDuration, minimumIdlePauseDuration);
        maximumIdlePaceDuration = Mathf.Max(maximumIdlePaceDuration, minimumIdlePaceDuration);
    }
}

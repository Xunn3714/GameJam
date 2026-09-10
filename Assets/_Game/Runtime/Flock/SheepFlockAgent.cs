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

    [Header("Group Action")]
    [Tooltip("蓄势停顿时轻微缩小舒适边界，让羊群自然回拢而不是瞬间冻结。")]
    [SerializeField, Range(0f, 0.3f)] private float windupCompression = 0.12f;
    [SerializeField, Min(0f)] private float windupSettleSpeed = 1.35f;

    [Header("Blocking")]
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField, Min(0f)] private float blockingRadius = 0f;

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
    private float nextImpactFeedbackTime;
    private Vector2 idleAnchor;
    private Vector2 idleTarget;
    private float idleStateTimer;
    private int simulationSlot;
    private bool idlePaceTowardLeft;
    private bool idleWasAllowed;
    private bool hasCachedSteering;
    private bool wasControllerMoving;
    private float shapeRadiusBias;
    private float facingReactionDelay;
    private float facingReactionCountdown;
    private int observedFacingIntentRevision = -1;
    private bool hasPendingFacingIntent;
    private bool hasAppliedFacingIntent;
    private bool hasNormalMovementReport;
    private bool wasNormalMovementBlocked;
    private Vector2 normalMovementReportDirection;

    private enum IdlePaceState
    {
        Settling,
        Pausing,
        Pacing,
    }

    private IdlePaceState idlePaceState;

    public Vector2 Velocity => velocity;
    public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
    public bool IsMovementLocked => visualAnimator != null && visualAnimator.IsMovementLocked;
    public int SimulationSlot => simulationSlot;

    internal bool TryGetNormalMovementBlocked(Vector2 direction, out bool blocked)
    {
        blocked = false;
        if (!hasNormalMovementReport || direction.sqrMagnitude <= 0.0001f)
            return false;

        if (Vector2.Dot(normalMovementReportDirection, direction.normalized) < 0.95f)
            return false;

        blocked = wasNormalMovementBlocked;
        return true;
    }

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
        hasCachedSteering = false;
        idleWasAllowed = false;
        observedFacingIntentRevision = -1;
        hasPendingFacingIntent = false;
        hasAppliedFacingIntent = false;
        hasNormalMovementReport = false;
        visualAnimator?.ClearFlockFacingIntent();
        visualAnimator?.SetGroupActionVisual(false);
        wasControllerMoving = owner != null && owner.IsMoving;
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
        visualAnimator?.SetGroupActionVisual(
            flock.IsGroupActionActive,
            flock.IsGroupActionAnticipating);
        UpdateFacingIntent(deltaTime);

        if (flock.IsGroupActionActive)
            hasNormalMovementReport = false;

        if (visualAnimator != null && visualAnimator.IsMovementLocked)
        {
            StopImmediately();
            return;
        }

        if (flock.IsGroupActionFollowThrough)
        {
            UpdateGroupActionFollowThrough(deltaTime);
            return;
        }

        if (flock.IsGroupActionHolding)
        {
            if (flock.IsGroupActionAnticipating)
                UpdateGroupActionHold(deltaTime);
            else
                StopImmediately();
            return;
        }

        // 所有成员直接读取同一份羊群中心速度，不再从某只领头羊向外延迟传播。
        Vector2 driveVelocity = flock.MovementVelocity;
        bool flockIsMoving = driveVelocity.sqrMagnitude > 0.0001f;
        if (!flockIsMoving)
            hasNormalMovementReport = false;
        float speedScale = flock.MemberSpeedMultiplier;
        bool controllerMovementChanged = wasControllerMoving != flock.IsMoving;
        int steeringInterval = flock.GetSteeringUpdateInterval();
        bool shouldUpdateSteering = !hasCachedSteering
            || controllerMovementChanged
            || flock.ShouldUpdateSteering(simulationSlot);
        if (shouldUpdateSteering)
        {
            float steeringDeltaTime = controllerMovementChanged
                ? deltaTime
                : deltaTime * steeringInterval;
            cachedSteeringVelocity = CalculateSteeringVelocity(
                driveVelocity,
                flockIsMoving,
                steeringDeltaTime);
            hasCachedSteering = true;
        }

        wasControllerMoving = flock.IsMoving;
        Vector2 steeringVelocity = cachedSteeringVelocity;
        float response = (flockIsMoving ? acceleration : idleBraking) * speedScale;
        velocity = flock.IsGroupActionActive
            ? steeringVelocity
            : Vector2.MoveTowards(velocity, steeringVelocity, response * deltaTime);

        if (!flockIsMoving
            && steeringVelocity.sqrMagnitude <= stopSpeed * stopSpeed
            && velocity.sqrMagnitude <= stopSpeed * stopSpeed)
        {
            velocity = Vector2.zero;
            return;
        }

        Vector2 from = body.position;
        Vector2 desiredStep = velocity * deltaTime;
        Vector2 target;
        MovementBlockResult blockResult;
        if (flock.IsGroupActionActive)
        {
            // 整群动作会瞬时提高成员速度，必须扫掠整段位移，
            // 否则终点重叠检查可能跨过较薄的围栏。主动动作期间也不应贴墙滑动。
            target = MovementBlocking.ResolveDashMove(
                from,
                from + desiredStep,
                blockingRadius,
                blockingLayers,
                out blockResult);
        }
        else
        {
            target = MovementBlocking.ResolveMove(
                from,
                from + desiredStep,
                blockingRadius,
                blockingLayers,
                out blockResult);
        }

        if (blockResult.WasBlocked)
        {
            if (flock.IsGroupActionActive)
            {
                Vector2 blockerPoint = blockResult.Blocker != null
                    ? blockResult.Blocker.ClosestPoint(from)
                    : from + desiredStep;
                bool stopsGroup = FlockActionController.ShouldMemberBlockStopAction(
                    flock.Center,
                    flock.GroupActionDirection,
                    desiredStep,
                    blockerPoint);
                if (stopsGroup)
                {
                    flock.ReportGroupActionMemberBlocked(blockResult.Blocker);
                    StopImmediately();
                    return;
                }

                // 后撤碰墙或后排成员从接触面向前脱离时，不让零距离 Sweep 命中
                // 把整群锁死。回退到普通解析，让该成员停住、滑边或走出身后障碍。
                target = MovementBlocking.ResolveMove(
                    from,
                    from + desiredStep,
                    blockingRadius,
                    blockingLayers,
                    out _);
            }
            else
            {
                bool hardImpact = !CanBreakOnContact(blockResult.Blocker);
                bool shouldRequestAnimation = hardImpact || Time.time >= nextImpactFeedbackTime;
                bool animationStarted = shouldRequestAnimation
                    && visualAnimator != null
                    && visualAnimator.PlayObstacleImpact(hardImpact, velocity);
                if (!hardImpact && animationStarted)
                    nextImpactFeedbackTime = Time.time + 0.2f;

                if (hardImpact && visualAnimator != null && visualAnimator.IsMovementLocked)
                {
                    RecordNormalMovementResult(driveVelocity, target - from);
                    StopImmediately();
                    return;
                }
            }
        }

        // 轴向滑动都不行时，沿垂直于前进方向、更靠近羊群中心的那一侧贴着障碍走。
        if (target == from && desiredStep.sqrMagnitude > 0.000001f)
        {
            target = TrySlideAroundObstacle(from, desiredStep);
        }

        RecordNormalMovementResult(driveVelocity, target - from);

        // 把实际走出去的位移反算回速度，避免贴墙的羊把"想走但没走成"的速度
        // 通过 alignment 传染给邻居，导致整群往墙里挤。
        velocity = (target - from) / deltaTime;

        if (target == from)
            return;

        body.MovePosition(target);
    }

    private void RecordNormalMovementResult(Vector2 driveVelocity, Vector2 actualDisplacement)
    {
        if (driveVelocity.sqrMagnitude <= 0.0001f)
        {
            hasNormalMovementReport = false;
            return;
        }

        Vector2 driveDirection = driveVelocity.normalized;
        hasNormalMovementReport = true;
        normalMovementReportDirection = driveDirection;
        wasNormalMovementBlocked = actualDisplacement.sqrMagnitude <= 0.000001f
            || Vector2.Dot(actualDisplacement, driveDirection) < -0.000001f;
    }

    private void StopImmediately()
    {
        velocity = Vector2.zero;
        cachedSteeringVelocity = Vector2.zero;
        hasCachedSteering = false;
    }

    private void UpdateGroupActionHold(float deltaTime)
    {
        Vector2 from = body.position;
        float activeRadius = GetComfortableRadius() * (1f - Mathf.Clamp01(windupCompression));
        Vector2 cohesionOffset = CalculateCohesionOffset(
            flock.Center - from,
            activeRadius,
            out _);
        float maximumSettleSpeed = Mathf.Max(0f, windupSettleSpeed) * flock.MemberSpeedMultiplier;
        Vector2 targetVelocity = Vector2.ClampMagnitude(
            cohesionOffset * cohesionWeight,
            maximumSettleSpeed);
        float response = Mathf.Max(0f, idleBraking) * flock.MemberSpeedMultiplier;
        velocity = Vector2.MoveTowards(velocity, targetVelocity, response * deltaTime);
        cachedSteeringVelocity = velocity;
        hasCachedSteering = true;
        wasControllerMoving = false;

        if (velocity.sqrMagnitude <= stopSpeed * stopSpeed)
        {
            velocity = Vector2.zero;
            return;
        }

        Vector2 desiredStep = velocity * deltaTime;
        Vector2 target = MovementBlocking.ResolveMove(
            from,
            from + desiredStep,
            blockingRadius,
            blockingLayers);
        if (target == from)
            target = TrySlideAroundObstacle(from, desiredStep);

        velocity = (target - from) / Mathf.Max(deltaTime, 0.0001f);
        if (target != from)
            body.MovePosition(target);
    }

    private void UpdateGroupActionFollowThrough(float deltaTime)
    {
        Vector2 forward = flock.GroupActionDirection.sqrMagnitude > 0.0001f
            ? flock.GroupActionDirection.normalized
            : Vector2.right;
        Vector2 driveVelocity = forward * flock.GroupActionFollowThroughSpeed;
        velocity = CalculateSteeringVelocity(driveVelocity, true, deltaTime);
        cachedSteeringVelocity = velocity;
        hasCachedSteering = true;
        wasControllerMoving = true;

        Vector2 from = body.position;
        Vector2 desiredStep = velocity * deltaTime;
        Vector2 target = MovementBlocking.ResolveDashMove(
            from,
            from + desiredStep,
            blockingRadius,
            blockingLayers,
            out MovementBlockResult blockResult);
        if (blockResult.WasBlocked)
        {
            bool hardImpact = flock.GroupActionFollowThroughHardImpact;
            bool shouldRequestAnimation = hardImpact || Time.time >= nextImpactFeedbackTime;
            bool animationStarted = shouldRequestAnimation
                && visualAnimator != null
                && visualAnimator.PlayObstacleImpact(hardImpact, forward);
            if (!hardImpact && animationStarted)
                nextImpactFeedbackTime = Time.time + 0.2f;

            StopImmediately();
            return;
        }

        velocity = (target - from) / Mathf.Max(deltaTime, 0.0001f);
        if (target != from)
            body.MovePosition(target);
    }

    private void UpdateFacingIntent(float deltaTime)
    {
        if (flock.IsGroupActionActive)
        {
            observedFacingIntentRevision = flock.FacingIntentRevision;
            hasPendingFacingIntent = false;
            hasAppliedFacingIntent = true;
            visualAnimator?.SetFlockFacingIntent(flock.FacingIntentLeft);
            return;
        }

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
            facingReactionCountdown = facingReactionDelay;
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

    private Vector2 CalculateSteeringVelocity(
        Vector2 driveVelocity,
        bool flockIsMoving,
        float deltaTime)
    {
        Vector2 position = body.position;
        Vector2 desiredVelocity = driveVelocity;
        Vector2 centerOffset = flock.Center - position;

        float activeRadius = GetComfortableRadius();
        if (!flockIsMoving)
        {
            activeRadius += idleCorrectionMargin;
        }
        Vector2 cohesionOffset = CalculateCohesionOffset(
            centerOffset,
            activeRadius,
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
        float activeSeparationRadius = separationRadius;
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

            if (flock.IsGroupActionActive)
            {
                wanderDirection = Vector2.MoveTowards(
                    wanderDirection,
                    Vector2.zero,
                    wanderTurnSpeed * deltaTime);
            }
            else
            {
                UpdateWander(deltaTime);
                desiredVelocity += wanderDirection * wanderStrength;
            }
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
        out bool isInsideComfortableShape)
    {
        float centerDistance = centerOffset.magnitude;
        if (centerDistance <= 0.0001f)
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
        float outlineScale = 1f
            + sharedOutlineWave * outlineIrregularity * shapeStrength
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
        bool isInsideComfortableShape,
        float deltaTime)
    {
        bool allowed = !flock.IsMoving
            && isInsideComfortableShape
            && flock.CanIdlePace(simulationSlot);
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
        return comfortableRadius + radiusGrowthPerSheep * Mathf.Sqrt(others);
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
        windupCompression = Mathf.Clamp(windupCompression, 0f, 0.3f);
        windupSettleSpeed = Mathf.Max(0f, windupSettleSpeed);
    }
}

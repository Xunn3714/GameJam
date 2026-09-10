using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class FlockMovementController : MonoBehaviour
{
    private const string MoveActionName = "Player/Move";
    private const float CenterColliderRadius = 0.35f;
    private const float StopSpeed = 0.02f;

    [Header("Movement")]
    [FormerlySerializedAs("moveSpeed")]
    [SerializeField, Min(0f)] private float normalSpeedLimit = 4f;
    [SerializeField, Min(0f)] private float acceleration = 12f;
    [SerializeField, Min(0f)] private float deceleration = 16f;

    [Header("Center Leash")]
    [Tooltip("安全区总宽度占当前镜头宽度的比例；区域内至少保留一只已入群成员。")]
    [SerializeField, Range(0.2f, 0.8f)] private float leashViewWidthRatio = 0.42f;
    [Tooltip("安全区总高度占当前镜头高度的比例。")]
    [SerializeField, Range(0.2f, 0.8f)] private float leashViewHeightRatio = 0.42f;
    [SerializeField, Min(0.5f)] private float minimumLeashHalfWidth = 2.8f;
    [SerializeField, Min(0.5f)] private float minimumLeashHalfHeight = 2.2f;
    [Tooltip("成员到达安全区这一比例后，继续远离时中心开始柔和减速。")]
    [SerializeField, Range(0.4f, 0.95f)] private float leashSoftZoneStart = 0.72f;
    [SerializeField, Range(0.05f, 1f)] private float minimumLeashOutwardSpeedFactor = 0.18f;
    [SerializeField, Min(0f)] private float centerReturnDelay = 0.1f;
    [SerializeField, Min(0.01f)] private float centerReturnSmoothTime = 0.32f;
    [SerializeField, Min(0.1f)] private float centerReturnMaximumSpeed = 14f;
    [SerializeField, Min(0f)] private float centerReturnSettleDistance = 0.04f;
    [Tooltip("新成员必须明显更靠近中心才接替回正锚点，避免镜头在羊之间跳动。")]
    [SerializeField, Range(0.2f, 0.95f)] private float leashAnchorSwitchRatio = 0.68f;

    [Header("Blocking")]
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField, Min(0f)] private float blockingRadius = 0f;

    private InputAction moveAction;
    private Vector2 moveInput;
    private Vector2 velocity;
    private Rigidbody2D body;
    private Vector2 positionBeforeFixedMove;
    private bool movedThisStep;
    private bool controlEnabled = true;
    private bool restrictToMovementBounds;
    private bool externalMovementCanLeaveBounds;
    private Rect movementBounds;
    private float temporarySpeedLimit;
    private float temporarySpeedLimitExpiresAt;
    private float speedMultiplier = 1f;
    private bool externalMovementActive;
    private FlockController flock;
    private Camera leashViewCamera;
    private CameraFollow2D leashCameraFollow;
    private SheepMember leashAnchor;
    private Vector2 centerReturnVelocity;
    private float lastMoveInputTime;

    /// <summary>随羊群规模 / 镜头放大整体提速；速度上限和加速度一起乘。</summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public float SpeedMultiplier => speedMultiplier;
    public bool ControlEnabled => controlEnabled;

    public Vector2 LastMoveDirection { get; private set; } = Vector2.right;
    public bool FacingLeft { get; private set; }
    public Vector2 MoveInput => moveInput;
    public bool HasMoveInput => moveInput.sqrMagnitude > 0.0001f;
    public bool IsMoving => !IsAutoReturning && velocity.sqrMagnitude > StopSpeed * StopSpeed;
    public Vector2 DesiredVelocity => IsAutoReturning ? Vector2.zero : velocity;
    public Vector2 Velocity => velocity;
    public bool IsAutoReturning { get; private set; }
    public float NormalSpeedLimit => normalSpeedLimit;
    public float UnmodifiedCurrentSpeedLimit => (HasTemporarySpeedLimit
        ? Mathf.Max(normalSpeedLimit, temporarySpeedLimit)
        : normalSpeedLimit) * speedMultiplier;
    public float CurrentSpeedLimit => UnmodifiedCurrentSpeedLimit;

    private bool HasTemporarySpeedLimit =>
        temporarySpeedLimit > normalSpeedLimit &&
        Time.time < temporarySpeedLimitExpiresAt;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        flock = GetComponent<FlockController>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D centerTrigger = GetComponent<CircleCollider2D>();
        centerTrigger.isTrigger = true;
        centerTrigger.radius = CenterColliderRadius;

        if (blockingLayers.value == 0)
            blockingLayers = MovementBlocking.DefaultMask();
        if (blockingRadius <= 0f)
            blockingRadius = CenterColliderRadius;
        moveAction = InputSystem.actions?.FindAction(MoveActionName);
        lastMoveInputTime = Time.time;
    }

    private void Update()
    {
        if (!controlEnabled || Time.timeScale == 0f)
        {
            CancelCenterReturn();
            if (Time.timeScale == 0f)
                lastMoveInputTime = Time.time;
            moveInput = Vector2.zero;
            return;
        }

        moveInput = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : ReadKeyboardFallback();
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        if (moveInput.sqrMagnitude > 0.0001f)
        {
            lastMoveInputTime = Time.time;
            CancelCenterReturn();
            LastMoveDirection = moveInput.normalized;
            if (Mathf.Abs(moveInput.x) > 0.01f)
                FacingLeft = moveInput.x < 0f;
        }
    }

    private void FixedUpdate()
    {
        movedThisStep = false;
        if (Time.timeScale == 0f)
            return;

        if (externalMovementActive)
            return;

        if (!HasTemporarySpeedLimit)
            temporarySpeedLimit = 0f;

        float deltaTime = Time.fixedDeltaTime;
        if (TryUpdateCenterReturn(deltaTime))
            return;

        float speedLimit = CurrentSpeedLimit;
        Vector2 targetVelocity = controlEnabled
            ? moveInput * speedLimit
            : Vector2.zero;
        float response = (targetVelocity.sqrMagnitude > 0.0001f
            ? acceleration
            : deceleration) * speedMultiplier;
        velocity = Vector2.MoveTowards(velocity, targetVelocity, response * deltaTime);
        velocity = Vector2.ClampMagnitude(velocity, speedLimit);

        if (velocity.sqrMagnitude <= StopSpeed * StopSpeed)
        {
            velocity = Vector2.zero;
            return;
        }

        Vector2 from = body.position;
        positionBeforeFixedMove = from;
        Vector2 displacement = velocity * deltaTime;
        Vector2 targetPosition = body.position + displacement;

        if (restrictToMovementBounds)
        {
            targetPosition.x = ClampInside(
                targetPosition.x,
                movementBounds.xMin + CenterColliderRadius,
                movementBounds.xMax - CenterColliderRadius);
            targetPosition.y = ClampInside(
                targetPosition.y,
                movementBounds.yMin + CenterColliderRadius,
                movementBounds.yMax - CenterColliderRadius);
        }

        // 普通输入只移动羊群想要抵达的中心；中心本身不与场景障碍碰撞。
        // 只在所有成员沿这个方向都没有任何可走路径时停下，避免不可见中心
        // 独自穿墙，同时允许仍能通过的成员带着中心继续前进。
        flock ??= GetComponent<FlockController>();
        Vector2 boundedDisplacement = targetPosition - from;
        bool blockedByAllMembers = boundedDisplacement.sqrMagnitude > 0.000001f
            && flock != null
            && flock.AreAllMembersBlocked(boundedDisplacement);
        if (blockedByAllMembers)
        {
            targetPosition = from;
        }
        else
        {
            targetPosition = ConstrainCenterToLeash(from, targetPosition);
        }

        if (targetPosition == from)
        {
            // 全员受阻时保留输入速度作为成员的移动意图；成员仍会逐帧尝试滑边。
            // 地图边界或松开输入造成的停下则正常清零。
            if (!blockedByAllMembers)
                velocity = Vector2.zero;
            return;
        }

        velocity = (targetPosition - from) / deltaTime;
        movedThisStep = true;
        body.MovePosition(targetPosition);
    }

    /// <summary>把中心限制在指定成员周围的椭圆安全区内。</summary>
    public static Vector2 ClampCenterToMemberEllipse(
        Vector2 center,
        Vector2 memberPosition,
        Vector2 halfExtents)
    {
        float radiusX = Mathf.Max(0.01f, halfExtents.x);
        float radiusY = Mathf.Max(0.01f, halfExtents.y);
        Vector2 offset = center - memberPosition;
        float normalizedLength = Mathf.Sqrt(
            offset.x * offset.x / (radiusX * radiusX)
            + offset.y * offset.y / (radiusY * radiusY));
        if (normalizedLength <= 1f)
            return center;

        return memberPosition + offset / normalizedLength;
    }

    public void ConfigureLeashView(CameraFollow2D cameraFollow)
    {
        leashCameraFollow = cameraFollow;
        leashViewCamera = cameraFollow != null
            ? cameraFollow.GetComponent<Camera>()
            : null;
    }

    private bool TryUpdateCenterReturn(float deltaTime)
    {
        if (!controlEnabled || HasMoveInput)
        {
            CancelCenterReturn();
            return false;
        }

        if (Time.time < lastMoveInputTime + Mathf.Max(0f, centerReturnDelay))
            return false;

        if (!TryGetReturnFocus(body.position, out Vector2 returnFocus))
        {
            CancelCenterReturn();
            return false;
        }

        Vector2 from = body.position;
        positionBeforeFixedMove = from;
        velocity = Vector2.zero;
        float settleDistance = Mathf.Max(0f, centerReturnSettleDistance);
        if ((returnFocus - from).sqrMagnitude <= settleDistance * settleDistance)
        {
            centerReturnVelocity = Vector2.zero;
            IsAutoReturning = false;
            if (returnFocus != from)
            {
                movedThisStep = true;
                body.MovePosition(ClampToMovementBounds(returnFocus));
            }
            return true;
        }

        IsAutoReturning = true;
        Vector2 target = Vector2.SmoothDamp(
            from,
            returnFocus,
            ref centerReturnVelocity,
            Mathf.Max(0.01f, centerReturnSmoothTime),
            Mathf.Max(0.1f, centerReturnMaximumSpeed),
            deltaTime);
        target = ClampToMovementBounds(target);
        if (target != from)
        {
            movedThisStep = true;
            body.MovePosition(target);
        }
        return true;
    }

    private void CancelCenterReturn()
    {
        bool wasReturning = IsAutoReturning
            || centerReturnVelocity.sqrMagnitude > 0.000001f;
        IsAutoReturning = false;
        centerReturnVelocity = Vector2.zero;
        if (wasReturning)
            leashCameraFollow?.ResetFollowVelocity();
    }

    private bool TryGetReturnFocus(Vector2 referencePosition, out Vector2 returnFocus)
    {
        returnFocus = referencePosition;
        if (!TryGetLeashAnchor(referencePosition, out Vector2 anchorPosition))
            return false;

        Vector2 halfExtents = GetLeashHalfExtents();
        float radiusX = Mathf.Max(0.01f, halfExtents.x);
        float radiusY = Mathf.Max(0.01f, halfExtents.y);
        Vector2 sum = Vector2.zero;
        int count = 0;
        for (int index = 0; index < flock.Members.Count; index++)
        {
            SheepMember member = flock.Members[index];
            if (member == null || member.Flock != flock)
                continue;

            Vector2 memberPosition = GetMemberPosition(member);
            Vector2 offset = memberPosition - anchorPosition;
            float normalizedDistanceSquared =
                offset.x * offset.x / (radiusX * radiusX)
                + offset.y * offset.y / (radiusY * radiusY);
            if (normalizedDistanceSquared > 1f)
                continue;

            sum += memberPosition;
            count++;
        }

        returnFocus = count > 0 ? sum / count : anchorPosition;
        return true;
    }

    private Vector2 ConstrainCenterToLeash(Vector2 from, Vector2 target)
    {
        Vector2 halfExtents = GetLeashHalfExtents();
        if (!TryFindClosestMember(target, halfExtents, out Vector2 closestPosition, out float targetDistance))
            return target;

        TryGetLeashAnchor(target, out _);
        if (!TryFindClosestMember(from, halfExtents, out _, out float currentDistance))
            currentDistance = targetDistance;

        bool movingAway = targetDistance > currentDistance + 0.0001f;
        if (movingAway && currentDistance >= leashSoftZoneStart)
        {
            float pressure = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(leashSoftZoneStart, 1f, currentDistance));
            float speedFactor = Mathf.Lerp(
                1f,
                Mathf.Clamp(minimumLeashOutwardSpeedFactor, 0.05f, 1f),
                pressure);
            target = from + (target - from) * speedFactor;
            TryFindClosestMember(target, halfExtents, out closestPosition, out targetDistance);
        }

        if (targetDistance <= 1f)
            return target;

        if (currentDistance > 1f)
            return targetDistance < currentDistance ? target : from;

        return ClampCenterToMemberEllipse(target, closestPosition, halfExtents);
    }

    private bool TryGetLeashAnchor(Vector2 referencePosition, out Vector2 anchorPosition)
    {
        anchorPosition = referencePosition;
        flock ??= GetComponent<FlockController>();
        if (flock == null || flock.MemberCount <= 0)
        {
            leashAnchor = null;
            return false;
        }

        SheepMember nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        for (int index = 0; index < flock.Members.Count; index++)
        {
            SheepMember candidate = flock.Members[index];
            if (candidate == null || candidate.Flock != flock)
                continue;

            Vector2 candidatePosition = GetMemberPosition(candidate);
            float distanceSquared = (candidatePosition - referencePosition).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = candidate;
                nearestDistanceSquared = distanceSquared;
            }
        }

        if (nearest == null)
        {
            leashAnchor = null;
            return false;
        }

        bool anchorIsValid = leashAnchor != null && leashAnchor.Flock == flock;
        if (!anchorIsValid)
        {
            leashAnchor = nearest;
        }
        else if (nearest != leashAnchor)
        {
            float anchorDistanceSquared =
                (GetMemberPosition(leashAnchor) - referencePosition).sqrMagnitude;
            float switchRatio = Mathf.Clamp(leashAnchorSwitchRatio, 0.2f, 0.95f);
            if (nearestDistanceSquared < anchorDistanceSquared * switchRatio * switchRatio)
                leashAnchor = nearest;
        }

        anchorPosition = GetMemberPosition(leashAnchor);
        return true;
    }

    private bool TryFindClosestMember(
        Vector2 center,
        Vector2 halfExtents,
        out Vector2 closestPosition,
        out float normalizedDistance)
    {
        closestPosition = center;
        normalizedDistance = float.PositiveInfinity;
        flock ??= GetComponent<FlockController>();
        if (flock == null)
            return false;

        float radiusX = Mathf.Max(0.01f, halfExtents.x);
        float radiusY = Mathf.Max(0.01f, halfExtents.y);
        bool found = false;
        float closestDistanceSquared = float.PositiveInfinity;
        for (int index = 0; index < flock.Members.Count; index++)
        {
            SheepMember member = flock.Members[index];
            if (member == null || member.Flock != flock)
                continue;

            Vector2 memberPosition = GetMemberPosition(member);
            Vector2 offset = memberPosition - center;
            float distanceSquared =
                offset.x * offset.x / (radiusX * radiusX)
                + offset.y * offset.y / (radiusY * radiusY);
            if (distanceSquared >= closestDistanceSquared)
                continue;

            found = true;
            closestDistanceSquared = distanceSquared;
            closestPosition = memberPosition;
        }

        if (found)
            normalizedDistance = Mathf.Sqrt(closestDistanceSquared);
        return found;
    }

    private Vector2 GetLeashHalfExtents()
    {
        float halfHeight = leashViewCamera != null && leashViewCamera.orthographic
            ? leashViewCamera.orthographicSize
            : minimumLeashHalfHeight / Mathf.Max(0.2f, leashViewHeightRatio);
        float halfWidth = leashViewCamera != null && leashViewCamera.orthographic
            ? halfHeight * leashViewCamera.aspect
            : minimumLeashHalfWidth / Mathf.Max(0.2f, leashViewWidthRatio);
        return new Vector2(
            Mathf.Max(minimumLeashHalfWidth, halfWidth * leashViewWidthRatio),
            Mathf.Max(minimumLeashHalfHeight, halfHeight * leashViewHeightRatio));
    }

    private Vector2 ClampToMovementBounds(Vector2 position)
    {
        if (!restrictToMovementBounds)
            return position;

        position.x = ClampInside(
            position.x,
            movementBounds.xMin + CenterColliderRadius,
            movementBounds.xMax - CenterColliderRadius);
        position.y = ClampInside(
            position.y,
            movementBounds.yMin + CenterColliderRadius,
            movementBounds.yMax - CenterColliderRadius);
        return position;
    }

    private static Vector2 GetMemberPosition(SheepMember member)
    {
        return member.Agent != null
            ? member.Agent.Position
            : (Vector2)member.transform.position;
    }

    public void SetNormalSpeedLimit(float speedLimit)
    {
        normalSpeedLimit = Mathf.Max(0f, speedLimit);
        velocity = Vector2.ClampMagnitude(velocity, CurrentSpeedLimit);
    }

    public void ApplyTemporarySpeedLimit(float speedLimit, float durationSeconds)
    {
        if (speedLimit <= normalSpeedLimit || durationSeconds <= 0f)
        {
            ClearTemporarySpeedLimit();
            return;
        }

        temporarySpeedLimit = speedLimit;
        temporarySpeedLimitExpiresAt = Time.time + durationSeconds;
    }

    public void ClearTemporarySpeedLimit()
    {
        temporarySpeedLimit = 0f;
        temporarySpeedLimitExpiresAt = 0f;
        velocity = Vector2.ClampMagnitude(velocity, CurrentSpeedLimit);
    }

    public void BeginExternalMovement()
    {
        externalMovementActive = true;
        CancelCenterReturn();
        movedThisStep = false;
        velocity = Vector2.zero;
    }

    /// <summary>在外部动作仍占用移动控制时，让羊群中心原地停住。</summary>
    public void HoldExternalMovement()
    {
        if (!externalMovementActive)
            return;

        movedThisStep = false;
        velocity = Vector2.zero;
    }

    public void EndExternalMovement()
    {
        externalMovementActive = false;
        lastMoveInputTime = Time.time;
        CancelCenterReturn();
        movedThisStep = false;
        velocity = Vector2.zero;
    }

    /// <summary>
    /// 执行一段不受普通速度上限约束的冲刺位移，返回实际移动距离和首个阻挡物。
    /// </summary>
    public float MoveExternalStep(
        Vector2 direction,
        float distance,
        out MovementBlockResult blockResult)
    {
        blockResult = default;
        if (!externalMovementActive || body == null || distance <= 0f || direction.sqrMagnitude <= 0.0001f)
            return 0f;

        direction.Normalize();
        Vector2 from = body.position;
        Vector2 requestedTarget = from + direction * distance;
        Vector2 boundedTarget = requestedTarget;
        if (restrictToMovementBounds && !externalMovementCanLeaveBounds)
        {
            boundedTarget.x = ClampInside(
                boundedTarget.x,
                movementBounds.xMin + CenterColliderRadius,
                movementBounds.xMax - CenterColliderRadius);
            boundedTarget.y = ClampInside(
                boundedTarget.y,
                movementBounds.yMin + CenterColliderRadius,
                movementBounds.yMax - CenterColliderRadius);
        }

        bool hitMovementBounds = (boundedTarget - requestedTarget).sqrMagnitude > 0.000001f;
        Vector2 resolvedTarget = MovementBlocking.ResolveDashMove(
            from,
            boundedTarget,
            blockingRadius,
            blockingLayers,
            out blockResult);
        if (!blockResult.WasBlocked && hitMovementBounds)
            blockResult = new MovementBlockResult(true, true, null);

        Vector2 displacement = resolvedTarget - from;
        float actualDistance = displacement.magnitude;
        velocity = actualDistance > 0.0001f
            ? displacement / Mathf.Max(Time.fixedDeltaTime, 0.0001f)
            : Vector2.zero;
        movedThisStep = actualDistance > 0.0001f;
        if (movedThisStep)
            body.MovePosition(resolvedTarget);
        return actualDistance;
    }

    public void ConfigureMovementBounds(Rect bounds)
    {
        movementBounds = bounds;
        restrictToMovementBounds = bounds.width > CenterColliderRadius * 2f
            && bounds.height > CenterColliderRadius * 2f;
    }

    /// <summary>
    /// 出口解锁后允许 E 冲刺扫过地图边界并命中外圈围栏；普通移动仍受边界限制。
    /// </summary>
    public void SetExternalMovementCanLeaveBounds(bool canLeave)
    {
        externalMovementCanLeaveBounds = canLeave;
    }

    public void RejectCurrentMovement()
    {
        if (!movedThisStep)
            return;

        CancelCenterReturn();
        body.position = positionBeforeFixedMove;
        velocity = Vector2.zero;
        movedThisStep = false;
    }

    public void SetControlEnabled(bool enabled)
    {
        bool wasEnabled = controlEnabled;
        controlEnabled = enabled;
        if (enabled && !wasEnabled)
            lastMoveInputTime = Time.time;
        if (!enabled)
        {
            CancelCenterReturn();
            moveInput = Vector2.zero;
            velocity = Vector2.zero;
        }
    }

    private void OnValidate()
    {
        normalSpeedLimit = Mathf.Max(0f, normalSpeedLimit);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        leashViewWidthRatio = Mathf.Clamp(leashViewWidthRatio, 0.2f, 0.8f);
        leashViewHeightRatio = Mathf.Clamp(leashViewHeightRatio, 0.2f, 0.8f);
        minimumLeashHalfWidth = Mathf.Max(0.5f, minimumLeashHalfWidth);
        minimumLeashHalfHeight = Mathf.Max(0.5f, minimumLeashHalfHeight);
        leashSoftZoneStart = Mathf.Clamp(leashSoftZoneStart, 0.4f, 0.95f);
        minimumLeashOutwardSpeedFactor = Mathf.Clamp(minimumLeashOutwardSpeedFactor, 0.05f, 1f);
        centerReturnDelay = Mathf.Max(0f, centerReturnDelay);
        centerReturnSmoothTime = Mathf.Max(0.01f, centerReturnSmoothTime);
        centerReturnMaximumSpeed = Mathf.Max(0.1f, centerReturnMaximumSpeed);
        centerReturnSettleDistance = Mathf.Max(0f, centerReturnSettleDistance);
        leashAnchorSwitchRatio = Mathf.Clamp(leashAnchorSwitchRatio, 0.2f, 0.95f);
    }

    private static Vector2 ReadKeyboardFallback()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        float horizontal = ReadAxis(
            keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
            keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
        float vertical = ReadAxis(
            keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
            keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
        return new Vector2(horizontal, vertical);
    }

    private static float ReadAxis(bool negativePressed, bool positivePressed)
    {
        return (positivePressed ? 1f : 0f) - (negativePressed ? 1f : 0f);
    }

    private static float ClampInside(float value, float minimum, float maximum)
    {
        return minimum <= maximum
            ? Mathf.Clamp(value, minimum, maximum)
            : (minimum + maximum) * 0.5f;
    }
}

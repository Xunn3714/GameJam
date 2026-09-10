using UnityEngine;
using UnityEngine.InputSystem;

public enum FlockActionPhase
{
    Idle,
    Retreating,
    Windup,
    Dashing,
    ImpactFollowThrough,
}

/// <summary>
/// Alpha 羊群主动动作：按下 E 后整群后退、停顿蓄势，再向前冲刺。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-75)]
[RequireComponent(typeof(FlockController), typeof(FlockMovementController))]
public sealed class FlockActionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlockController flock;
    [SerializeField] private FlockMovementController movement;

    [Header("E Group Dash")]
    [Tooltip("冲刺前整群向反方向退开的速度。")]
    [SerializeField, Min(0f)] private float retreatSpeed = 3f;
    [Tooltip("冲刺前整群向反方向退开的距离。")]
    [SerializeField, Min(0f)] private float retreatDistance = 0.65f;
    [Tooltip("后退结束后原地蓄势的时间。")]
    [SerializeField, Min(0f)] private float windupDuration = 0.3f;
    [SerializeField, Min(0.1f)] private float dashSpeed = 9f;
    [SerializeField, Min(0.1f)] private float dashDistance = 3.2f;

    [Header("Action Feel")]
    [Tooltip("后撤开始与结束时的最低速度比例，中段仍会达到完整后撤速度。")]
    [SerializeField, Range(0.1f, 1f)] private float retreatEdgeSpeedFactor = 0.45f;
    [Tooltip("冲刺起步速度比例；随后会迅速爆发到完整速度。")]
    [SerializeField, Range(0.1f, 1f)] private float dashStartSpeedFactor = 0.65f;
    [Tooltip("冲刺完成前的收尾速度比例，降低动作结束时的生硬急停。")]
    [SerializeField, Range(0.1f, 1f)] private float dashEndSpeedFactor = 0.75f;
    [Tooltip("撞不开障碍后，后排成员继续向前涌动的时间。")]
    [SerializeField, Min(0f)] private float impactFollowThroughDuration = 0.28f;
    [Tooltip("撞击续冲相对于完整冲刺速度的比例，会在持续时间内衰减到零。")]
    [SerializeField, Range(0.1f, 1f)] private float impactFollowThroughSpeedFactor = 0.78f;

    private bool controlEnabled = true;
    private float initialRetreatDistance;
    private float remainingRetreatDistance;
    private float remainingWindupTime;
    private float initialDashDistance;
    private float remainingDashDistance;
    private float currentDashSpeed;
    private float currentImpactForce;
    private float remainingImpactFollowThroughTime;
    private bool lastImpactDamagedObstacle;
    private Vector2 dashDirection = Vector2.right;
    private FlockActionPhase phase;

    public FlockActionPhase Phase => phase;
    public bool IsActing => phase != FlockActionPhase.Idle;
    public bool IsDashing => phase == FlockActionPhase.Dashing;
    public float CurrentImpactForce => currentImpactForce;

    private void Awake()
    {
        flock ??= GetComponent<FlockController>();
        movement ??= GetComponent<FlockMovementController>();
    }

    public void Configure(FlockController flockController, FlockMovementController movementController)
    {
        flock = flockController != null ? flockController : GetComponent<FlockController>();
        movement = movementController != null ? movementController : GetComponent<FlockMovementController>();
    }

    private void Update()
    {
        if (!controlEnabled || movement == null || !movement.ControlEnabled)
        {
            FinishAction();
            return;
        }

        if (Time.timeScale == 0f)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            StartGroupDash();
    }

    private void FixedUpdate()
    {
        if (!IsActing || movement == null || flock == null || Time.timeScale == 0f)
            return;

        if (flock.TryConsumeGroupActionMemberBlock(out Collider2D memberBlocker))
        {
            // 后撤时后排成员贴到障碍，只让该成员停下或沿边缘脱离。
            // 不能因此提前结束整群后撤，否则下一帧反向冲刺时会显得卡顿。
            if (phase == FlockActionPhase.Dashing && !HandleDashBlock(memberBlocker))
                return;
        }

        switch (phase)
        {
            case FlockActionPhase.Retreating:
                UpdateRetreat();
                break;
            case FlockActionPhase.Windup:
                UpdateWindup();
                break;
            case FlockActionPhase.Dashing:
                UpdateDash();
                break;
            case FlockActionPhase.ImpactFollowThrough:
                UpdateImpactFollowThrough();
                break;
        }
    }

    /// <summary>供键盘、未来手柄或 UI 输入共同调用；一次调用执行完整的后退、蓄势和冲刺。</summary>
    public bool StartGroupDash()
    {
        if (!controlEnabled
            || IsActing
            || flock == null
            || movement == null
            || flock.MemberCount <= 0
            || flock.HasMovementLockedMembers())
            return false;

        Vector2 inputDirection = movement.MoveInput;
        dashDirection = inputDirection.sqrMagnitude > 0.0001f
            ? inputDirection.normalized
            : movement.LastMoveDirection;
        if (dashDirection.sqrMagnitude <= 0.0001f)
            dashDirection = Vector2.right;

        float stageScale = movement.SpeedMultiplier;
        remainingRetreatDistance = Mathf.Max(0f, retreatDistance) * stageScale;
        initialRetreatDistance = remainingRetreatDistance;
        remainingWindupTime = Mathf.Max(0f, windupDuration);
        currentDashSpeed = Mathf.Max(0.1f, dashSpeed) * stageScale;
        remainingDashDistance = Mathf.Max(0.1f, dashDistance) * stageScale;
        initialDashDistance = remainingDashDistance;
        currentImpactForce = CalculateImpactForce(flock.MemberCount);

        movement.BeginExternalMovement();
        float fastestActionSpeed = Mathf.Max(retreatSpeed * stageScale, currentDashSpeed);
        float memberSpeedMultiplier = fastestActionSpeed
            / Mathf.Max(0.1f, movement.UnmodifiedCurrentSpeedLimit);
        flock.SetActionMemberSpeedMultiplier(memberSpeedMultiplier);
        phase = remainingRetreatDistance > 0.001f && retreatSpeed > 0f
            ? FlockActionPhase.Retreating
            : FlockActionPhase.Windup;
        bool startsInWindup = phase == FlockActionPhase.Windup;
        flock.SetGroupActionState(true, startsInWindup, dashDirection);
        flock.SetGroupActionAnticipating(startsInWindup);

        if (phase == FlockActionPhase.Windup)
            movement.HoldExternalMovement();
        return true;
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if (enabled)
            return;

        FinishAction();
    }

    public static float CalculateImpactForce(int memberCount)
    {
        return Mathf.Max(0, memberCount);
    }

    public static bool MeetsBreakThreshold(float impactForce, int requiredForce)
    {
        return impactForce >= Mathf.Max(1, requiredForce);
    }

    /// <summary>
    /// 成员碰撞只有发生在冲刺方向、并且接触点位于羊群中心前缘时，才应截停整群。
    /// 后撤受阻以及仍在中心后方的成员接触都由成员自己消解，避免后排羊卡住整段动作。
    /// </summary>
    public static bool ShouldMemberBlockStopAction(
        Vector2 flockCenter,
        Vector2 actionDirection,
        Vector2 attemptedDisplacement,
        Vector2 blockerPoint)
    {
        if (actionDirection.sqrMagnitude <= 0.0001f
            || attemptedDisplacement.sqrMagnitude <= 0.000001f)
            return false;

        Vector2 forward = actionDirection.normalized;
        if (Vector2.Dot(attemptedDisplacement, forward) <= 0.0001f)
            return false;

        const float centerFrontTolerance = 0.05f;
        return Vector2.Dot(blockerPoint - flockCenter, forward) >= -centerFrontTolerance;
    }

    public static float CalculateRetreatSpeedFactor(float normalizedProgress, float edgeFactor)
    {
        float edge = Mathf.Clamp(edgeFactor, 0.1f, 1f);
        float arc = Mathf.Sin(Mathf.Clamp01(normalizedProgress) * Mathf.PI);
        return Mathf.Lerp(edge, 1f, arc);
    }

    public static float CalculateDashSpeedFactor(
        float normalizedProgress,
        float startFactor,
        float endFactor)
    {
        float progress = Mathf.Clamp01(normalizedProgress);
        float launch = Mathf.SmoothStep(
            Mathf.Clamp(startFactor, 0.1f, 1f),
            1f,
            Mathf.Clamp01(progress / 0.24f));
        if (progress <= 0.72f)
            return launch;

        return Mathf.SmoothStep(
            launch,
            Mathf.Clamp(endFactor, 0.1f, 1f),
            Mathf.InverseLerp(0.72f, 1f, progress));
    }

    public static float CalculateImpactFollowThroughSpeedFactor(
        float normalizedTimeRemaining,
        float maximumFactor)
    {
        return Mathf.Clamp(maximumFactor, 0.1f, 1f)
            * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedTimeRemaining));
    }

    private void UpdateRetreat()
    {
        float progress = CalculateProgress(initialRetreatDistance, remainingRetreatDistance);
        float speedFactor = CalculateRetreatSpeedFactor(progress, retreatEdgeSpeedFactor);
        float requestedDistance = Mathf.Min(
            remainingRetreatDistance,
            Mathf.Max(0f, retreatSpeed)
            * movement.SpeedMultiplier
            * speedFactor
            * Time.fixedDeltaTime);
        float movedDistance = movement.MoveExternalStep(
            -dashDirection,
            requestedDistance,
            out MovementBlockResult blockResult);
        remainingRetreatDistance = Mathf.Max(0f, remainingRetreatDistance - movedDistance);

        if (blockResult.WasBlocked || remainingRetreatDistance <= 0.001f || requestedDistance <= 0f)
            BeginWindup();
    }

    private void BeginWindup()
    {
        phase = FlockActionPhase.Windup;
        movement.HoldExternalMovement();
        flock.SetGroupActionState(true, true, dashDirection);
        flock.SetGroupActionAnticipating(true);
        if (remainingWindupTime <= 0f)
            BeginDash();
    }

    private void UpdateWindup()
    {
        movement.HoldExternalMovement();
        remainingWindupTime -= Time.fixedDeltaTime;
        if (remainingWindupTime <= 0f)
            BeginDash();
    }

    private void BeginDash()
    {
        phase = FlockActionPhase.Dashing;
        flock.SetGroupActionState(true, false, dashDirection);
    }

    private void UpdateDash()
    {
        float progress = CalculateProgress(initialDashDistance, remainingDashDistance);
        float speedFactor = CalculateDashSpeedFactor(
            progress,
            dashStartSpeedFactor,
            dashEndSpeedFactor);
        float requestedDistance = Mathf.Min(
            remainingDashDistance,
            currentDashSpeed * speedFactor * Time.fixedDeltaTime);
        float movedDistance = movement.MoveExternalStep(
            dashDirection,
            requestedDistance,
            out MovementBlockResult blockResult);
        remainingDashDistance = Mathf.Max(0f, remainingDashDistance - movedDistance);

        if (blockResult.WasBlocked)
        {
            if (!HandleDashBlock(blockResult.Blocker))
                return;
        }

        if (remainingDashDistance <= 0.001f)
            FinishAction();
    }

    private void BeginImpactFollowThrough(bool hardImpact)
    {
        phase = FlockActionPhase.ImpactFollowThrough;
        remainingImpactFollowThroughTime = Mathf.Max(0f, impactFollowThroughDuration);
        movement.HoldExternalMovement();
        flock.SetGroupActionState(true, false, dashDirection);
        flock.SetGroupActionFollowThrough(
            remainingImpactFollowThroughTime > 0f,
            currentDashSpeed * impactFollowThroughSpeedFactor,
            hardImpact);

        if (remainingImpactFollowThroughTime <= 0f)
            FinishAction();
    }

    private void UpdateImpactFollowThrough()
    {
        movement.HoldExternalMovement();
        remainingImpactFollowThroughTime = Mathf.Max(
            0f,
            remainingImpactFollowThroughTime - Time.fixedDeltaTime);
        float normalizedRemaining = impactFollowThroughDuration > 0.0001f
            ? remainingImpactFollowThroughTime / impactFollowThroughDuration
            : 0f;
        float speedFactor = CalculateImpactFollowThroughSpeedFactor(
            normalizedRemaining,
            impactFollowThroughSpeedFactor);
        flock.SetGroupActionFollowThrough(
            remainingImpactFollowThroughTime > 0f,
            currentDashSpeed * speedFactor,
            hardImpact: !lastImpactDamagedObstacle);

        if (remainingImpactFollowThroughTime <= 0f)
            FinishAction();
    }

    private bool HandleDashBlock(Collider2D blocker)
    {
        bool brokeObstacle = TryBreakObstacle(blocker);
        if (brokeObstacle)
        {
            PlayFlockImpact(hardImpact: false);
            return true;
        }

        BeginImpactFollowThrough(hardImpact: !lastImpactDamagedObstacle);
        return false;
    }

    private bool TryBreakObstacle(Collider2D blocker)
    {
        lastImpactDamagedObstacle = false;
        if (blocker == null)
        {
            flock.ReportFenceChargeImpact(hardImpact: true);
            return false;
        }

        BreakableObstacle breakable = blocker.GetComponentInParent<BreakableObstacle>();
        if (breakable == null || breakable.IsBroken)
        {
            flock.ReportFenceChargeImpact(hardImpact: true);
            return false;
        }

        FenceObstacle fence = breakable.GetComponent<FenceObstacle>();
        if (fence != null)
            return fence.ReceiveDashImpact(flock, currentImpactForce);

        int requiredForce = breakable.Definition == null
            || breakable.Definition.BreakRule == ObstacleBreakRule.OnAnyContact
            ? 1
            : breakable.Definition.RequiredFlockCount;
        bool canBreak = MeetsBreakThreshold(currentImpactForce, requiredForce);
        flock.ReportFenceChargeImpact(hardImpact: !canBreak);
        if (!canBreak)
            return false;

        // 多段障碍（大石头）吃掉本次冲刺，下一次 E 才能完成破坏。
        bool destroyed = breakable.Break();
        lastImpactDamagedObstacle = !destroyed && breakable.IsDamaged;
        return destroyed;
    }

    private bool PlayFlockImpact(bool hardImpact)
    {
        bool played = false;
        if (flock == null)
            return false;

        for (int index = 0; index < flock.Members.Count; index++)
        {
            SheepMember member = flock.Members[index];
            SheepVisualAnimator animator = member != null
                ? member.GetComponent<SheepVisualAnimator>()
                : null;
            played |= animator != null
                && animator.PlayObstacleImpact(hardImpact, dashDirection);
        }

        return played;
    }

    private void FinishAction()
    {
        if (!IsActing)
            return;

        phase = FlockActionPhase.Idle;
        initialRetreatDistance = 0f;
        remainingRetreatDistance = 0f;
        remainingWindupTime = 0f;
        initialDashDistance = 0f;
        remainingDashDistance = 0f;
        currentDashSpeed = 0f;
        currentImpactForce = 0f;
        remainingImpactFollowThroughTime = 0f;
        movement?.EndExternalMovement();
        flock?.SetActionMemberSpeedMultiplier(1f);
        flock?.SetGroupActionState(false, false, dashDirection);
    }

    private void OnDisable()
    {
        FinishAction();
    }

    private void OnValidate()
    {
        retreatSpeed = Mathf.Max(0f, retreatSpeed);
        retreatDistance = Mathf.Max(0f, retreatDistance);
        windupDuration = Mathf.Max(0f, windupDuration);
        dashSpeed = Mathf.Max(0.1f, dashSpeed);
        dashDistance = Mathf.Max(0.1f, dashDistance);
        retreatEdgeSpeedFactor = Mathf.Clamp(retreatEdgeSpeedFactor, 0.1f, 1f);
        dashStartSpeedFactor = Mathf.Clamp(dashStartSpeedFactor, 0.1f, 1f);
        dashEndSpeedFactor = Mathf.Clamp(dashEndSpeedFactor, 0.1f, 1f);
        impactFollowThroughDuration = Mathf.Max(0f, impactFollowThroughDuration);
        impactFollowThroughSpeedFactor = Mathf.Clamp(impactFollowThroughSpeedFactor, 0.1f, 1f);
    }

    private static float CalculateProgress(float initialDistance, float remainingDistance)
    {
        if (initialDistance <= 0.0001f)
            return 1f;

        return 1f - Mathf.Clamp01(remainingDistance / initialDistance);
    }
}

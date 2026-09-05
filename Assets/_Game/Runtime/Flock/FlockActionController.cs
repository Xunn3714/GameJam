using UnityEngine;
using UnityEngine.InputSystem;

public enum FlockActionPhase
{
    Idle,
    Retreating,
    Windup,
    Dashing,
    ImpactStun,
}

/// <summary>
/// Alpha 羊群主动动作：按下 E 后整群后退、停顿蓄势，再向前冲刺；Q 持续收拢队形。
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
    [SerializeField, Min(0.1f)] private float dashSpeed = 8f;
    [SerializeField, Min(0.1f)] private float dashDistance = 2.2f;

    [Header("Q Compression")]
    [SerializeField, Range(0.2f, 1f)] private float minimumManualCompactness = 0.45f;
    [SerializeField, Min(0.01f)] private float compactingSpeed = 0.45f;
    [SerializeField, Min(0.01f)] private float restoringSpeed = 0.2f;
    [SerializeField, Range(0.05f, 1f)] private float compactedMoveSpeedMultiplier = 0.58f;

    private bool controlEnabled = true;
    private bool compressionHeld;
    private float manualCompactness = 1f;
    private float remainingRetreatDistance;
    private float remainingWindupTime;
    private float remainingDashDistance;
    private float currentDashSpeed;
    private float currentImpactForce;
    private Vector2 dashDirection = Vector2.right;
    private FlockActionPhase phase;

    public FlockActionPhase Phase => phase;
    public bool IsActing => phase != FlockActionPhase.Idle;
    public bool IsDashing => phase == FlockActionPhase.Dashing;
    public float ManualCompactness => manualCompactness;
    public float CurrentImpactForce => currentImpactForce;

    private void Awake()
    {
        flock ??= GetComponent<FlockController>();
        movement ??= GetComponent<FlockMovementController>();
        manualCompactness = 1f;
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
            SetCompressionHeld(false);
            UpdateCompression(Time.deltaTime);
            UpdateMovementSpeedScale();
            return;
        }

        if (Time.timeScale == 0f)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            SetCompressionHeld(keyboard.qKey.isPressed);
            if (keyboard.eKey.wasPressedThisFrame)
                StartGroupDash();
        }

        UpdateCompression(Time.deltaTime);
        UpdateMovementSpeedScale();
    }

    private void FixedUpdate()
    {
        if (!IsActing || movement == null || flock == null || Time.timeScale == 0f)
            return;

        if (flock.TryConsumeGroupActionMemberBlock(out Collider2D memberBlocker))
        {
            if (phase == FlockActionPhase.Retreating)
            {
                BeginWindup();
                return;
            }

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
            case FlockActionPhase.ImpactStun:
                movement.HoldExternalMovement();
                if (!flock.HasMovementLockedMembers())
                    FinishAction();
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
        remainingWindupTime = Mathf.Max(0f, windupDuration);
        currentDashSpeed = Mathf.Max(0.1f, dashSpeed) * stageScale;
        remainingDashDistance = Mathf.Max(0.1f, dashDistance) * stageScale;
        currentImpactForce = CalculateImpactForce(flock.MemberCount);

        movement.BeginExternalMovement();
        float fastestActionSpeed = Mathf.Max(retreatSpeed * stageScale, currentDashSpeed);
        float memberSpeedMultiplier = fastestActionSpeed
            / Mathf.Max(0.1f, movement.UnmodifiedCurrentSpeedLimit);
        flock.SetActionMemberSpeedMultiplier(memberSpeedMultiplier);
        phase = remainingRetreatDistance > 0.001f && retreatSpeed > 0f
            ? FlockActionPhase.Retreating
            : FlockActionPhase.Windup;
        flock.SetGroupActionState(true, phase == FlockActionPhase.Windup, dashDirection);

        if (phase == FlockActionPhase.Windup)
            movement.HoldExternalMovement();
        return true;
    }

    public void SetCompressionHeld(bool held)
    {
        compressionHeld = held;
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if (enabled)
            return;

        FinishAction();
        compressionHeld = false;
        manualCompactness = 1f;
        flock?.SetManualCompactness(1f);
        movement?.SetActionSpeedScale(1f);
    }

    public static float CalculateImpactForce(int memberCount)
    {
        return Mathf.Max(0, memberCount);
    }

    public static bool MeetsBreakThreshold(float impactForce, int requiredForce)
    {
        return impactForce >= Mathf.Max(1, requiredForce);
    }

    public static float CalculateCompressionSpeedScale(
        float compactness,
        float minimumCompactness,
        float minimumSpeedScale)
    {
        minimumCompactness = Mathf.Clamp(minimumCompactness, 0.2f, 1f);
        float amount = Mathf.Clamp01(
            (1f - compactness) / Mathf.Max(0.0001f, 1f - minimumCompactness));
        return Mathf.Lerp(1f, Mathf.Clamp(minimumSpeedScale, 0.05f, 1f), amount);
    }

    private void UpdateRetreat()
    {
        float requestedDistance = Mathf.Min(
            remainingRetreatDistance,
            Mathf.Max(0f, retreatSpeed) * movement.SpeedMultiplier * Time.fixedDeltaTime);
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
        float requestedDistance = Mathf.Min(
            remainingDashDistance,
            currentDashSpeed * Time.fixedDeltaTime);
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

    private void BeginImpactStun()
    {
        phase = FlockActionPhase.ImpactStun;
        movement.HoldExternalMovement();
        flock.SetGroupActionState(true, true, dashDirection);
    }

    private bool HandleDashBlock(Collider2D blocker)
    {
        bool brokeObstacle = TryBreakObstacle(blocker);
        bool impactAnimationStarted = PlayFlockImpact(hardImpact: !brokeObstacle);
        if (brokeObstacle)
            return true;

        if (impactAnimationStarted)
            BeginImpactStun();
        else
            FinishAction();
        return false;
    }

    private void UpdateCompression(float deltaTime)
    {
        float target = compressionHeld ? minimumManualCompactness : 1f;
        float speed = compressionHeld ? compactingSpeed : restoringSpeed;
        manualCompactness = Mathf.MoveTowards(
            manualCompactness,
            target,
            Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime));
        flock?.SetManualCompactness(manualCompactness);
    }

    private void UpdateMovementSpeedScale()
    {
        if (movement == null)
            return;

        float compressionScale = CalculateCompressionSpeedScale(
            flock != null ? flock.ManualCompactness : manualCompactness,
            minimumManualCompactness,
            compactedMoveSpeedMultiplier);
        movement.SetActionSpeedScale(compressionScale);
    }

    private bool TryBreakObstacle(Collider2D blocker)
    {
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
        if (canBreak)
            breakable.Break();
        return canBreak;
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
        remainingRetreatDistance = 0f;
        remainingWindupTime = 0f;
        remainingDashDistance = 0f;
        currentDashSpeed = 0f;
        currentImpactForce = 0f;
        movement?.EndExternalMovement();
        flock?.SetActionMemberSpeedMultiplier(1f);
        flock?.SetGroupActionState(false, false, dashDirection);
    }

    private void OnDisable()
    {
        FinishAction();
        compressionHeld = false;
        manualCompactness = 1f;
        flock?.SetManualCompactness(1f);
        movement?.SetActionSpeedScale(1f);
    }

    private void OnValidate()
    {
        retreatSpeed = Mathf.Max(0f, retreatSpeed);
        retreatDistance = Mathf.Max(0f, retreatDistance);
        windupDuration = Mathf.Max(0f, windupDuration);
        dashSpeed = Mathf.Max(0.1f, dashSpeed);
        dashDistance = Mathf.Max(0.1f, dashDistance);
    }
}

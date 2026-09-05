using UnityEngine;
using UnityEngine.InputSystem;

public enum FlockDashChargeTier
{
    Tap,
    HalfSecond,
    OneSecond,
    OnePointFiveSeconds,
    TwoSeconds,
}

public readonly struct FlockDashProfile
{
    public FlockDashProfile(
        FlockDashChargeTier tier,
        float speed,
        float distance,
        float impactMultiplier)
    {
        Tier = tier;
        Speed = speed;
        Distance = distance;
        ImpactMultiplier = impactMultiplier;
    }

    public FlockDashChargeTier Tier { get; }
    public float Speed { get; }
    public float Distance { get; }
    public float ImpactMultiplier { get; }
}

/// <summary>
/// Alpha 羊群主动动作：E 短按/蓄力冲刺，Q 持续收拢队形。
/// 围栏的贴身撞击仍由 FenceObstacle 处理，并绑定到 F。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-75)]
[RequireComponent(typeof(FlockController), typeof(FlockMovementController))]
public sealed class FlockActionController : MonoBehaviour
{
    public const float MaximumChargeSeconds = 2f;

    [Header("References")]
    [SerializeField] private FlockController flock;
    [SerializeField] private FlockMovementController movement;

    [Header("Charge")]
    [SerializeField, Range(0.05f, 1f)] private float chargingMoveSpeedMultiplier = 0.35f;

    [Header("Dash Speed And Distance")]
    [SerializeField, Min(0.1f)] private float tapDashSpeed = 8f;
    [SerializeField, Min(0.1f)] private float tapDashDistance = 2.2f;
    [SerializeField, Min(0.1f)] private float halfSecondDashSpeed = 10f;
    [SerializeField, Min(0.1f)] private float halfSecondDashDistance = 3f;
    [SerializeField, Min(0.1f)] private float oneSecondDashSpeed = 12f;
    [SerializeField, Min(0.1f)] private float oneSecondDashDistance = 4f;
    [SerializeField, Min(0.1f)] private float onePointFiveSecondDashSpeed = 14f;
    [SerializeField, Min(0.1f)] private float onePointFiveSecondDashDistance = 5.25f;
    [SerializeField, Min(0.1f)] private float twoSecondDashSpeed = 16f;
    [SerializeField, Min(0.1f)] private float twoSecondDashDistance = 7f;

    [Header("Charge Impact Multipliers")]
    [Tooltip("蓄力达到 1 秒时的冲撞力度倍率。")]
    [SerializeField, Min(0f)] private float oneSecondImpactMultiplier = 1f;
    [Tooltip("蓄力达到 1.5 秒时的冲撞力度倍率。")]
    [SerializeField, Min(0f)] private float onePointFiveSecondImpactMultiplier = 1f;
    [Tooltip("蓄力达到 2 秒时的冲撞力度倍率。")]
    [SerializeField, Min(0f)] private float twoSecondImpactMultiplier = 1f;

    [Header("Q Compression")]
    [SerializeField, Range(0.2f, 1f)] private float minimumManualCompactness = 0.45f;
    [SerializeField, Min(0.01f)] private float compactingSpeed = 0.45f;
    [SerializeField, Min(0.01f)] private float restoringSpeed = 0.2f;
    [SerializeField, Range(0.05f, 1f)] private float compactedMoveSpeedMultiplier = 0.58f;

    private bool controlEnabled = true;
    private bool isCharging;
    private bool compressionHeld;
    private bool isDashing;
    private float chargeStartedAt;
    private float manualCompactness = 1f;
    private float remainingDashDistance;
    private float currentDashSpeed;
    private float currentImpactForce;
    private Vector2 dashDirection = Vector2.right;

    public bool IsCharging => isCharging;
    public bool IsDashing => isDashing;
    public float ChargeSeconds => isCharging
        ? Mathf.Clamp(Time.time - chargeStartedAt, 0f, MaximumChargeSeconds)
        : 0f;
    public float ChargeProgress => ChargeSeconds / MaximumChargeSeconds;
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
            CancelCharge();
            SetCompressionHeld(false);
            UpdateCompression(Time.deltaTime);
            UpdateMovementSpeedScale();
            return;
        }

        if (Time.timeScale == 0f)
        {
            CancelCharge();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            SetCompressionHeld(keyboard.qKey.isPressed);

            if (keyboard.eKey.wasPressedThisFrame)
                BeginCharge();
            if (keyboard.eKey.wasReleasedThisFrame)
                ReleaseCharge();
        }

        UpdateCompression(Time.deltaTime);
        UpdateMovementSpeedScale();
    }

    private void FixedUpdate()
    {
        if (!isDashing || movement == null || flock == null || Time.timeScale == 0f)
            return;

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
            bool brokeObstacle = TryBreakObstacle(blockResult.Blocker);
            PlayLeaderImpact(hardImpact: !brokeObstacle);
            if (!brokeObstacle)
            {
                FinishDash();
                return;
            }
        }

        if (remainingDashDistance <= 0.001f)
            FinishDash();
    }

    public bool BeginCharge()
    {
        if (!controlEnabled || isCharging || isDashing || flock == null || movement == null)
            return false;

        isCharging = true;
        chargeStartedAt = Time.time;
        return true;
    }

    public bool ReleaseCharge()
    {
        if (!isCharging)
            return false;

        float heldSeconds = ChargeSeconds;
        isCharging = false;
        return StartDash(heldSeconds);
    }

    /// <summary>供未来手柄/UI 输入复用；chargeSeconds 会限制在 0～2 秒。</summary>
    public bool StartDash(float chargeSeconds)
    {
        if (!controlEnabled || isDashing || flock == null || movement == null || flock.MemberCount <= 0)
            return false;

        isCharging = false;
        FlockDashProfile profile = GetDashProfile(chargeSeconds);
        Vector2 inputDirection = movement.MoveInput;
        dashDirection = inputDirection.sqrMagnitude > 0.0001f
            ? inputDirection.normalized
            : movement.LastMoveDirection;
        if (dashDirection.sqrMagnitude <= 0.0001f)
            dashDirection = Vector2.right;

        float stageScale = movement.SpeedMultiplier;
        currentDashSpeed = profile.Speed * stageScale;
        remainingDashDistance = profile.Distance * stageScale;
        currentImpactForce = CalculateImpactForce(flock.MemberCount, profile.ImpactMultiplier);
        isDashing = true;

        movement.BeginExternalMovement();
        float memberFollowMultiplier = currentDashSpeed
            / Mathf.Max(0.1f, movement.UnmodifiedCurrentSpeedLimit);
        flock.SetActionMemberSpeedMultiplier(memberFollowMultiplier);
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

        CancelCharge();
        FinishDash();
        compressionHeld = false;
        manualCompactness = 1f;
        flock?.SetManualCompactness(1f);
        movement?.SetActionSpeedScale(1f);
    }

    public FlockDashProfile GetDashProfile(float chargeSeconds)
    {
        FlockDashChargeTier tier = ResolveChargeTier(chargeSeconds);
        return tier switch
        {
            FlockDashChargeTier.HalfSecond => new FlockDashProfile(
                tier, halfSecondDashSpeed, halfSecondDashDistance, 1f),
            FlockDashChargeTier.OneSecond => new FlockDashProfile(
                tier, oneSecondDashSpeed, oneSecondDashDistance, oneSecondImpactMultiplier),
            FlockDashChargeTier.OnePointFiveSeconds => new FlockDashProfile(
                tier,
                onePointFiveSecondDashSpeed,
                onePointFiveSecondDashDistance,
                onePointFiveSecondImpactMultiplier),
            FlockDashChargeTier.TwoSeconds => new FlockDashProfile(
                tier, twoSecondDashSpeed, twoSecondDashDistance, twoSecondImpactMultiplier),
            _ => new FlockDashProfile(tier, tapDashSpeed, tapDashDistance, 1f),
        };
    }

    public static FlockDashChargeTier ResolveChargeTier(float chargeSeconds)
    {
        float duration = Mathf.Clamp(chargeSeconds, 0f, MaximumChargeSeconds);
        if (duration >= 2f) return FlockDashChargeTier.TwoSeconds;
        if (duration >= 1.5f) return FlockDashChargeTier.OnePointFiveSeconds;
        if (duration >= 1f) return FlockDashChargeTier.OneSecond;
        if (duration >= 0.5f) return FlockDashChargeTier.HalfSecond;
        return FlockDashChargeTier.Tap;
    }

    public static float CalculateImpactForce(int memberCount, float multiplier)
    {
        return Mathf.Max(0, memberCount) * Mathf.Max(0f, multiplier);
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
            flock != null ? flock.Compactness : manualCompactness,
            minimumManualCompactness,
            compactedMoveSpeedMultiplier);
        float chargeScale = isCharging ? chargingMoveSpeedMultiplier : 1f;
        movement.SetActionSpeedScale(compressionScale * chargeScale);
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

    private void PlayLeaderImpact(bool hardImpact)
    {
        SheepMember leader = flock != null ? flock.Leader : null;
        leader?.GetComponent<SheepVisualAnimator>()?.PlayObstacleImpact(hardImpact, dashDirection);
    }

    private void CancelCharge()
    {
        isCharging = false;
    }

    private void FinishDash()
    {
        if (!isDashing)
            return;

        isDashing = false;
        remainingDashDistance = 0f;
        currentDashSpeed = 0f;
        currentImpactForce = 0f;
        movement?.EndExternalMovement();
        flock?.SetActionMemberSpeedMultiplier(1f);
    }

    private void OnDisable()
    {
        CancelCharge();
        FinishDash();
        compressionHeld = false;
        manualCompactness = 1f;
        flock?.SetManualCompactness(1f);
        movement?.SetActionSpeedScale(1f);
    }

    private void OnValidate()
    {
        halfSecondDashSpeed = Mathf.Max(tapDashSpeed, halfSecondDashSpeed);
        oneSecondDashSpeed = Mathf.Max(halfSecondDashSpeed, oneSecondDashSpeed);
        onePointFiveSecondDashSpeed = Mathf.Max(oneSecondDashSpeed, onePointFiveSecondDashSpeed);
        twoSecondDashSpeed = Mathf.Max(onePointFiveSecondDashSpeed, twoSecondDashSpeed);
        halfSecondDashDistance = Mathf.Max(tapDashDistance, halfSecondDashDistance);
        oneSecondDashDistance = Mathf.Max(halfSecondDashDistance, oneSecondDashDistance);
        onePointFiveSecondDashDistance = Mathf.Max(oneSecondDashDistance, onePointFiveSecondDashDistance);
        twoSecondDashDistance = Mathf.Max(onePointFiveSecondDashDistance, twoSecondDashDistance);
    }
}

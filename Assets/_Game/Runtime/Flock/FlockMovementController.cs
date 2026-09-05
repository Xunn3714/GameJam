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
    private Rect movementBounds;
    private float temporarySpeedLimit;
    private float temporarySpeedLimitExpiresAt;
    private float speedMultiplier = 1f;

    /// <summary>随羊群规模 / 镜头放大整体提速；速度上限和加速度一起乘。</summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public float SpeedMultiplier => speedMultiplier;

    public Vector2 LastMoveDirection { get; private set; } = Vector2.right;
    public bool FacingLeft { get; private set; }
    public Vector2 MoveInput => moveInput;
    public bool HasMoveInput => moveInput.sqrMagnitude > 0.0001f;
    public bool IsMoving => velocity.sqrMagnitude > StopSpeed * StopSpeed;
    public Vector2 DesiredVelocity => velocity;
    public Vector2 Velocity => velocity;
    public float NormalSpeedLimit => normalSpeedLimit;
    public float CurrentSpeedLimit => (HasTemporarySpeedLimit
        ? Mathf.Max(normalSpeedLimit, temporarySpeedLimit)
        : normalSpeedLimit) * speedMultiplier;

    private bool HasTemporarySpeedLimit =>
        temporarySpeedLimit > normalSpeedLimit &&
        Time.time < temporarySpeedLimitExpiresAt;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
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
    }

    private void Update()
    {
        if (!controlEnabled || Time.timeScale == 0f)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : ReadKeyboardFallback();
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        if (moveInput.sqrMagnitude > 0.0001f)
        {
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

        if (!HasTemporarySpeedLimit)
            temporarySpeedLimit = 0f;

        float deltaTime = Time.fixedDeltaTime;
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

        targetPosition = MovementBlocking.ResolveMove(
            body.position,
            targetPosition,
            blockingRadius,
            blockingLayers);

        if (targetPosition == from)
        {
            velocity = Vector2.zero;
            return;
        }

        velocity = (targetPosition - from) / deltaTime;
        movedThisStep = true;
        body.MovePosition(targetPosition);
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
        velocity = Vector2.ClampMagnitude(velocity, normalSpeedLimit);
    }

    public void ConfigureMovementBounds(Rect bounds)
    {
        movementBounds = bounds;
        restrictToMovementBounds = bounds.width > CenterColliderRadius * 2f
            && bounds.height > CenterColliderRadius * 2f;
    }

    public void RejectCurrentMovement()
    {
        if (!movedThisStep)
            return;

        body.position = positionBeforeFixedMove;
        velocity = Vector2.zero;
        movedThisStep = false;
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if (!enabled)
        {
            moveInput = Vector2.zero;
            velocity = Vector2.zero;
        }
    }

    private void OnValidate()
    {
        normalSpeedLimit = Mathf.Max(0f, normalSpeedLimit);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
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

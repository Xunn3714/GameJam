using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class FlockMovementController : MonoBehaviour
{
    private const string MoveActionName = "Player/Move";
    private const float CenterColliderRadius = 0.35f;

    [SerializeField, Min(0f)] private float moveSpeed = 4f;

    private InputAction moveAction;
    private Vector2 moveInput;
    private Rigidbody2D body;
    private Vector2 positionBeforeFixedMove;
    private bool movedThisStep;
    private bool controlEnabled = true;
    private bool restrictToMovementBounds;
    private Rect movementBounds;

    public Vector2 LastMoveDirection { get; private set; } = Vector2.down;
    public bool IsMoving => controlEnabled && moveInput.sqrMagnitude > 0.0001f;
    public Vector2 DesiredVelocity => IsMoving ? moveInput * moveSpeed : Vector2.zero;

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
        }
    }

    private void FixedUpdate()
    {
        movedThisStep = false;
        if (!controlEnabled || Time.timeScale == 0f || moveInput.sqrMagnitude <= 0.0001f)
            return;

        positionBeforeFixedMove = body.position;
        movedThisStep = true;
        Vector2 displacement = moveInput * moveSpeed * Time.fixedDeltaTime;
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

        body.MovePosition(targetPosition);
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
        movedThisStep = false;
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if (!enabled)
        {
            moveInput = Vector2.zero;
        }
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

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class SheepPlayerController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 4f;

    private Rigidbody2D body;
    private Vector2 moveInput;
    private bool controlEnabled = true;

    public Vector2 LastMoveDirection { get; private set; } = Vector2.down;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!controlEnabled || Time.timeScale == 0f || Keyboard.current == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = new Vector2(
            ReadAxis(Keyboard.current.aKey, Keyboard.current.dKey),
            ReadAxis(Keyboard.current.sKey, Keyboard.current.wKey));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        if (moveInput.sqrMagnitude > 0f)
        {
            LastMoveDirection = moveInput;
        }
    }

    private void FixedUpdate()
    {
        body.MovePosition(body.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if (!enabled) moveInput = Vector2.zero;
    }

    private static float ReadAxis(KeyControl negative, KeyControl positive)
    {
        return (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
    }
}

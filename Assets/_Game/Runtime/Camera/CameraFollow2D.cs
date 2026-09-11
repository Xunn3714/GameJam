using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CameraFollow2D : MonoBehaviour
{
    private const float SettleDistance = 0.01f;
    private const float ZoomComparisonTolerance = 0.001f;

    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;
    [SerializeField, Min(0f)] private float zoomSmoothTime = 0.6f;

    [Header("Player Zoom")]
    [Tooltip("游戏进行时允许鼠标滚轮调整显示范围。滚轮向上放大画面，向下拉远画面。")]
    [SerializeField] private bool enableMouseWheelZoom = true;
    [Tooltip("玩家能缩小到的最低正交视角。阶段配置只控制最高视角。")]
    [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 3.5f;
    [Tooltip("每次滚轮输入改变多少正交视角单位。")]
    [SerializeField, Min(0.05f)] private float mouseWheelZoomStep = 1f;

    private Vector3 velocity;
    private float zoomVelocity;
    private float targetOrthographicSize;
    private float maximumOrthographicSize;
    private float cameraZ;
    private FlockController flockController;
    private Camera attachedCamera;
    private bool keepInsideBounds;
    private Rect cameraBounds;
    private float shakeAmplitude;
    private float shakeDuration;
    private float shakeTimer;
    private Vector2 shakeDirection = Vector2.right;
    private Vector3 basePosition;
    private bool hasBasePosition;

    /// <summary>触发一次镜头震动（例如撞破围栏）。</summary>
    public void Shake(float amplitude, float duration)
    {
        shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude);
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTimer = shakeDuration;
        Vector2 randomDirection = Random.insideUnitCircle;
        shakeDirection = randomDirection.sqrMagnitude > 0.0001f
            ? randomDirection.normalized
            : Vector2.right;
    }

    public float CurrentOrthographicSize => attachedCamera != null && attachedCamera.orthographic
        ? attachedCamera.orthographicSize
        : targetOrthographicSize;
    public float TargetOrthographicSize => targetOrthographicSize;
    public float MinimumOrthographicSize => Mathf.Max(0.1f, minimumOrthographicSize);
    public float MaximumOrthographicSize => Mathf.Max(MinimumOrthographicSize, maximumOrthographicSize);

    /// <summary>
    /// 玩家重新输入或主动动作接管时清除镜头跟随惯性，避免目标已改向、
    /// 画面却仍沿上一次自动回正方向继续滑动。
    /// </summary>
    public void ResetFollowVelocity()
    {
        velocity = Vector3.zero;
    }

    private void Awake()
    {
        cameraZ = transform.position.z;
        EnsureZoomState();
    }

    private void LateUpdate()
    {
        HandleMouseWheelZoom();

        if (target == null)
            return;

        // 震动是叠加在跟随位置上的偏移，先把上一帧的偏移去掉再算跟随。
        if (hasBasePosition)
        {
            transform.position = basePosition;
        }
        FollowTarget();
        basePosition = transform.position;
        hasBasePosition = true;
        ApplyShake();
    }

    private void ApplyShake()
    {
        if (shakeTimer <= 0f)
            return;

        float progress = 1f - Mathf.Clamp01(shakeTimer / shakeDuration);
        float envelope = 1f - progress;
        envelope *= envelope;
        float angle = progress * Mathf.PI * 6f;
        Vector2 perpendicular = new Vector2(-shakeDirection.y, shakeDirection.x);
        Vector2 offset = (
            shakeDirection * Mathf.Cos(angle)
            + perpendicular * Mathf.Sin(angle * 0.83f) * 0.35f)
            * (shakeAmplitude * envelope);
        transform.position = basePosition + new Vector3(offset.x, offset.y, 0f);

        shakeTimer -= Time.deltaTime;

        if (shakeTimer <= 0f)
        {
            shakeAmplitude = 0f;
            transform.position = basePosition;
        }
    }

    private void FollowTarget()
    {
        if (flockController == null)
        {
            flockController = target.GetComponent<FlockController>();
        }

        Vector2 focusPosition = flockController != null
            ? flockController.CameraFocus
            : (Vector2)target.position;

        UpdateZoom();

        Vector3 targetPosition = new Vector3(
            focusPosition.x,
            focusPosition.y,
            cameraZ
        );

        if (keepInsideBounds && attachedCamera != null && attachedCamera.orthographic)
        {
            float halfHeight = attachedCamera.orthographicSize;
            float halfWidth = halfHeight * attachedCamera.aspect;
            targetPosition.x = ClampInside(
                targetPosition.x,
                cameraBounds.xMin + halfWidth,
                cameraBounds.xMax - halfWidth);
            targetPosition.y = ClampInside(
                targetPosition.y,
                cameraBounds.yMin + halfHeight,
                cameraBounds.yMax - halfHeight);
        }

        if ((targetPosition - transform.position).sqrMagnitude <= SettleDistance * SettleDistance)
        {
            transform.position = targetPosition;
            velocity = Vector3.zero;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );
    }

    public void ConfigureBounds(Rect bounds)
    {
        cameraBounds = bounds;
        keepInsideBounds = bounds.width > 0f && bounds.height > 0f;
    }

    public void SetOrthographicSize(float size, bool immediate = false)
    {
        EnsureAttachedCamera();
        if (attachedCamera == null || !attachedCamera.orthographic)
            return;

        targetOrthographicSize = Mathf.Clamp(
            size,
            MinimumOrthographicSize,
            MaximumOrthographicSize);
        if (!immediate)
            return;

        attachedCamera.orthographicSize = targetOrthographicSize;
        zoomVelocity = 0f;
    }

    /// <summary>
    /// 按历史最高羊数单向解锁更大视野。首次解锁更高视野时自动把目标
    /// 拉到新的阶段尺寸；重复应用同一阶段不会覆盖玩家之后的滚轮选择。
    /// </summary>
    public void UnlockMaximumOrthographicSize(float size)
    {
        EnsureZoomState();
        float previousMaximum = MaximumOrthographicSize;
        float unlockedMaximum = Mathf.Max(previousMaximum, MinimumOrthographicSize, size);
        if (unlockedMaximum <= previousMaximum + ZoomComparisonTolerance)
            return;

        maximumOrthographicSize = unlockedMaximum;
        targetOrthographicSize = unlockedMaximum;
    }

    /// <summary>正方向放大画面、缩小显示范围；负方向拉远画面、扩大显示范围。</summary>
    public bool AdjustOrthographicSize(float scrollDirection)
    {
        if (Mathf.Abs(scrollDirection) <= Mathf.Epsilon)
            return false;

        float previousTarget = targetOrthographicSize;
        float nextTarget = previousTarget - Mathf.Sign(scrollDirection) * mouseWheelZoomStep;
        SetOrthographicSize(nextTarget);
        return Mathf.Abs(targetOrthographicSize - previousTarget) > ZoomComparisonTolerance;
    }

    private void HandleMouseWheelZoom()
    {
        if (!enableMouseWheelZoom || Time.timeScale <= 0f)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > Mathf.Epsilon)
            AdjustOrthographicSize(scroll);
    }

    private void EnsureAttachedCamera()
    {
        if (attachedCamera == null)
            attachedCamera = GetComponent<Camera>();
    }

    private void EnsureZoomState()
    {
        EnsureAttachedCamera();
        if (targetOrthographicSize > 0f || attachedCamera == null || !attachedCamera.orthographic)
            return;

        targetOrthographicSize = attachedCamera.orthographicSize;
        maximumOrthographicSize = Mathf.Max(MinimumOrthographicSize, targetOrthographicSize);
    }

    private void UpdateZoom()
    {
        if (attachedCamera == null || !attachedCamera.orthographic || targetOrthographicSize <= 0f)
            return;

        if (Mathf.Abs(attachedCamera.orthographicSize - targetOrthographicSize) <= ZoomComparisonTolerance)
        {
            attachedCamera.orthographicSize = targetOrthographicSize;
            zoomVelocity = 0f;
            return;
        }

        attachedCamera.orthographicSize = Mathf.SmoothDamp(
            attachedCamera.orthographicSize,
            targetOrthographicSize,
            ref zoomVelocity,
            zoomSmoothTime);
    }

    private static float ClampInside(float value, float minimum, float maximum)
    {
        return minimum <= maximum
            ? Mathf.Clamp(value, minimum, maximum)
            : (minimum + maximum) * 0.5f;
    }
}

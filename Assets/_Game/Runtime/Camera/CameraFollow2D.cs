using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    private const float SettleDistance = 0.01f;

    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;
    [SerializeField, Min(0f)] private float zoomSmoothTime = 0.6f;

    private Vector3 velocity;
    private float zoomVelocity;
    private float targetOrthographicSize;
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

    private void Awake()
    {
        cameraZ = transform.position.z;
        attachedCamera = GetComponent<Camera>();
        targetOrthographicSize = attachedCamera != null && attachedCamera.orthographic
            ? attachedCamera.orthographicSize
            : 0f;
    }

    private void LateUpdate()
    {
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
            ? flockController.Center
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
        if (attachedCamera == null)
            attachedCamera = GetComponent<Camera>();

        if (attachedCamera == null || !attachedCamera.orthographic)
            return;

        targetOrthographicSize = Mathf.Max(0.01f, size);
        if (!immediate)
            return;

        attachedCamera.orthographicSize = targetOrthographicSize;
        zoomVelocity = 0f;
    }

    private void UpdateZoom()
    {
        if (attachedCamera == null || !attachedCamera.orthographic || targetOrthographicSize <= 0f)
            return;

        if (Mathf.Abs(attachedCamera.orthographicSize - targetOrthographicSize) <= 0.001f)
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

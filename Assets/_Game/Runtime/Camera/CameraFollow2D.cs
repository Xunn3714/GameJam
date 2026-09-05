using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    private const float SettleDistance = 0.01f;

    [Header("Follow")]
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    [Header("Camera Shake")]
    [SerializeField, Min(0f)] private float defaultShakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float defaultShakeStrength = 0.10f;

    private Vector3 velocity;
    private Vector3 basePosition;

    private float cameraZ;

    private FlockController flockController;
    private Camera attachedCamera;

    private bool keepInsideBounds;
    private Rect cameraBounds;

    private float shakeTimeRemaining;
    private float shakeTotalDuration;
    private float shakeStrength;


    private void Awake()
    {
        cameraZ = transform.position.z;
        basePosition = transform.position;

        attachedCamera = GetComponent<Camera>();
    }


    private void LateUpdate()
    {
        UpdateFollowPosition();
        ApplyCameraShake();
    }


    private void UpdateFollowPosition()
    {
        if (target == null)
            return;


        if (flockController == null)
        {
            flockController =
                target.GetComponent<FlockController>();
        }


        Vector2 focusPosition =
            flockController != null
                ? flockController.Center
                : (Vector2)target.position;


        Vector3 targetPosition =
            new Vector3(
                focusPosition.x,
                focusPosition.y,
                cameraZ
            );


        // =========================
        // Camera Bounds
        // =========================

        if (keepInsideBounds &&
            attachedCamera != null &&
            attachedCamera.orthographic)
        {
            float halfHeight =
                attachedCamera.orthographicSize;

            float halfWidth =
                halfHeight * attachedCamera.aspect;


            targetPosition.x =
                ClampInside(
                    targetPosition.x,
                    cameraBounds.xMin + halfWidth,
                    cameraBounds.xMax - halfWidth
                );


            targetPosition.y =
                ClampInside(
                    targetPosition.y,
                    cameraBounds.yMin + halfHeight,
                    cameraBounds.yMax - halfHeight
                );
        }


        // =========================
        // Follow
        // =========================

        if ((targetPosition - basePosition).sqrMagnitude
            <= SettleDistance * SettleDistance)
        {
            basePosition = targetPosition;
            velocity = Vector3.zero;
            return;
        }


        basePosition =
            Vector3.SmoothDamp(
                basePosition,
                targetPosition,
                ref velocity,
                smoothTime
            );
    }


    private void ApplyCameraShake()
    {
        if (shakeTimeRemaining <= 0f)
        {
            shakeTimeRemaining = 0f;
            shakeStrength = 0f;

            transform.position = basePosition;
            return;
        }


        // 使用 Unscaled 时间：
        // 即使撞击时 TimeScale 被降低，
        // Camera Shake 仍然保持正常速度。
        shakeTimeRemaining -=
            Time.unscaledDeltaTime;


        float strengthMultiplier = 1f;

        if (shakeTotalDuration > 0f)
        {
            strengthMultiplier =
                Mathf.Clamp01(
                    shakeTimeRemaining /
                    shakeTotalDuration
                );
        }


        Vector2 randomOffset =
            Random.insideUnitCircle
            * shakeStrength
            * strengthMultiplier;


        transform.position =
            basePosition +
            new Vector3(
                randomOffset.x,
                randomOffset.y,
                0f
            );
    }


    /// <summary>
    /// 使用默认参数播放一次 Camera Shake。
    /// </summary>
    public void Shake()
    {
        Shake(
            defaultShakeDuration,
            defaultShakeStrength
        );
    }


    /// <summary>
    /// 播放 Camera Shake。
    /// </summary>
    public void Shake(
        float duration,
        float strength
    )
    {
        if (duration <= 0f ||
            strength <= 0f)
        {
            return;
        }


        // 连续撞击时延长 / 加强已有 Shake，
        // 不会强制先结束上一段。
        shakeTimeRemaining =
            Mathf.Max(
                shakeTimeRemaining,
                duration
            );


        shakeTotalDuration =
            Mathf.Max(
                shakeTotalDuration,
                duration
            );


        shakeStrength =
            Mathf.Max(
                shakeStrength,
                strength
            );
    }


    public void ConfigureBounds(Rect bounds)
    {
        cameraBounds = bounds;

        keepInsideBounds =
            bounds.width > 0f &&
            bounds.height > 0f;
    }


    private static float ClampInside(
        float value,
        float minimum,
        float maximum
    )
    {
        return minimum <= maximum
            ? Mathf.Clamp(
                value,
                minimum,
                maximum
            )
            : (minimum + maximum) * 0.5f;
    }
}

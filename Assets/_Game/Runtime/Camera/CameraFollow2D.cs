using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    private const float SettleDistance = 0.01f;

    [Header("Follow")]
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    [Header("Zoom")]
    [SerializeField, Min(0f)] private float zoomSmoothTime = 0.6f;

    [Header("Camera Shake")]
    [SerializeField, Min(0f)] private float defaultShakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float defaultShakeStrength = 0.10f;

    private Vector3 velocity;
    private Vector3 basePosition;

    private float zoomVelocity;
    private float targetOrthographicSize;

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

        targetOrthographicSize =
            attachedCamera != null && attachedCamera.orthographic
                ? attachedCamera.orthographicSize
                : 0f;
    }


    private void LateUpdate()
    {
        UpdateZoom();
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


    // =========================================================
    // Camera Shake
    // =========================================================

    /// <summary>
    /// 使用默认参数播放一次镜头震动。
    /// </summary>
    public void Shake()
    {
        Shake(
            defaultShakeStrength,
            defaultShakeDuration
        );
    }


    /// <summary>
    /// 播放镜头震动。
    /// 参数顺序保持 main 的接口：
    /// amplitude = 震动强度
    /// duration = 持续时间
    /// </summary>
    public void Shake(
        float amplitude,
        float duration
    )
    {
        if (amplitude <= 0f ||
            duration <= 0f)
        {
            return;
        }

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
                amplitude
            );
    }


    private void ApplyCameraShake()
    {
        if (shakeTimeRemaining <= 0f)
        {
            shakeTimeRemaining = 0f;
            shakeTotalDuration = 0f;
            shakeStrength = 0f;

            transform.position = basePosition;
            return;
        }

        // Hit Slow 时仍保持正常的震动速度。
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


    // =========================================================
    // Camera Zoom
    // =========================================================

    public void SetOrthographicSize(
        float size,
        bool immediate = false
    )
    {
        if (attachedCamera == null)
        {
            attachedCamera =
                GetComponent<Camera>();
        }

        if (attachedCamera == null ||
            !attachedCamera.orthographic)
        {
            return;
        }

        targetOrthographicSize =
            Mathf.Max(
                0.01f,
                size
            );

        if (!immediate)
            return;

        attachedCamera.orthographicSize =
            targetOrthographicSize;

        zoomVelocity = 0f;
    }


    private void UpdateZoom()
    {
        if (attachedCamera == null ||
            !attachedCamera.orthographic ||
            targetOrthographicSize <= 0f)
        {
            return;
        }

        if (Mathf.Abs(
                attachedCamera.orthographicSize -
                targetOrthographicSize
            ) <= 0.001f)
        {
            attachedCamera.orthographicSize =
                targetOrthographicSize;

            zoomVelocity = 0f;
            return;
        }

        attachedCamera.orthographicSize =
            Mathf.SmoothDamp(
                attachedCamera.orthographicSize,
                targetOrthographicSize,
                ref zoomVelocity,
                zoomSmoothTime
            );
    }


    // =========================================================
    // Camera Bounds
    // =========================================================

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

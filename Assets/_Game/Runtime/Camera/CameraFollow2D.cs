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

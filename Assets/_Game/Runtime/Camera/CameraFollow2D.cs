using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    private const float SettleDistance = 0.01f;

    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    private Vector3 velocity;
    private float cameraZ;
    private FlockController flockController;

    private void Awake()
    {
        cameraZ = transform.position.z;
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

        Vector3 targetPosition = new Vector3(
            focusPosition.x,
            focusPosition.y,
            cameraZ
        );

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
}

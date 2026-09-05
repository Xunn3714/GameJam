using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional long-wolf attack. A swept rectangular body captures every active sheep it touches.
/// Designer edits length/width on LongWolf.prefab; runtime reads them without changing configuration.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Wolf))]
public sealed class LongWolfSweep : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float bodyLength = 7f;
    [SerializeField, Min(0.1f)] private float bodyWidth = 1.3f;
    [Tooltip("Unit body: local X from -1 to 0, local Y from -0.5 to 0.5.")]
    [SerializeField] private Transform bodyVisual;
    [SerializeField] private Transform headVisual;

    private readonly List<SheepMember> candidates = new List<SheepMember>();
    public float BodyLength => bodyLength;
    public float BodyWidth => bodyWidth;
    public int CapturedCount { get; private set; }

    public void SetDirection(Vector2 direction)
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        if (bodyVisual != null)
        {
            bodyVisual.localRotation = rotation;
            bodyVisual.localScale = new Vector3(bodyLength, bodyWidth, 1f);
        }
        if (headVisual != null)
            headVisual.localRotation = rotation;
    }

    public void Sweep(Wolf wolf, FlockController flock, Vector2 previousHead, Vector2 nextHead, Vector2 direction)
    {
        if (flock == null)
            return;

        // Copy before removal: captures immediately modify the roster and notify game-over listeners.
        candidates.Clear();
        candidates.AddRange(flock.Members);
        foreach (SheepMember sheep in candidates)
        {
            if (sheep == null || sheep.Flock != flock)
                continue;
            CircleCollider2D collider = sheep.GetComponent<CircleCollider2D>();
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            Vector2 center = collider.transform.TransformPoint(collider.offset);
            Vector3 scale = collider.transform.lossyScale;
            float radius = collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            if (!TouchesSweep(center, radius, previousHead, nextHead, direction, bodyLength, bodyWidth))
                continue;

            wolf.CaptureAlongPath(sheep, CapturedCount++);
        }
    }

    // Circle versus swept rectangle, including the tail and distance travelled during this physics step.
    public static bool TouchesSweep(Vector2 center, float radius, Vector2 previousHead,
        Vector2 nextHead, Vector2 direction, float length, float width)
    {
        Vector2 forward = direction.normalized;
        Vector2 relative = center - previousHead;
        float along = Vector2.Dot(relative, forward);
        float across = Vector2.Dot(relative, new Vector2(-forward.y, forward.x));
        float travel = Mathf.Max(0f, Vector2.Dot(nextHead - previousHead, forward));
        float dx = along - Mathf.Clamp(along, -length, travel);
        float dy = across - Mathf.Clamp(across, -width * 0.5f, width * 0.5f);
        return dx * dx + dy * dy <= radius * radius;
    }

    private void OnValidate()
    {
        bodyLength = Mathf.Max(0.5f, bodyLength);
        bodyWidth = Mathf.Max(0.1f, bodyWidth);
        SetDirection(Vector2.right);
    }
}

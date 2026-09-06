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
    [SerializeField, Min(0.1f)] private float bodyWidth = 0.8f;
    [Header("Screen Coverage Growth")]
    [Tooltip("Optional gameplay camera. Falls back to the tagged main camera at launch.")]
    [SerializeField] private Camera coverageCamera;
    [SerializeField] private bool scaleLengthToScreen = true;
    [SerializeField, Min(0f)] private float minCoverageSeconds = 0.8f;
    [SerializeField, Min(0f)] private float maxCoverageSeconds = 1.2f;
    [Tooltip("Unit body: local X from -1 to 0, local Y from -0.5 to 0.5.")]
    [SerializeField] private Transform bodyVisual;
    [SerializeField] private Transform headVisual;
    [Header("Skins")]
    [Tooltip("美术配置的长狼皮肤；每次出场等概率选一个，运行时不修改配置。")]
    [SerializeField] private Sprite[] skins = System.Array.Empty<Sprite>();
    [SerializeField] private SpriteRenderer skinRenderer;
    public int SelectedSkinIndex { get; private set; } = -1;

    private readonly List<SheepMember> candidates = new List<SheepMember>();
    private float runtimeLength;
    private float runtimeWidthMultiplier = 1f;
    public float BodyLength => runtimeLength > 0f ? runtimeLength : bodyLength;
    public float BodyWidth => bodyWidth * runtimeWidthMultiplier;
    public int CapturedCount { get; private set; }
    public float CoverageSeconds { get; private set; }
    public float ClearTravelDistance { get; private set; }

    public void Prepare()
    {
        SelectedSkinIndex = -1;
        if (skinRenderer != null && skins != null && skins.Length > 0)
        {
            SelectedSkinIndex = Random.Range(0, skins.Length);
            skinRenderer.sprite = skins[SelectedSkinIndex];
        }
        runtimeLength = 0f;
        runtimeWidthMultiplier = 1f;
        ClearTravelDistance = 0f;
        CoverageSeconds = Random.Range(minCoverageSeconds, maxCoverageSeconds);
        if (coverageCamera == null) coverageCamera = Camera.main;
    }

    public void ConfigureCharge(Vector2 origin, Vector2 direction, float speed)
    {
        if (!scaleLengthToScreen || speed <= 0f || !TryGetScreenSpan(origin, direction, out float min, out float max))
            return;

        runtimeLength = CalculateCoverageLength(max - min, speed, CoverageSeconds);
        ClearTravelDistance = Mathf.Max(0f, max) + runtimeLength + BodyWidth;
        SetDirection(direction);
    }

    /// <summary>仅影响本次出场的身体宽度，不修改 Prefab 配置。</summary>
    public void SetRuntimeWidthMultiplier(float multiplier)
    {
        runtimeWidthMultiplier = Mathf.Max(0.1f, multiplier);
    }

    // Visual only: extend both ends beyond the viewport without changing the attack duration.
    public Vector2 GetWarningSpan(Vector2 origin, Vector2 direction, float fallbackLength)
    {
        if (!TryGetScreenSpan(origin, direction, out float min, out float max))
            return new Vector2(0f, fallbackLength);
        const float screenMargin = 2f;
        return new Vector2(Mathf.Min(0f, min - screenMargin), Mathf.Max(fallbackLength, max + screenMargin));
    }

    private bool TryGetScreenSpan(Vector2 origin, Vector2 direction, out float min, out float max)
    {
        min = float.PositiveInfinity;
        max = float.NegativeInfinity;
        if (coverageCamera == null || !coverageCamera.orthographic || direction.sqrMagnitude < 0.0001f)
            return false;
        // Project the viewport onto the attack axis. This also covers diagonal attacks.
        Plane gamePlane = new Plane(Vector3.forward, Vector3.zero);
        for (int y = 0; y < 2; y++)
        for (int x = 0; x < 2; x++)
        {
            Ray ray = coverageCamera.ViewportPointToRay(new Vector3(x, y, 0f));
            if (!gamePlane.Raycast(ray, out float distance)) return false;
            float along = Vector2.Dot((Vector2)ray.GetPoint(distance) - origin, direction.normalized);
            min = Mathf.Min(min, along);
            max = Mathf.Max(max, along);
        }
        return true;
    }

    public static float CalculateCoverageLength(float screenSpan, float speed, float seconds)
        => Mathf.Max(0.5f, Mathf.Max(0f, screenSpan) + Mathf.Max(0f, speed) * Mathf.Max(0f, seconds));

    public void SetDirection(Vector2 direction)
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        if (skinRenderer != null && skinRenderer.sprite != null)
        {
            if (bodyVisual != null) bodyVisual.gameObject.SetActive(false);
            if (headVisual != null) headVisual.gameObject.SetActive(false);
            float height = skinRenderer.sprite.rect.height / skinRenderer.sprite.pixelsPerUnit;
            float scale = bodyWidth / Mathf.Max(0.001f, height);
            skinRenderer.transform.localRotation = rotation;
            skinRenderer.transform.localPosition = (Vector3)(-direction.normalized * BodyLength * 0.5f);
            skinRenderer.transform.localScale = Vector3.one * scale;
            skinRenderer.drawMode = SpriteDrawMode.Tiled;
            skinRenderer.tileMode = SpriteTileMode.Continuous;
            skinRenderer.size = new Vector2(BodyLength / scale, height);
            // Keep paws below the body when the attack comes from the right.
            skinRenderer.flipY = direction.x < 0f;
            return;
        }
        if (bodyVisual != null)
        {
            bodyVisual.localRotation = rotation;
            bodyVisual.localScale = new Vector3(BodyLength, bodyWidth, 1f);
        }
        if (headVisual != null)
        {
            headVisual.localRotation = rotation;
            // Keep the original head proportions relative to the narrower body.
            headVisual.localScale = Vector3.one * (bodyWidth / 1.3f);
        }
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
            if (!TouchesSweep(center, radius, previousHead, nextHead, direction, BodyLength, bodyWidth))
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
        minCoverageSeconds = Mathf.Max(0f, minCoverageSeconds);
        maxCoverageSeconds = Mathf.Max(minCoverageSeconds, maxCoverageSeconds);
    }
}

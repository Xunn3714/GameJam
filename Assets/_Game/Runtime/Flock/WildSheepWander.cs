using UnityEngine;

/// <summary>
/// 未入队羊的轻量状态机：在出生点附近缓慢徘徊，并随机停下来发呆。
/// 招募后由 SheepFlockAgent 接管；再次离队时可以恢复徘徊。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class WildSheepWander : MonoBehaviour
{
    private enum WanderState
    {
        Walking,
        Daydreaming,
    }

    [Header("Wander")]
    [SerializeField, Min(0f)] private float maximumSpeed = 0.62f;
    [SerializeField, Min(0f)] private float acceleration = 1.35f;
    [SerializeField, Min(0.5f)] private float roamingRadius = 3.8f;
    [SerializeField, Min(0.1f)] private float minimumWalkDuration = 1.4f;
    [SerializeField, Min(0.1f)] private float maximumWalkDuration = 3.4f;

    [Header("Daydream")]
    [SerializeField, Min(0.1f)] private float minimumDaydreamDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float maximumDaydreamDuration = 4.2f;
    [SerializeField, Range(0f, 1f)] private float startDaydreamChance = 0.6f;

    [Header("Blocking")]
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField, Min(0f)] private float blockingRadius;

    private Rigidbody2D body;
    private SheepVisualAnimator visualAnimator;
    private WanderState state;
    private Rect worldBounds;
    private Vector2 home;
    private Vector2 direction;
    private Vector2 velocity;
    private float stateTimer;
    private float nextImpactFeedbackTime;
    private bool hasWorldBounds;
    private bool isRecruited;

    public static WildSheepWander Ensure(GameObject sheep)
    {
        if (sheep == null)
            return null;

        WildSheepWander wander = sheep.GetComponent<WildSheepWander>();
        return wander != null ? wander : sheep.AddComponent<WildSheepWander>();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        circle.isTrigger = true;
        blockingRadius = blockingRadius > 0f ? blockingRadius : circle.radius;
        if (blockingLayers.value == 0)
            blockingLayers = MovementBlocking.DefaultMask();

        visualAnimator = SheepVisualAnimator.Ensure(gameObject);
        home = body.position;
        BeginRandomState();
    }

    public void Configure(Rect bounds, float roamingLimit = -1f)
    {
        worldBounds = bounds;
        home = transform.position;
        hasWorldBounds = bounds.width > 0f && bounds.height > 0f;
        if (roamingLimit > 0f)
            roamingRadius = roamingLimit;
    }

    public void SetRecruited(bool recruited)
    {
        isRecruited = recruited;
        velocity = Vector2.zero;
        enabled = !recruited;

        if (!recruited)
        {
            home = transform.position;
            BeginDaydream();
        }
    }

    private void FixedUpdate()
    {
        if (isRecruited || body == null || Time.timeScale == 0f)
            return;

        ScatteredSheep scattered = GetComponent<ScatteredSheep>();
        if (scattered != null && scattered.IsScattered)
            return;

        float deltaTime = Time.fixedDeltaTime;
        stateTimer -= deltaTime;
        if (stateTimer <= 0f)
        {
            if (state == WanderState.Walking && Random.value < startDaydreamChance)
                BeginDaydream();
            else
                BeginWalking();
        }

        Vector2 desiredVelocity = state == WanderState.Walking
            ? direction * maximumSpeed
            : Vector2.zero;
        velocity = Vector2.MoveTowards(velocity, desiredVelocity, acceleration * deltaTime);
        if (velocity.sqrMagnitude <= 0.0001f)
        {
            velocity = Vector2.zero;
            return;
        }

        Vector2 from = body.position;
        Vector2 target = ClampToAllowedArea(from + velocity * deltaTime);
        target = MovementBlocking.ResolveMove(
            from,
            target,
            blockingRadius,
            blockingLayers,
            out MovementBlockResult blockResult);

        if (blockResult.WasBlocked)
        {
            PlayImpact(blockResult.FullyBlocked);
            velocity = blockResult.FullyBlocked ? -direction * maximumSpeed * 0.45f : velocity;
            BeginDaydream();
        }

        if (target != from)
            body.MovePosition(target);
    }

    private Vector2 ClampToAllowedArea(Vector2 target)
    {
        Vector2 offset = target - home;
        if (offset.sqrMagnitude > roamingRadius * roamingRadius)
        {
            direction = (-offset).normalized;
            target = home + Vector2.ClampMagnitude(offset, roamingRadius);
        }

        if (!hasWorldBounds)
            return target;

        float margin = Mathf.Max(0.1f, blockingRadius);
        Vector2 clamped = new(
            Mathf.Clamp(target.x, worldBounds.xMin + margin, worldBounds.xMax - margin),
            Mathf.Clamp(target.y, worldBounds.yMin + margin, worldBounds.yMax - margin));
        if (clamped != target)
            direction = (home - body.position).normalized;
        return clamped;
    }

    private void BeginRandomState()
    {
        if (Random.value < 0.5f)
            BeginWalking();
        else
            BeginDaydream();
    }

    private void BeginWalking()
    {
        state = WanderState.Walking;
        stateTimer = Random.Range(minimumWalkDuration, maximumWalkDuration);
        direction = PickDirection();
    }

    private void BeginDaydream()
    {
        state = WanderState.Daydreaming;
        stateTimer = Random.Range(minimumDaydreamDuration, maximumDaydreamDuration);
        visualAnimator?.PlayDaydreamGesture();
    }

    private Vector2 PickDirection()
    {
        Vector2 towardHome = home - body.position;
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        if (towardHome.sqrMagnitude > roamingRadius * roamingRadius * 0.35f)
            randomDirection = (randomDirection + towardHome.normalized * 1.5f).normalized;
        return randomDirection.sqrMagnitude > 0.001f ? randomDirection : Vector2.right;
    }

    private void PlayImpact(bool hardImpact)
    {
        if (Time.time < nextImpactFeedbackTime)
            return;

        nextImpactFeedbackTime = Time.time + (hardImpact ? 0.5f : 0.2f);
        visualAnimator?.PlayObstacleImpact(hardImpact, direction);
    }

    private void OnValidate()
    {
        maximumWalkDuration = Mathf.Max(maximumWalkDuration, minimumWalkDuration);
        maximumDaydreamDuration = Mathf.Max(maximumDaydreamDuration, minimumDaydreamDuration);
    }
}

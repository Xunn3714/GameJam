using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(100)]
public sealed class SheepVisualAnimator : MonoBehaviour
{
    private const float MovementThreshold = 0.035f;
    private const float HorizontalFacingThreshold = 0.08f;
    private const float FlipDuration = 0.18f;
    private const float IdleAnimationDuration = 0.78f;
    private const float SoftImpactDuration = 0.26f;
    private const float HardImpactDuration = 0.62f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementStretch = 0.055f;
    [SerializeField, Min(0f)] private float movementBounce = 0.025f;
    [SerializeField, Min(0f)] private float movementTilt = 2f;
    [Tooltip("走路时的抖动频率（每秒几个周期），x = 慢走，y = 全速。")]
    [SerializeField] private Vector2 movementFrequencyRange = new Vector2(2.2f, 3.4f);

    [Header("Idle")]
    [SerializeField, Min(0f)] private float breathingStrength = 0.012f;
    [SerializeField, Min(0f)] private float idleStretch = 0.11f;
    [SerializeField, Min(0.1f)] private float minimumIdleDelay = 1.6f;
    [SerializeField, Min(0.1f)] private float maximumIdleDelay = 4.8f;

    [Header("Obstacle Impact")]
    [SerializeField, Min(0f)] private float softImpactSquash = 0.16f;
    [SerializeField, Min(0f)] private float hardImpactSquash = 0.28f;
    [SerializeField, Min(0f)] private float hardImpactShake = 0.045f;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer animatedRenderer;
    private Transform visualTransform;
    private Vector3 previousPosition;
    private float smoothedSpeed;
    private float movementPhase;
    private float breathingPhase;
    private float idleCountdown;
    private float idleAge = -1f;
    private float flipAge = -1f;
    private float impactAge = -1f;
    private Vector2 impactDirection;
    private bool hardImpact;
    private bool currentFacingLeft;
    private bool requestedFacingLeft;
    private bool wasMoving;

    public void PlayObstacleImpact(bool cannotBreak, Vector2 movementDirection)
    {
        if (impactAge >= 0f && hardImpact && !cannotBreak)
            return;

        hardImpact = cannotBreak;
        impactDirection = movementDirection.sqrMagnitude > 0.001f
            ? movementDirection.normalized
            : Vector2.right;
        impactAge = 0f;
        idleAge = -1f;
        idleCountdown = Mathf.Max(idleCountdown, cannotBreak ? 0.8f : 0.25f);
    }

    public void PlayDaydreamGesture()
    {
        if (impactAge >= 0f)
            return;

        idleAge = 0f;
        idleCountdown = IdleAnimationDuration;
    }

    public static SheepVisualAnimator Ensure(GameObject sheep)
    {
        if (sheep == null || sheep.GetComponent<SpriteRenderer>() == null)
            return null;

        SheepVisualAnimator animator = sheep.GetComponent<SheepVisualAnimator>();
        return animator != null ? animator : sheep.AddComponent<SheepVisualAnimator>();
    }

    private void Awake()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
        CreateAnimatedRenderer();
        breathingPhase = Random.Range(0f, Mathf.PI * 2f);
        ScheduleNextIdle();
        previousPosition = transform.position;
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
        if (sourceRenderer != null)
            sourceRenderer.forceRenderingOff = true;
        if (animatedRenderer != null)
            animatedRenderer.enabled = sourceRenderer != null && sourceRenderer.enabled;
    }

    private void OnDisable()
    {
        if (sourceRenderer != null)
            sourceRenderer.forceRenderingOff = false;
        if (animatedRenderer != null)
            animatedRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (sourceRenderer == null || animatedRenderer == null || Time.deltaTime <= 0f)
            return;

        CopyRendererState();

        Vector3 currentPosition = transform.position;
        Vector2 frameVelocity = (currentPosition - previousPosition) / Time.deltaTime;
        previousPosition = currentPosition;

        float targetSpeed = frameVelocity.magnitude;
        smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, targetSpeed, 12f * Time.deltaTime);
        bool isMoving = smoothedSpeed > MovementThreshold;

        UpdateFacing(frameVelocity);
        UpdateIdle(isMoving);
        UpdateImpact();
        ApplyAnimation(isMoving);
        wasMoving = isMoving;
    }

    private void CreateAnimatedRenderer()
    {
        GameObject visual = new GameObject("AnimatedVisual");
        visual.layer = gameObject.layer;
        visualTransform = visual.transform;
        visualTransform.SetParent(transform, false);
        animatedRenderer = visual.AddComponent<SpriteRenderer>();
        CopyRendererState();
        sourceRenderer.forceRenderingOff = true;
    }

    private void CopyRendererState()
    {
        if (animatedRenderer.enabled != sourceRenderer.enabled)
            animatedRenderer.enabled = sourceRenderer.enabled;
        if (animatedRenderer.sprite != sourceRenderer.sprite)
            animatedRenderer.sprite = sourceRenderer.sprite;
        if (animatedRenderer.color != sourceRenderer.color)
            animatedRenderer.color = sourceRenderer.color;
        if (animatedRenderer.sharedMaterial != sourceRenderer.sharedMaterial)
            animatedRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        if (animatedRenderer.sortingLayerID != sourceRenderer.sortingLayerID)
            animatedRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        if (animatedRenderer.sortingOrder != sourceRenderer.sortingOrder)
            animatedRenderer.sortingOrder = sourceRenderer.sortingOrder;
        if (animatedRenderer.maskInteraction != sourceRenderer.maskInteraction)
            animatedRenderer.maskInteraction = sourceRenderer.maskInteraction;
        if (animatedRenderer.spriteSortPoint != sourceRenderer.spriteSortPoint)
            animatedRenderer.spriteSortPoint = sourceRenderer.spriteSortPoint;
        if (animatedRenderer.flipY != sourceRenderer.flipY)
            animatedRenderer.flipY = sourceRenderer.flipY;

        bool facingFlip = sourceRenderer.flipX ^ currentFacingLeft;
        if (animatedRenderer.flipX != facingFlip)
            animatedRenderer.flipX = facingFlip;
    }

    private void UpdateFacing(Vector2 frameVelocity)
    {
        if (Mathf.Abs(frameVelocity.x) > HorizontalFacingThreshold &&
            Mathf.Abs(frameVelocity.x) >= Mathf.Abs(frameVelocity.y) * 0.2f)
        {
            requestedFacingLeft = frameVelocity.x < 0f;
        }

        if (flipAge < 0f && requestedFacingLeft != currentFacingLeft)
            flipAge = 0f;

        if (flipAge < 0f)
            return;

        flipAge += Time.deltaTime;
        if (flipAge >= FlipDuration * 0.5f)
            currentFacingLeft = requestedFacingLeft;

        if (flipAge >= FlipDuration)
            flipAge = -1f;
    }

    private void UpdateIdle(bool isMoving)
    {
        if (isMoving)
        {
            idleAge = -1f;
            if (!wasMoving)
                ScheduleNextIdle();
            return;
        }

        idleCountdown -= Time.deltaTime;
        if (idleAge < 0f && idleCountdown <= 0f)
            idleAge = 0f;

        if (idleAge < 0f)
            return;

        idleAge += Time.deltaTime;
        if (idleAge >= IdleAnimationDuration)
        {
            idleAge = -1f;
            ScheduleNextIdle();
        }
    }

    private void ApplyAnimation(bool isMoving)
    {
        float scaleX = 1f;
        float scaleY = 1f;
        float offsetY = 0f;
        float tilt = 0f;

        if (isMoving)
        {
            movementPhase += Time.deltaTime * Mathf.Lerp(
                movementFrequencyRange.x,
                movementFrequencyRange.y,
                Mathf.Clamp01(smoothedSpeed / 5f));
            float step = Mathf.Sin(movementPhase * Mathf.PI * 2f);
            scaleX += step * movementStretch;
            scaleY -= step * movementStretch * 0.85f;
            offsetY = Mathf.Abs(step) * movementBounce;
            tilt = step * movementTilt;
        }
        else
        {
            float breath = Mathf.Sin(Time.time * 1.7f + breathingPhase) * breathingStrength;
            scaleX += breath;
            scaleY -= breath;

            if (idleAge >= 0f)
            {
                float progress = Mathf.Clamp01(idleAge / IdleAnimationDuration);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float squashStretch = Mathf.Sin(progress * Mathf.PI * 2f) * envelope;
                scaleX += squashStretch * idleStretch;
                scaleY -= squashStretch * idleStretch * 1.15f;
                offsetY = Mathf.Max(0f, -squashStretch) * 0.025f;
            }
        }

        float flipFold = 1f;
        if (flipAge >= 0f)
        {
            float progress = Mathf.Clamp01(flipAge / FlipDuration);
            flipFold = Mathf.Lerp(0.12f, 1f, Mathf.Abs(progress * 2f - 1f));
        }

        Vector2 impactOffset = Vector2.zero;
        if (impactAge >= 0f)
        {
            float duration = hardImpact ? HardImpactDuration : SoftImpactDuration;
            float progress = Mathf.Clamp01(impactAge / duration);
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float squash = (hardImpact ? hardImpactSquash : softImpactSquash) * envelope;
            scaleX += squash;
            scaleY -= squash * 0.9f;
            impactOffset = -impactDirection * envelope * (hardImpact ? 0.12f : 0.055f);

            if (hardImpact)
            {
                float shake = Mathf.Sin(progress * Mathf.PI * 10f) * hardImpactShake * envelope;
                impactOffset += new Vector2(-impactDirection.y, impactDirection.x) * shake;
                tilt += Mathf.Sin(progress * Mathf.PI * 8f) * 9f * envelope;
                animatedRenderer.color = Color.Lerp(
                    sourceRenderer.color,
                    new Color(1f, 0.48f, 0.38f, sourceRenderer.color.a),
                    envelope * 0.7f);
            }
            else
            {
                animatedRenderer.color = Color.Lerp(
                    sourceRenderer.color,
                    new Color(1f, 0.92f, 0.62f, sourceRenderer.color.a),
                    envelope * 0.45f);
            }
        }

        visualTransform.localScale = new Vector3(scaleX * flipFold, scaleY, 1f);
        visualTransform.localPosition = new Vector3(impactOffset.x, offsetY + impactOffset.y, 0f);
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        bool facingFlip = sourceRenderer.flipX ^ currentFacingLeft;
        if (animatedRenderer.flipX != facingFlip)
            animatedRenderer.flipX = facingFlip;
    }

    private void ScheduleNextIdle()
    {
        maximumIdleDelay = Mathf.Max(maximumIdleDelay, minimumIdleDelay);
        idleCountdown = Random.Range(minimumIdleDelay, maximumIdleDelay);
    }

    private void UpdateImpact()
    {
        if (impactAge < 0f)
            return;

        impactAge += Time.deltaTime;
        float duration = hardImpact ? HardImpactDuration : SoftImpactDuration;
        if (impactAge >= duration)
            impactAge = -1f;
    }

    private void OnValidate()
    {
        maximumIdleDelay = Mathf.Max(maximumIdleDelay, minimumIdleDelay);
    }
}

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
    private const float PoopReactionDuration = 0.82f;
    private const float PoopCompressionEnd = 0.18f;
    private const float PoopAirborneEnd = 0.58f;

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

    [Header("Group Action")]
    [SerializeField, Min(0f)] private float windupAnticipationSquash = 0.1f;
    [SerializeField, Min(0f)] private float windupAnticipationBlendSpeed = 8f;

    [Header("Obstacle Impact")]
    [SerializeField, Min(0f)] private float softImpactSquash = 0.16f;
    [SerializeField, Min(0f)] private float hardImpactSquash = 0.28f;
    [SerializeField, Min(0f)] private float hardImpactShake = 0.045f;
    [Tooltip("撞不开障碍的动画播完后，多久才能再次自动播放。")]
    [SerializeField, Min(0f)] private float hardImpactCooldown = 2f;

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
    private float nextHardImpactAllowedTime;
    private Vector2 impactDirection;
    private bool hardImpact;
    private bool currentFacingLeft;
    private bool requestedFacingLeft;
    private bool wasMoving;
    private bool hasFlockFacingIntent;
    private bool flockFacingIntentLeft;
    private bool groupActionVisualActive;
    private float poopReactionAge = -1f;
    private bool groupActionHolding;
    private float groupActionHoldBlend;

    public bool IsHardImpactPlaying => impactAge >= 0f && hardImpact;
    public bool IsPoopReactionPlaying => poopReactionAge >= 0f;
    public bool IsMovementLocked => IsHardImpactPlaying;
    public float HardImpactCooldownRemaining => Mathf.Max(0f, nextHardImpactAllowedTime - Time.time);

    public void SetFlockFacingIntent(bool facingLeft)
    {
        hasFlockFacingIntent = true;
        flockFacingIntentLeft = facingLeft;
    }

    public void ClearFlockFacingIntent()
    {
        hasFlockFacingIntent = false;
    }

    public void SetFacingImmediately(bool facingLeft)
    {
        currentFacingLeft = facingLeft;
        requestedFacingLeft = facingLeft;
        flipAge = -1f;
        if (sourceRenderer != null && animatedRenderer != null)
            CopyRendererState();
    }

    /// <summary>主动动作期间保留伸缩反馈，但禁止 Sprite 绕 Z 轴摇摆。</summary>
    public void SetGroupActionVisual(bool active, bool holding = false)
    {
        groupActionVisualActive = active;
        groupActionHolding = active && holding;
    }

    public bool PlayObstacleImpact(bool cannotBreak, Vector2 movementDirection)
    {
        if (cannotBreak)
        {
            // 持续顶着同一个障碍时不允许重置动画；冷却从上一段硬撞动画结束后才开始。
            if (IsHardImpactPlaying || Time.time < nextHardImpactAllowedTime)
                return false;
        }
        else if (IsHardImpactPlaying)
        {
            return false;
        }

        hardImpact = cannotBreak;
        impactDirection = movementDirection.sqrMagnitude > 0.001f
            ? movementDirection.normalized
            : Vector2.right;
        impactAge = 0f;
        idleAge = -1f;
        idleCountdown = Mathf.Max(idleCountdown, cannotBreak ? 0.8f : 0.25f);
        return true;
    }

    public void PlayDaydreamGesture()
    {
        if (impactAge >= 0f)
            return;

        idleAge = 0f;
        idleCountdown = IdleAnimationDuration;
    }

    /// <summary>拉屎后的挤压、轻跳和落地回弹；只改显示子节点，不移动羊的物理根节点。</summary>
    public void PlayPoopReaction()
    {
        poopReactionAge = 0f;
        idleAge = -1f;
        idleCountdown = Mathf.Max(idleCountdown, PoopReactionDuration);
    }

    private float jumpAge = -1f;
    private float jumpHeight;
    private float jumpDuration = 0.55f;

    /// <summary>真结局用的整群起跳：把 offsetY 抬起来再落下，走的是和走路弹跳同一条通道。</summary>
    public void PlayJump(float height = 1.6f, float duration = 0.55f)
    {
        jumpHeight = Mathf.Max(0f, height);
        jumpDuration = Mathf.Max(0.05f, duration);
        jumpAge = 0f;
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
        groupActionVisualActive = false;
        poopReactionAge = -1f;
        groupActionHolding = false;
        groupActionHoldBlend = 0f;
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
        bool anticipating = groupActionHolding && impactAge < 0f;
        groupActionHoldBlend = Mathf.MoveTowards(
            groupActionHoldBlend,
            anticipating ? 1f : 0f,
            Mathf.Max(0f, windupAnticipationBlendSpeed) * Time.deltaTime);
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
        if (hasFlockFacingIntent && impactAge < 0f)
        {
            requestedFacingLeft = flockFacingIntentLeft;
        }
        else if (Mathf.Abs(frameVelocity.x) > HorizontalFacingThreshold &&
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

        if (groupActionHoldBlend > 0f)
        {
            float anticipation = windupAnticipationSquash * groupActionHoldBlend;
            scaleX += anticipation * 0.8f;
            scaleY -= anticipation;
            offsetY -= anticipation * 0.16f;
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

        if (poopReactionAge >= 0f)
        {
            poopReactionAge += Time.deltaTime;
            float reactionTime = Mathf.Min(poopReactionAge, PoopReactionDuration);

            if (reactionTime < PoopCompressionEnd)
            {
                float compression = Mathf.Sin(reactionTime / PoopCompressionEnd * Mathf.PI) * 0.16f;
                scaleX += compression;
                scaleY -= compression;
            }
            else if (reactionTime < PoopAirborneEnd)
            {
                float airProgress = Mathf.InverseLerp(PoopCompressionEnd, PoopAirborneEnd, reactionTime);
                float airArc = Mathf.Sin(airProgress * Mathf.PI);
                offsetY += airArc * 0.2f;
                scaleX -= airArc * 0.035f;
                scaleY += airArc * 0.05f;
            }
            else
            {
                float landingProgress = Mathf.InverseLerp(PoopAirborneEnd, PoopReactionDuration, reactionTime);
                float landingSquash = Mathf.Sin(landingProgress * Mathf.PI) * 0.13f;
                scaleX += landingSquash;
                scaleY -= landingSquash;
            }

            if (poopReactionAge >= PoopReactionDuration)
                poopReactionAge = -1f;
        }

        visualTransform.localScale = new Vector3(scaleX * flipFold, scaleY, 1f);
        // 跳跃：半个正弦当抛物线，叠在原有的弹跳偏移上。
        float jumpOffset = 0f;
        if (jumpAge >= 0f)
        {
            jumpAge += Time.unscaledDeltaTime;
            if (jumpAge >= jumpDuration)
            {
                jumpAge = -1f;
            }
            else
            {
                jumpOffset = Mathf.Sin(Mathf.Clamp01(jumpAge / jumpDuration) * Mathf.PI) * jumpHeight;
            }
        }

        visualTransform.localPosition = new Vector3(impactOffset.x, offsetY + impactOffset.y + jumpOffset, 0f);
        visualTransform.localRotation = Quaternion.Euler(
            0f,
            0f,
            groupActionVisualActive ? 0f : tilt);
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
        {
            if (hardImpact)
                nextHardImpactAllowedTime = Time.time + Mathf.Max(0f, hardImpactCooldown);
            impactAge = -1f;
            hardImpact = false;
        }
    }

    private void OnValidate()
    {
        maximumIdleDelay = Mathf.Max(maximumIdleDelay, minimumIdleDelay);
        hardImpactCooldown = Mathf.Max(0f, hardImpactCooldown);
        windupAnticipationSquash = Mathf.Max(0f, windupAnticipationSquash);
        windupAnticipationBlendSpeed = Mathf.Max(0f, windupAnticipationBlendSpeed);
    }
}

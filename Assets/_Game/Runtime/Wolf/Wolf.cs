using System;
using UnityEngine;

/// <summary>
/// 狼：出现在羊群外围 → 显示长方形红色预警 → 沿直线冲锋 →
/// 首次接触叼走一只成员羊，后续接触逐只撞散为待收集羊 → 沿冲锋方向跑出画面后销毁。
/// 通过 <see cref="Launch"/> 指定目标羊群；不依赖任何场景对象名字。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class Wolf : MonoBehaviour
{
    private enum State
    {
        Idle,
        Warning,
        Charging,
        Fleeing
    }

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [Tooltip("预警长方形。要求 sprite 为 1x1 单位、pivot 在左侧中点，脚本会按冲锋路径拉伸。")]
    [SerializeField] private SpriteRenderer warningRenderer;

    [Header("Warning")]
    [SerializeField, Min(0f)] private float warningDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float warningWidth = 1.5f;
    [SerializeField] private Color warningColor = new Color(1f, 0.2f, 0.1f, 0.3f);
    [SerializeField] private Color warningFlashColor = new Color(1f, 0.2f, 0.1f, 0.65f);
    [SerializeField, Min(0f)] private float warningFlashFrequency = 5f;
    [Tooltip("勾选后预警期间会持续瞄准羊群中心；不勾选则预警一出现路线就锁定，玩家可以躲开。")]
    [SerializeField] private bool aimFollowsFlockDuringWarning;

    [Header("Charge")]
    [SerializeField, Min(0f)] private float chargeSpeed = 14f;
    [Tooltip("冲过羊群中心之后再继续冲多远才转入逃离。")]
    [SerializeField, Min(0f)] private float chargeOverrun = 5f;
    // Retain legacy serialized values; contact behavior now uses the actual collider only.
    [SerializeField, HideInInspector] private float centerHitRadius = 1f;
    [SerializeField, HideInInspector] private float grazeRadius = 1.1f;

    [Header("Contact Capture")]
    [Tooltip("旧版开关：仅控制首次接触前的可选追踪。无论是否勾选，首次实际接触都叼走一只羊，后续接触只撞散；未接触不捕获。")]
    [SerializeField] private bool alwaysCaptureOne = false;
    [Tooltip("冲锋途中朝最近的羊转向的速度（度/秒），0 = 严格沿红色预警框直线冲。")]
    [SerializeField, Min(0f)] private float homingTurnSpeed = 0f;
    [Tooltip("只追这个距离内的羊。")]
    [SerializeField, Min(0f)] private float homingRange = 14f;

    [Header("Smart Aim (预判躲避)")]
    [Tooltip("勾选后，狼会参考最近几次进攻玩家的躲避习惯，在预警最后一瞬把路线偏向玩家常躲的方向。")]
    [SerializeField] private bool useDodgePrediction = true;
    [Tooltip("预警结束前多少秒开始预判；红框会在这段时间里平滑转到预判方向，冲锋开始时刚好转完。")]
    [SerializeField, Min(0f)] private float predictionLeadTime = 0.35f;
    [Tooltip("至少积累多少次进攻记录才开始预判。")]
    [SerializeField, Min(1)] private int predictionMinimumSamples = 3;
    [Tooltip("把平均躲避角度乘以这个系数作为偏转量（1 = 完全按平均值）。")]
    [SerializeField, Range(0f, 1.5f)] private float predictionStrength = 1f;
    [Tooltip("预判最多偏转多少度。")]
    [SerializeField, Range(0f, 90f)] private float predictionMaxDegrees = 60f;

    [Header("Impact")]
    [SerializeField, Min(0f)] private float scatterSpeed = 7.5f;
    [SerializeField, Range(0f, 1f)] private float scatterRandomness = 0.45f;
    [SerializeField] private Color carriedSheepColor = new Color(0.3f, 0.5f, 1f, 1f);
    [SerializeField, Min(0f)] private float carriedSheepDistance = 0.75f;

    [Header("Sheep Contact (羊主动碰狼)")]
    [Tooltip("预警 / 撤退期间羊主动碰到狼也会结算：狼嘴里有羊 → 把碰上来的羊踢开（散落，可捡回）；嘴里没羊 → 叼走碰上来的那只。玩家没有救羊的手段。")]
    [SerializeField] private bool sheepContactInteraction = true;

    [Header("Flee")]
    [SerializeField, Min(0f)] private float fleeSpeed = 9f;
    [Tooltip("离羊群中心超过这个距离后销毁。")]
    [SerializeField, Min(1f)] private float despawnDistance = 24f;
    [SerializeField, Min(1f)] private float maxLifetime = 20f;

    private Rigidbody2D body;
    private FlockController flock;
    private State state = State.Idle;
    private float stateTimer;
    private float lifetime;
    private Vector2 chargeOrigin;
    private Vector2 chargeDirection = Vector2.right;
    private float chargeLength;
    private float chargeTravelled;
    private bool attackResolved;
    private bool hasCapturedMember;
    private bool finished;
    private SheepMember carriedSheep;
    private WolfDodgeMemory dodgeMemory;
    private Vector2 warnedOrigin;
    private Vector2 warnedDirection = Vector2.right;
    private bool predictionApplied;
    private bool dodgeRecorded;
    private float appliedPredictionDegrees;
    private float predictionStartAngle;
    private float predictionTargetAngle;
    private float predictionStartTime;
    private LongWolfSweep longSweep;

    /// <summary>每次实际捕获或撞散成员时触发。</summary>
    public event Action<Wolf, WolfAttackResult> Attacked;

    /// <summary>狼离开并销毁前触发。</summary>
    public event Action<Wolf> Finished;

    public bool IsCarryingSheep => carriedSheep != null;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        longSweep = GetComponent<LongWolfSweep>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        GetComponent<CircleCollider2D>().isTrigger = true;

        if (warningRenderer != null)
        {
            warningRenderer.enabled = false;
        }
    }

    /// <summary>开始进攻指定羊群：从当前位置瞄准羊群中心并进入预警阶段。</summary>
    /// <summary>这只狼这次预判偏转了多少度（0 = 没预判）。</summary>
    public float AppliedPredictionDegrees => appliedPredictionDegrees;

    /// <summary>这只狼是否已经在本次预警里做过预判。</summary>
    public bool PredictionApplied => predictionApplied;
    public bool IsWarning => state == State.Warning;
    public bool IsCharging => state == State.Charging;
    public bool UseDodgePrediction => useDodgePrediction;
    public int PredictionMinimumSamples => predictionMinimumSamples;
    public float PredictionStrength => predictionStrength;
    public float PredictionMaxDegrees => predictionMaxDegrees;
    public float PredictionLeadTime => predictionLeadTime;
    public float WarningDuration => warningDuration;
    /// <summary>预警阶段已经过去的秒数（不在预警时为 0）。</summary>
    public float WarningElapsed => state == State.Warning ? stateTimer : 0f;

    /// <summary>预判转向动画的进度 0~1（没预判时为 0，冲锋后为 1）。</summary>
    public float PredictionTurnProgress
    {
        get
        {
            if (!predictionApplied)
                return 0f;
            if (state != State.Warning)
                return 1f;
            float turnDuration = Mathf.Max(0.0001f, warningDuration - predictionStartTime);
            return Mathf.Clamp01((stateTimer - predictionStartTime) / turnDuration);
        }
    }

    /// <summary>
    /// 按这只狼（或它的 prefab）的参数，算出给定记忆会带来多少度偏转；样本不够或关闭预判时为 0。
    /// 给调试窗口预览“下一只狼会往哪偏”用。
    /// </summary>
    public float PreviewPredictionDegrees(WolfDodgeMemory memory)
    {
        if (!useDodgePrediction || memory == null || !memory.HasEnoughSamples(predictionMinimumSamples))
            return 0f;

        float degrees = Mathf.Clamp(memory.AverageDegrees * predictionStrength, -predictionMaxDegrees, predictionMaxDegrees);
        return Mathf.Abs(degrees) < 0.5f ? 0f : degrees;
    }

    /// <summary>开始进攻，并接入狼群共享的躲避记忆（可为 null）。</summary>
    public void Launch(FlockController target, WolfDodgeMemory memory)
    {
        dodgeMemory = memory;
        Launch(target);
    }

    public void Launch(FlockController target)
    {
        flock = target;
        if (flock == null)
        {
            Debug.LogWarning("Wolf.Launch was called without a flock.", this);
            Finish();
            return;
        }

        state = State.Warning;
        stateTimer = 0f;
        attackResolved = false;
        hasCapturedMember = false;
        predictionApplied = false;
        dodgeRecorded = false;
        appliedPredictionDegrees = 0f;
        if (longSweep != null) longSweep.Prepare();
        AimAtFlock();
        // 记住玩家看到的那条预警线，之后用它衡量玩家往哪边躲。
        warnedOrigin = chargeOrigin;
        warnedDirection = chargeDirection;

        if (warningRenderer != null)
        {
            warningRenderer.enabled = true;
            warningRenderer.color = warningColor;
        }
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        lifetime += Time.deltaTime;
        float effectiveLifetime = longSweep != null && chargeSpeed > 0f
            ? Mathf.Max(maxLifetime, warningDuration + longSweep.ClearTravelDistance / chargeSpeed + 2f)
            : maxLifetime;
        if (lifetime >= effectiveLifetime)
        {
            Finish();
            return;
        }

        switch (state)
        {
            case State.Warning:
                UpdateWarning();
                break;
            case State.Fleeing:
                if (flock == null || ((Vector2)transform.position - flock.Center).magnitude >= despawnDistance + (longSweep != null ? longSweep.BodyLength : 0f))
                {
                    Finish();
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f)
            return;

        float deltaTime = Time.fixedDeltaTime;
        switch (state)
        {
            case State.Charging:
            {
                if (longSweep == null && alwaysCaptureOne && !attackResolved)
                {
                    HomeTowardNearestSheep(deltaTime);
                }

                float step = chargeSpeed * deltaTime;
                Vector2 nextPosition = body.position + chargeDirection * step;
                if (longSweep != null)
                    longSweep.Sweep(this, flock, body.position, nextPosition, chargeDirection);
                body.MovePosition(nextPosition);
                chargeTravelled += step;

                // 狼的投影越过羊群中心的那一刻，记录这次玩家躲到了哪边。
                if (!dodgeRecorded && flock != null
                    && Vector2.Dot(flock.Center - nextPosition, chargeDirection) <= 0f)
                {
                    RecordDodge();
                }

                if (chargeTravelled >= chargeLength)
                {
                    RecordDodge();
                    state = State.Fleeing;
                }
                break;
            }
            case State.Fleeing:
                Vector2 fleePosition = body.position + chargeDirection * ((longSweep != null ? chargeSpeed : fleeSpeed) * deltaTime);
                if (longSweep != null)
                    longSweep.Sweep(this, flock, body.position, fleePosition, chargeDirection);
                body.MovePosition(fleePosition);
                break;
        }
    }

    /// <summary>可选追踪转向；速度为 0 时禁用，转向本身不触发捕获。</summary>
    private void HomeTowardNearestSheep(float deltaTime)
    {
        if (flock == null || homingTurnSpeed <= 0f)
            return;

        Vector2 position = body.position;
        SheepMember nearest = null;
        float nearestDistance = homingRange * homingRange;
        foreach (SheepMember member in flock.Members)
        {
            if (member == null)
                continue;

            Vector2 offset = (Vector2)member.transform.position - position;
            // 已经跑过去的羊不追，避免原地打转。
            if (Vector2.Dot(offset, chargeDirection) < 0f)
                continue;

            float distance = offset.sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = member;
            }
        }

        if (nearest == null)
            return;

        Vector2 desired = ((Vector2)nearest.transform.position - position).normalized;
        float currentAngle = Mathf.Atan2(chargeDirection.y, chargeDirection.x) * Mathf.Rad2Deg;
        float desiredAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, desiredAngle, homingTurnSpeed * deltaTime);
        if (Mathf.Approximately(newAngle, currentAngle))
            return;

        chargeDirection = new Vector2(Mathf.Cos(newAngle * Mathf.Deg2Rad), Mathf.Sin(newAngle * Mathf.Deg2Rad));
        chargeOrigin = position;
        // 目标还在前面就把冲锋距离补足，别在追上之前就转入逃离。
        chargeLength = Mathf.Max(chargeLength, chargeTravelled + Mathf.Sqrt(nearestDistance) + chargeOverrun);
    }

    private void UpdateWarning()
    {
        stateTimer += Time.deltaTime;

        if (aimFollowsFlockDuringWarning && !predictionApplied)
        {
            AimAtFlock();
        }

        if (useDodgePrediction
            && !predictionApplied
            && dodgeMemory != null
            && dodgeMemory.HasEnoughSamples(predictionMinimumSamples)
            && stateTimer >= warningDuration - predictionLeadTime)
        {
            ApplyDodgePrediction();
        }

        if (predictionApplied)
        {
            UpdatePredictionTurn();
        }

        if (warningRenderer != null)
        {
            if (longSweep != null)
            {
                // Long wolves blink fully off between pulses instead of staying red.
                bool visible = warningFlashFrequency <= 0f
                    || Mathf.Repeat(stateTimer * warningFlashFrequency, 1f) < 0.5f;
                Color flash = warningFlashColor;
                flash.a = visible ? warningFlashColor.a : 0f;
                warningRenderer.color = flash;
            }
            else if (predictionApplied)
            {
                // 转向期间红框常亮，让玩家看清它在转。
                warningRenderer.color = warningFlashColor;
            }
            else
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(stateTimer * warningFlashFrequency * Mathf.PI * 2f);
                warningRenderer.color = Color.Lerp(warningColor, warningFlashColor, blink);
            }
        }

        if (stateTimer >= warningDuration)
        {
            BeginCharge();
        }
    }

    /// <summary>
    /// 预警最后一段：算出“对准羊群现在的位置 + 玩家习惯躲的那一侧偏转平均角度”的目标方向，
    /// 之后红框在剩余预警时间里平滑旋转过去（见 UpdatePredictionTurn），不直接闪现。
    /// </summary>
    private void ApplyDodgePrediction()
    {
        predictionApplied = true;
        predictionStartTime = stateTimer;

        chargeOrigin = transform.position;
        Vector2 toCenter = flock.Center - chargeOrigin;
        float distance = toCenter.magnitude;
        Vector2 aim = distance > 0.001f ? toCenter / distance : chargeDirection;
        chargeLength = distance + chargeOverrun;

        float degrees = PreviewPredictionDegrees(dodgeMemory);
        appliedPredictionDegrees = degrees;

        predictionStartAngle = Mathf.Atan2(chargeDirection.y, chargeDirection.x) * Mathf.Rad2Deg;
        float aimAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        // 走最短的那一侧转过去。
        predictionTargetAngle = predictionStartAngle + Mathf.DeltaAngle(predictionStartAngle, aimAngle + degrees);

        if (Mathf.Abs(degrees) >= 0.5f)
        {
            Debug.Log(
                $"Smart wolf: player tends to dodge {dodgeMemory.AverageDegrees:0.0}° ({dodgeMemory.Count} samples); charge rotated by {degrees:0.0}°.",
                this);
        }
    }

    /// <summary>把红框从预判开始时的方向平滑转到目标方向，冲锋开始那一刻刚好转完。</summary>
    private void UpdatePredictionTurn()
    {
        float eased = Mathf.SmoothStep(0f, 1f, PredictionTurnProgress);
        SetChargeAngle(Mathf.Lerp(predictionStartAngle, predictionTargetAngle, eased));
    }

    private void SetChargeAngle(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        chargeDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        if (longSweep != null)
            longSweep.SetDirection(chargeDirection);
        UpdateWarningShape();
    }

    /// <summary>
    /// 记录这次进攻玩家的躲避角度：羊群中心相对预警时那条红线的带符号夹角（正 = 红线左侧）。
    /// 每只狼只记一次，在冲过羊群中心那一刻或冲锋结束时调用。
    /// </summary>
    private void RecordDodge()
    {
        if (dodgeRecorded || dodgeMemory == null || flock == null)
            return;

        dodgeRecorded = true;
        Vector2 toCenter = flock.Center - warnedOrigin;
        if (toCenter.sqrMagnitude < 0.01f)
            return;

        dodgeMemory.Record(Vector2.SignedAngle(warnedDirection, toCenter));
    }

    private void AimAtFlock()
    {
        chargeOrigin = transform.position;
        Vector2 toCenter = flock.Center - chargeOrigin;
        float distance = toCenter.magnitude;
        chargeDirection = distance > 0.001f ? toCenter / distance : Vector2.right;
        chargeLength = distance + chargeOverrun;
        if (longSweep != null)
            longSweep.SetDirection(chargeDirection);
        UpdateWarningShape();
    }

    private void UpdateWarningShape()
    {
        if (warningRenderer == null)
            return;

        Transform warningTransform = warningRenderer.transform;
        Vector2 span = longSweep != null
            ? longSweep.GetWarningSpan(transform.position, chargeDirection, chargeLength)
            : new Vector2(0f, chargeLength);
        warningTransform.position = transform.position + (Vector3)(chargeDirection * span.x);
        warningTransform.rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(chargeDirection.y, chargeDirection.x) * Mathf.Rad2Deg);
        warningTransform.localScale = new Vector3(span.y - span.x, longSweep != null ? longSweep.BodyWidth : warningWidth, 1f);
    }

    private void LateUpdate()
    {
        // Refresh the visible span as the camera follows/zooms, without changing the locked aim.
        if (longSweep != null && state == State.Warning)
            UpdateWarningShape();
    }

    private void BeginCharge()
    {
        if (predictionApplied)
        {
            // 无论帧率如何，冲锋方向都精确落在预判目标上。
            SetChargeAngle(predictionTargetAngle);
        }

        state = State.Charging;
        chargeTravelled = 0f;
        if (longSweep != null)
        {
            longSweep.ConfigureCharge(body.position, chargeDirection, chargeSpeed);
            // Keep the same speed until the tail has cleared the launch-time viewport.
            chargeLength = Mathf.Max(chargeLength, longSweep.ClearTravelDistance);
        }
        if (warningRenderer != null)
        {
            warningRenderer.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (longSweep != null)
            return;
        if (state == State.Idle || flock == null || Time.timeScale == 0f)
            return;

        // The flock root is a control marker, not a sheep. Only an actual active member can be caught.
        SheepMember contactMember = other.GetComponentInParent<SheepMember>();
        if (contactMember == null || contactMember.Flock != flock || !other.enabled
            || !other.gameObject.activeInHierarchy)
            return;

        ColliderDistance2D contact = GetComponent<CircleCollider2D>().Distance(other);
        if (!contact.isValid || contact.distance > 0.001f)
            return;

        if (state == State.Charging)
        {
            // Resolve at contact, with this exact member. No delayed capture survives a successful dodge.
            ResolveContact(contactMember);
            return;
        }

        // 预警 / 撤退期间是羊主动撞上来：嘴里有羊 → 把这只羊踢开；嘴里没羊 → 叼走这只。
        ResolveSheepInitiatedContact(contactMember);
    }

    private void ResolveSheepInitiatedContact(SheepMember member)
    {
        if (!sheepContactInteraction || member == null || member.Flock != flock)
            return;

        attackResolved = true;
        if (carriedSheep == null)
        {
            hasCapturedMember = true;
            CaptureSheep(member);
            Attacked?.Invoke(this, new WolfAttackResult(false, Array.Empty<SheepMember>(), member));
            return;
        }

        // 嘴里已经有羊：撞上来的羊被踢开，朝远离狼的方向散落。
        ScatteredSheep.Scatter(member, ComputeKickAwayVelocity(member.transform.position));
        Attacked?.Invoke(this, new WolfAttackResult(false, new[] { member }, null));
    }

    /// <summary>羊主动撞上狼时的击退：以狼为中心向外弹开，带一点随机。</summary>
    private Vector2 ComputeKickAwayVelocity(Vector2 sheepPosition)
    {
        Vector2 away = sheepPosition - (Vector2)transform.position;
        if (away.sqrMagnitude < 0.0001f)
            away = flock != null ? flock.Center - (Vector2)transform.position : -chargeDirection;
        Vector2 direction = away.sqrMagnitude > 0.0001f ? away.normalized : -chargeDirection;
        direction = (direction + UnityEngine.Random.insideUnitCircle * scatterRandomness).normalized;
        return direction * scatterSpeed * UnityEngine.Random.Range(0.8f, 1.2f);
    }

    // Each real contact is processed once while the sheep is still an active flock member.
    private void ResolveContact(SheepMember member)
    {
        if (member == null || member.Flock != flock) return;
        attackResolved = true;
        if (!hasCapturedMember)
        {
            hasCapturedMember = true;
            CaptureSheep(member);
            Attacked?.Invoke(this, new WolfAttackResult(false, Array.Empty<SheepMember>(), member));
        }
        else
        {
            ScatteredSheep.Scatter(member, ComputeKnockbackVelocity(member.transform.position));
            Attacked?.Invoke(this, new WolfAttackResult(false, new[] { member }, null));
        }
    }

    private Vector2 ComputeKnockbackVelocity(Vector2 sheepPosition)
    {
        float side = SignedDistanceToChargeLine(sheepPosition) >= 0f ? 1f : -1f;
        Vector2 perpendicular = new Vector2(-chargeDirection.y, chargeDirection.x) * side;
        Vector2 direction = (perpendicular + chargeDirection * 0.35f).normalized;
        direction = (direction + UnityEngine.Random.insideUnitCircle * scatterRandomness).normalized;
        float speed = scatterSpeed * UnityEngine.Random.Range(0.8f, 1.2f);
        return direction * speed;
    }

    private void CaptureSheep(SheepMember sheep)
    {
        FlockController owner = sheep.Flock;
        if (owner != null)
        {
            owner.Remove(sheep);
        }

        ScatteredSheep scattered = sheep.GetComponent<ScatteredSheep>();
        if (scattered != null)
        {
            scattered.enabled = false;
        }

        foreach (Collider2D collider in sheep.GetComponents<Collider2D>())
        {
            collider.enabled = false;
        }

        Rigidbody2D sheepBody = sheep.GetComponent<Rigidbody2D>();
        if (sheepBody != null)
        {
            sheepBody.simulated = false;
        }

        SpriteRenderer sheepRenderer = sheep.GetComponent<SpriteRenderer>();
        if (sheepRenderer != null)
        {
            sheepRenderer.color = carriedSheepColor;
            sheepRenderer.sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder + 1 : 21;
        }

        Transform sheepTransform = sheep.transform;
        Vector3 worldScale = sheepTransform.lossyScale;
        sheepTransform.SetParent(transform, false);
        sheepTransform.localScale = new Vector3(
            worldScale.x / Mathf.Max(transform.lossyScale.x, 0.0001f),
            worldScale.y / Mathf.Max(transform.lossyScale.y, 0.0001f),
            1f);
        sheepTransform.localPosition = -(Vector3)(chargeDirection * carriedSheepDistance);
        carriedSheep = sheep;
    }

    // Long wolves publish one capture notification per member, keeping existing HUD statistics valid.
    internal void CaptureAlongPath(SheepMember sheep, int index)
    {
        if (sheep == null || sheep.Flock != flock)
            return;

        CaptureSheep(sheep);
        RecruitableSheep recruitable = sheep.GetComponent<RecruitableSheep>();
        if (recruitable != null)
            recruitable.enabled = false;
        foreach (Collider2D hitbox in sheep.GetComponentsInChildren<Collider2D>())
            hitbox.enabled = false;
        float distance = 0.65f + index * 0.55f;
        Vector2 side = new Vector2(-chargeDirection.y, chargeDirection.x);
        sheep.transform.localPosition = -chargeDirection * distance + side * (index % 2 == 0 ? 0.24f : -0.24f);
        Attacked?.Invoke(this, new WolfAttackResult(false, Array.Empty<SheepMember>(), sheep));
    }

    private float SignedDistanceToChargeLine(Vector2 point)
    {
        Vector2 perpendicular = new Vector2(-chargeDirection.y, chargeDirection.x);
        return Vector2.Dot(point - chargeOrigin, perpendicular);
    }

    private void Finish()
    {
        if (finished)
            return;

        finished = true;
        state = State.Idle;
        Finished?.Invoke(this);
        Finished = null;
        Attacked = null;
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (state == State.Idle)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(chargeOrigin, chargeOrigin + chargeDirection * chargeLength);
    }
}

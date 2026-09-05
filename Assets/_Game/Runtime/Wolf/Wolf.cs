using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 狼：出现在羊群外围 → 显示长方形红色预警 → 沿直线冲锋 →
/// 若正面撞进羊群中心则把羊群一分为二并叼走一只羊 → 沿冲锋方向跑出画面后销毁。
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
    [Tooltip("冲锋路线离羊群中心多近算“正面撞进中心”。")]
    [SerializeField, Min(0f)] private float centerHitRadius = 1f;
    [Tooltip("没撞到中心时，路线两侧多宽范围内的羊会被擦撞开。")]
    [SerializeField, Min(0f)] private float grazeRadius = 1.1f;

    [Header("Impact")]
    [SerializeField, Min(0f)] private float scatterSpeed = 7.5f;
    [SerializeField, Range(0f, 1f)] private float scatterRandomness = 0.45f;
    [SerializeField] private Color carriedSheepColor = new Color(0.3f, 0.5f, 1f, 1f);
    [SerializeField, Min(0f)] private float carriedSheepDistance = 0.75f;

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
    private bool pendingCenterHit;
    private bool finished;
    private SheepMember carriedSheep;
    private LongWolfSweep longSweep;

    /// <summary>冲锋结算完成时触发。</summary>
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
        pendingCenterHit = false;
        AimAtFlock();

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
        if (lifetime >= maxLifetime)
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
                float step = chargeSpeed * deltaTime;
                Vector2 nextPosition = body.position + chargeDirection * step;
                if (longSweep != null)
                    longSweep.Sweep(this, flock, body.position, nextPosition, chargeDirection);
                body.MovePosition(nextPosition);
                chargeTravelled += step;

                // 正面命中：等狼真正冲到羊群中心那一刻再结算，视觉上更像“撞开”。
                if (pendingCenterHit && flock != null
                    && Vector2.Dot(flock.Center - nextPosition, chargeDirection) <= 0.25f)
                {
                    ResolveAttack(true);
                }

                if (chargeTravelled >= chargeLength)
                {
                    if (pendingCenterHit)
                    {
                        ResolveAttack(true);
                    }
                    state = State.Fleeing;
                }
                break;
            }
            case State.Fleeing:
                Vector2 fleePosition = body.position + chargeDirection * (fleeSpeed * deltaTime);
                if (longSweep != null)
                    longSweep.Sweep(this, flock, body.position, fleePosition, chargeDirection);
                body.MovePosition(fleePosition);
                break;
        }
    }

    private void UpdateWarning()
    {
        stateTimer += Time.deltaTime;

        if (aimFollowsFlockDuringWarning)
        {
            AimAtFlock();
        }

        if (warningRenderer != null)
        {
            float blink = 0.5f + 0.5f * Mathf.Sin(stateTimer * warningFlashFrequency * Mathf.PI * 2f);
            warningRenderer.color = Color.Lerp(warningColor, warningFlashColor, blink);
        }

        if (stateTimer >= warningDuration)
        {
            BeginCharge();
        }
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
        warningTransform.localPosition = Vector3.zero;
        warningTransform.rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(chargeDirection.y, chargeDirection.x) * Mathf.Rad2Deg);
        warningTransform.localScale = new Vector3(chargeLength, longSweep != null ? longSweep.BodyWidth : warningWidth, 1f);
    }

    private void BeginCharge()
    {
        state = State.Charging;
        chargeTravelled = 0f;
        if (warningRenderer != null)
        {
            warningRenderer.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (longSweep != null)
            return;
        if (state != State.Charging || attackResolved || pendingCenterHit || flock == null)
            return;

        bool touchedFlock = other.GetComponent<FlockController>() == flock;
        if (!touchedFlock)
        {
            SheepMember member = other.GetComponent<SheepMember>();
            touchedFlock = member != null && member.Flock == flock;
        }

        if (!touchedFlock)
            return;

        bool centerHit = Mathf.Abs(SignedDistanceToChargeLine(flock.Center)) <= centerHitRadius;
        if (centerHit)
        {
            // 已经确定会正面撞进中心，结算延后到冲抵中心时（见 FixedUpdate）。
            pendingCenterHit = true;
            return;
        }

        ResolveAttack(false);
    }

    /// <summary>
    /// 第一次接触羊群时一次性结算：
    /// 路线穿过中心 → 按路线左右一分为二，少的那一半被撞开，并从中叼走一只；
    /// 否则 → 只把路线上擦到的羊撞开。
    /// </summary>
    private void ResolveAttack(bool centerHit)
    {
        if (attackResolved)
            return;

        attackResolved = true;
        pendingCenterHit = false;

        List<SheepMember> members = new List<SheepMember>(flock.Members);
        List<SheepMember> knocked = new List<SheepMember>();

        if (centerHit)
        {
            List<SheepMember> leftSide = new List<SheepMember>();
            List<SheepMember> rightSide = new List<SheepMember>();
            foreach (SheepMember member in members)
            {
                if (member == null)
                    continue;

                (SignedDistanceToChargeLine(member.transform.position) >= 0f ? leftSide : rightSide).Add(member);
            }

            // 少的一半被撞开；人数相同时随机挑一边。
            bool knockLeft = leftSide.Count == rightSide.Count
                ? UnityEngine.Random.value < 0.5f
                : leftSide.Count < rightSide.Count;
            knocked.AddRange(knockLeft ? leftSide : rightSide);
        }
        else
        {
            Vector2 wolfPosition = body.position;
            foreach (SheepMember member in members)
            {
                if (member == null)
                    continue;

                Vector2 memberPosition = member.transform.position;
                bool ahead = Vector2.Dot(memberPosition - wolfPosition, chargeDirection) >= -0.5f;
                if (ahead && Mathf.Abs(SignedDistanceToChargeLine(memberPosition)) <= grazeRadius)
                {
                    knocked.Add(member);
                }
            }
        }

        SheepMember captured = null;
        if (centerHit)
        {
            captured = PickNearest(knocked.Count > 0 ? knocked : members);
            if (captured != null)
            {
                knocked.Remove(captured);
            }
        }

        foreach (SheepMember member in knocked)
        {
            ScatteredSheep.Scatter(member, ComputeKnockbackVelocity(member.transform.position));
        }

        if (captured != null)
        {
            CaptureSheep(captured);
        }

        Debug.Log(
            centerHit
                ? $"Wolf hit the flock center: {knocked.Count} sheep scattered, {(captured != null ? captured.name : "no sheep")} taken."
                : $"Wolf grazed the flock: {knocked.Count} sheep scattered.",
            this);

        Attacked?.Invoke(this, new WolfAttackResult(centerHit, knocked, captured));
    }

    private SheepMember PickNearest(List<SheepMember> candidates)
    {
        SheepMember best = null;
        float bestDistance = float.MaxValue;
        Vector2 wolfPosition = body.position;
        foreach (SheepMember candidate in candidates)
        {
            if (candidate == null)
                continue;

            float distance = ((Vector2)candidate.transform.position - wolfPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
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

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class RecruitableSheep : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color recruitedColor = new Color(0.65f, 1f, 0.65f, 1f);

    private CircleCollider2D recruitTrigger;
    private WildSheepWander wildWander;
    private float recruitLockedUntil;

    public bool IsRecruited { get; private set; }

    private void Awake()
    {
        recruitTrigger = GetComponent<CircleCollider2D>();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        SheepVisualAnimator.Ensure(gameObject);
        wildWander = WildSheepWander.Ensure(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRecruitByContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 被狼撞开后锁定期结束时羊群可能仍压在身上，靠 Stay 补一次判定。
        if (IsRecruited)
            return;

        TryRecruitByContact(other);
    }

    private void TryRecruitByContact(Collider2D other)
    {
        SheepMember member = other.GetComponentInParent<SheepMember>();
        if (member != null && member.Flock != null)
        {
            TryRecruit(member.Flock);
        }
    }

    public bool TryRecruit(FlockController flock)
    {
        return !IsRecruited
            && Time.time >= recruitLockedUntil
            && flock != null
            && flock.TryRecruit(this);
    }

    /// <summary>
    /// 让一只已被招募（或从未招募）的羊重新变为可招募状态，例如被狼撞离羊群之后。
    /// </summary>
    /// <param name="lockoutSeconds">多少秒内暂不允许被招募。</param>
    public void ReleaseForRecruitment(float lockoutSeconds)
    {
        IsRecruited = false;
        recruitLockedUntil = Time.time + Mathf.Max(0f, lockoutSeconds);
        wildWander?.SetRecruited(false);
    }

    public void ConfigureWanderBounds(Rect worldBounds, float roamingLimit = -1f)
    {
        wildWander ??= WildSheepWander.Ensure(gameObject);
        wildWander?.Configure(worldBounds, roamingLimit);
    }

    internal void CompleteRecruitment()
    {
        if (IsRecruited)
            return;

        IsRecruited = true;
        wildWander?.SetRecruited(true);
        if (spriteRenderer != null) spriteRenderer.color = recruitedColor;
        Debug.Log($"{name} joined the flock.", this);
    }

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.65f;
    }
}

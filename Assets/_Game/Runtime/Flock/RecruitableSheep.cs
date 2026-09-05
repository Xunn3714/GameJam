using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class RecruitableSheep : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color recruitedColor = Color.white;

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

    private static LayerMask blockingMask;
    private static bool blockingMaskResolved;

    private void TryRecruitByContact(Collider2D other)
    {
        SheepMember member = other.GetComponentInParent<SheepMember>();
        if (member == null || member.Flock == null)
            return;

        // 隔着围栏不能招募，否则被招进来的羊会卡在栏杆另一边。
        if (!blockingMaskResolved)
        {
            blockingMask = MovementBlocking.DefaultMask();
            blockingMaskResolved = true;
        }

        if (MovementBlocking.IsLineBlocked(member.transform.position, transform.position, blockingMask))
            return;

        TryRecruit(member.Flock);
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
        ReleaseForRecruitment(lockoutSeconds, recruitedColor);
    }

    /// <summary>
    /// 让羊重新可招募，并指定它归队后应恢复的颜色。
    /// 被狼打散时使用撞击前的颜色，避免运行时新增组件的默认颜色污染外观。
    /// </summary>
    internal void ReleaseForRecruitment(float lockoutSeconds, Color colorOnRecruitment)
    {
        IsRecruited = false;
        recruitLockedUntil = Time.time + Mathf.Max(0f, lockoutSeconds);
        recruitedColor = colorOnRecruitment;
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

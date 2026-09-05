using UnityEngine;

/// <summary>
/// 被狼撞开、脱离羊群的羊：先被击退一段距离，然后停在原地等待玩家重新接触捡回。
/// 由 <see cref="Wolf"/> 在结算时通过 <see cref="Scatter"/> 挂载/激活。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SheepMember), typeof(Rigidbody2D))]
public sealed class ScatteredSheep : MonoBehaviour
{
    [Header("Knockback")]
    [SerializeField, Min(0f)] private float knockbackDamping = 6f;
    [SerializeField, Min(0f)] private float stopSpeed = 0.05f;

    [Header("Recruit Again")]
    [Tooltip("被撞开后多少秒内不能被重新捡回，避免羊群还压在身上时立刻归队。")]
    [SerializeField, Min(0f)] private float recruitLockout = 0.6f;
    [SerializeField] private Color scatteredColor = new Color(0.78f, 0.78f, 0.85f, 1f);

    private SheepMember member;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Vector2 velocity;

    /// <summary>当前是否仍散落在地上（尚未被重新捡回）。</summary>
    public bool IsScattered { get; private set; }

    /// <summary>
    /// 把一只羊从羊群中撞开：移出羊群、施加击退速度，并允许之后被重新招募。
    /// </summary>
    public static ScatteredSheep Scatter(SheepMember target, Vector2 knockbackVelocity)
    {
        if (target == null)
            return null;

        FlockController flock = target.Flock;
        if (flock != null)
        {
            flock.Remove(target);
        }

        ScatteredSheep scattered = target.GetComponent<ScatteredSheep>();
        if (scattered == null)
        {
            scattered = target.gameObject.AddComponent<ScatteredSheep>();
        }

        scattered.BeginKnockback(knockbackVelocity);
        return scattered;
    }

    private void Awake()
    {
        member = GetComponent<SheepMember>();
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void BeginKnockback(Vector2 knockbackVelocity)
    {
        velocity = knockbackVelocity;
        IsScattered = true;
        enabled = true;

        Color colorBeforeScatter = spriteRenderer != null
            ? spriteRenderer.color
            : Color.white;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = scatteredColor;
        }

        RecruitableSheep recruitable = GetComponent<RecruitableSheep>();
        if (recruitable == null)
        {
            recruitable = gameObject.AddComponent<RecruitableSheep>();
        }
        recruitable.ReleaseForRecruitment(recruitLockout, colorBeforeScatter);
    }

    private void FixedUpdate()
    {
        if (!IsScattered || Time.timeScale == 0f)
            return;

        // 被重新捡回：交还给 SheepFlockAgent 控制。
        if (member.Flock != null)
        {
            IsScattered = false;
            velocity = Vector2.zero;
            enabled = false;
            return;
        }

        if (velocity.sqrMagnitude <= stopSpeed * stopSpeed)
        {
            velocity = Vector2.zero;
            IsScattered = false;
            enabled = false;
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        body.MovePosition(body.position + velocity * deltaTime);
        velocity = Vector2.MoveTowards(velocity, Vector2.zero, knockbackDamping * deltaTime);
    }
}

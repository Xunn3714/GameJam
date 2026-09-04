using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public sealed class FlockSizeObstacle : MonoBehaviour
{
    [SerializeField, Min(1)] private int minimumMembersToBreak = 6;
    [SerializeField] private SpriteRenderer obstacleRenderer;
    [SerializeField] private Color blockedFlashColor = new Color(0.9f, 0.25f, 0.18f, 1f);
    [SerializeField] private Color breakColor = new Color(1f, 0.75f, 0.2f, 1f);
    [SerializeField, Min(0f)] private float feedbackDuration = 0.12f;

    private BoxCollider2D obstacleTrigger;
    private Color normalColor;
    private float blockedFlashUntil;
    private bool isBroken;

    private void Awake()
    {
        obstacleTrigger = GetComponent<BoxCollider2D>();
        obstacleTrigger.isTrigger = true;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;

        obstacleRenderer ??= GetComponent<SpriteRenderer>();
        if (obstacleRenderer != null)
        {
            normalColor = obstacleRenderer.color;
        }
    }

    private void Update()
    {
        if (!isBroken && obstacleRenderer != null && Time.unscaledTime >= blockedFlashUntil)
        {
            obstacleRenderer.color = normalColor;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ResolveContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        ResolveContact(other);
    }

    private void ResolveContact(Collider2D other)
    {
        if (isBroken)
            return;

        FlockController flock = other.GetComponent<FlockController>();
        if (flock != null)
        {
            if (flock.MemberCount >= minimumMembersToBreak)
            {
                BreakObstacle();
            }
            else
            {
                flock.RejectCurrentMovement();
                ShowBlockedFeedback();
            }

            return;
        }

        SheepMember member = other.GetComponent<SheepMember>();
        if (member != null
            && member.Flock != null
            && member.Flock.MemberCount >= minimumMembersToBreak)
        {
            BreakObstacle();
        }
    }

    private void ShowBlockedFeedback()
    {
        if (obstacleRenderer == null)
            return;

        obstacleRenderer.color = blockedFlashColor;
        blockedFlashUntil = Time.unscaledTime + feedbackDuration;
    }

    private void BreakObstacle()
    {
        isBroken = true;
        obstacleTrigger.enabled = false;

        if (obstacleRenderer != null)
        {
            obstacleRenderer.color = breakColor;
        }

        transform.localScale *= 1.12f;
        Destroy(gameObject, feedbackDuration);
    }
}

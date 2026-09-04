using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class RecruitableSheep : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color recruitedColor = new Color(0.65f, 1f, 0.65f, 1f);

    private CircleCollider2D recruitTrigger;

    public bool IsRecruited { get; private set; }

    private void Awake()
    {
        recruitTrigger = GetComponent<CircleCollider2D>();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        SheepMember member = other.GetComponentInParent<SheepMember>();
        if (member != null && member.Flock != null)
        {
            TryRecruit(member.Flock);
        }
    }

    public bool TryRecruit(FlockController flock)
    {
        return !IsRecruited && flock != null && flock.TryRecruit(this);
    }

    internal void CompleteRecruitment()
    {
        if (IsRecruited)
            return;

        IsRecruited = true;
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

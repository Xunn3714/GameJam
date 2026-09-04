using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class RecruitableSheep : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color recruitedColor = new Color(0.65f, 1f, 0.65f, 1f);

    private CircleCollider2D recruitTrigger;

    public event Action<RecruitableSheep> Recruited;

    public bool IsRecruited { get; private set; }

    private void Awake()
    {
        recruitTrigger = GetComponent<CircleCollider2D>();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<SheepPlayerController>() != null)
        {
            TryRecruit();
        }
    }

    public bool TryRecruit()
    {
        if (IsRecruited) return false;

        IsRecruited = true;
        recruitTrigger.enabled = false;
        if (spriteRenderer != null) spriteRenderer.color = recruitedColor;

        Recruited?.Invoke(this);
        Debug.Log($"{name} joined the flock.", this);
        return true;
    }

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.65f;
    }
}

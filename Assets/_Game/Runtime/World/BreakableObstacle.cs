using System;
using System.Collections;
using UnityEngine;

/// 通用可破坏障碍。挂在建筑、木桶、石头、花草、围栏的 Prefab 上。
/// - BreakRule = OnAnyContact 时：任意羊群成员碰到即 Break()。
/// - BreakRule = RequireCountAndInteract 时：自己不主动碎，由 FenceObstacle 判定后调用 Break()。
/// 需要至少一个 Trigger 类型的 Collider2D 用来侦测接触。
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class BreakableObstacle : MonoBehaviour
{
    [SerializeField] private ObstacleDefinition definition;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private string breakTriggerName = "Break";

    private Collider2D[] colliders;
    private bool isBroken;

    public ObstacleDefinition Definition => definition;
    public bool IsBroken => isBroken;

    public event Action<BreakableObstacle> Broken;

    private void Awake()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        colliders = GetComponentsInChildren<Collider2D>();

        if (definition == null)
        {
            Debug.LogWarning($"{name} 没有指定 ObstacleDefinition，将按 OnAnyContact + BecomeBackground 处理。", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[Breakable] {name} 被 {other.name} 触发, isBroken={isBroken}", this);

        if (isBroken)
            return;

        ObstacleBreakRule rule = definition != null
            ? definition.BreakRule
            : ObstacleBreakRule.OnAnyContact;

        bool flockContact = IsFlockContact(other);
        Debug.Log($"[Breakable] rule={rule} flockContact={flockContact}", this);

        if (rule != ObstacleBreakRule.OnAnyContact)
            return;

        if (flockContact)
        {
            Break();
        }
    }

    /// 判断碰上来的是不是羊群的一员（中心点或任意已入群成员）。野外未招募的羊不算。
    public static bool IsFlockContact(Collider2D other)
    {
        if (other.GetComponentInParent<FlockController>() != null)
            return true;

        SheepMember member = other.GetComponentInParent<SheepMember>();
        return member != null && member.Flock != null;
    }

    public void Break()
    {
        if (isBroken)
            return;

        isBroken = true;

        foreach (Collider2D collider in colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }

        Broken?.Invoke(this);

        if (animator != null && !string.IsNullOrEmpty(breakTriggerName))
        {
            animator.SetTrigger(breakTriggerName);
        }
        else if (spriteRenderer != null && definition != null && definition.BrokenSprite != null)
        {
            spriteRenderer.sprite = definition.BrokenSprite;
        }

        float duration = definition != null ? definition.BreakAnimationDuration : 0.4f;
        StartCoroutine(FinishBreakAfter(duration));
    }

    private IEnumerator FinishBreakAfter(float duration)
    {
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        ObstacleBrokenBehavior behavior = definition != null
            ? definition.BrokenBehavior
            : ObstacleBrokenBehavior.BecomeBackground;

        if (behavior == ObstacleBrokenBehavior.Disappear)
        {
            Destroy(gameObject);
            yield break;
        }

        // BecomeBackground：保留物体，只是换到背景层，让羊能从上面走过去。
        if (spriteRenderer != null && definition != null)
        {
            int layerId = SortingLayer.NameToID(definition.BrokenSortingLayer);
            if (SortingLayer.IsValid(layerId))
            {
                spriteRenderer.sortingLayerID = layerId;
            }
            else
            {
                Debug.LogWarning(
                    $"Sorting Layer \"{definition.BrokenSortingLayer}\" 不存在，只降低 Order in Layer。",
                    this);
            }

            spriteRenderer.sortingOrder = definition.BrokenSortingOrder;
        }
    }
}

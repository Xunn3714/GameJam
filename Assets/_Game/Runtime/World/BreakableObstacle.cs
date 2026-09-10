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
    private int receivedDashHits;

    public ObstacleDefinition Definition => definition;
    public bool IsBroken => isBroken;
    public int ReceivedDashHits => receivedDashHits;
    public int RequiredDashHits => definition != null ? definition.RequiredDashHits : 1;
    public bool IsDamaged => receivedDashHits > 0 && !isBroken;

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

    public void Configure(ObstacleDefinition obstacleDefinition, SpriteRenderer renderer)
    {
        definition = obstacleDefinition;
        spriteRenderer = renderer != null ? renderer : GetComponentInChildren<SpriteRenderer>();
        colliders = GetComponentsInChildren<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBroken)
            return;

        ObstacleBreakRule rule = definition != null
            ? definition.BreakRule
            : ObstacleBreakRule.OnAnyContact;

        if (rule != ObstacleBreakRule.OnAnyContact)
            return;

        if (IsFlockContact(other))
        {
            SheepVisualAnimator visualAnimator = other.GetComponentInParent<SheepVisualAnimator>();
            Vector2 direction = transform.position - other.transform.position;
            visualAnimator?.PlayObstacleImpact(false, direction);
            Break();
        }
    }

    /// 判断碰上来的是不是已入群的真实羊。羊群目标中心只表达移动意图，
    /// 不能隔着障碍代替成员触发破坏或围栏范围。
    public static bool IsFlockContact(Collider2D other)
    {
        SheepMember member = other.GetComponentInParent<SheepMember>();
        return member != null && member.Flock != null;
    }

    /// <summary>
    /// 记录一次有效冲撞。多段障碍在最后一次之前仍保持实体碰撞，
    /// 因而必须由下一次 E 冲刺完成摧毁。
    /// </summary>
    public bool Break()
    {
        if (isBroken)
            return false;

        receivedDashHits++;
        if (receivedDashHits < RequiredDashHits)
        {
            ApplyDamagedVisual();
            return false;
        }

        isBroken = true;

        foreach (Collider2D collider in colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }

        Broken?.Invoke(this);

        ObstacleBrokenBehavior behavior = definition != null
            ? definition.BrokenBehavior
            : ObstacleBrokenBehavior.BecomeBackground;

        if (behavior == ObstacleBrokenBehavior.Disappear)
        {
            // 多段障碍已经在前一击展示过破损图；最后一击直接从地图移除。
            if (RequiredDashHits > 1)
            {
                Destroy(gameObject);
                return true;
            }

            // 花草：被踩扁——压矮、变宽、淡出，然后销毁。花草本身在羊下面（order -1），压扁过程不会盖住羊。
            float duration = definition != null ? definition.BreakAnimationDuration : 0.4f;
            StartCoroutine(TrampleThenDestroy(duration));
            return true;
        }

        // 有坏图的：立刻换成坏图并沉到 Background 层，成为地面的一部分，羊从上面走过。
        if (animator != null && !string.IsNullOrEmpty(breakTriggerName))
        {
            animator.SetTrigger(breakTriggerName);
        }
        else if (spriteRenderer != null && definition != null && definition.BrokenSprite != null)
        {
            spriteRenderer.sprite = definition.BrokenSprite;
        }

        ApplyBrokenSorting();
        return true;
    }

    private void ApplyDamagedVisual()
    {
        if (spriteRenderer != null && definition != null && definition.BrokenSprite != null)
            spriteRenderer.sprite = definition.BrokenSprite;
    }

    private IEnumerator TrampleThenDestroy(float duration)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        Color[] startColors = new Color[renderers.Length];
        for (int index = 0; index < renderers.Length; index++)
            startColors[index] = renderers[index] != null ? renderers[index].color : Color.white;

        Vector3 startScale = transform.localScale;
        Vector3 squashedScale = new Vector3(startScale.x * 1.25f, startScale.y * 0.3f, startScale.z);
        float tilt = UnityEngine.Random.Range(-12f, 12f);
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, 0f, tilt);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            // 前 40% 快速压扁，之后慢慢淡出。
            float squash = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.4f));
            float fade = Mathf.Clamp01((t - 0.3f) / 0.7f);
            transform.localScale = Vector3.Lerp(startScale, squashedScale, squash);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, squash);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] == null)
                    continue;
                Color color = startColors[index];
                color.a = startColors[index].a * (1f - fade);
                renderers[index].color = color;
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    private void ApplyBrokenSorting()
    {
        if (definition == null || definition.BrokenBehavior == ObstacleBrokenBehavior.Disappear)
            return;

        int layerId = SortingLayer.NameToID(definition.BrokenSortingLayer);
        bool layerValid = SortingLayer.IsValid(layerId);
        if (!layerValid)
        {
            Debug.LogWarning(
                $"Sorting Layer \"{definition.BrokenSortingLayer}\" 不存在，只降低 Order in Layer。",
                this);
        }

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (layerValid)
                renderer.sortingLayerID = layerId;
            renderer.sortingOrder = definition.BrokenSortingOrder;
        }
    }
}

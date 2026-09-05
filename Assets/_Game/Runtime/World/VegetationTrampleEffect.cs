using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 羊踩过花草时压扁本体并散出数片旋转、褪色的小花瓣。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public sealed class VegetationTrampleEffect : MonoBehaviour
{
    private sealed class Fragment
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector2 Origin;
        public Vector2 Velocity;
        public float AngularSpeed;
    }

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0.1f)] private float duration = 0.55f;
    [SerializeField, Range(2, 8)] private int fragmentCount = 5;

    private readonly List<Fragment> fragments = new();
    private Vector3 originalScale;
    private Color originalColor;
    private float age = -1f;

    private void Awake()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
        originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
    }

    public void Configure(SpriteRenderer renderer)
    {
        spriteRenderer = renderer != null ? renderer : GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
        originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (age >= 0f || !IsSheep(other))
            return;

        BeginTrample();
    }

    private void Update()
    {
        if (age < 0f)
            return;

        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / duration);
        float pop = Mathf.Sin(progress * Mathf.PI);

        transform.localScale = new Vector3(
            originalScale.x * (1f + pop * 0.28f),
            originalScale.y * Mathf.Lerp(1f, 0.12f, progress),
            originalScale.z);
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 3f) * 14f * pop);

        if (spriteRenderer != null)
        {
            Color color = Color.Lerp(originalColor, new Color(0.72f, 0.9f, 0.4f, 0f), progress);
            spriteRenderer.color = color;
        }

        for (int index = 0; index < fragments.Count; index++)
        {
            Fragment fragment = fragments[index];
            Vector2 position = fragment.Origin
                + fragment.Velocity * age
                + Vector2.down * (1.7f * age * age);
            fragment.Transform.position = new Vector3(position.x, position.y, transform.position.z - 0.01f);
            fragment.Transform.Rotate(0f, 0f, fragment.AngularSpeed * Time.deltaTime);
            float fragmentScale = Mathf.Lerp(0.22f, 0.04f, progress);
            fragment.Transform.localScale = Vector3.one * fragmentScale;
            Color color = fragment.Renderer.color;
            color.a = 1f - progress;
            fragment.Renderer.color = color;
        }

        if (progress < 1f)
            return;

        for (int index = 0; index < fragments.Count; index++)
            if (fragments[index].Transform != null) Destroy(fragments[index].Transform.gameObject);
        Destroy(gameObject);
    }

    private void BeginTrample()
    {
        age = 0f;
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.enabled = false;

        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        for (int index = 0; index < fragmentCount; index++)
        {
            float angle = 360f * index / fragmentCount + Random.Range(-22f, 22f);
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
            GameObject fragmentObject = new("FlowerPetal");
            fragmentObject.layer = gameObject.layer;
            fragmentObject.transform.position = transform.position;

            SpriteRenderer fragmentRenderer = fragmentObject.AddComponent<SpriteRenderer>();
            fragmentRenderer.sprite = spriteRenderer.sprite;
            fragmentRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
            fragmentRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            fragmentRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            fragmentRenderer.color = Color.Lerp(
                originalColor,
                index % 2 == 0 ? new Color(1f, 0.92f, 0.35f, 1f) : new Color(0.55f, 0.9f, 0.35f, 1f),
                0.45f);

            fragments.Add(new Fragment
            {
                Transform = fragmentObject.transform,
                Renderer = fragmentRenderer,
                Origin = transform.position,
                Velocity = direction * Random.Range(0.65f, 1.25f) + Vector2.up * Random.Range(0.45f, 0.9f),
                AngularSpeed = Random.Range(-520f, 520f),
            });
        }
    }

    private static bool IsSheep(Collider2D other)
    {
        return other.GetComponentInParent<RecruitableSheep>() != null
            || other.GetComponentInParent<SheepMember>() != null;
    }
}

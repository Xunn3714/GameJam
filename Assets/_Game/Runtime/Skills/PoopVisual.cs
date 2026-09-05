using UnityEngine;

// Small spawn feedback; the ability owns lifetime and capacity.
public sealed class PoopVisual : MonoBehaviour
{
    private const float PopDuration = 0.32f;
    private const float DespawnDuration = 0.45f;
    private const float StartScale = 0.18f;
    private const float PeakScale = 1.16f;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite rareSprite;
    [SerializeField, Range(0f, 1f)] private float rareChance = 0.07f;

    private Vector3 fullScale;
    private Quaternion restingRotation;
    private Color restingColor;
    private float startingTilt;
    private float lifetimeSeconds = 5f;
    private float age;

    private void Awake()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && rareSprite != null && Random.value < rareChance)
            spriteRenderer.sprite = rareSprite;

        fullScale = transform.localScale;
        restingRotation = transform.localRotation;
        restingColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        startingTilt = Random.Range(-10f, 10f);
        ApplySpawnPose(0f);
    }

    public void BeginLifetime(float durationSeconds)
    {
        lifetimeSeconds = Mathf.Max(0.01f, durationSeconds);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (age < PopDuration)
        {
            ApplySpawnPose(Mathf.Clamp01(age / PopDuration));
            return;
        }

        float despawnStart = Mathf.Max(PopDuration, lifetimeSeconds - DespawnDuration);
        if (age >= despawnStart)
        {
            ApplyDespawnPose(Mathf.InverseLerp(despawnStart, lifetimeSeconds, age));
            return;
        }

        ApplyRestingPose();
    }

    private void ApplySpawnPose(float progress)
    {
        float scale;
        float horizontalSquash;

        if (progress < 0.65f)
        {
            float rise = Mathf.SmoothStep(0f, 1f, progress / 0.65f);
            scale = Mathf.Lerp(StartScale, PeakScale, rise);
            horizontalSquash = -Mathf.Sin(rise * Mathf.PI) * 0.08f;
        }
        else
        {
            float settle = Mathf.SmoothStep(0f, 1f, (progress - 0.65f) / 0.35f);
            scale = Mathf.Lerp(PeakScale, 1f, settle);
            horizontalSquash = Mathf.Sin(settle * Mathf.PI) * 0.1f;
        }

        transform.localScale = new Vector3(
            fullScale.x * scale * (1f + horizontalSquash),
            fullScale.y * scale * (1f - horizontalSquash * 0.65f),
            fullScale.z);
        transform.localRotation = restingRotation * Quaternion.Euler(
            0f,
            0f,
            startingTilt * (1f - progress) + Mathf.Sin(progress * Mathf.PI * 2f) * 2f * (1f - progress));

        if (spriteRenderer != null)
        {
            Color color = restingColor;
            color.a *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 3f));
            spriteRenderer.color = color;
        }
    }

    private void ApplyRestingPose()
    {
        transform.localScale = fullScale;
        transform.localRotation = restingRotation;
        if (spriteRenderer != null)
            spriteRenderer.color = restingColor;
    }

    private void ApplyDespawnPose(float progress)
    {
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        transform.localScale = new Vector3(
            fullScale.x * Mathf.Lerp(1f, 1.2f, eased),
            fullScale.y * Mathf.Lerp(1f, 0.05f, eased),
            fullScale.z);
        transform.localRotation = restingRotation * Quaternion.Euler(
            0f,
            0f,
            Mathf.Sin(eased * Mathf.PI) * 5f);

        if (spriteRenderer != null)
        {
            Color color = restingColor;
            color.a *= 1f - eased;
            spriteRenderer.color = color;
        }
    }
}

using UnityEngine;

// Handles the poop's spawn feedback, render order, lifetime and reusable despawn VFX.
public sealed class PoopVisual : MonoBehaviour
{
    private const float PopDuration = 0.32f;
    private const float StartScale = 0.18f;
    private const float PeakScale = 1.16f;
    private const float DespawnParticleLifetime = 1f;
    private static readonly Color DespawnParticleColor = new Color(0.32f, 0.14f, 0.045f, 1f);

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite rareSprite;
    [SerializeField, Range(0f, 1f)] private float rareChance;
    [Tooltip("复用 World/VFX/BreakParticles；运行时改成两个棕色小圆粒。")]
    [SerializeField] private GameObject despawnEffectPrefab;

    private Vector3 fullScale;
    private Quaternion restingRotation;
    private Color restingColor;
    private float startingTilt;
    private float lifetimeSeconds = 10f;
    private float age;
    private bool despawning;

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

    public void Configure(float durationSeconds, SpriteRenderer sheepRenderer)
    {
        lifetimeSeconds = Mathf.Max(0.01f, durationSeconds);
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null && sheepRenderer != null)
        {
            spriteRenderer.sortingLayerID = sheepRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = sheepRenderer.sortingOrder - 1;
        }
    }

    public void BeginLifetime(float durationSeconds)
    {
        lifetimeSeconds = Mathf.Max(0.01f, durationSeconds);
    }

    public void Despawn(bool playEffect = true)
    {
        if (despawning)
            return;

        despawning = true;
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        if (playEffect)
            SpawnDespawnEffect();

        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetimeSeconds)
        {
            Despawn();
            return;
        }

        if (age < PopDuration)
        {
            ApplySpawnPose(Mathf.Clamp01(age / PopDuration));
            return;
        }

        ApplyRestingPose();
    }

    private void SpawnDespawnEffect()
    {
        if (despawnEffectPrefab == null)
            return;

        GameObject effect = Instantiate(despawnEffectPrefab, transform.position, Quaternion.identity);
        ParticleSystem particles = effect.GetComponentInChildren<ParticleSystem>();
        if (particles == null)
        {
            if (Application.isPlaying)
                Destroy(effect);
            else
                DestroyImmediate(effect);
            return;
        }

        ConfigureDespawnParticles(effect, particles);
    }

    private void ConfigureDespawnParticles(GameObject effect, ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = DespawnParticleLifetime;
        main.startLifetime = DespawnParticleLifetime;
        main.startSpeed = 0f;
        main.startSize = 0.14f;
        main.startColor = DespawnParticleColor;
        main.maxParticles = 2;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0.35f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = false;

        GameObject groundObject = new GameObject("PoopParticleGround");
        groundObject.transform.SetParent(effect.transform, false);
        groundObject.transform.localPosition = new Vector3(0f, -0.04f, 0f);

        ParticleSystem.CollisionModule collision = particles.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.28f;
        collision.dampen = 0.35f;
        collision.lifetimeLoss = 0f;
        collision.SetPlane(0, groundObject.transform);

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null && spriteRenderer != null)
        {
            particleRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            particleRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }

        particles.Clear(true);
        particles.Play(true);
        EmitParticle(particles, -0.24f, 0.92f, 0.12f);
        EmitParticle(particles, 0.24f, 1.02f, 0.15f);
    }

    private static void EmitParticle(
        ParticleSystem particles,
        float horizontalVelocity,
        float verticalVelocity,
        float size)
    {
        ParticleSystem.EmitParams emission = new ParticleSystem.EmitParams
        {
            position = new Vector3(horizontalVelocity * 0.08f, 0.08f, 0f),
            velocity = new Vector3(horizontalVelocity, verticalVelocity, 0f),
            startColor = DespawnParticleColor,
            startLifetime = DespawnParticleLifetime,
            startSize = size,
        };
        particles.Emit(emission, 1);
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
}

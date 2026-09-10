using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 紫色或金色羊被获得时播放品质发光，发光结束后开启低成本的移动粒子拖尾。
/// 金色羊还会获得常驻的环绕粒子。
/// </summary>
[DisallowMultipleComponent]
public sealed class SpecialSheepAcquisitionVfx : MonoBehaviour, ISpecialSheepFeature
{
    private const string OuterGlowName = "SpecialSheepOuterGlow";
    private const string InnerGlowName = "SpecialSheepInnerGlow";
    private const string AcquisitionBurstName = "SpecialSheepAcquisitionBurst";
    private const string TrailName = "SpecialSheepQualityTrail";
    private const string PremiumAuraName = "SpecialSheepPremiumAura";
    private const string VfxShaderResourcePath = "SpecialSheepVfx/SpecialSheepUnlit";

    [Header("Acquisition Glow")]
    [SerializeField, Min(0.1f)] private float glowDuration = 2.5f;
    [SerializeField, Min(0f)] private float glowPulseSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float outerGlowAlpha = 0.7f;
    [SerializeField, Range(0f, 1f)] private float innerGlowAlpha = 0.38f;
    [SerializeField, Min(1f)] private float glowMinScale = 1.08f;
    [SerializeField, Min(1f)] private float glowMaxScale = 1.4f;

    [Header("Acquisition Particle Burst")]
    [SerializeField, Min(1)] private int acquisitionBurstCount = 18;
    [SerializeField] private Vector2 acquisitionBurstSize = new(0.12f, 0.22f);

    [Header("Particle Trail")]
    [SerializeField, Min(0f)] private float trailParticlesPerUnit = 5f;
    [SerializeField] private Vector2 trailLifetime = new(0.55f, 0.9f);
    [SerializeField] private Vector2 trailSize = new(0.08f, 0.14f);
    [SerializeField, Min(1)] private int trailMaxParticles = 24;

    [Header("Gold Aura")]
    [SerializeField, Min(0f)] private float premiumEmissionRate = 3.5f;
    [SerializeField, Min(1)] private int premiumMaxParticles = 14;

    private static Material sharedParticleMaterial;
    private static Texture2D sharedParticleTexture;
    private static Shader sharedVfxShader;
    private static Material sharedGlowMaterial;
    private SpriteRenderer sourceRenderer;
    private SpriteRenderer outerGlow;
    private SpriteRenderer innerGlow;
    private ParticleSystem acquisitionBurstParticles;
    private ParticleSystem trailParticles;
    private ParticleSystem premiumParticles;
    private Coroutine acquisitionRoutine;
    private SheepQuality quality;
    private Color qualityColor = Color.white;
    private bool glowVisible;

    public static SpecialSheepAcquisitionVfx Ensure(GameObject target)
    {
        if (target == null)
            return null;

        SpecialSheepAcquisitionVfx effect = target.GetComponent<SpecialSheepAcquisitionVfx>();
        return effect != null ? effect : target.AddComponent<SpecialSheepAcquisitionVfx>();
    }

    public static bool SupportsQuality(SheepQuality sheepQuality)
    {
        return sheepQuality == SheepQuality.Purple || sheepQuality == SheepQuality.Gold;
    }

    public void OnSpecialSheepSpawned(SpecialSheepMarker sheep)
    {
        if (sheep == null || !SupportsQuality(sheep.Quality))
            return;

        quality = sheep.Quality;
        qualityColor = GetQualityColor(quality);
        // 野生羊阶段只记录品质，不创建渲染节点。这样特效完全不会影响生成成功与否，
        // 也避免为尚未获得、可能被距离系统回收的羊分配粒子系统与材质。
        if (sourceRenderer == null)
            sourceRenderer = GetComponent<SpriteRenderer>();
    }

    public void OnSpecialSheepRecruited(SpecialSheepMarker sheep, FlockController flock)
    {
        if (sheep == null || !SupportsQuality(sheep.Quality))
            return;

        quality = sheep.Quality;
        qualityColor = GetQualityColor(quality);
        EnsureVisuals();
        if (outerGlow == null || innerGlow == null
            || acquisitionBurstParticles == null || trailParticles == null)
        {
            Debug.LogWarning("特殊羊缺少可用的 SpriteRenderer，无法播放获得特效。", this);
            return;
        }

        if (acquisitionRoutine != null)
            StopCoroutine(acquisitionRoutine);

        ResetVisuals();
        StartPremiumAuraIfNeeded();
        acquisitionBurstParticles.Play(true);
        acquisitionRoutine = StartCoroutine(PlayAcquisitionSequence());
        Debug.Log($"特殊羊获得特效已触发：{sheep.TypeName} / {quality}", this);
    }

    /// <summary>
    /// 点击预览：只播放对应品质的发光脉冲，不触发获得时的粒子爆发和常驻拖尾。
    /// 复用 <see cref="SheepCardView.GetQualityColor"/>，覆盖普通羊在内的全部品质。
    /// </summary>
    public void PlayGlowPreview(SheepQuality previewQuality)
    {
        quality = previewQuality;
        qualityColor = SheepCardView.GetQualityColor(previewQuality);
        EnsureVisuals();
        if (outerGlow == null || innerGlow == null)
            return;

        if (acquisitionRoutine != null)
            StopCoroutine(acquisitionRoutine);

        ResetVisuals();
        acquisitionRoutine = StartCoroutine(PlayAcquisitionSequence(startTrailAfterGlow: false));
    }

    private void LateUpdate()
    {
        if (!glowVisible || sourceRenderer == null)
            return;

        SyncGlowRenderer(outerGlow, -1);
        SyncGlowRenderer(innerGlow, 1);
    }

    private void OnDisable()
    {
        if (acquisitionRoutine != null)
        {
            StopCoroutine(acquisitionRoutine);
            acquisitionRoutine = null;
        }

        ResetVisuals();
    }

    private IEnumerator PlayAcquisitionSequence(bool startTrailAfterGlow = true)
    {
        glowVisible = true;
        outerGlow.gameObject.SetActive(true);
        innerGlow.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < glowDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / glowDuration);
            float fadeIn = Mathf.Clamp01(progress / 0.08f);
            float fadeOut = Mathf.Clamp01((1f - progress) / 0.18f);
            float envelope = Mathf.Min(fadeIn, fadeOut);
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * glowPulseSpeed * Mathf.PI * 2f);
            float scale = Mathf.Lerp(glowMinScale, glowMaxScale, pulse);

            outerGlow.transform.localScale = Vector3.one * scale;
            innerGlow.transform.localScale = Vector3.one * Mathf.Lerp(1.01f, 1.07f, pulse);
            outerGlow.color = WithAlpha(qualityColor, outerGlowAlpha * envelope * Mathf.Lerp(0.7f, 1f, pulse));
            innerGlow.color = WithAlpha(qualityColor, innerGlowAlpha * envelope);
            yield return null;
        }

        glowVisible = false;
        outerGlow.gameObject.SetActive(false);
        innerGlow.gameObject.SetActive(false);

        // 需求要求拖尾在发光播放完成后才出现；点击预览不需要留下常驻拖尾。
        if (startTrailAfterGlow && trailParticles != null)
            trailParticles.Play(true);

        acquisitionRoutine = null;
    }

    private void EnsureVisuals()
    {
        if (sourceRenderer == null)
            sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer == null)
            sourceRenderer = GetComponentInChildren<SpriteRenderer>();
        if (sourceRenderer == null)
            return;

        if (outerGlow == null)
            outerGlow = CreateGlowRenderer(OuterGlowName);
        if (innerGlow == null)
            innerGlow = CreateGlowRenderer(InnerGlowName);
        Material particleMaterial = GetSharedParticleMaterial();
        if (acquisitionBurstParticles == null)
        {
            acquisitionBurstParticles = CreateAcquisitionBurstParticles(
                particleMaterial,
                1f);
        }

        if (trailParticles == null)
            trailParticles = CreateTrailParticles(particleMaterial, 1f);

        if (quality == SheepQuality.Gold && premiumParticles == null)
        {
            premiumParticles = CreatePremiumParticles();
        }

        SyncGlowRenderer(outerGlow, -1);
        SyncGlowRenderer(innerGlow, 1);
    }

    private SpriteRenderer CreateGlowRenderer(string objectName)
    {
        Transform existing = sourceRenderer.transform.Find(objectName);
        GameObject glowObject = existing != null ? existing.gameObject : new GameObject(objectName);
        glowObject.layer = gameObject.layer;
        glowObject.transform.SetParent(sourceRenderer.transform, false);

        SpriteRenderer renderer = glowObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = glowObject.AddComponent<SpriteRenderer>();
        if (renderer == null)
        {
            Debug.LogWarning($"无法为 {objectName} 创建 SpriteRenderer。", glowObject);
            glowObject.SetActive(false);
            return null;
        }

        renderer.enabled = true;
        glowObject.SetActive(false);
        return renderer;
    }

    private void SyncGlowRenderer(SpriteRenderer glow, int sortingOffset)
    {
        if (glow == null || sourceRenderer == null)
            return;

        glow.sprite = sourceRenderer.sprite;
        glow.flipX = sourceRenderer.flipX;
        glow.flipY = sourceRenderer.flipY;
        glow.drawMode = sourceRenderer.drawMode;
        glow.size = sourceRenderer.size;
        glow.maskInteraction = sourceRenderer.maskInteraction;
        glow.spriteSortPoint = sourceRenderer.spriteSortPoint;
        glow.sortingLayerID = sourceRenderer.sortingLayerID;
        glow.sortingOrder = sourceRenderer.sortingOrder + sortingOffset;
        glow.sharedMaterial = GetSharedGlowMaterial() ?? sourceRenderer.sharedMaterial;
    }

    private ParticleSystem CreateTrailParticles(
        Material particleMaterial,
        float sizeMultiplier)
    {
        ParticleSystem particles = CreateParticleSystem(TrailName, particleMaterial);
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = OrderedCurve(trailLifetime, 0.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
        main.startSize = ScaleCurve(OrderedCurve(trailSize, 0.02f), sizeMultiplier);
        main.startColor = WithAlpha(qualityColor, 1f);
        main.maxParticles = Mathf.Max(1, trailMaxParticles);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = Mathf.Max(0f, trailParticlesPerUnit);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.18f;
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = 0.2f;

        // 品质色由 startColor 提供；生命周期曲线只控制明暗与透明度，避免颜色相乘后变暗。
        ApplyFadeAndShrink(particles, Color.white, 1f);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private ParticleSystem CreateAcquisitionBurstParticles(
        Material particleMaterial,
        float sizeMultiplier)
    {
        ParticleSystem particles = CreateParticleSystem(AcquisitionBurstName, particleMaterial);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 1.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
        main.startSize = ScaleCurve(OrderedCurve(acquisitionBurstSize, 0.04f), sizeMultiplier);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = WithAlpha(qualityColor, 1f);
        main.maxParticles = Mathf.Max(1, acquisitionBurstCount);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Clamp(acquisitionBurstCount, 1, short.MaxValue)),
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = 1f;

        ApplyFadeAndShrink(particles, Color.white, 1f);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private ParticleSystem CreatePremiumParticles()
    {
        ParticleSystem particles = CreateParticleSystem(PremiumAuraName, GetSharedParticleMaterial());
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.maxParticles = Mathf.Max(1, premiumMaxParticles);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, premiumEmissionRate);
        emission.rateOverDistance = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radiusThickness = 1f;

        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.04f);
        main.startColor = WithAlpha(qualityColor, 0.9f);
        shape.radius = 0.48f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        // Orbital X/Y/Z 必须使用相同的曲线模式。X/Y 默认为 Constant，
        // 因此为每只金羊随机一个 Constant 转速，而不是把 Z 设为 TwoConstants。
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(Random.Range(1.6f, 2.4f));
        ApplyFadeAndShrink(particles, Color.white, 0.75f);

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private ParticleSystem CreateParticleSystem(string objectName, Material material)
    {
        Transform existing = transform.Find(objectName);
        GameObject particleObject = existing != null ? existing.gameObject : new GameObject(objectName);
        particleObject.layer = gameObject.layer;
        particleObject.transform.SetParent(transform, false);

        ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
        if (particles == null)
            particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.prewarm = false;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            renderer = particleObject.AddComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = sourceRenderer.sortingOrder + 12;
        renderer.sharedMaterial = material != null ? material : GetSharedParticleMaterial();
        return particles;
    }

    private void StartPremiumAuraIfNeeded()
    {
        if (premiumParticles != null)
            premiumParticles.Play(true);
    }

    private void ResetVisuals()
    {
        glowVisible = false;
        if (outerGlow != null)
            outerGlow.gameObject.SetActive(false);
        if (innerGlow != null)
            innerGlow.gameObject.SetActive(false);

        StopAndClear(trailParticles);
        StopAndClear(acquisitionBurstParticles);
        StopAndClear(premiumParticles);
    }

    private static void StopAndClear(ParticleSystem particles)
    {
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void ApplyFadeAndShrink(ParticleSystem particles, Color color, float startAlpha = 0.7f)
    {
        Gradient fade = new();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(startAlpha, 0.12f),
                new GradientAlphaKey(startAlpha * 0.7f, 0.68f),
                new GradientAlphaKey(0f, 1f),
            });

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = fade;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.55f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0f)));
    }

    private static Material GetSharedParticleMaterial()
    {
        if (sharedParticleMaterial != null)
            return sharedParticleMaterial;

        // Sprites/Default 已被羊本身引用，不会在 Player 构建时因没有资产引用而被剥离。
        Shader shader = FindParticleShader();
        if (shader == null)
            return null;

        sharedParticleMaterial = CreateParticleMaterial(
            shader,
            GetSharedParticleTexture(),
            "Special Sheep Shared Particle Material");
        return sharedParticleMaterial;
    }

    private static Shader FindParticleShader()
    {
        if (sharedVfxShader != null)
            return sharedVfxShader;

        sharedVfxShader = Resources.Load<Shader>(VfxShaderResourcePath)
            ?? Shader.Find("GameJam/SpecialSheepUnlit")
            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Sprites/Default");
        return sharedVfxShader;
    }

    private static Material GetSharedGlowMaterial()
    {
        if (sharedGlowMaterial != null)
            return sharedGlowMaterial;

        Shader shader = FindParticleShader();
        if (shader == null)
            return null;

        sharedGlowMaterial = new Material(shader)
        {
            name = "Special Sheep Shared Glow Material",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent,
        };
        if (sharedGlowMaterial.HasProperty("_Color"))
            sharedGlowMaterial.SetColor("_Color", Color.white);
        return sharedGlowMaterial;
    }

    private static Material CreateParticleMaterial(Shader shader, Texture texture, string materialName)
    {
        Material material = new(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent,
        };
        material.SetOverrideTag("RenderType", "Transparent");
        SetMaterialFloatIfPresent(material, "_Surface", 1f);
        SetMaterialFloatIfPresent(material, "_ZWrite", 0f);
        SetMaterialFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetMaterialFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        return material;
    }

    private static Texture2D GetSharedParticleTexture()
    {
        if (sharedParticleTexture != null)
            return sharedParticleTexture;

        const int size = 4;
        sharedParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Special Sheep Quality Pixel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        Color[] pixels = new Color[size * size];
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = Color.white;

        sharedParticleTexture.SetPixels(pixels);
        sharedParticleTexture.Apply(false, true);
        return sharedParticleTexture;
    }

    private static void SetMaterialFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static ParticleSystem.MinMaxCurve OrderedCurve(Vector2 range, float minimum)
    {
        float min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return new ParticleSystem.MinMaxCurve(min, max);
    }

    private static ParticleSystem.MinMaxCurve ScaleCurve(
        ParticleSystem.MinMaxCurve curve,
        float multiplier)
    {
        multiplier = Mathf.Max(0.01f, multiplier);
        return new ParticleSystem.MinMaxCurve(
            curve.constantMin * multiplier,
            curve.constantMax * multiplier);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    public static Color GetQualityColor(SheepQuality sheepQuality)
    {
        return sheepQuality switch
        {
            SheepQuality.Purple => new Color32(190, 120, 245, 255),
            SheepQuality.Gold => new Color32(255, 205, 70, 255),
            _ => Color.white,
        };
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在屏幕边缘用狼头像提示真实来袭方向。每只已经确定路线、但仍在画面外的狼
/// 对应一个头像；不显示平静期或顶部阶段 HUD。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
[DefaultExecutionOrder(100)]
public sealed class WolfEdgeThreatView : MonoBehaviour
{
    private const string ViewName = "WolfEdgeThreats";

    [Header("References")]
    [SerializeField] private WolfEventDirector director;
    [SerializeField] private Camera viewCamera;

    [Header("Layout")]
    [SerializeField, Min(0f)] private float edgeInset = 74f;
    [SerializeField, Min(24f)] private float iconSize = 116f;
    [SerializeField, Min(1)] private int initialPoolSize = 6;

    [Header("Warning Feel")]
    [SerializeField] private Color earlyWarningColor = new Color(1f, 0.62f, 0.12f, 1f);
    [SerializeField] private Color imminentColor = new Color(1f, 0.12f, 0.08f, 1f);
    [SerializeField] private Color retreatColor = new Color(0.48f, 0.52f, 0.58f, 1f);
    [SerializeField, Min(0.1f)] private float earlyFlashFrequency = 1.6f;
    [SerializeField, Min(0.1f)] private float imminentFlashFrequency = 8.5f;
    [Tooltip("边缘位置跟随速度。过滤镜头震动，但仍快速响应来袭方向变化。")]
    [SerializeField, Min(0.1f)] private float edgeFollowSpeed = 24f;
    [SerializeField, Min(0.1f)] private float fadeSpeed = 8f;
    [SerializeField, Min(0.1f)] private float retreatFadeSpeed = 3.5f;
    [SerializeField, Min(0f)] private float retreatSlideSpeed = 42f;

    private sealed class Indicator
    {
        public Wolf Wolf;
        public RectTransform Root;
        public Image Glow;
        public Image Portrait;
        public CanvasGroup CanvasGroup;
        public Vector2 EdgePosition;
        public Vector2 Inward = Vector2.up;
        public float Alpha;
        public float Scale = 1f;
        public float PulsePhase;
        public bool HasEdgePosition;
        public bool HasEnteredViewport;
        public bool Retiring;
    }

    private readonly List<Indicator> activeIndicators = new List<Indicator>();
    private readonly Stack<Indicator> pooledIndicators = new Stack<Indicator>();
    private RectTransform root;
    private CanvasGroup rootCanvasGroup;
    private bool subscribed;

    public int ActiveIndicatorCount => activeIndicators.Count;

    public static WolfEdgeThreatView Create(
        Transform canvasParent,
        WolfEventDirector eventDirector,
        Camera camera)
    {
        if (canvasParent == null)
            return null;

        WolfEdgeThreatView existing = canvasParent.GetComponentInChildren<WolfEdgeThreatView>(true);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.Bind(eventDirector, camera);
            return existing;
        }

        RectTransform rect = MvpUiFactory.CreateRect(ViewName, canvasParent);
        MvpUiFactory.Stretch(rect);
        rect.SetAsLastSibling();
        WolfEdgeThreatView view = rect.gameObject.AddComponent<WolfEdgeThreatView>();
        view.Bind(eventDirector, camera);
        return view;
    }

    private void Awake()
    {
        root = (RectTransform)transform;
        rootCanvasGroup = GetComponent<CanvasGroup>();
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
        EnsurePool();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReleaseAll(immediate: true);
    }

    public void Bind(WolfEventDirector eventDirector, Camera camera)
    {
        Unsubscribe();
        director = eventDirector;
        viewCamera = camera;
        if (isActiveAndEnabled)
            Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed || director == null)
            return;

        director.PhaseChanged += HandlePhaseChanged;
        director.WolfReleased += HandleWolfReleased;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (director != null)
        {
            director.PhaseChanged -= HandlePhaseChanged;
            director.WolfReleased -= HandleWolfReleased;
        }
        subscribed = false;
    }

    private void LateUpdate()
    {
        if (rootCanvasGroup == null)
            return;

        bool gameplayVisible = director != null && director.IsRunning && Time.timeScale > 0f;
        rootCanvasGroup.alpha = gameplayVisible ? 1f : 0f;
        if (!gameplayVisible)
        {
            if (director == null || !director.IsRunning)
                ReleaseAll(immediate: true);
            return;
        }

        Camera camera = viewCamera != null ? viewCamera : Camera.main;
        float deltaTime = Time.deltaTime;
        for (int index = activeIndicators.Count - 1; index >= 0; index--)
        {
            Indicator indicator = activeIndicators[index];
            if (indicator.Wolf == null)
                indicator.Retiring = true;

            UpdateIndicator(indicator, camera, deltaTime);
            if (indicator.Retiring && indicator.Alpha <= 0.001f)
                ReleaseAt(index);
        }
    }

    private void HandlePhaseChanged(WolfEventPhase phase)
    {
        if (phase == WolfEventPhase.Attack)
            return;

        if (phase == WolfEventPhase.Retreat)
        {
            for (int index = 0; index < activeIndicators.Count; index++)
                BeginRetreat(activeIndicators[index]);
            return;
        }

        ReleaseAll(immediate: true);
    }

    private void HandleWolfReleased(Wolf wolf)
    {
        if (wolf == null || FindIndicator(wolf) != null)
            return;

        Indicator indicator = AcquireIndicator();
        indicator.Wolf = wolf;
        indicator.Alpha = 0f;
        indicator.Scale = 0.82f;
        indicator.PulsePhase = 0f;
        indicator.HasEdgePosition = false;
        indicator.HasEnteredViewport = false;
        indicator.Retiring = false;
        Sprite portrait = director != null ? director.ThreatIndicatorSprite : null;
        if (portrait == null)
            portrait = wolf.ThreatIndicatorSprite;
        indicator.Glow.sprite = portrait;
        indicator.Portrait.sprite = portrait;
        indicator.Root.gameObject.SetActive(true);
        indicator.CanvasGroup.alpha = 0f;
        activeIndicators.Add(indicator);
        wolf.Finished += HandleWolfFinished;
    }

    private void HandleWolfFinished(Wolf wolf)
    {
        Indicator indicator = FindIndicator(wolf);
        if (indicator == null)
            return;

        wolf.Finished -= HandleWolfFinished;
        indicator.Wolf = null;
        BeginRetreat(indicator);
    }

    private void UpdateIndicator(Indicator indicator, Camera camera, float deltaTime)
    {
        if (indicator.Retiring)
        {
            indicator.Alpha = Mathf.MoveTowards(
                indicator.Alpha,
                0f,
                retreatFadeSpeed * deltaTime);
            indicator.Scale = Mathf.MoveTowards(indicator.Scale, 0.72f, 2.5f * deltaTime);
            indicator.EdgePosition -= indicator.Inward * retreatSlideSpeed * deltaTime;
            ApplyVisual(indicator, retreatColor, indicator.Alpha, indicator.Scale, indicator.EdgePosition);
            return;
        }

        Wolf wolf = indicator.Wolf;
        if (wolf == null)
        {
            BeginRetreat(indicator);
            return;
        }
        if (camera == null || root.rect.width <= 0f || root.rect.height <= 0f)
            return;

        Vector3 projected = camera.WorldToViewportPoint(wolf.transform.position);
        Vector2 viewport = new Vector2(projected.x, projected.y);
        bool inside = projected.z >= 0f && IsInsideViewport(viewport);
        indicator.HasEnteredViewport |= inside;

        Vector3 threatProjected = camera.WorldToViewportPoint(wolf.ThreatWorldPosition);
        Vector2 threatViewport = new Vector2(threatProjected.x, threatProjected.y);
        if ((threatViewport - Vector2.one * 0.5f).sqrMagnitude <= 0.0001f)
        {
            Vector2 fallbackPoint = wolf.ThreatWorldPosition - wolf.ChargeDirection;
            Vector3 fallbackProjected = camera.WorldToViewportPoint(fallbackPoint);
            threatViewport = new Vector2(fallbackProjected.x, fallbackProjected.y);
        }
        if (!indicator.HasEnteredViewport
            && TryCalculateDirectionEdgePosition(
                root.rect,
                threatViewport,
                edgeInset,
                out Vector2 edge,
                out Vector2 inward))
        {
            if (!indicator.HasEdgePosition)
            {
                indicator.EdgePosition = edge;
                indicator.Inward = inward;
                indicator.HasEdgePosition = true;
            }
            else
            {
                float follow = 1f - Mathf.Exp(-edgeFollowSpeed * deltaTime);
                indicator.EdgePosition = Vector2.Lerp(indicator.EdgePosition, edge, follow);
                Vector2 smoothedInward = Vector2.Lerp(indicator.Inward, inward, follow);
                if (smoothedInward.sqrMagnitude > 0.0001f)
                    indicator.Inward = smoothedInward.normalized;
            }
        }

        bool threatening = wolf.IsWarning || wolf.IsCharging;
        if (!threatening)
        {
            BeginRetreat(indicator);
            return;
        }

        float urgency = wolf.IsWarning
            ? Mathf.Clamp01(wolf.WarningElapsed / Mathf.Max(0.01f, wolf.WarningDuration))
            : CalculateOffscreenUrgency(viewport);
        float frequency = Mathf.Lerp(earlyFlashFrequency, imminentFlashFrequency, urgency);
        indicator.PulsePhase = AdvancePulsePhase(indicator.PulsePhase, frequency, deltaTime);
        float pulse01 = 0.5f + 0.5f * Mathf.Sin(indicator.PulsePhase * Mathf.PI * 2f);
        float targetAlpha = indicator.HasEnteredViewport
            ? 0f
            : Mathf.Lerp(0.68f, 1f, pulse01);
        indicator.Alpha = Mathf.MoveTowards(indicator.Alpha, targetAlpha, fadeSpeed * deltaTime);
        float targetScale = indicator.HasEnteredViewport
            ? 0.82f
            : Mathf.Lerp(0.98f, 1.18f, pulse01 * Mathf.Lerp(0.45f, 1f, urgency));
        indicator.Scale = Mathf.MoveTowards(indicator.Scale, targetScale, 5f * deltaTime);

        Color color = Color.Lerp(earlyWarningColor, imminentColor, urgency);
        ApplyVisual(indicator, color, indicator.Alpha, indicator.Scale, indicator.EdgePosition);
    }

    /// <summary>
    /// 用增量相位驱动变频闪烁。不能用 Time.time * frequency：frequency 每帧变化时会直接跳相位。
    /// </summary>
    public static float AdvancePulsePhase(float phase, float frequency, float deltaTime)
    {
        return Mathf.Repeat(phase + Mathf.Max(0f, frequency) * Mathf.Max(0f, deltaTime), 1f);
    }

    private static float CalculateOffscreenUrgency(Vector2 viewport)
    {
        float outsideX = Mathf.Max(0f, Mathf.Abs(viewport.x - 0.5f) - 0.5f);
        float outsideY = Mathf.Max(0f, Mathf.Abs(viewport.y - 0.5f) - 0.5f);
        float outsideDistance = Mathf.Max(outsideX, outsideY);
        return 1f - Mathf.Clamp01(outsideDistance / 0.65f);
    }

    private static void ApplyVisual(
        Indicator indicator,
        Color color,
        float alpha,
        float scale,
        Vector2 position)
    {
        indicator.Root.anchoredPosition = position;
        indicator.Root.localScale = Vector3.one * scale;
        indicator.CanvasGroup.alpha = alpha;
        Color glow = color;
        glow.a = 0.78f;
        indicator.Glow.color = glow;
        indicator.Portrait.color = Color.white;
    }

    private void BeginRetreat(Indicator indicator)
    {
        if (indicator == null || indicator.Retiring)
            return;

        if (indicator.Wolf != null)
            indicator.Wolf.Finished -= HandleWolfFinished;
        indicator.Wolf = null;
        indicator.Retiring = true;
    }

    private Indicator FindIndicator(Wolf wolf)
    {
        for (int index = 0; index < activeIndicators.Count; index++)
            if (activeIndicators[index].Wolf == wolf)
                return activeIndicators[index];
        return null;
    }

    private void EnsurePool()
    {
        while (pooledIndicators.Count < initialPoolSize)
        {
            Indicator indicator = CreateIndicator();
            indicator.Root.gameObject.SetActive(false);
            pooledIndicators.Push(indicator);
        }
    }

    private Indicator AcquireIndicator()
    {
        return pooledIndicators.Count > 0
            ? pooledIndicators.Pop()
            : CreateIndicator();
    }

    private Indicator CreateIndicator()
    {
        RectTransform indicatorRoot = MvpUiFactory.CreateRect("WolfThreat", root);
        indicatorRoot.anchorMin = new Vector2(0.5f, 0.5f);
        indicatorRoot.anchorMax = new Vector2(0.5f, 0.5f);
        indicatorRoot.pivot = new Vector2(0.5f, 0.5f);
        indicatorRoot.sizeDelta = Vector2.one * iconSize;

        Image glow = MvpUiFactory.CreateImage("Glow", indicatorRoot, earlyWarningColor);
        MvpUiFactory.Stretch(glow.rectTransform);
        glow.preserveAspect = true;
        glow.raycastTarget = false;
        glow.rectTransform.localScale = Vector3.one * 1.28f;

        Image portrait = MvpUiFactory.CreateImage("Wolf", indicatorRoot, Color.white);
        MvpUiFactory.Stretch(portrait.rectTransform, 9f);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        CanvasGroup group = indicatorRoot.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        return new Indicator
        {
            Root = indicatorRoot,
            Glow = glow,
            Portrait = portrait,
            CanvasGroup = group,
        };
    }

    private void ReleaseAt(int index)
    {
        Indicator indicator = activeIndicators[index];
        if (indicator.Wolf != null)
            indicator.Wolf.Finished -= HandleWolfFinished;
        indicator.Wolf = null;
        indicator.Root.gameObject.SetActive(false);
        activeIndicators.RemoveAt(index);
        pooledIndicators.Push(indicator);
    }

    private void ReleaseAll(bool immediate)
    {
        for (int index = activeIndicators.Count - 1; index >= 0; index--)
        {
            if (immediate)
                ReleaseAt(index);
            else
                BeginRetreat(activeIndicators[index]);
        }
    }

    public static bool IsInsideViewport(Vector2 viewport)
    {
        return viewport.x >= 0f && viewport.x <= 1f
            && viewport.y >= 0f && viewport.y <= 1f;
    }

    /// <summary>把画面外的 viewport 点投到带安全边距的 UI 矩形边缘。</summary>
    public static bool TryCalculateEdgePosition(
        Rect canvasRect,
        Vector2 viewport,
        float inset,
        out Vector2 localPosition,
        out Vector2 inwardDirection)
    {
        if (IsInsideViewport(viewport))
        {
            localPosition = Vector2.zero;
            inwardDirection = Vector2.up;
            return false;
        }

        return TryCalculateDirectionEdgePosition(
            canvasRect,
            viewport,
            inset,
            out localPosition,
            out inwardDirection);
    }

    /// <summary>
    /// 把目标相对画面中心的方向投到 UI 边缘。目标本身即使已位于视口内，也仍可用于表示
    /// 狼正式出现的方向；是否隐藏提示由狼当前可见位置单独决定。
    /// </summary>
    public static bool TryCalculateDirectionEdgePosition(
        Rect canvasRect,
        Vector2 viewport,
        float inset,
        out Vector2 localPosition,
        out Vector2 inwardDirection)
    {
        localPosition = Vector2.zero;
        inwardDirection = Vector2.up;
        if (canvasRect.width <= 0f || canvasRect.height <= 0f)
            return false;

        Vector2 fromCenter = new Vector2(
            (viewport.x - 0.5f) * canvasRect.width,
            (viewport.y - 0.5f) * canvasRect.height);
        if (fromCenter.sqrMagnitude <= 0.0001f)
            return false;

        float halfWidth = Mathf.Max(0f, canvasRect.width * 0.5f - Mathf.Max(0f, inset));
        float halfHeight = Mathf.Max(0f, canvasRect.height * 0.5f - Mathf.Max(0f, inset));
        float xScale = Mathf.Abs(fromCenter.x) > 0.0001f
            ? halfWidth / Mathf.Abs(fromCenter.x)
            : float.PositiveInfinity;
        float yScale = Mathf.Abs(fromCenter.y) > 0.0001f
            ? halfHeight / Mathf.Abs(fromCenter.y)
            : float.PositiveInfinity;
        float scale = Mathf.Min(xScale, yScale);
        localPosition = fromCenter * scale;
        inwardDirection = -fromCenter.normalized;
        return true;
    }
}

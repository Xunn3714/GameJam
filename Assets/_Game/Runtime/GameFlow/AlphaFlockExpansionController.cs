using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 羊群暴力扩张 Alpha 的关卡控制器：
/// 阶段推进（只升不降）、周边野生羊密度维持、狼群节奏接入、出口解锁与冲出地图、全灭失败、按类型统计、提示横幅。
/// </summary>
[DisallowMultipleComponent]
public sealed class AlphaFlockExpansionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlockController flock;
    [SerializeField] private FlockMovementController flockMovement;
    [SerializeField] private FlockActionController flockActions;
    [SerializeField] private ProgressiveSheepSpawner sheepSpawner;
    [SerializeField] private CameraFollow2D cameraFollow;
    [Tooltip("狼群节奏（生长空挡 → 狼嚎 → 攻击 → 跑路）。有它时狼由它掌控；为空则退回旧的 WolfSpawner 定时。")]
    [SerializeField] private WolfEventDirector wolfDirector;
    [SerializeField] private WolfSpawner wolfSpawner;
    [SerializeField] private BorderFenceRing borderRing;
    [SerializeField] private TutorialPen tutorialPen;
    [SerializeField] private AlphaBannerView bannerView;
    [Tooltip("结算页挂到这个 Canvas 下。")]
    [SerializeField] private Canvas uiCanvas;

    [Header("Level UI (复用 Level_01 的界面)")]
    [Tooltip("任务列表 + 族群数（Level_01 的 SheepHUD）。")]
    [SerializeField] private MvpHudView hudView;
    [Tooltip("“xx 加入了族群”提示。")]
    [SerializeField] private JoinToastView joinToastView;
    [SerializeField] private PauseManager pauseManager;
    [Tooltip("仓库里的 ResultPanel 预制体；留空则用 Alpha 自己的占位结算页。")]
    [SerializeField] private ResultPanelView resultPanelPrefab;

    [Header("Progression")]
    [SerializeField] private FlockGrowthStage[] stages =
    {
        new("孤羊", 1, 1, 1, 5f),
        new("小群", 12, 2, 3, 7f),
        new("狼群来袭", 20, 5, 7, 10f),
        new("暴力扩张", 50, 10, 15, 14f),
        new("羊潮", 90, 20, 30, 18f)
    };
    [Tooltip("镜头放大时整体提速：倍率 = (当前相机尺寸 / 第一阶段相机尺寸) ^ 指数。0 = 不提速。")]
    [SerializeField, Range(0f, 1.5f)] private float speedScaleExponent = 0.75f;
    [Tooltip("没有 WolfEventDirector 时，旧式狼生成器在这个羊数后启动。有 Director 时以 Director 的 Required Member Count 为准。")]
    [SerializeField, Min(1)] private int wolfUnlockFlockSize = 20;
    [Tooltip("羊数第一次达到这个值时弹出「狼群闻讯而来」的提示。")]
    [SerializeField, Min(1)] private int wolfPackWarningFlockSize = 80;
    [SerializeField, Min(0.1f)] private float failedSpawnRetryDelay = 1.5f;

    [Header("Impact Feedback")]
    [Tooltip("第一阶段镜头尺寸下的主动撞击振幅；实际值会随当前视野等比放大。")]
    [SerializeField, Min(0f)] private float impactShakeAmplitude = 0.16f;
    [SerializeField, Min(0f)] private float impactShakeDuration = 0.2f;
    [SerializeField, Min(0.02f)] private float impactFeedbackInterval = 0.12f;

    [Header("Exit")]
    [Tooltip("历史最高羊数达到这个值后永久解锁出口；撞开围栏时当前羊数也必须达标。")]
    [SerializeField, Min(1)] private int exitUnlockFlockSize = 100;
    [Tooltip("羊群中心越过地图边界多远算成功冲出。")]
    [SerializeField, Min(0.5f)] private float exitMargin = 2.5f;
    [Tooltip("解锁后移动 / 相机边界向外放宽多少，让羊群能穿过围栏缺口。")]
    [SerializeField, Min(1f)] private float exitBoundsExpansion = 10f;
    [SerializeField, Min(0f)] private float fenceBreakShakeAmplitude = 0.35f;
    [SerializeField, Min(0f)] private float fenceBreakShakeDuration = 0.45f;

    [Header("Nearby Wild Sheep")]
    [SerializeField, Min(0.1f)] private float populationRefreshInterval = 0.4f;
    [SerializeField, Min(0.1f)] private float nearbySheepPerCameraSize = 2.5f;
    [SerializeField, Min(1f)] private float nearbyRadiusMultiplier = 4f;
    [SerializeField, Min(1f)] private float retentionRadiusMultiplier = 1.6f;
    [SerializeField, Min(1)] private int maximumBatchesPerRefresh = 16;
    [Tooltip("单次刷新最多生成多少只，避免阶段升级瞬间一帧生成太多。")]
    [SerializeField, Min(1)] private int maximumSheepPerRefresh = 12;

    [Header("Debug HUD")]
    [Tooltip("左上角开发者调试信息；只在 Inspector 里勾选才显示，正常游玩不要开。")]
    [SerializeField] private bool showDebugHud;

    [Header("Special Sheep Debug Shortcut")]
    [Tooltip("同时按住 O + P 多少秒后，在出生羊圈外生成五种品质测试羊。")]
    [SerializeField, Min(0.1f)] private float specialSheepDebugHoldDuration = 5f;
    [SerializeField, Min(0.8f)] private float specialSheepDebugSpacing = 2f;
    [SerializeField, Min(0.8f)] private float specialSheepDebugOutsideOffset = 2.2f;

    private AlphaProgression progression;
    private AlphaRunStats stats;
    private AlphaResultView resultView;
    private SheepDiscoveryToastView discoveryToastView;
    private bool wolvesUnlocked;
    private bool wolfPackWarningShown;
    private bool scaredWolfHintShown;
    private bool wolvesAnnounced;
    private bool initialized;
    private bool ended;
    private bool penOpened;
    private bool borderBroken;
    private bool escaped;
    private bool firstWolfEventCompleted;
    private int newRecruitCount;
    private readonly List<MvpObjectiveSnapshot> objectiveScratch = new List<MvpObjectiveSnapshot>();
    private float nextPopulationRefreshTime;
    private float nextImpactFeedbackTime;
    private float runStartTime;
    private float specialSheepDebugHeldTime;
    private bool specialSheepDebugTriggered;
    private readonly List<string> typeScratch = new List<string>();

    public int CurrentStageIndex => progression != null ? progression.StageIndex : 0;
    public int HighestFlockSize => progression != null ? progression.HighestFlockSize : 0;
    public bool WolvesUnlocked => wolvesUnlocked;
    public bool ExitUnlocked => progression != null && progression.ExitUnlocked;
    public bool HasEnded => ended;
    public AlphaRunStats Stats => stats;

    private void Awake()
    {
        // Alpha 不再显示屏幕顶部的狼群阶段 / 倒计时提示；狼群玩法本身保持不变。
        WolfEventHudView wolfHud = uiCanvas != null
            ? uiCanvas.GetComponentInChildren<WolfEventHudView>(true)
            : null;
        if (wolfHud != null)
            wolfHud.gameObject.SetActive(false);

        if (flock != null && flockMovement == null)
            flockMovement = flock.GetComponent<FlockMovementController>();
        if (flock != null && flockActions == null)
            flockActions = flock.GetComponent<FlockActionController>();
        flockActions?.Configure(flock, flockMovement);
    }

    private void OnEnable()
    {
        if (flock != null)
        {
            flock.MemberCountChanged += HandleMemberCountChanged;
            flock.SheepRecruited += HandleSheepRecruited;
            flock.MembersSeparated += HandleMembersSeparated;
            flock.FenceChargeImpact += HandleFenceChargeImpact;
        }

        if (wolfDirector != null)
        {
            wolfDirector.PhaseChanged += HandleWolfPhaseChanged;
            wolfDirector.WolfReleased += HandleWolfReleased;
            wolfDirector.WolfScared += HandleWolfScared;
        }

        if (borderRing != null)
            borderRing.FenceBroken += HandleBorderFenceBroken;

        if (tutorialPen != null)
        {
            tutorialPen.Opened += HandleTutorialPenOpened;
            tutorialPen.HintRequested += ShowLatestBanner;
        }
    }

    private void OnDisable()
    {
        if (flock != null)
        {
            flock.MemberCountChanged -= HandleMemberCountChanged;
            flock.SheepRecruited -= HandleSheepRecruited;
            flock.MembersSeparated -= HandleMembersSeparated;
            flock.FenceChargeImpact -= HandleFenceChargeImpact;
        }

        if (wolfDirector != null)
        {
            wolfDirector.PhaseChanged -= HandleWolfPhaseChanged;
            wolfDirector.WolfReleased -= HandleWolfReleased;
            wolfDirector.WolfScared -= HandleWolfScared;
        }

        if (borderRing != null)
            borderRing.FenceBroken -= HandleBorderFenceBroken;

        if (tutorialPen != null)
        {
            tutorialPen.Opened -= HandleTutorialPenOpened;
            tutorialPen.HintRequested -= ShowLatestBanner;
        }
    }

    private void Start()
    {
        if (flock == null || sheepSpawner == null || stages == null || stages.Length == 0)
        {
            Debug.LogError("Alpha 羊群扩张场景缺少必要引用或阶段配置。", this);
            enabled = false;
            return;
        }

        if (flock.Members.Count > 0 && flock.Members[0] != null)
        {
            SheepVisualAnimator initialAnimator =
                flock.Members[0].GetComponent<SheepVisualAnimator>();
            initialAnimator?.SetFacingImmediately(true);
        }

        if (joinToastView != null)
        {
            UnityEngine.UI.Image bannerBackground = bannerView != null
                ? bannerView.GetComponent<UnityEngine.UI.Image>() : null;
            Sprite notificationSprite = bannerBackground != null ? bannerBackground.sprite : null;
            joinToastView.ConfigureStack(notificationSprite);
            discoveryToastView = SheepDiscoveryToastView.Create(joinToastView.transform.parent, notificationSprite);
            discoveryToastView.transform.SetSiblingIndex(joinToastView.transform.GetSiblingIndex() + 1);
        }

        runStartTime = Time.time;
        stats = new AlphaRunStats();
        progression = new AlphaProgression(stages, exitUnlockFlockSize, Mathf.Max(1, flock.MemberCount));

        // 有节奏控制器时狼由它管（含阶段门槛、狼嚎抱团）；否则退回旧的定时生成器。
        if (wolfDirector == null)
            wolfSpawner?.StopSpawning();

        sheepSpawner.Initialize();
        if (tutorialPen != null)
        {
            Rect pen = tutorialPen.PenRect;
            sheepSpawner.SetExclusionZones(new Rect(pen.xMin - 2f, pen.yMin - 2f, pen.width + 4f, pen.height + 4f));
        }

        Rect worldRect = borderRing != null ? borderRing.WorldRect : sheepSpawner.SpawnBounds;
        flockMovement?.ConfigureMovementBounds(worldRect);
        cameraFollow?.ConfigureBounds(worldRect);
        borderRing?.ApplyRequiredCount(exitUnlockFlockSize);

        if (uiCanvas != null && resultPanelPrefab == null)
            resultView = AlphaResultView.Create(uiCanvas.transform);

        ApplyStage(true);
        RefreshComposition();
        MaintainNearbyPopulation();
        initialized = true;
        RefreshObjectives();

        // 还在出生羊圈里时不放狼（不然羊圈里的羊会被叼光，永远凑不够）；羊圈打开后再开始狼群节奏。
        if (wolfDirector != null && (tutorialPen == null || tutorialPen.IsOpen))
            wolfDirector.Run();
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (ended)
            return;

        UpdateSpecialSheepDebugShortcut();

        stats.SurvivalSeconds = Time.time - runStartTime;

        if (CheckEscaped())
            return;

        if (Time.unscaledTime >= nextPopulationRefreshTime)
            MaintainNearbyPopulation();
    }

    // ---------------------------------------------------------------- flock events

    private void HandleMemberCountChanged(int memberCount)
    {
        if (!initialized || ended)
            return;

        RefreshComposition();

        if (memberCount <= 0)
        {
            EndRun(false);
            return;
        }

        AlphaProgression.Change change = progression.Observe(memberCount);
        RefreshObjectives();
        if (!change.HighestChanged)
            return;

        if (change.StageChanged)
        {
            ApplyStage(false);
            FlockGrowthStage stage = progression.CurrentStage;
            ShowLatestBanner($"阶段 {progression.StageIndex + 1} · {stage.DisplayName}");
            Debug.Log($"羊群升级到阶段 {progression.StageIndex + 1}：{stage.DisplayName}。", this);
        }

        if (change.ExitJustUnlocked)
            UnlockExit();

        if (!wolfPackWarningShown && progression.HighestFlockSize >= wolfPackWarningFlockSize)
        {
            wolfPackWarningShown = true;
            ShowBanner("羊群逐渐长大，狼群闻讯而来");
        }

        if (wolfDirector == null && !wolvesUnlocked && progression.HighestFlockSize >= wolfUnlockFlockSize)
        {
            wolvesUnlocked = true;
            wolfSpawner?.StartSpawning();
            AnnounceWolves();
        }
    }

    private void HandleSheepRecruited(RecruitableSheep sheep, int recruitedCount)
    {
        if (!initialized || sheep == null)
            return;

        // 被狼撞散后捡回来的老成员不算新招募。
        ScatteredSheep scattered = sheep.GetComponent<ScatteredSheep>();
        if (scattered != null && scattered.IsScattered)
            return;

        newRecruitCount++;

        SheepIdentity identity = sheep.GetComponent<SheepIdentity>();
        string typeId = identity != null ? identity.SheepTypeId : MvpSheepCatalog.DefaultTypeId;
        stats.RecordRecruit(typeId);

        string sheepName = identity != null && !string.IsNullOrWhiteSpace(identity.DisplayName) ? identity.DisplayName : "";
        if (joinToastView != null && !string.IsNullOrEmpty(sheepName))
            joinToastView.Show(sheepName);

        // FlockController records EncounterSheep after SheepRecruited returns, so this
        // reads the existing cross-run unlock state before this recruitment unlocks it.
        SheepCollectionManager collection = SheepCollectionManager.Instance;
        if (discoveryToastView != null && collection != null && !collection.IsUnlocked(typeId))
        {
            SheepCollectionEntry entry = collection.GetSheepData(typeId);
            SpecialSheepMarker marker = sheep.GetComponent<SpecialSheepMarker>();
            if (entry != null)
                discoveryToastView.Show(typeId, entry.displayName,
                    marker != null ? marker.Quality : SheepQuality.Common);
        }

        RefreshObjectives();

        if (sheepSpawner.MarkRecruited(sheep))
            MaintainNearbyPopulation();
    }

    private void HandleMembersSeparated(int count)
    {
        if (!initialized || ended || count <= 0)
            return;

        ShowLatestBanner(count == 1 ? "一只羊脱队了！" : $"有 {count} 只羊脱队了！");
    }

    private void HandleFenceChargeImpact(bool hardImpact)
    {
        if (ended || Time.unscaledTime < nextImpactFeedbackTime)
            return;

        nextImpactFeedbackTime = Time.unscaledTime + impactFeedbackInterval;
        float amplitude = hardImpact ? impactShakeAmplitude : impactShakeAmplitude * 0.65f;
        float referenceSize = stages != null && stages.Length > 0 && stages[0] != null
            ? Mathf.Max(0.1f, stages[0].CameraSize)
            : 5f;
        float currentSize = cameraFollow != null
            ? Mathf.Max(referenceSize, cameraFollow.CurrentOrthographicSize)
            : referenceSize;
        amplitude *= currentSize / referenceSize;
        cameraFollow?.Shake(amplitude, impactShakeDuration);
    }

    private void RefreshComposition()
    {
        typeScratch.Clear();
        foreach (SheepMember member in flock.Members)
        {
            if (member == null)
                continue;

            SheepIdentity identity = member.GetComponent<SheepIdentity>();
            typeScratch.Add(identity != null ? identity.SheepTypeId : MvpSheepCatalog.DefaultTypeId);
        }

        stats.ObserveComposition(typeScratch);
    }

    // ---------------------------------------------------------------- wolves

    private void HandleWolfPhaseChanged(WolfEventPhase phase)
    {
        if (phase == WolfEventPhase.Retreat && !firstWolfEventCompleted)
        {
            firstWolfEventCompleted = true;
            RefreshObjectives();
        }

        if (phase == WolfEventPhase.Dormant)
            return;

        if (!wolvesUnlocked)
        {
            wolvesUnlocked = true;
            AnnounceWolves();
        }
    }

    private void AnnounceWolves()
    {
        if (wolvesAnnounced)
            return;

        wolvesAnnounced = true;
        ShowBanner(
            "狼群盯上了你的羊群……听到狼嚎就抱紧！",
            null,
            wolfDirector != null ? wolfDirector.HowlClip : null);
        Debug.Log("狼开始进攻。", this);
    }

    private void HandleWolfReleased(Wolf wolf)
    {
        if (wolf != null)
            wolf.Attacked += HandleWolfAttacked;
    }

    private void HandleWolfScared(Wolf wolf)
    {
        if (scaredWolfHintShown)
            return;

        scaredWolfHintShown = true;
        ShowBanner("羊群已经足够庞大，面对一只弱小的狼，也许……？");
    }

    private void HandleWolfAttacked(Wolf wolf, WolfAttackResult result)
    {
        if (result.CapturedSheep == null)
            return;

        SheepIdentity identity = result.CapturedSheep.GetComponent<SheepIdentity>();
        stats.RecordTaken(identity != null ? identity.SheepTypeId : MvpSheepCatalog.DefaultTypeId);
    }

    // ---------------------------------------------------------------- exit

    private void UnlockExit()
    {
        borderRing?.ApplyRequiredCount(exitUnlockFlockSize);
        ExpandBoundsForExit();
        ShowBanner($"历史最高达到 {exitUnlockFlockSize} 只！按 E 让整群蓄势冲刺，撞开围栏后冲出草原");
        Debug.Log("出口已解锁。", this);
    }

    private void ExpandBoundsForExit()
    {
        Rect worldRect = borderRing != null ? borderRing.WorldRect : sheepSpawner.SpawnBounds;
        Rect expanded = new Rect(
            worldRect.xMin - exitBoundsExpansion,
            worldRect.yMin - exitBoundsExpansion,
            worldRect.width + exitBoundsExpansion * 2f,
            worldRect.height + exitBoundsExpansion * 2f);
        flockMovement?.ConfigureMovementBounds(expanded);
        cameraFollow?.ConfigureBounds(expanded);
    }

    private void HandleBorderFenceBroken(FenceObstacle fence)
    {
        borderBroken = true;
        cameraFollow?.Shake(fenceBreakShakeAmplitude, fenceBreakShakeDuration);
        ExpandBoundsForExit();
        ShowBanner("围栏破了！带着羊群冲出去！");
        RefreshObjectives();
    }

    private bool CheckEscaped()
    {
        if (!ExitUnlocked || borderRing == null || !borderRing.AnyBroken)
            return false;

        Rect worldRect = borderRing.WorldRect;
        Vector2 center = flock.Center;
        bool outside = center.x < worldRect.xMin - exitMargin
            || center.x > worldRect.xMax + exitMargin
            || center.y < worldRect.yMin - exitMargin
            || center.y > worldRect.yMax + exitMargin;

        if (!outside)
            return false;

        EndRun(true);
        return true;
    }

    // ---------------------------------------------------------------- tutorial

    private void HandleTutorialPenOpened()
    {
        penOpened = true;
        if (wolfDirector != null)
            wolfDirector.Run();
        cameraFollow?.Shake(fenceBreakShakeAmplitude * 0.6f, fenceBreakShakeDuration);
        sheepSpawner.SetExclusionZones();
        ShowBanner("羊圈打开了！去草原上壮大羊群吧");
        RefreshObjectives();
    }

    /// <summary>把 Alpha 的进度翻译成任务栏当前唯一显示的条目。</summary>
    private void RefreshObjectives()
    {
        if (hudView == null || progression == null)
            return;

        int members = flock != null ? flock.MemberCount : 0;
        int wolfStageTarget = StageMinimum(2, 20);
        int armyTarget = StageMinimum(3, 50);
        int sheepTideTarget = StageMinimum(4, 90);

        objectiveScratch.Clear();
        objectiveScratch.Add(AlphaTaskSequence.Current(
            newRecruitCount,
            penOpened,
            progression.HighestFlockSize,
            firstWolfEventCompleted || wolfDirector == null,
            borderBroken,
            escaped,
            members,
            5,
            wolfStageTarget,
            armyTarget,
            sheepTideTarget,
            exitUnlockFlockSize));

        hudView.UpdateObjectives(objectiveScratch, members);
    }

    private int StageMinimum(int index, int fallback)
    {
        return stages != null && index >= 0 && index < stages.Length && stages[index] != null
            ? stages[index].MinimumFlockSize
            : fallback;
    }

    // ---------------------------------------------------------------- end of run

    private void EndRun(bool victory)
    {
        if (ended)
            return;

        ended = true;
        escaped = victory;
        stats.SurvivalSeconds = Time.time - runStartTime;

        wolfDirector?.Stop();
        wolfSpawner?.StopSpawning();
        flockActions?.SetControlEnabled(false);
        flockMovement?.SetControlEnabled(false);
        if (pauseManager != null)
            pauseManager.SetResultLocked(true);
        RefreshObjectives();
        Time.timeScale = 0f;

        // 狼叼走最后一只羊时，Remove 先触发归零，Attacked 事件还在后面；晚一帧再结算，统计才完整。
        StartCoroutine(FinishEndRun(victory));
    }

    private System.Collections.IEnumerator FinishEndRun(bool victory)
    {
        yield return null;
        RefreshComposition();

        if (wolfDirector != null)
            stats.SetWolfBreakdown(wolfDirector.LossTracker.TakenByLongWolves, wolfDirector.LossTracker.TakenBySingleWolves);
        if (GameStatsManager.Instance != null)
            GameStatsManager.Instance.RecordRunResult(stats, victory);
        string report = stats.BuildReport(sheepSpawner.GetTypeDisplayName);
        string description = victory
            ? $"羊群带着 {flock.MemberCount} 只羊冲出了草原（历史最高 {stats.HighestFlockSize} 只）"
            : "最后一只羊也没了……";

        Debug.Log((victory ? "胜利：" : "失败：") + description + "\n" + report, this);

        if (resultPanelPrefab != null && uiCanvas != null)
        {
            ResultPanelView panel = Instantiate(resultPanelPrefab, uiCanvas.transform);
            panel.transform.SetAsLastSibling();
            panel.ReturnTitleRequested += ReturnToTitle;
            string panelDescription = description + "\n" + report + "\n按 R 再来一局";
            if (victory)
                panel.ShowVictory(panelDescription, flock.MemberCount, stats.HighestFlockSize, stats.TotalRecruited, stats.TotalTaken, stats.SurvivalSeconds);
            else
                panel.ShowDefeat(panelDescription, 0, stats.HighestFlockSize, stats.TotalRecruited, stats.TotalTaken, stats.SurvivalSeconds);
            resultPanelShown = true;
        }
        else if (resultView != null)
        {
            if (victory) resultView.ShowVictory(description, report);
            else resultView.ShowDefeat(description, report);
        }
    }

    private bool resultPanelShown;

    private void LateUpdate()
    {
        // 仓库的 ResultPanel 只有返回标题按钮；这里补一个 R 快速重开。
        if (!resultPanelShown)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            SceneReloadUtility.ReloadActiveScene();
    }

    private static void ReturnToTitle()
    {
        Time.timeScale = 1f;
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    // ---------------------------------------------------------------- helpers

    private void ApplyStage(bool immediateCamera)
    {
        FlockGrowthStage stage = progression.CurrentStage;
        if (stage == null)
            return;

        cameraFollow?.SetOrthographicSize(stage.CameraSize, immediateCamera);

        float baseCameraSize = stages[0] != null ? stages[0].CameraSize : stage.CameraSize;
        float multiplier = speedScaleExponent <= 0f
            ? 1f
            : Mathf.Pow(stage.CameraSize / Mathf.Max(0.1f, baseCameraSize), speedScaleExponent);
        flock.SetSpeedMultiplier(multiplier);
    }

    public float CurrentSpeedMultiplier => flock != null ? flock.SpeedMultiplier : 1f;

    private void ShowBanner(string message)
    {
        if (bannerView != null)
            bannerView.Show(message);
    }

    private void ShowBanner(string message, Sprite icon)
    {
        if (bannerView != null)
            bannerView.Show(message, icon);
    }

    private void ShowBanner(string message, Sprite icon, AudioClip sound)
    {
        if (bannerView != null)
            bannerView.Show(message, icon, sound);
    }

    private void ShowLatestBanner(string message)
    {
        if (bannerView != null)
            bannerView.ShowLatest(message);
    }

    private void MaintainNearbyPopulation()
    {
        FlockGrowthStage stage = progression.CurrentStage;
        if (stage == null)
            return;

        Vector2 center = flock.Center;
        float nearbyRadius = stage.CameraSize * nearbyRadiusMultiplier;
        float retentionRadius = nearbyRadius * retentionRadiusMultiplier;
        int desiredNearbySheep = Mathf.CeilToInt(stage.CameraSize * nearbySheepPerCameraSize);

        sheepSpawner.DespawnWildSheepFartherThan(center, retentionRadius);
        int nearbySheep = sheepSpawner.CountWildSheepNear(center, nearbyRadius);
        int batchesSpawned = 0;
        int spawnedBefore = sheepSpawner.TotalSpawned;

        while (nearbySheep < desiredNearbySheep
            && batchesSpawned < maximumBatchesPerRefresh
            && sheepSpawner.TotalSpawned - spawnedBefore < maximumSheepPerRefresh)
        {
            if (!sheepSpawner.SpawnBatch(stage.MinimumBatchSize, stage.MaximumBatchSize))
            {
                nextPopulationRefreshTime = Time.unscaledTime + failedSpawnRetryDelay;
                return;
            }

            batchesSpawned++;
            nearbySheep = sheepSpawner.CountWildSheepNear(center, nearbyRadius);
        }

        nextPopulationRefreshTime = Time.unscaledTime + populationRefreshInterval;
    }

    private void UpdateSpecialSheepDebugShortcut()
    {
        Keyboard keyboard = Keyboard.current;
        bool chordHeld = keyboard != null
            && keyboard.oKey.isPressed
            && keyboard.pKey.isPressed
            && Time.timeScale > 0f;
        if (!chordHeld)
        {
            specialSheepDebugHeldTime = 0f;
            specialSheepDebugTriggered = false;
            return;
        }

        if (specialSheepDebugTriggered)
            return;

        specialSheepDebugHeldTime += Time.unscaledDeltaTime;
        if (specialSheepDebugHeldTime < Mathf.Max(0.1f, specialSheepDebugHoldDuration))
            return;

        specialSheepDebugTriggered = true;
        Vector2 spawnCenter = ResolveSpecialSheepDebugSpawnCenter();
        int spawned = sheepSpawner.SpawnDebugQualitySamples(spawnCenter, specialSheepDebugSpacing);
        string message = spawned == 5
            ? "调试：已在出生羊圈外生成五种品质羊"
            : $"调试：品质羊生成 {spawned}/5 只，请检查 Catalog";
        ShowLatestBanner(message);
        Debug.Log(message, this);
    }

    private Vector2 ResolveSpecialSheepDebugSpawnCenter()
    {
        Rect pen = tutorialPen != null
            ? tutorialPen.PenRect
            : new Rect(flock.Center - new Vector2(9f, 5f), new Vector2(18f, 10f));
        Rect world = sheepSpawner.SpawnBounds;
        float rowHalfWidth = specialSheepDebugSpacing * 2f;
        float margin = 0.75f;
        float x = Mathf.Clamp(
            pen.center.x,
            world.xMin + rowHalfWidth + margin,
            world.xMax - rowHalfWidth - margin);

        float below = pen.yMin - Mathf.Max(0.8f, specialSheepDebugOutsideOffset);
        float y = below >= world.yMin + margin
            ? below
            : Mathf.Min(world.yMax - margin, pen.yMax + specialSheepDebugOutsideOffset);
        return new Vector2(x, y);
    }

    private void OnGUI()
    {
        if (progression == null)
            return;

        if (!showDebugHud)
            return;

        FlockGrowthStage stage = progression.CurrentStage;
        int currentFlock = flock != null ? flock.MemberCount : 0;
        int wildSheep = sheepSpawner != null ? sheepSpawner.ActiveWildSheepCount : 0;
        int desiredNearbySheep = stage != null ? Mathf.CeilToInt(stage.CameraSize * nearbySheepPerCameraSize) : 0;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            richText = true
        };
        labelStyle.normal.textColor = Color.white;

        string wolfLine;
        if (wolfDirector != null)
        {
            wolfLine = wolfDirector.Phase == WolfEventPhase.Dormant
                ? $"狼：羊群达到 {wolfDirector.RequiredMemberCount} 只后出现"
                : $"狼：已出没（第 {wolfDirector.RoundIndex} 轮）";
        }
        else
        {
            int wolves = wolfSpawner != null ? wolfSpawner.AliveCount : 0;
            wolfLine = wolvesUnlocked ? $"狼：已解锁（场上 {wolves}）" : $"狼：达到 {wolfUnlockFlockSize} 只后出现";
        }

        string exitLine;
        if (!ExitUnlocked)
            exitLine = $"出口：历史最高 {progression.HighestFlockSize}/{exitUnlockFlockSize}";
        else if (borderRing != null && borderRing.AnyBroken)
            exitLine = "出口：围栏已破，冲出去！";
        else if (currentFlock >= exitUnlockFlockSize)
            exitLine = "出口：已解锁，按 E 整群冲刺破栏";
        else
            exitLine = $"出口：已解锁，当前羊数 {currentFlock}/{exitUnlockFlockSize}";

        GUI.Box(new Rect(12f, 12f, 390f, 200f), GUIContent.none);
        GUILayout.BeginArea(new Rect(24f, 20f, 370f, 190f));
        GUILayout.Label($"阶段 {progression.StageIndex + 1} · <b>{(stage != null ? stage.DisplayName : "")}</b>", labelStyle);
        GUILayout.Label($"羊群：<b>{currentFlock}</b>　历史最高：{progression.HighestFlockSize}　速度 ×{CurrentSpeedMultiplier:0.00}", labelStyle);
        GUILayout.Label($"周围野生羊：{wildSheep}　密度目标：约 {desiredNearbySheep}", labelStyle);
        GUILayout.Label(wolfLine, labelStyle);
        if (wolfSpawner != null && wolfSpawner.DodgeMemory.Count > 0)
        {
            GUILayout.Label(
                $"狼记忆：{wolfSpawner.DodgeMemory.Count} 次，玩家平均躲 {wolfSpawner.DodgeMemory.AverageDegrees:0.0}°",
                labelStyle);
        }
        GUILayout.Label(exitLine, labelStyle);
        GUILayout.Label("WASD 移动 · E 整群后退蓄势冲刺 · Q 收拢", labelStyle);
        GUILayout.EndArea();
    }

    private void DrawStatsOverlay()
    {
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            richText = true
        };
        labelStyle.normal.textColor = Color.white;

        const float width = 520f;
        const float height = 140f;
        float x = 12f;
        float y = Screen.height - height - 12f;
        GUI.Box(new Rect(x, y, width, height), GUIContent.none);
        GUILayout.BeginArea(new Rect(x + 12f, y + 8f, width - 24f, height - 16f));
        GUILayout.Label("<b>本局统计</b>（Tab 关闭）", labelStyle);
        GUILayout.Label(stats.BuildReport(sheepSpawner.GetTypeDisplayName), labelStyle);
        GUILayout.EndArea();
    }
}

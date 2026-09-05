using UnityEngine;

[DisallowMultipleComponent]
public sealed class AlphaFlockExpansionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlockController flock;
    [SerializeField] private FlockMovementController flockMovement;
    [SerializeField] private ProgressiveSheepSpawner sheepSpawner;
    [SerializeField] private CameraFollow2D cameraFollow;
    [SerializeField] private WolfSpawner wolfSpawner;

    [Header("Progression")]
    [SerializeField] private FlockGrowthStage[] stages =
    {
        new("孤羊", 1, 1, 1, 5f),
        new("小群", 5, 2, 3, 7f),
        new("狼群来袭", 20, 5, 7, 10f),
        new("暴力扩张", 50, 10, 15, 14f),
        new("羊潮", 100, 20, 30, 18f)
    };
    [SerializeField, Min(1)] private int wolfUnlockFlockSize = 20;
    [SerializeField, Min(0.1f)] private float failedSpawnRetryDelay = 1.5f;

    [Header("Nearby Wild Sheep")]
    [SerializeField, Min(0.1f)] private float populationRefreshInterval = 0.4f;
    [SerializeField, Min(0.1f)] private float nearbySheepPerCameraSize = 2.5f;
    [SerializeField, Min(1f)] private float nearbyRadiusMultiplier = 4f;
    [SerializeField, Min(1f)] private float retentionRadiusMultiplier = 1.6f;
    [SerializeField, Min(1)] private int maximumBatchesPerRefresh = 16;

    [Header("Debug HUD")]
    [SerializeField] private bool showDebugHud = true;

    private int currentStageIndex;
    private int highestFlockSize;
    private bool wolvesUnlocked;
    private bool initialized;
    private float nextPopulationRefreshTime;

    public int CurrentStageIndex => currentStageIndex;
    public int HighestFlockSize => highestFlockSize;
    public bool WolvesUnlocked => wolvesUnlocked;

    private void Awake()
    {
        if (flock != null && flockMovement == null)
            flockMovement = flock.GetComponent<FlockMovementController>();
    }

    private void OnEnable()
    {
        if (flock == null)
            return;

        flock.MemberCountChanged += HandleMemberCountChanged;
        flock.SheepRecruited += HandleSheepRecruited;
    }

    private void Start()
    {
        if (flock == null || sheepSpawner == null || stages == null || stages.Length == 0)
        {
            Debug.LogError("Alpha 羊群扩张场景缺少必要引用或阶段配置。", this);
            enabled = false;
            return;
        }

        wolfSpawner?.StopSpawning();
        sheepSpawner.Initialize();
        flockMovement?.ConfigureMovementBounds(sheepSpawner.SpawnBounds);
        cameraFollow?.ConfigureBounds(sheepSpawner.SpawnBounds);

        currentStageIndex = 0;
        highestFlockSize = Mathf.Max(1, flock.MemberCount);
        ApplyHighestUnlockedStage(true);
        MaintainNearbyPopulation();
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || Time.unscaledTime < nextPopulationRefreshTime)
            return;

        MaintainNearbyPopulation();
    }

    private void OnDisable()
    {
        if (flock == null)
            return;

        flock.MemberCountChanged -= HandleMemberCountChanged;
        flock.SheepRecruited -= HandleSheepRecruited;
    }

    private void HandleMemberCountChanged(int memberCount)
    {
        if (memberCount <= highestFlockSize)
            return;

        highestFlockSize = memberCount;
        ApplyHighestUnlockedStage(false);
    }

    private void HandleSheepRecruited(RecruitableSheep sheep, int recruitedCount)
    {
        if (sheepSpawner.MarkRecruited(sheep))
            MaintainNearbyPopulation();
    }

    private void ApplyHighestUnlockedStage(bool immediateCamera)
    {
        int unlockedIndex = 0;
        for (int index = 0; index < stages.Length; index++)
        {
            if (stages[index] != null && highestFlockSize >= stages[index].MinimumFlockSize)
                unlockedIndex = index;
        }

        bool stageChanged = unlockedIndex != currentStageIndex;
        currentStageIndex = unlockedIndex;
        FlockGrowthStage stage = stages[currentStageIndex];
        cameraFollow?.SetOrthographicSize(stage.CameraSize, immediateCamera);

        if (stageChanged)
        {
            Debug.Log(
                $"羊群升级到阶段 {currentStageIndex + 1}：{stage.DisplayName}，" +
                $"后续每批 {stage.MinimumBatchSize}-{stage.MaximumBatchSize} 只。",
                this);
        }

        if (!wolvesUnlocked && highestFlockSize >= wolfUnlockFlockSize)
        {
            wolvesUnlocked = true;
            wolfSpawner?.StartSpawning();
            Debug.Log($"羊群达到 {wolfUnlockFlockSize} 只：狼开始进攻。", this);
        }
    }

    private void MaintainNearbyPopulation()
    {
        FlockGrowthStage stage = stages[Mathf.Clamp(currentStageIndex, 0, stages.Length - 1)];
        Vector2 center = flock.Center;
        float nearbyRadius = stage.CameraSize * nearbyRadiusMultiplier;
        float retentionRadius = nearbyRadius * retentionRadiusMultiplier;
        int desiredNearbySheep = Mathf.CeilToInt(stage.CameraSize * nearbySheepPerCameraSize);

        sheepSpawner.DespawnWildSheepFartherThan(center, retentionRadius);
        int nearbySheep = sheepSpawner.CountWildSheepNear(center, nearbyRadius);
        int batchesSpawned = 0;

        while (nearbySheep < desiredNearbySheep && batchesSpawned < maximumBatchesPerRefresh)
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

    private void OnGUI()
    {
        if (!showDebugHud || stages == null || stages.Length == 0)
            return;

        FlockGrowthStage stage = stages[Mathf.Clamp(currentStageIndex, 0, stages.Length - 1)];
        int currentFlock = flock != null ? flock.MemberCount : 0;
        int wildSheep = sheepSpawner != null ? sheepSpawner.ActiveWildSheepCount : 0;
        int desiredNearbySheep = Mathf.CeilToInt(stage.CameraSize * nearbySheepPerCameraSize);
        int wolves = wolfSpawner != null ? wolfSpawner.AliveCount : 0;

        GUIStyle labelStyle = new(GUI.skin.label)
        {
            fontSize = 19,
            richText = true
        };
        labelStyle.normal.textColor = Color.white;

        GUI.Box(new Rect(12f, 12f, 370f, 174f), GUIContent.none);
        GUILayout.BeginArea(new Rect(24f, 20f, 350f, 160f));
        GUILayout.Label($"阶段 {currentStageIndex + 1} · <b>{stage.DisplayName}</b>", labelStyle);
        GUILayout.Label($"羊群：<b>{currentFlock}</b>　历史最高：{highestFlockSize}", labelStyle);
        GUILayout.Label($"周围野生羊：{wildSheep}　密度目标：约 {desiredNearbySheep}", labelStyle);
        GUILayout.Label(wolvesUnlocked ? $"狼：已解锁（场上 {wolves}）" : $"狼：达到 {wolfUnlockFlockSize} 只后出现", labelStyle);
        GUILayout.Label("WASD 移动 · 接触羊即可扩张", labelStyle);
        GUILayout.EndArea();
    }
}

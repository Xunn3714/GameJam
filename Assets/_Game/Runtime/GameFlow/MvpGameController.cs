using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class MvpGameController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private FlockController flockController;
    [SerializeField] private PoopAbility poopAbility;

    [Header("World And Spawning")]
    [SerializeField] private SpriteRenderer worldBackground;
    [SerializeField] private CameraFollow2D cameraFollow;
    [SerializeField] private RecruitableSheep recruitableSheepPrefab;
    [SerializeField, Range(1, 50)] private int recruitableSheepCount = 5;
    [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero;
    [SerializeField] private Vector2 spawnAreaSize = new(100f, 50f);
    [SerializeField, Min(0f)] private float playerSafeRadius = 3f;
    [SerializeField, Min(0.5f)] private float clusterRadius = 1.5f;
    [SerializeField, Min(1f)] private float minimumClusterDistance = 5f;
    [SerializeField, Min(1f)] private float localDensityRadius = 3f;
    [SerializeField, Min(0.1f)] private float sheepClearance = 0.75f;
    [SerializeField] private int fixedSpawnSeed;

    [Header("Obstacle Encounters")]
    [SerializeField] private bool generateObstacleEncounters = true;
    [SerializeField, Range(0, 12)] private int obstacleEncounterCount = 5;
    [SerializeField] private Sprite fenceSprite;
    [SerializeField] private ObstacleDefinition fenceDefinition;
    [SerializeField] private Sprite barrelSprite;
    [SerializeField] private ObstacleDefinition barrelDefinition;
    [SerializeField] private Sprite flowerSprite;

    [Header("Regular Sheep Group Weights")]
    [SerializeField, Min(0f)] private float groupOfOneWeight = 60f;
    [SerializeField, Min(0f)] private float groupOfTwoWeight = 30f;
    [SerializeField, Min(0f)] private float groupOfThreeWeight = 10f;

    [Header("Sheep Quality")]
    [SerializeField] private SpecialSheepPool specialSheepPool;
    [SerializeField] private SpecialSheepSpawnPoint[] fixedSpecialSpawnPoints;

    [Header("UI")]
    [SerializeField] private MvpHudView hudView;
    [SerializeField] private JoinToastView joinToastView;
    [SerializeField] private PauseManager pauseManager;

    private MvpTaskSystem taskSystem;
    private MvpSessionStats sessionStats;
    private MvpResultPanelView resultPanel;
    private MvpCodexView codexView;
    private FlockMovementController movementController;
    private bool settled;
    private int recruitTarget;

    private void Awake()
    {
        if (flockController != null)
        {
            poopAbility ??= flockController.GetComponent<PoopAbility>();
            movementController = flockController.GetComponent<FlockMovementController>();
        }

        Rect spawnArea = new(spawnAreaCenter - spawnAreaSize * 0.5f, spawnAreaSize);
        ConfigureWorld(spawnArea);
        if (fixedSpecialSpawnPoints == null || fixedSpecialSpawnPoints.Length == 0)
        {
            fixedSpecialSpawnPoints = FindObjectsByType<SpecialSheepSpawnPoint>(
                FindObjectsInactive.Exclude);
        }

        IReadOnlyList<RecruitableSheep> spawned = MvpSheepSpawnDistributor.PrepareAndDistribute(
            recruitableSheepPrefab,
            specialSheepPool,
            fixedSpecialSpawnPoints,
            recruitableSheepCount,
            groupOfOneWeight,
            groupOfTwoWeight,
            groupOfThreeWeight,
            spawnArea,
            flockController != null ? flockController.Center : Vector2.zero,
            playerSafeRadius,
            clusterRadius,
            minimumClusterDistance,
            localDensityRadius,
            sheepClearance,
            fixedSpawnSeed);

        if (generateObstacleEncounters)
        {
            MvpObstacleEncounterSpawner.Generate(
                spawnArea,
                spawned,
                obstacleEncounterCount,
                fenceSprite,
                fenceDefinition,
                barrelSprite,
                barrelDefinition,
                flowerSprite);
        }

        for (int index = 0; index < spawned.Count; index++)
            spawned[index]?.ConfigureWanderBounds(spawnArea);

        recruitTarget = spawned.Count > 0 ? spawned.Count : recruitableSheepCount;

        taskSystem = CreateTaskSystem(recruitTarget);
        sessionStats = new MvpSessionStats();
        sessionStats.Begin();
    }

    private void OnValidate()
    {
        recruitableSheepCount = Mathf.Max(1, recruitableSheepCount);
        obstacleEncounterCount = Mathf.Max(0, obstacleEncounterCount);

        if (groupOfOneWeight + groupOfTwoWeight + groupOfThreeWeight <= 0f)
            groupOfOneWeight = 1f;
    }

    private void ConfigureWorld(Rect worldBounds)
    {
        if (worldBackground != null)
        {
            worldBackground.transform.position = new Vector3(
                worldBounds.center.x,
                worldBounds.center.y,
                worldBackground.transform.position.z);
            worldBackground.drawMode = SpriteDrawMode.Tiled;
            worldBackground.size = worldBounds.size;
        }

        movementController?.ConfigureMovementBounds(worldBounds);
        cameraFollow?.ConfigureBounds(worldBounds);
    }

    private void OnEnable()
    {
        if (flockController != null)
        {
            flockController.SheepRecruited += HandleSheepRecruited;
            flockController.MemberCountChanged += HandleMemberCountChanged;
        }

        if (poopAbility != null)
        {
            poopAbility.Used += HandlePoopUsed;
            poopAbility.StockChanged += HandlePoopStockChanged;
        }

        taskSystem.Changed += HandleTasksChanged;
        taskSystem.AllRequiredCompleted += HandleAllRequiredTasksCompleted;
    }

    private void Start()
    {
        Canvas canvas = hudView != null ? hudView.GetComponentInParent<Canvas>() : null;
        if (canvas != null)
        {
            resultPanel = MvpResultPanelView.CreatePlaceholder(canvas.transform);
            codexView = MvpCodexView.CreatePlaceholder(canvas.transform, flockController);
        }

        pauseManager ??= FindAnyObjectByType<PauseManager>();
        if (pauseManager != null && codexView != null)
            pauseManager.ConfigureCodex(codexView);

        EvaluateTasks();
        taskSystem.MarkPresented();

        if (poopAbility != null)
            HandlePoopStockChanged(poopAbility.StoredPoops, poopAbility.MaxStoredPoops);
    }

    private void OnDisable()
    {
        if (flockController != null)
        {
            flockController.SheepRecruited -= HandleSheepRecruited;
            flockController.MemberCountChanged -= HandleMemberCountChanged;
        }

        if (poopAbility != null)
        {
            poopAbility.Used -= HandlePoopUsed;
            poopAbility.StockChanged -= HandlePoopStockChanged;
        }

        if (taskSystem != null)
        {
            taskSystem.Changed -= HandleTasksChanged;
            taskSystem.AllRequiredCompleted -= HandleAllRequiredTasksCompleted;
        }
    }

    private static MvpTaskSystem CreateTaskSystem(int targetRecruitCount)
    {
        int flockMilestone = Mathf.Min(3, targetRecruitCount + 1);

        return new MvpTaskSystem(new[]
        {
            new MvpTaskDefinition(
                "recruit.first", "迎接第一位同伴", true, MvpTaskConditionMode.All,
                new MvpTaskCondition(MvpTaskMetric.RecruitedTotal, 1)),
            new MvpTaskDefinition(
                "flock.grow", $"把羊群壮大到 {flockMilestone} 只", true, MvpTaskConditionMode.All,
                new MvpTaskCondition(MvpTaskMetric.CurrentFlockCount, flockMilestone)),
            new MvpTaskDefinition(
                "flock.complete", $"找到另外 {targetRecruitCount} 只羊", true, MvpTaskConditionMode.All,
                new MvpTaskCondition(MvpTaskMetric.RecruitedTotal, targetRecruitCount)),
            new MvpTaskDefinition(
                "skill.poop", $"带着 {flockMilestone} 只羊留下记号", false, MvpTaskConditionMode.All,
                new MvpTaskCondition(MvpTaskMetric.CurrentFlockCount, flockMilestone),
                new MvpTaskCondition(MvpTaskMetric.PoopUses, 1))
        });
    }

    private void HandleMemberCountChanged(int memberCount) => EvaluateTasks();

    private void HandleSheepRecruited(RecruitableSheep sheep, int recruitedCount)
    {
        ShowJoinToast(sheep);
        EvaluateTasks();
    }

    private void HandlePoopUsed()
    {
        sessionStats.RecordPoop();
        EvaluateTasks();
    }

    private void HandlePoopStockChanged(int stored, int capacity)
    {
        hudView?.UpdatePoopStock(stored, capacity);
    }

    private void EvaluateTasks()
    {
        if (settled || taskSystem == null)
            return;

        taskSystem.Evaluate(new MvpRunMetrics(
            flockController != null ? flockController.RecruitedCount : 0,
            flockController != null ? flockController.MemberCount : 0,
            sessionStats != null ? sessionStats.PoopUses : 0));
    }

    private void HandleTasksChanged(System.Collections.Generic.IReadOnlyList<MvpObjectiveSnapshot> objectives)
    {
        if (hudView != null)
            hudView.UpdateObjectives(objectives, flockController != null ? flockController.MemberCount : 0);
    }

    private void HandleAllRequiredTasksCompleted()
    {
        if (settled)
            return;

        settled = true;
        movementController?.SetControlEnabled(false);
        poopAbility?.SetControlEnabled(false);
        pauseManager?.SetResultLocked(true);

        MvpResultSnapshot snapshot = sessionStats.Complete(
            flockController != null ? flockController.Members : null,
            flockController != null ? flockController.RecruitedCount : 0);

        if (resultPanel != null)
            resultPanel.Show(snapshot, ReturnToTitle);
        else
            Debug.LogError("MVP result panel could not be created because GameCanvas was not found.", this);

        Time.timeScale = 0f;
    }

    private static void ReturnToTitle()
    {
        Time.timeScale = 1f;

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
        else
            SceneManager.LoadScene("MainMenu");
    }

    private void ShowJoinToast(RecruitableSheep sheep)
    {
        if (joinToastView == null || sheep == null)
            return;

        SheepIdentity identity = sheep.GetComponent<SheepIdentity>();
        if (identity == null || string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            Debug.LogWarning($"{sheep.name} 尚未分配随机名字。", sheep);
            return;
        }

        joinToastView.Show(identity.DisplayName);
    }
}

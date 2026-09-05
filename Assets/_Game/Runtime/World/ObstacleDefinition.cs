using UnityEngine;

public enum ObstacleSizeCategory
{
    Small,
    Medium,
    Large,
}

public enum ObstacleBreakRule
{
    /// 任意一只羊群成员接触即碎（建筑、木桶、石头、花草）。
    OnAnyContact,

    /// 围栏：羊群数量达到要求后才允许破坏。具体触发方式由围栏组件决定。
    RequireCountAndInteract,
}

public enum ObstacleCountSource
{
    /// 按羊群当前数量判断。
    CurrentFlockCount,

    /// 按本局历史最高羊数判断（一旦达到永久解锁，被狼叼走也不回退）。
    HighestFlockCountThisRun,
}

public enum ObstacleBrokenBehavior
{
    /// 碎掉后保留在场景里，关闭碰撞并换到背景层。
    BecomeBackground,

    /// 碎掉后直接消失。
    Disappear,
}

[CreateAssetMenu(
    fileName = "ObstacleDefinition",
    menuName = "Game/Obstacle Definition")]
public sealed class ObstacleDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string obstacleId = "obstacle.new";
    [SerializeField] private string displayName = "New Obstacle";
    [SerializeField] private ObstacleSizeCategory sizeCategory = ObstacleSizeCategory.Medium;

    [Header("Break Rule")]
    [SerializeField] private ObstacleBreakRule breakRule = ObstacleBreakRule.OnAnyContact;
    [SerializeField, Min(1)] private int requiredFlockCount = 6;
    [Tooltip("围栏门槛按当前羊数还是本局历史最高羊数判断。")]
    [SerializeField] private ObstacleCountSource countSource = ObstacleCountSource.CurrentFlockCount;

    [Header("Broken State")]
    [SerializeField] private ObstacleBrokenBehavior brokenBehavior = ObstacleBrokenBehavior.BecomeBackground;
    [SerializeField, Min(0f)] private float breakAnimationDuration = 0.4f;
    [SerializeField] private string brokenSortingLayer = "Background";
    [SerializeField] private int brokenSortingOrder = 0;

    [Tooltip("没有 Animator 时用这张图替换为碎掉后的样子；可留空。")]
    [SerializeField] private Sprite brokenSprite;

    public string ObstacleId => obstacleId;
    public string DisplayName => displayName;
    public ObstacleSizeCategory SizeCategory => sizeCategory;
    public ObstacleBreakRule BreakRule => breakRule;
    public int RequiredFlockCount => requiredFlockCount;
    public ObstacleCountSource CountSource => countSource;
    public ObstacleBrokenBehavior BrokenBehavior => brokenBehavior;
    public float BreakAnimationDuration => breakAnimationDuration;
    public string BrokenSortingLayer => brokenSortingLayer;
    public int BrokenSortingOrder => brokenSortingOrder;
    public Sprite BrokenSprite => brokenSprite;

    private void OnValidate()
    {
        obstacleId = obstacleId?.Trim();
        displayName = displayName?.Trim();
    }
}

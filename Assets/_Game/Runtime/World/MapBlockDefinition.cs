using System;
using UnityEngine;

public enum MapBlockRole
{
    /// 出生羊圈所在格，每局随机一格。
    Spawn,

    /// 洪山宝通寺所在格，全图唯一。
    Pagoda,

    /// 贴外圈的出口格，外围围栏只在它的一条边上可撞开。
    Exit,

    Forest,
    Plains,
    Village,
    Lake,
}

/// <summary>
/// 一种地图区块模板。地图按 3×2 网格拼装，每格从这些定义里抽一份；
/// 出生点 / 宝塔 / 出口是固定角色，其余按权重随机。
/// 散布物和地标数量按格配置，由 WorldDebrisSpawner / WorldLandmarkSpawner 读取。
/// </summary>
[CreateAssetMenu(fileName = "MapBlock", menuName = "Game/Map Block Definition")]
public sealed class MapBlockDefinition : ScriptableObject
{
    [SerializeField] private string blockId = "block.new";
    [SerializeField] private string displayName = "New Block";
    [SerializeField] private MapBlockRole role = MapBlockRole.Plains;
    [Tooltip("随机槽位的抽取权重；出生点 / 宝塔 / 出口这类固定角色忽略此值。")]
    [SerializeField, Min(0f)] private float weight = 1f;

    [Header("Debris")]
    [Tooltip("本格散布的可破坏物及权重；为空则本格不撒散布物。")]
    [SerializeField] private WorldDebrisSpawner.DebrisEntry[] debris = Array.Empty<WorldDebrisSpawner.DebrisEntry>();
    [SerializeField, Min(0f)] private float debrisDensityPer100SquareUnits = 0.8f;

    [Header("Landmarks")]
    [Tooltip("房屋区块（房子 + 干草垛 + 木桶 + 90° 羊圈围栏）数量范围。")]
    [SerializeField, Min(0)] private int minimumHouseCount;
    [SerializeField, Min(0)] private int maximumHouseCount;
    [Tooltip("允许把房子随机换成大房子。")]
    [SerializeField] private bool allowBigHouse = true;
    [Tooltip("本格固定生成的红箱子数；全图总数由 WorldLandmarkSpawner.redChestCount 兜底。")]
    [SerializeField, Min(0)] private int fixedRedChestCount;
    [SerializeField, Min(0)] private int minimumTractorCount;
    [SerializeField, Min(0)] private int maximumTractorCount;
    [SerializeField, Min(0)] private int farmClusterCount;

    public string BlockId => blockId;
    public string DisplayName => displayName;
    public MapBlockRole Role => role;
    public float Weight => Mathf.Max(0f, weight);
    public bool IsFixedRole => IsFixed(role);

    public WorldDebrisSpawner.DebrisEntry[] Debris => debris ?? Array.Empty<WorldDebrisSpawner.DebrisEntry>();
    public float DebrisDensityPer100SquareUnits => Mathf.Max(0f, debrisDensityPer100SquareUnits);
    public int MinimumHouseCount => Mathf.Max(0, minimumHouseCount);
    public int MaximumHouseCount => Mathf.Max(MinimumHouseCount, maximumHouseCount);
    public bool AllowBigHouse => allowBigHouse;
    public int FixedRedChestCount => Mathf.Max(0, fixedRedChestCount);
    public int MinimumTractorCount => Mathf.Max(0, minimumTractorCount);
    public int MaximumTractorCount => Mathf.Max(MinimumTractorCount, maximumTractorCount);
    public int FarmClusterCount => Mathf.Max(0, farmClusterCount);

    public static bool IsFixed(MapBlockRole blockRole)
    {
        return blockRole == MapBlockRole.Spawn
            || blockRole == MapBlockRole.Pagoda
            || blockRole == MapBlockRole.Exit;
    }

    private void OnValidate()
    {
        blockId = blockId?.Trim();
        displayName = displayName?.Trim();
        maximumHouseCount = Mathf.Max(minimumHouseCount, maximumHouseCount);
        maximumTractorCount = Mathf.Max(minimumTractorCount, maximumTractorCount);
    }
}

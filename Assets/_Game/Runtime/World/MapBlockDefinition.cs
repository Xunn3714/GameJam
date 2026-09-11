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
/// 区块内的散布物、地标配置在后续 PR 中逐步接入。
/// </summary>
[CreateAssetMenu(fileName = "MapBlock", menuName = "Game/Map Block Definition")]
public sealed class MapBlockDefinition : ScriptableObject
{
    [SerializeField] private string blockId = "block.new";
    [SerializeField] private string displayName = "New Block";
    [SerializeField] private MapBlockRole role = MapBlockRole.Plains;
    [Tooltip("随机槽位的抽取权重；出生点 / 宝塔 / 出口这类固定角色忽略此值。")]
    [SerializeField, Min(0f)] private float weight = 1f;

    public string BlockId => blockId;
    public string DisplayName => displayName;
    public MapBlockRole Role => role;
    public float Weight => Mathf.Max(0f, weight);
    public bool IsFixedRole => IsFixed(role);

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
    }
}

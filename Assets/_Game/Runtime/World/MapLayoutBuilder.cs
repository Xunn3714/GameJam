using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>网格里的一格：矩形和抽到的区块定义。</summary>
public sealed class MapCell
{
    public MapCell(int column, int row, Rect rect)
    {
        Column = column;
        Row = row;
        Rect = rect;
    }

    public int Column { get; }
    public int Row { get; }
    public Rect Rect { get; }
    public MapBlockDefinition Definition { get; internal set; }
    /// <summary>洪山宝通寺落在这一格之上（与区块类型无关）。</summary>
    public bool HasPagoda { get; internal set; }
    public MapBlockRole Role => Definition != null ? Definition.Role : MapBlockRole.Plains;
}

/// <summary>
/// 把外围围栏内的地图切成 columns×rows 个等大格子，按世界种子分配区块：
/// 出生点 / 森林 / 平原 / 村庄各至少一格，剩余格子在森林 / 平原 / 村庄里随机；宝塔再随机落在某一格之上。
/// 在 Awake 里（早于所有 Start）把羊圈、羊群、初始羊和相机整体挪到出生格，
/// 并同步散布物 / 地标的羊圈排除区，后面的系统不需要知道地图变过。
/// 四条边规则一致，撞开任意一段并越过地图边界都能获胜。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class MapLayoutBuilder : MonoBehaviour
{
    /// <summary>WorldSeed 随机流编号；1/2/3 已被羊刷新、散布物、地标占用。</summary>
    public const int RandomStream = 4;

    [Header("References")]
    [SerializeField] private WorldSeed worldSeed;
    [SerializeField] private BorderFenceRing borderRing;
    [SerializeField] private TutorialPen tutorialPen;
    [SerializeField] private Transform flockRoot;
    [SerializeField] private Transform initialSheep;
    [SerializeField] private Transform gameplayCamera;
    [SerializeField] private WorldDebrisSpawner debrisSpawner;
    [SerializeField] private WorldLandmarkSpawner landmarkSpawner;

    [Header("Grid")]
    [SerializeField, Min(1)] private int columns = 3;
    [SerializeField, Min(1)] private int rows = 2;
    [Tooltip("没有 BorderFenceRing 时使用的地图矩形。")]
    [SerializeField] private Rect worldRect = new(-120f, -70f, 240f, 140f);
    [Tooltip("每种角色至少一份定义；同角色多份时按权重抽。")]
    [SerializeField] private MapBlockDefinition[] blockPool = Array.Empty<MapBlockDefinition>();

    [Header("Spawn Pen")]

    [Header("Spawn Pen Exclusion")]
    [Tooltip("与 Setup 里给散布物 / 地标预留的羊圈外扩距离保持一致。")]
    [SerializeField, Min(0f)] private float debrisPenPadding = 4f;
    [SerializeField, Min(0f)] private float landmarkPenPadding = 5f;

    private MapCell[] cells = Array.Empty<MapCell>();

    public IReadOnlyList<MapCell> Cells => cells;
    public Rect WorldRect => borderRing != null ? borderRing.WorldRect : worldRect;
    public MapCell SpawnCell => FindCell(MapBlockRole.Spawn);
    public MapCell PagodaCell
    {
        get
        {
            foreach (MapCell cell in cells)
                if (cell.HasPagoda) return cell;
            return null;
        }
    }
    public MapCell CellAt(Vector2 position)
    {
        foreach (MapCell cell in cells)
        {
            if (cell.Rect.Contains(position))
                return cell;
        }

        return null;
    }

    private void Awake()
    {
        System.Random random = worldSeed != null
            ? worldSeed.CreateRandom(RandomStream)
            : new System.Random();
        cells = Assign(random, columns, rows, WorldRect, blockPool);

        foreach (MapBlockRole role in RequiredRoles)
        {
            if (FindCell(role) == null)
                Debug.LogWarning($"区块池里没有 {role} 定义，该格保持空白。", this);
        }

        RelocateSpawn();
    }

    /// <summary>四种角色各至少一格；多出来的格子在 FillerRoles 里随机。</summary>
    public static readonly MapBlockRole[] RequiredRoles =
    {
        MapBlockRole.Spawn,
        MapBlockRole.Forest,
        MapBlockRole.Plains,
        MapBlockRole.Village,
    };

    public static readonly MapBlockRole[] FillerRoles =
    {
        MapBlockRole.Forest,
        MapBlockRole.Plains,
        MapBlockRole.Village,
    };

    /// <summary>纯逻辑的分配过程，供 EditMode 测试直接调用。</summary>
    public static MapCell[] Assign(
        System.Random random,
        int columns,
        int rows,
        Rect world,
        IReadOnlyList<MapBlockDefinition> pool)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));

        pool ??= Array.Empty<MapBlockDefinition>();
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        MapCell[] result = new MapCell[columns * rows];
        float cellWidth = world.width / columns;
        float cellHeight = world.height / rows;
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            Rect rect = new(world.xMin + column * cellWidth, world.yMin + row * cellHeight, cellWidth, cellHeight);
            result[row * columns + column] = new MapCell(column, row, rect);
        }

        // 必选角色 + 随机补位角色凑满格子数，洗牌后依次落格。
        List<MapBlockRole> roles = new(RequiredRoles);
        while (roles.Count < result.Length)
            roles.Add(FillerRoles[random.Next(FillerRoles.Length)]);
        for (int index = roles.Count - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);
            (roles[index], roles[swap]) = (roles[swap], roles[index]);
        }

        for (int index = 0; index < result.Length; index++)
        {
            MapBlockRole role = roles[index];
            result[index].Definition = PickByRole(pool, role, random);
        }

        // 宝塔随机落在出生格以外的任意一格之上。
        List<MapCell> pagodaCandidates = new();
        foreach (MapCell cell in result)
        {
            if (cell.Definition == null || !MapBlockDefinition.IsFixed(cell.Role))
                pagodaCandidates.Add(cell);
        }
        if (pagodaCandidates.Count > 0)
            pagodaCandidates[random.Next(pagodaCandidates.Count)].HasPagoda = true;

        return result;
    }

    /// <summary>同一角色多份定义时按权重抽一份；没有则返回 null。</summary>
    private static MapBlockDefinition PickByRole(IReadOnlyList<MapBlockDefinition> pool, MapBlockRole role, System.Random random)
    {
        List<MapBlockDefinition> candidates = new();
        foreach (MapBlockDefinition definition in pool)
        {
            if (definition != null && definition.Role == role && definition.Weight > 0f)
                candidates.Add(definition);
        }

        return candidates.Count > 0 ? PickWeighted(candidates, random) : FindByRole(pool, role);
    }

    private static MapBlockDefinition FindByRole(IReadOnlyList<MapBlockDefinition> pool, MapBlockRole role)
    {
        foreach (MapBlockDefinition definition in pool)
        {
            if (definition != null && definition.Role == role)
                return definition;
        }

        return null;
    }

    private static MapBlockDefinition PickWeighted(List<MapBlockDefinition> candidates, System.Random random)
    {
        float total = 0f;
        foreach (MapBlockDefinition candidate in candidates)
            total += candidate.Weight;
        if (total <= 0f)
            return null;

        float roll = (float)random.NextDouble() * total;
        foreach (MapBlockDefinition candidate in candidates)
        {
            roll -= candidate.Weight;
            if (roll <= 0f)
                return candidate;
        }

        return candidates[candidates.Count - 1];
    }

    private MapCell FindCell(MapBlockRole role)
    {
        foreach (MapCell cell in cells)
        {
            if (cell.Definition != null && cell.Role == role)
                return cell;
        }

        return null;
    }

    /// <summary>
    /// 羊圈的围栏、教程羊、标识都是 TutorialPen 的子物体，挪根节点即可；
    /// 羊群中心、初始羊和相机是独立对象，按同一位移一起挪，避免开局镜头横扫。
    /// </summary>
    private void RelocateSpawn()
    {
        MapCell spawn = SpawnCell;
        if (spawn == null || tutorialPen == null)
            return;

        Vector2 delta = spawn.Rect.center - tutorialPen.PenRect.center;
        if (delta.sqrMagnitude > 0.0001f)
        {
            tutorialPen.Relocate(spawn.Rect.center);
            Shift(flockRoot, delta);
            Shift(initialSheep, delta);
            Shift(gameplayCamera, delta);
        }

        Rect pen = tutorialPen.PenRect;
        debrisSpawner?.SetExclusionZones(Expand(pen, debrisPenPadding));
        landmarkSpawner?.SetExclusionZones(Expand(pen, landmarkPenPadding));
    }

    private static void Shift(Transform target, Vector2 delta)
    {
        if (target == null)
            return;

        target.position += (Vector3)delta;
        TutorialPen.SyncBodies(target);
    }

    private static Rect Expand(Rect rect, float margin)
    {
        return new Rect(rect.xMin - margin, rect.yMin - margin, rect.width + margin * 2f, rect.height + margin * 2f);
    }

    private void OnDrawGizmos()
    {
        IReadOnlyList<MapCell> drawn = cells.Length > 0
            ? cells
            : Assign(new System.Random(0), columns, rows, WorldRect, Array.Empty<MapBlockDefinition>());

        foreach (MapCell cell in drawn)
        {
            Gizmos.color = RoleColor(cell.Definition != null ? cell.Role : (MapBlockRole?)null);
            Gizmos.DrawWireCube(cell.Rect.center, cell.Rect.size);
#if UNITY_EDITOR
            string label = cell.Definition != null ? $"{cell.Role} · {cell.Definition.DisplayName}" : $"({cell.Column},{cell.Row})";
            if (cell.HasPagoda)
                label += " + 宝塔";
            UnityEditor.Handles.Label(cell.Rect.center, label);
#endif
        }
    }

    private static Color RoleColor(MapBlockRole? role)
    {
        return role switch
        {
            MapBlockRole.Spawn => Color.cyan,
            MapBlockRole.Forest => new Color(0.1f, 0.6f, 0.1f),
            MapBlockRole.Plains => Color.yellow,
            MapBlockRole.Village => new Color(1f, 0.6f, 0.2f),
            MapBlockRole.Lake => Color.blue,
            _ => Color.white,
        };
    }
}

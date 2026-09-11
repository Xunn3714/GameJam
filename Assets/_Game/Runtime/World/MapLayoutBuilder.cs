using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

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
/// 围栏外再围一圈"公路 + 河流"；四条边规则一致，撞开任意一段并越过地图边界都能获胜。
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

    [Header("Outer Ring (road + river)")]
    [Tooltip("围栏外侧紧贴的公路带宽度；踩上去会有运羊卡车全速开过来。")]
    [SerializeField, Min(0.5f)] private float roadWidth = 8f;
    [Tooltip("公路外侧的河流带宽度：空气墙，撞开围栏也过不去。")]
    [SerializeField, Min(0.5f)] private float riverWidth = 6f;
    [Tooltip("河流贴图（草地 + 河一体的方形图，河道竖直贯穿）。")]
    [SerializeField] private Sprite riverSprite;
    [Tooltip("河道在贴图中的中心位置（0~1，从左数）和宽度占比，用来把河道对齐到河流带中线。")]
    [SerializeField, Range(0f, 1f)] private float riverCenterInTile = 0.38f;
    [SerializeField, Range(0.05f, 1f)] private float riverWidthInTile = 0.42f;
    [SerializeField] private Sprite truckSprite;
    [SerializeField, Min(0.1f)] private float truckSpeed = 40f;
    [SerializeField, Min(0f)] private float truckCooldown = 2f;

    private MapCell[] cells = Array.Empty<MapCell>();
    private Transform ringRoot;

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
    /// <summary>围栏外公路与河流带的总宽度。</summary>
    public float OuterRingWidth => roadWidth + riverWidth;

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
        BuildOuterRing();
    }

    /// <summary>五种角色各至少一格；多出来的格子在 FillerRoles 里随机。</summary>
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

    private static Vector2 EdgeDirection(MapEdge edge)
    {
        return edge switch
        {
            MapEdge.Top => Vector2.up,
            MapEdge.Bottom => Vector2.down,
            MapEdge.Left => Vector2.left,
            _ => Vector2.right,
        };
    }

    // ---------------------------------------------------------------- outer ring

    /// <summary>
    /// 围栏外侧四边统一铺设公路（可走，触发卡车）和河流（空气墙）。
    /// </summary>
    private void BuildOuterRing()
    {
        ringRoot = new GameObject("OuterRing").transform;
        ringRoot.SetParent(transform, false);

        Rect world = WorldRect;
        float total = roadWidth + riverWidth;

        foreach (MapEdge edge in new[] { MapEdge.Top, MapEdge.Bottom, MapEdge.Left, MapEdge.Right })
        {
            bool horizontal = edge == MapEdge.Top || edge == MapEdge.Bottom;
            Vector2 outward = EdgeDirection(edge);
            // 沿边的起止（含拐角，两条带在角上重叠一点没关系）。
            float from = horizontal ? world.xMin - total : world.yMin - total;
            float to = horizontal ? world.xMax + total : world.yMax + total;
            float edgeLine = edge switch
            {
                MapEdge.Top => world.yMax,
                MapEdge.Bottom => world.yMin,
                MapEdge.Left => world.xMin,
                _ => world.xMax,
            };

            float roadCenter = edgeLine + outward[horizontal ? 1 : 0] * roadWidth * 0.5f;
            float riverCenter = edgeLine + outward[horizontal ? 1 : 0] * (roadWidth + riverWidth * 0.5f);
            BuildRoad(edge, horizontal, roadCenter, from, to);
            BuildRiver(edge, horizontal, riverCenter, from, to);
        }
    }

    /// <summary>沿某条边铺一段条带；horizontal = 条带沿 x 方向延伸，此时 center 是 y。</summary>
    private static (Vector2 position, float length, float rotation) Strip(bool horizontal, float center, float from, float to)
    {
        float mid = (from + to) * 0.5f;
        Vector2 position = horizontal ? new Vector2(mid, center) : new Vector2(center, mid);
        // 条带本地 y 轴为长边：水平边转 90°，竖直边不转。
        return (position, to - from, horizontal ? 90f : 0f);
    }

    private void BuildRiver(MapEdge edge, bool horizontal, float center, float from, float to)
    {
        (Vector2 position, float length, float rotation) = Strip(horizontal, center, from, to);
        GameObject segment = new GameObject($"River_{edge}");
        segment.transform.SetParent(ringRoot, false);
        segment.transform.position = position;
        segment.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        int blockingLayer = LayerMask.NameToLayer(MovementBlocking.BlockingLayerName);
        if (blockingLayer >= 0)
            segment.layer = blockingLayer;

        // 空气墙：Blocking 层、没有 BreakableObstacle，走路挡、E 冲刺也挡。
        Rigidbody2D body = segment.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        BoxCollider2D wall = segment.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(riverWidth, length);

        GameObject visual = new GameObject("RiverSprite");
        visual.transform.SetParent(segment.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = -50;
        if (riverSprite != null)
        {
            // 贴图是"草地 + 河"一体的方图：按河道占比换算出方图边长，再把河道中心平移到带子中线。
            float tile = riverWidth / Mathf.Max(0.05f, riverWidthInTile);
            float spriteUnits = riverSprite.rect.width / riverSprite.pixelsPerUnit;
            float scale = tile / Mathf.Max(0.01f, spriteUnits);
            renderer.sprite = riverSprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            visual.transform.localScale = Vector3.one * scale;
            renderer.size = new Vector2(spriteUnits, length / scale);
            visual.transform.localPosition = new Vector3((0.5f - riverCenterInTile) * tile, 0f, 0f);
        }
        else
        {
            renderer.sprite = RuntimeSprites.Solid();
            renderer.color = new Color(0.25f, 0.55f, 0.95f, 0.9f);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(riverWidth, length);
        }
    }

    private void BuildRoad(MapEdge edge, bool horizontal, float center, float from, float to)
    {
        (Vector2 position, float length, float rotation) = Strip(horizontal, center, from, to);
        GameObject road = new GameObject($"Road_{edge}");
        road.transform.SetParent(ringRoot, false);
        road.transform.position = position;
        road.transform.rotation = Quaternion.Euler(0f, 0f, rotation);

        SpriteRenderer renderer = road.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSprites.Road();
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(roadWidth, length);
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = -45;

        BoxCollider2D trigger = road.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(roadWidth, length);

        // 卡车沿这段路的长边跑：起止点按本地 y 轴换算回世界坐标。
        Vector2 axis = horizontal ? Vector2.right : Vector2.up;
        Vector2 start = position - axis * length * 0.5f;
        Vector2 end = position + axis * length * 0.5f;
        RoadHazard hazard = road.AddComponent<RoadHazard>();
        hazard.Configure(start, end, roadWidth, truckSprite, truckSpeed, truckCooldown);
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

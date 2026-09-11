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

/// <summary>网格里的一格：矩形、抽到的区块定义，以及出口格贴外圈的那条边。</summary>
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
    public MapEdge? ExitEdge { get; internal set; }
    /// <summary>洪山宝通寺落在这一格之上（与区块类型无关）。</summary>
    public bool HasPagoda { get; internal set; }
    public MapBlockRole Role => Definition != null ? Definition.Role : MapBlockRole.Plains;
}

/// <summary>
/// 把外围围栏内的地图切成 columns×rows 个等大格子，按世界种子分配区块：
/// 出生点 / 出口 / 森林 / 平原 / 村庄各一格，第六格在森林 / 平原 / 村庄里随机；宝塔再随机落在某一格之上。
/// 在 Awake 里（早于所有 Start）把羊圈、羊群、初始羊和相机整体挪到出生格，
/// 并同步散布物 / 地标的羊圈排除区，后面的系统不需要知道地图变过。
/// 围栏外再围一圈"公路 + 河流"，只有出口格的那条外边留空，是唯一能冲出去的地方。
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
    [Tooltip("开局拆掉出生羊圈的围栏；凑够人数即算教程完成。默认保留撞栏教程。")]
    [SerializeField] private bool removeSpawnPenFences;

    [Header("Spawn Pen Exclusion")]
    [Tooltip("与 Setup 里给散布物 / 地标预留的羊圈外扩距离保持一致。")]
    [SerializeField, Min(0f)] private float debrisPenPadding = 4f;
    [SerializeField, Min(0f)] private float landmarkPenPadding = 5f;

    [Header("Exit")]
    [Tooltip("出口格地面上的指向标识；留空则用运行时生成的三角形。")]
    [SerializeField] private Sprite arrowSprite;
    [SerializeField, Min(0)] private int arrowCount = 3;
    [Tooltip("true = 出口以外的围栏锁死；false = 整圈围栏都用同一个（100 只）门槛，撞出去会落在公路上。")]
    [SerializeField] private bool lockFencesOutsideExit;
    [Tooltip("美术路牌（箭头.png）的缩放；贴图本身箭头朝上、带木桩。")]
    [SerializeField, Min(0.1f)] private float arrowScale = 2f;

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
    [Tooltip("出口缺口外面铺的一小块草地（和地图背景同一张贴图），不然冲出去是一片虚空。")]
    [SerializeField] private Sprite grassSprite;
    [Tooltip("出口外草地往外延伸多远（从围栏算起，含公路 + 河的宽度）。")]
    [SerializeField, Min(0f)] private float exitApronDepth = 40f;
    [SerializeField, Min(0.1f)] private float truckSpeed = 40f;
    [SerializeField, Min(0f)] private float truckCooldown = 2f;

    private MapCell[] cells = Array.Empty<MapCell>();
    private Transform ringRoot;

    public IReadOnlyList<MapCell> Cells => cells;
    public Rect WorldRect => borderRing != null ? borderRing.WorldRect : worldRect;
    public MapCell SpawnCell => FindCell(MapBlockRole.Spawn);
    public MapCell ExitCell => FindCell(MapBlockRole.Exit);
    public MapCell PagodaCell
    {
        get
        {
            foreach (MapCell cell in cells)
                if (cell.HasPagoda) return cell;
            return null;
        }
    }
    /// <summary>公路 + 河流带的总宽度：出口方向之外，围栏外这么远都不算冲出去。</summary>
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
        ConfigureExit();
        BuildOuterRing();
    }

    /// <summary>羊群中心是否从出口边冲出了地图（其他方向被公路 + 河流挡住，不算）。</summary>
    public bool IsBeyondExit(Vector2 position, float margin)
    {
        MapCell exit = ExitCell;
        if (exit == null || !exit.ExitEdge.HasValue)
            return false;

        Rect rect = exit.Rect;
        return exit.ExitEdge.Value switch
        {
            MapEdge.Top => position.y > rect.yMax + margin,
            MapEdge.Bottom => position.y < rect.yMin - margin,
            MapEdge.Left => position.x < rect.xMin - margin,
            _ => position.x > rect.xMax + margin,
        };
    }

    /// <summary>出口格贴外圈的那条边的围栏矩形；其余外围围栏由 BorderFenceRing 锁死。</summary>
    public Rect? ExitSpan
    {
        get
        {
            MapCell exit = ExitCell;
            if (exit == null || !exit.ExitEdge.HasValue)
                return null;

            const float thickness = 6f;
            Rect rect = exit.Rect;
            return exit.ExitEdge.Value switch
            {
                MapEdge.Top => new Rect(rect.xMin, rect.yMax - thickness * 0.5f, rect.width, thickness),
                MapEdge.Bottom => new Rect(rect.xMin, rect.yMin - thickness * 0.5f, rect.width, thickness),
                MapEdge.Left => new Rect(rect.xMin - thickness * 0.5f, rect.yMin, thickness, rect.height),
                _ => new Rect(rect.xMax - thickness * 0.5f, rect.yMin, thickness, rect.height),
            };
        }
    }

    /// <summary>五种角色各至少一格；多出来的格子在 FillerRoles 里随机。</summary>
    public static readonly MapBlockRole[] RequiredRoles =
    {
        MapBlockRole.Spawn,
        MapBlockRole.Exit,
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
            if (role == MapBlockRole.Exit)
                result[index].ExitEdge = PickOuterEdge(result[index], columns, rows, random);
        }

        // 宝塔随机落在出生格、出口格以外的任意一格之上（出口格中间有一排路牌，别盖住）。
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

    private static MapEdge PickOuterEdge(MapCell cell, int columns, int rows, System.Random random)
    {
        List<MapEdge> edges = new(4);
        if (cell.Row == rows - 1) edges.Add(MapEdge.Top);
        if (cell.Row == 0) edges.Add(MapEdge.Bottom);
        if (cell.Column == 0) edges.Add(MapEdge.Left);
        if (cell.Column == columns - 1) edges.Add(MapEdge.Right);
        return edges[random.Next(edges.Count)];
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

        if (removeSpawnPenFences && tutorialPen.HasFences)
            tutorialPen.RemoveFences();

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

    // ---------------------------------------------------------------- exit

    /// <summary>只让出口边的外围围栏可撞，并在出口格地面铺几枚指向出口的箭头。</summary>
    private void ConfigureExit()
    {
        MapCell exit = ExitCell;
        Rect? span = ExitSpan;
        if (exit == null || !span.HasValue)
            return;

        // 围栏整圈都按 100 只的门槛可撞：出口之外撞出去只会落到公路上（卡车）、再外面是河（空气墙），
        // 真正能离开地图的仍然只有出口那一段。
        if (lockFencesOutsideExit)
            borderRing?.SetBreakableSpan(span.Value);

        Vector2 direction = EdgeDirection(exit.ExitEdge.Value);
        Vector2 edgeCenter = EdgeCenter(exit.Rect, exit.ExitEdge.Value);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // 占位箭头朝 +X；美术路牌箭头朝上（+Y），要少转 90°，且不染色、按自己的比例缩放。
        bool signpost = arrowSprite != null;
        Sprite sprite = signpost ? arrowSprite : RuntimeSprites.Arrow();
        float spriteAngle = signpost ? angle - 90f : angle;
        Transform arrows = new GameObject("ExitArrows").transform;
        arrows.SetParent(transform, false);
        for (int index = 0; index < arrowCount; index++)
        {
            // 从格子中心往出口边等距排，最后一枚离围栏 6 单位。
            float t = (index + 1f) / (arrowCount + 1f);
            Vector2 position = Vector2.Lerp(exit.Rect.center, edgeCenter - direction * 6f, t);
            GameObject arrow = new GameObject($"ExitArrow_{index + 1}");
            arrow.transform.SetParent(arrows, false);
            arrow.transform.position = position;
            arrow.transform.rotation = Quaternion.Euler(0f, 0f, spriteAngle);
            arrow.transform.localScale = Vector3.one * (signpost ? arrowScale : 2.5f);
            SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = signpost ? Color.white : new Color(1f, 0.95f, 0.6f, 0.85f);
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = -40;
        }
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
    /// 围栏外侧：先一圈公路（可走，触发卡车），再一圈河流（空气墙）。
    /// 出口格贴外圈的那一段两者都不铺，是唯一能冲出去的缺口。
    /// </summary>
    private void BuildOuterRing()
    {
        ringRoot = new GameObject("OuterRing").transform;
        ringRoot.SetParent(transform, false);

        Rect world = WorldRect;
        MapCell exit = ExitCell;
        MapEdge? exitEdge = exit != null ? exit.ExitEdge : null;
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

            // 出口那一段（出口格的整条边）留空。
            float gapFrom = float.NaN;
            float gapTo = float.NaN;
            if (exitEdge.HasValue && exitEdge.Value == edge)
            {
                gapFrom = horizontal ? exit.Rect.xMin : exit.Rect.yMin;
                gapTo = horizontal ? exit.Rect.xMax : exit.Rect.yMax;
            }

            if (!float.IsNaN(gapFrom))
                BuildExitApron(edge, horizontal, edgeLine, outward[horizontal ? 1 : 0], gapFrom, gapTo);

            float roadCenter = edgeLine + outward[horizontal ? 1 : 0] * roadWidth * 0.5f;
            float riverCenter = edgeLine + outward[horizontal ? 1 : 0] * (roadWidth + riverWidth * 0.5f);
            foreach ((float a, float b) in SplitAround(from, to, gapFrom, gapTo))
            {
                BuildRoad(edge, horizontal, roadCenter, a, b);
                BuildRiver(edge, horizontal, riverCenter, a, b);
            }
        }
    }

    /// <summary>出口缺口外贴一块草地：沿边覆盖整个缺口，往外铺 exitApronDepth。</summary>
    private void BuildExitApron(MapEdge edge, bool horizontal, float edgeLine, float sign, float from, float to)
    {
        float depth = Mathf.Max(roadWidth + riverWidth, exitApronDepth);
        float center = edgeLine + sign * depth * 0.5f;
        (Vector2 position, float length, float rotation) = Strip(horizontal, center, from, to);
        GameObject apron = new GameObject($"ExitApron_{edge}");
        apron.transform.SetParent(ringRoot, false);
        apron.transform.position = position;
        apron.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        SpriteRenderer renderer = apron.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = -100;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(depth, length);
        if (grassSprite != null)
        {
            renderer.sprite = grassSprite;
        }
        else
        {
            renderer.sprite = RuntimeSprites.Solid();
            renderer.color = new Color(0.28f, 0.46f, 0.25f, 1f);
        }
    }

    private static IEnumerable<(float, float)> SplitAround(float from, float to, float gapFrom, float gapTo)
    {
        if (float.IsNaN(gapFrom))
        {
            yield return (from, to);
            yield break;
        }

        if (gapFrom - from > 0.5f)
            yield return (from, gapFrom);
        if (to - gapTo > 0.5f)
            yield return (gapTo, to);
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
            if (cell.ExitEdge.HasValue)
                Gizmos.DrawWireCube(EdgeCenter(cell.Rect, cell.ExitEdge.Value), Vector3.one * 3f);
#if UNITY_EDITOR
            string label = cell.Definition != null ? $"{cell.Role} · {cell.Definition.DisplayName}" : $"({cell.Column},{cell.Row})";
            if (cell.ExitEdge.HasValue)
                label += $" → {cell.ExitEdge.Value}";
            if (cell.HasPagoda)
                label += " + 宝塔";
            UnityEditor.Handles.Label(cell.Rect.center, label);
#endif
        }
    }

    private static Vector2 EdgeCenter(Rect rect, MapEdge edge)
    {
        return edge switch
        {
            MapEdge.Top => new Vector2(rect.center.x, rect.yMax),
            MapEdge.Bottom => new Vector2(rect.center.x, rect.yMin),
            MapEdge.Left => new Vector2(rect.xMin, rect.center.y),
            _ => new Vector2(rect.xMax, rect.center.y),
        };
    }

    private static Color RoleColor(MapBlockRole? role)
    {
        return role switch
        {
            MapBlockRole.Spawn => Color.cyan,
            MapBlockRole.Exit => Color.red,
            MapBlockRole.Forest => new Color(0.1f, 0.6f, 0.1f),
            MapBlockRole.Plains => Color.yellow,
            MapBlockRole.Village => new Color(1f, 0.6f, 0.2f),
            MapBlockRole.Lake => Color.blue,
            _ => Color.white,
        };
    }
}

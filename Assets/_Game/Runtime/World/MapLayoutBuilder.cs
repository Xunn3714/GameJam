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
    public MapBlockRole Role => Definition != null ? Definition.Role : MapBlockRole.Plains;
}

/// <summary>
/// 把外围围栏内的地图切成 columns×rows 个等大格子，按世界种子给每格分配区块：
/// 1 格出生点、1 格宝塔、1 格出口，其余从池里按权重抽（可重复，保底村庄）。
/// 在 Awake 里（早于所有 Start）把羊圈、羊群、初始羊和相机整体挪到出生格，
/// 并同步散布物 / 地标的羊圈排除区，后面的系统不需要知道地图变过。
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
    [SerializeField] private MapBlockDefinition[] blockPool = Array.Empty<MapBlockDefinition>();
    [Tooltip("随机槽位至少保证多少个村庄格；池里没有村庄定义时忽略。")]
    [SerializeField, Min(0)] private int minimumVillageCount = 1;

    [Header("Spawn Pen Exclusion")]
    [Tooltip("与 Setup 里给散布物 / 地标预留的羊圈外扩距离保持一致。")]
    [SerializeField, Min(0f)] private float debrisPenPadding = 4f;
    [SerializeField, Min(0f)] private float landmarkPenPadding = 5f;

    [Header("Exit")]
    [Tooltip("出口格地面上的指向标识；留空则用运行时生成的三角形。")]
    [SerializeField] private Sprite arrowSprite;
    [SerializeField, Min(0)] private int arrowCount = 3;

    [Header("Seams (river / road)")]
    [SerializeField, Min(0)] private int riverCount = 2;
    [SerializeField, Min(0)] private int roadCount = 2;
    [Tooltip("河流贴图（草地 + 河一体的方形图，河道竖直贯穿）。")]
    [SerializeField] private Sprite riverSprite;
    [Tooltip("河道在贴图中的中心位置（0~1，从左数）和宽度占比，用来把河道对齐到格子交界线。")]
    [SerializeField, Range(0f, 1f)] private float riverCenterInTile = 0.38f;
    [SerializeField, Range(0.05f, 1f)] private float riverWidthInTile = 0.42f;
    [Tooltip("空气墙宽度（世界单位）。")]
    [SerializeField, Min(0.5f)] private float riverWallWidth = 5f;
    [Tooltip("浅滩缺口占交界线长度的比例。")]
    [SerializeField, Range(0f, 0.9f)] private float fordFraction = 0.35f;
    [SerializeField, Min(0.5f)] private float roadWidth = 6f;
    [SerializeField] private Sprite truckSprite;
    [SerializeField, Min(0.1f)] private float truckSpeed = 16f;
    [SerializeField, Min(0f)] private float truckCooldown = 8f;

    private MapCell[] cells = Array.Empty<MapCell>();
    private Transform seamRoot;

    public IReadOnlyList<MapCell> Cells => cells;
    public Rect WorldRect => borderRing != null ? borderRing.WorldRect : worldRect;
    public MapCell SpawnCell => FindCell(MapBlockRole.Spawn);
    public MapCell PagodaCell => FindCell(MapBlockRole.Pagoda);
    public MapCell ExitCell => FindCell(MapBlockRole.Exit);

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
        cells = Assign(random, columns, rows, WorldRect, blockPool, minimumVillageCount);

        foreach (MapBlockRole role in new[] { MapBlockRole.Spawn, MapBlockRole.Pagoda, MapBlockRole.Exit })
        {
            if (FindCell(role) == null)
                Debug.LogWarning($"区块池里没有 {role} 定义，该格保持空白。", this);
        }

        RelocateSpawn();
        ConfigureExit();
        BuildSeams(random);
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

    /// <summary>纯逻辑的分配过程，供 EditMode 测试直接调用。</summary>
    public static MapCell[] Assign(
        System.Random random,
        int columns,
        int rows,
        Rect world,
        IReadOnlyList<MapBlockDefinition> pool,
        int minimumVillages)
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

        List<int> free = new();
        for (int index = 0; index < result.Length; index++)
            free.Add(index);

        // 固定角色：随机各占一格。
        MapCell spawn = TakeCell(result, free, random);
        MapCell pagoda = TakeCell(result, free, random);
        MapCell exit = TakeCell(result, free, random);
        if (spawn != null) spawn.Definition = FindByRole(pool, MapBlockRole.Spawn);
        if (pagoda != null) pagoda.Definition = FindByRole(pool, MapBlockRole.Pagoda);
        if (exit != null)
        {
            exit.Definition = FindByRole(pool, MapBlockRole.Exit);
            exit.ExitEdge = PickOuterEdge(exit, columns, rows, random);
        }

        // 其余槽位按权重抽，可重复。
        List<MapBlockDefinition> randomPool = new();
        foreach (MapBlockDefinition definition in pool)
        {
            if (definition != null && !definition.IsFixedRole && definition.Weight > 0f)
                randomPool.Add(definition);
        }

        List<MapCell> randomCells = new();
        foreach (int index in free)
        {
            result[index].Definition = PickWeighted(randomPool, random);
            randomCells.Add(result[index]);
        }

        // 保底村庄：红箱子集中在村庄格，所以随机槽里至少要有一个。
        List<MapBlockDefinition> villages = randomPool.FindAll(item => item.Role == MapBlockRole.Village);
        int villageCount = randomCells.FindAll(cell => cell.Role == MapBlockRole.Village).Count;
        for (int index = randomCells.Count - 1; index >= 0 && villageCount < minimumVillages && villages.Count > 0; index--)
        {
            if (randomCells[index].Role == MapBlockRole.Village)
                continue;

            randomCells[index].Definition = PickWeighted(villages, random);
            villageCount++;
        }

        return result;
    }

    private static MapCell TakeCell(MapCell[] cells, List<int> free, System.Random random)
    {
        if (free.Count == 0)
            return null;

        int slot = random.Next(free.Count);
        MapCell cell = cells[free[slot]];
        free.RemoveAt(slot);
        return cell;
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
        if (target != null)
            target.position += (Vector3)delta;
    }

    // ---------------------------------------------------------------- exit

    /// <summary>只让出口边的外围围栏可撞，并在出口格地面铺几枚指向出口的箭头。</summary>
    private void ConfigureExit()
    {
        MapCell exit = ExitCell;
        Rect? span = ExitSpan;
        if (exit == null || !span.HasValue)
            return;

        borderRing?.SetBreakableSpan(span.Value);

        Vector2 direction = EdgeDirection(exit.ExitEdge.Value);
        Vector2 edgeCenter = EdgeCenter(exit.Rect, exit.ExitEdge.Value);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Sprite sprite = arrowSprite != null ? arrowSprite : RuntimeSprites.Arrow();
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
            arrow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            arrow.transform.localScale = Vector3.one * 2.5f;
            SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 0.95f, 0.6f, 0.85f);
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

    // ---------------------------------------------------------------- seams

    private readonly struct Seam
    {
        public Seam(Vector2 start, Vector2 end, bool vertical)
        {
            Start = start;
            End = end;
            Vertical = vertical;
        }

        public Vector2 Start { get; }
        public Vector2 End { get; }
        public bool Vertical { get; }
        public Vector2 Center => (Start + End) * 0.5f;
        public float Length => (End - Start).magnitude;
        /// <summary>竖直交界线的条带不旋转；水平交界线旋转 90°。</summary>
        public float Rotation => Vertical ? 0f : 90f;
    }

    /// <summary>相邻格子的共享边里随机挑几条做河（空气墙 + 浅滩缺口）、几条做路（触发运羊卡车）。</summary>
    private void BuildSeams(System.Random random)
    {
        List<Seam> seams = new();
        foreach (MapCell cell in cells)
        {
            if (cell.Column < columns - 1)
                seams.Add(new Seam(new Vector2(cell.Rect.xMax, cell.Rect.yMin), new Vector2(cell.Rect.xMax, cell.Rect.yMax), true));
            if (cell.Row < rows - 1)
                seams.Add(new Seam(new Vector2(cell.Rect.xMin, cell.Rect.yMax), new Vector2(cell.Rect.xMax, cell.Rect.yMax), false));
        }

        // 洗牌后前几条做河、再几条做路。
        for (int index = seams.Count - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);
            (seams[index], seams[swap]) = (seams[swap], seams[index]);
        }

        seamRoot = new GameObject("MapSeams").transform;
        seamRoot.SetParent(transform, false);
        int rivers = Mathf.Min(riverCount, seams.Count);
        int roads = Mathf.Min(roadCount, seams.Count - rivers);
        for (int index = 0; index < rivers; index++)
            BuildRiver(seams[index], random);
        for (int index = rivers; index < rivers + roads; index++)
            BuildRoad(seams[index]);
    }

    private void BuildRiver(Seam seam, System.Random random)
    {
        float length = seam.Length;
        float gap = length * fordFraction;
        float gapStart = (float)(0.15 + random.NextDouble() * 0.5) * (length - gap);
        Vector2 axis = (seam.End - seam.Start).normalized;

        BuildRiverSegment(seam, seam.Start, seam.Start + axis * gapStart, "River_A");
        BuildRiverSegment(seam, seam.Start + axis * (gapStart + gap), seam.End, "River_B");
    }

    private void BuildRiverSegment(Seam seam, Vector2 from, Vector2 to, string name)
    {
        float length = (to - from).magnitude;
        if (length < 1f)
            return;

        GameObject segment = new GameObject(name);
        segment.transform.SetParent(seamRoot, false);
        segment.transform.position = (from + to) * 0.5f;
        segment.transform.rotation = Quaternion.Euler(0f, 0f, seam.Rotation);
        int blockingLayer = LayerMask.NameToLayer(MovementBlocking.BlockingLayerName);
        if (blockingLayer >= 0)
            segment.layer = blockingLayer;

        // 空气墙：Blocking 层、没有 BreakableObstacle，走路挡、E 冲刺也挡。
        Rigidbody2D body = segment.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        BoxCollider2D wall = segment.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(riverWallWidth, length);

        GameObject visual = new GameObject("RiverSprite");
        visual.transform.SetParent(segment.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = -50;
        if (riverSprite != null)
        {
            // 贴图是"草地 + 河"一体的方图：按河道占比换算出方图边长，再把河道中心平移到交界线上。
            float tile = riverWallWidth / Mathf.Max(0.05f, riverWidthInTile);
            float spriteUnits = riverSprite.rect.width / riverSprite.pixelsPerUnit;
            float scale = tile / Mathf.Max(0.01f, spriteUnits);
            renderer.sprite = riverSprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            // Tiled 的 size 是本地单位：整体缩放到一块方图 = tile 世界单位，再沿交界线平铺。
            visual.transform.localScale = Vector3.one * scale;
            renderer.size = new Vector2(spriteUnits, length / scale);
            visual.transform.localPosition = new Vector3((0.5f - riverCenterInTile) * tile, 0f, 0f);
        }
        else
        {
            renderer.sprite = RuntimeSprites.Solid();
            renderer.color = new Color(0.25f, 0.55f, 0.95f, 0.9f);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(riverWallWidth, length);
        }
    }

    private void BuildRoad(Seam seam)
    {
        GameObject road = new GameObject("Road");
        road.transform.SetParent(seamRoot, false);
        road.transform.position = seam.Center;
        road.transform.rotation = Quaternion.Euler(0f, 0f, seam.Rotation);

        SpriteRenderer renderer = road.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSprites.Road();
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(roadWidth, seam.Length);
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = -45;

        BoxCollider2D trigger = road.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(roadWidth, seam.Length);

        RoadHazard hazard = road.AddComponent<RoadHazard>();
        hazard.Configure(seam.Start, seam.End, roadWidth, truckSprite, truckSpeed, truckCooldown);
    }

    private static Rect Expand(Rect rect, float margin)
    {
        return new Rect(rect.xMin - margin, rect.yMin - margin, rect.width + margin * 2f, rect.height + margin * 2f);
    }

    private void OnDrawGizmos()
    {
        IReadOnlyList<MapCell> drawn = cells.Length > 0
            ? cells
            : Assign(new System.Random(0), columns, rows, WorldRect, Array.Empty<MapBlockDefinition>(), 0);

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
            MapBlockRole.Pagoda => Color.magenta,
            MapBlockRole.Exit => Color.red,
            MapBlockRole.Forest => new Color(0.1f, 0.6f, 0.1f),
            MapBlockRole.Plains => Color.yellow,
            MapBlockRole.Village => new Color(1f, 0.6f, 0.2f),
            MapBlockRole.Lake => Color.blue,
            _ => Color.white,
        };
    }
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// 用 Assets/Art/Debris 里的贴图和 Content/Data/World/Obstacles 的定义生成可破坏物预制体。
/// 菜单：Game Jam / World / Build Obstacle Prefabs。可重复执行（保留 GUID）。
/// </summary>
public static class WorldObstaclePrefabBuilder
{
    public const string PrefabFolder = "Assets/_Game/Content/Perfabs/World";
    public const string DefinitionFolder = "Assets/_Game/Content/Data/World/Obstacles";
    private const string DebrisFolder = "Assets/Art/Debris";
    private const string BuildingFolder = "Assets/Art/Buildings";
    private const string WoodBreakClipPath = "Assets/_Game/Content/Audio/SFX/wood.mp3";
    private const string StoneBreakClipPath = "Assets/_Game/Content/Audio/SFX/stone.mp3";
    private static readonly string[] VegetationBreakClipPaths =
    {
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand1.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand2.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand3.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand4.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand5.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand6.wav"
    };
    private const string VfxFolder = PrefabFolder + "/VFX";

    public const string WoodFragmentPrefabPath = VfxFolder + "/WoodFragment.prefab";
    public const string RockFragmentPrefabPath = VfxFolder + "/RockFragment.prefab";
    public const string GreenFragmentPrefabPath = VfxFolder + "/GreenFragment.prefab";
    public const string CropFragmentPrefabPath = VfxFolder + "/CropFragment.prefab";

    public const string FencePrefabPath = PrefabFolder + "/Obstacle_Fence.prefab";
    public const string BarrelPrefabPath = PrefabFolder + "/Obstacle_Barrel.prefab";
    public const string RockPrefabPath = PrefabFolder + "/Obstacle_Rock.prefab";
    public const string FlowerPrefabPath = PrefabFolder + "/Obstacle_Flower.prefab";

    public const string FenceDefinitionPath = DefinitionFolder + "/obstacle.fence.asset";
    public const string BorderFenceDefinitionPath = DefinitionFolder + "/obstacle.border_fence.asset";
    public const string RedChestDefinitionPath = DefinitionFolder + "/obstacle.red_chest.asset";
    public const string HouseDefinitionPath = DefinitionFolder + "/obstacle.house.asset";
    public const string BigHouseDefinitionPath = DefinitionFolder + "/obstacle.big_house.asset";

    /// <summary>
    /// 美术组提供的散布物（Assets/Art/Debris 下的中文命名 PNG）。
    /// 花草类碰到即消失；有"坏"图的（树、石、木桶、干草垛、稻田）碰到后换成坏图并沉到 Background 层。
    /// </summary>
    public sealed class DebrisSpec
    {
        public string Id;            // obstacle.xxx
        public string DisplayName;
        public string PrefabName;    // Obstacle_Xxx
        public string Sprite;        // Art/Debris 下的文件名（不含 .png）
        public string BrokenSprite;  // 坏图文件名；null = 碰到就消失；"=" = 坏了仍用原图（只沉到背景）
        public ObstacleSizeCategory Size;
        public float Scale;          // prefab 缩放
        public float SolidRadius;    // 实体碰撞半径（世界单位；SolidSize 为零时用圆形碰撞）
        /// <summary>非零时改用矩形实体碰撞（世界单位），用来贴合"底座"而不是把整张图都算进去。</summary>
        public Vector2 SolidSize = Vector2.zero;
        /// <summary>矩形实体碰撞相对 transform 原点的偏移（世界单位）。</summary>
        public Vector2 SolidOffset = Vector2.zero;
        public float Weight;         // 散布权重
        public float Clearance;      // 散布时与其他物体的最小间距
        /// <summary>未破坏时在 Default 层的 Order：花草 / 小石头这类矮的贴地物用 -1，永远在羊下面；树、木桶这类高的用 1。</summary>
        public int SortingOrder = 1;
        public string BreakClipPath;
        public string[] RandomBreakClipPaths;
        public float BreakVolume = 1f;
        public string FragmentPrefabPath;
        public int FragmentCount;
        public Vector2 FragmentSpawnOffset;
        public float FragmentSpawnRadius;

        public bool Disappears => BrokenSprite == null;
        public string PrefabPath => PrefabFolder + "/" + PrefabName + ".prefab";
        public string DefinitionPath => DefinitionFolder + "/" + Id + ".asset";
    }

    public static readonly DebrisSpec[] DebrisSpecs =
    {
        // ---- 花草：碰到直接消失 ----
        new DebrisSpec { Id = "obstacle.flower", DisplayName = "小花", PrefabName = "Obstacle_Flower", Sprite = "小花（1）", BrokenSprite = null, Size = ObstacleSizeCategory.Small, Scale = 0.55f, SolidRadius = 0.35f, Weight = 5f, Clearance = 1.8f, SortingOrder = -1, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = GreenFragmentPrefabPath, FragmentCount = 8 },
        new DebrisSpec { Id = "obstacle.flower_daisy", DisplayName = "雏菊", PrefabName = "Obstacle_FlowerDaisy", Sprite = "小花（2）", BrokenSprite = null, Size = ObstacleSizeCategory.Small, Scale = 0.55f, SolidRadius = 0.35f, Weight = 5f, Clearance = 1.8f, SortingOrder = -1, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = GreenFragmentPrefabPath, FragmentCount = 8 },
        new DebrisSpec { Id = "obstacle.flower_cluster", DisplayName = "花丛", PrefabName = "Obstacle_FlowerCluster", Sprite = "花丛", BrokenSprite = null, Size = ObstacleSizeCategory.Small, Scale = 0.65f, SolidRadius = 0.5f, Weight = 3f, Clearance = 2.2f, SortingOrder = -1, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = GreenFragmentPrefabPath, FragmentCount = 8 },
        new DebrisSpec { Id = "obstacle.grass_1", DisplayName = "小草", PrefabName = "Obstacle_Grass1", Sprite = "小草（1）", BrokenSprite = null, Size = ObstacleSizeCategory.Small, Scale = 0.5f, SolidRadius = 0.3f, Weight = 6f, Clearance = 1.6f, SortingOrder = -1, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = GreenFragmentPrefabPath, FragmentCount = 8 },
        new DebrisSpec { Id = "obstacle.grass_2", DisplayName = "小草", PrefabName = "Obstacle_Grass2", Sprite = "小草（2）", BrokenSprite = null, Size = ObstacleSizeCategory.Small, Scale = 0.5f, SolidRadius = 0.3f, Weight = 6f, Clearance = 1.6f, SortingOrder = -1, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = GreenFragmentPrefabPath, FragmentCount = 8 },
        new DebrisSpec { Id = "obstacle.grass_3", DisplayName = "小草", PrefabName = "Obstacle_Grass3", Sprite = "小草（3）", BrokenSprite = null, Size = ObstacleSizeCategory.Small, Scale = 0.5f, SolidRadius = 0.3f, Weight = 6f, Clearance = 1.6f, SortingOrder = -1, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = GreenFragmentPrefabPath, FragmentCount = 8 },
        new DebrisSpec { Id = "obstacle.bush_1", DisplayName = "草丛", PrefabName = "Obstacle_Bush1", Sprite = "小草丛（1）", BrokenSprite = null, Size = ObstacleSizeCategory.Small, Scale = 0.65f, SolidRadius = 0.5f, Weight = 3f, Clearance = 2.4f, SortingOrder = -1, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = GreenFragmentPrefabPath, FragmentCount = 8 },
        new DebrisSpec { Id = "obstacle.bush_2", DisplayName = "草丛", PrefabName = "Obstacle_Bush2", Sprite = "小草丛（2）", BrokenSprite = null, Size = ObstacleSizeCategory.Small, Scale = 0.65f, SolidRadius = 0.5f, Weight = 3f, Clearance = 2.4f, SortingOrder = -1, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = GreenFragmentPrefabPath, FragmentCount = 8 },
        // ---- 有坏图：碰到后换坏图、沉到背景 ----
        new DebrisSpec { Id = "obstacle.barrel", DisplayName = "木桶", PrefabName = "Obstacle_Barrel", Sprite = "木桶", BrokenSprite = "木桶（坏）", Size = ObstacleSizeCategory.Small, Scale = 0.7f, SolidRadius = 0.55f, Weight = 1.5f, Clearance = 3f, BreakClipPath = WoodBreakClipPath, FragmentPrefabPath = WoodFragmentPrefabPath, FragmentCount = 8, FragmentSpawnRadius = 0.15f },
        new DebrisSpec { Id = "obstacle.rock", DisplayName = "石块", PrefabName = "Obstacle_Rock", Sprite = "石块（2）", BrokenSprite = "石块2（坏）", Size = ObstacleSizeCategory.Medium, Scale = 0.75f, SolidRadius = 0.7f, SolidSize = new Vector2(3.3f, 1.5f), SolidOffset = new Vector2(0.05f, -0.7f), Weight = 1.5f, Clearance = 3.2f, BreakClipPath = StoneBreakClipPath, BreakVolume = 1f, FragmentPrefabPath = RockFragmentPrefabPath, FragmentCount = 12, FragmentSpawnRadius = 0.5f },
        new DebrisSpec { Id = "obstacle.pebble", DisplayName = "小石头", PrefabName = "Obstacle_Pebble", Sprite = "小石头（1）", BrokenSprite = "=", Size = ObstacleSizeCategory.Small, Scale = 0.5f, SolidRadius = 0.35f, Weight = 2f, Clearance = 2f, SortingOrder = -1 },
        new DebrisSpec { Id = "obstacle.haystack", DisplayName = "干草垛", PrefabName = "Obstacle_Haystack", Sprite = "干草垛", BrokenSprite = "干草垛（坏）", Size = ObstacleSizeCategory.Medium, Scale = 0.8f, SolidRadius = 0.7f, Weight = 1f, Clearance = 3.5f, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = CropFragmentPrefabPath, FragmentCount = 8 },
        new DebrisSpec { Id = "obstacle.rice_field", DisplayName = "稻田", PrefabName = "Obstacle_RiceField", Sprite = "稻田", BrokenSprite = "稻田（坏）", Size = ObstacleSizeCategory.Medium, Scale = 0.8f, SolidRadius = 0.75f, Weight = 1f, Clearance = 3.5f, RandomBreakClipPaths = VegetationBreakClipPaths, FragmentPrefabPath = CropFragmentPrefabPath, FragmentCount = 8 },
        new DebrisSpec { Id = "obstacle.tree", DisplayName = "大树", PrefabName = "Obstacle_Tree", Sprite = "大树（完整）", BrokenSprite = "大树（断）", Size = ObstacleSizeCategory.Large, Scale = 1.1f, SolidRadius = 0.55f, Weight = 2.5f, Clearance = 4.5f, BreakClipPath = WoodBreakClipPath, FragmentPrefabPath = WoodFragmentPrefabPath, FragmentCount = 8, FragmentSpawnOffset = new Vector2(0f, -0.5f), FragmentSpawnRadius = 0.2f },
        // ---- 新美术（2304x1728 画布，完好 / 坏同画布）----
        new DebrisSpec { Id = "obstacle.tree_2", DisplayName = "大树 II", PrefabName = "Obstacle_Tree2", Sprite = "大树2", BrokenSprite = "大树2（坏）", Size = ObstacleSizeCategory.Large, Scale = 0.35f, SolidRadius = 0.7f, Weight = 2.5f, Clearance = 6f, BreakClipPath = WoodBreakClipPath, FragmentPrefabPath = WoodFragmentPrefabPath, FragmentCount = 10, FragmentSpawnOffset = new Vector2(0f, -1.2f), FragmentSpawnRadius = 0.3f },
        new DebrisSpec { Id = "obstacle.tractor", DisplayName = "拖拉机", PrefabName = "Obstacle_Tractor", Sprite = "拖拉机", BrokenSprite = "拖拉机（坏）", Size = ObstacleSizeCategory.Medium, Scale = 0.6f, SolidRadius = 0.7f, Weight = 0f, Clearance = 2.5f, BreakClipPath = StoneBreakClipPath, FragmentPrefabPath = RockFragmentPrefabPath, FragmentCount = 8, FragmentSpawnRadius = 0.3f },
    };

    public const string Tree2PrefabPath = PrefabFolder + "/Obstacle_Tree2.prefab";
    public const string TractorPrefabPath = PrefabFolder + "/Obstacle_Tractor.prefab";

    [MenuItem("Game Jam/World/Build Obstacle Prefabs")]
    public static void BuildAll()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(DefinitionFolder);

        Sprite fence = LoadSprite("obstacle_fence_256x128");
        Sprite fenceBroken = LoadSprite("obstacle_fence_broken_256x128");
        Sprite redChestBroken = LoadBuildingSprite("红箱子-坏");
        Sprite houseBroken = LoadBuildingSprite("房子--坏");
        Sprite bigHouseBroken = LoadBuildingSprite("大房子-坏", optional: true);

        ObstacleDefinition fenceDefinition = GetOrCreateDefinition(
            FenceDefinitionPath, "obstacle.fence", "围栏", ObstacleSizeCategory.Medium,
            ObstacleBreakRule.RequireCountAndInteract, 6, ObstacleCountSource.CurrentFlockCount,
            ObstacleBrokenBehavior.BecomeBackground, fenceBroken);
        GetOrCreateDefinition(
            BorderFenceDefinitionPath, "obstacle.border_fence", "外围围栏", ObstacleSizeCategory.Large,
            ObstacleBreakRule.RequireCountAndInteract, 100, ObstacleCountSource.CurrentFlockCount,
            ObstacleBrokenBehavior.BecomeBackground, fenceBroken);
        GetOrCreateDefinition(
            RedChestDefinitionPath, "obstacle.red_chest", "红色箱子", ObstacleSizeCategory.Medium,
            ObstacleBreakRule.RequireCountAndInteract, 15, ObstacleCountSource.CurrentFlockCount,
            ObstacleBrokenBehavior.BecomeBackground, redChestBroken);
        GetOrCreateDefinition(
            HouseDefinitionPath, "obstacle.house", "房子", ObstacleSizeCategory.Large,
            ObstacleBreakRule.RequireCountAndInteract, 50, ObstacleCountSource.CurrentFlockCount,
            ObstacleBrokenBehavior.BecomeBackground, houseBroken);
        if (bigHouseBroken != null)
        {
            GetOrCreateDefinition(
                BigHouseDefinitionPath, "obstacle.big_house", "大房子", ObstacleSizeCategory.Large,
                ObstacleBreakRule.RequireCountAndInteract, 50, ObstacleCountSource.CurrentFlockCount,
                ObstacleBrokenBehavior.BecomeBackground, bigHouseBroken);
        }

        BuildFencePrefab(
            FencePrefabPath,
            fence,
            fenceDefinition,
            LoadGameObjectPrefab(WoodFragmentPrefabPath),
            8,
            Vector2.zero,
            0f,
            LoadAudioClip(WoodBreakClipPath),
            1f);

        int built = 0;
        foreach (DebrisSpec spec in DebrisSpecs)
        {
            Sprite sprite = LoadSprite(spec.Sprite);
            if (sprite == null)
                continue;

            Sprite broken = spec.BrokenSprite == null
                ? null
                : spec.BrokenSprite == "=" ? sprite : LoadSprite(spec.BrokenSprite);

            ObstacleDefinition definition = GetOrCreateDefinition(
                spec.DefinitionPath, spec.Id, spec.DisplayName, spec.Size,
            spec.Id == "obstacle.rock" ? ObstacleBreakRule.RequireCountAndInteract : ObstacleBreakRule.OnAnyContact,
            spec.Id == "obstacle.rock" ? 20 : 1, ObstacleCountSource.CurrentFlockCount,
            spec.Id == "obstacle.rock" || spec.Disappears
                ? ObstacleBrokenBehavior.Disappear
                : ObstacleBrokenBehavior.BecomeBackground,
            broken,
            spec.Id == "obstacle.rock" ? 2 : 1);

            BuildDebrisPrefab(
                spec.PrefabPath,
                spec.PrefabName,
                sprite,
                definition,
                spec.SolidRadius,
                spec.SolidSize,
                spec.SolidOffset,
                spec.Scale,
                spec.SortingOrder,
                LoadGameObjectPrefab(spec.FragmentPrefabPath),
                spec.FragmentCount,
                spec.FragmentSpawnOffset,
                spec.FragmentSpawnRadius,
                LoadAudioClip(spec.BreakClipPath),
                LoadAudioClips(spec.RandomBreakClipPaths),
                spec.BreakVolume);
            built++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Obstacle prefabs built under {PrefabFolder}: fence + {built} debris; definitions under {DefinitionFolder}.");
    }

    /// <summary>
    /// 把 DebrisSpecs 里的散布物写进当前打开场景的 WorldDebrisSpawner（权重 / 间距 / 密度），不重建整个场景。
    /// </summary>
    [MenuItem("Game Jam/World/Apply Debris Set To Open Scene")]
    public static void ApplyDebrisSetToOpenScene()
    {
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        WorldDebrisSpawner spawner = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            spawner = root.GetComponentInChildren<WorldDebrisSpawner>(true);
            if (spawner != null)
                break;
        }

        if (spawner == null)
        {
            Debug.LogWarning("Open scene has no WorldDebrisSpawner.");
            return;
        }

        SerializedObject serialized = new SerializedObject(spawner);
        ApplyDebrisEntries(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log($"Applied {DebrisSpecs.Length} debris entries to {spawner.name} in {scene.path}.");
    }

    /// <summary>
    /// 开发用：往当前打开的场景（例如 Scenes/Test 下的狼测试场景）加一个 WorldDebrisSpawner，
    /// 在原点周围 60×40 的范围随机散布，方便测试踩花草 / 撞木桶的表现。可重复执行。
    /// </summary>
    [MenuItem("Game Jam/World/Add Debris Spawner To Open Scene (Dev)")]
    public static void AddDebrisSpawnerToOpenScene()
    {
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        WorldDebrisSpawner spawner = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            spawner = root.GetComponentInChildren<WorldDebrisSpawner>(true);
            if (spawner != null)
                break;
        }

        if (spawner == null)
        {
            GameObject spawnerObject = new GameObject("WorldDebrisSpawner");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(spawnerObject, scene);
            spawner = spawnerObject.AddComponent<WorldDebrisSpawner>();
        }

        SerializedObject serialized = new SerializedObject(spawner);
        ApplyDebrisEntries(serialized);
        serialized.FindProperty("area").rectValue = new Rect(-30f, -20f, 60f, 40f);
        serialized.FindProperty("borderPadding").floatValue = 1f;
        serialized.FindProperty("densityPer100SquareUnits").floatValue = 1.2f;
        serialized.FindProperty("maximumCount").intValue = 40;
        // 羊群出生点周围留空。
        SerializedProperty zones = serialized.FindProperty("exclusionZones");
        zones.arraySize = 1;
        zones.GetArrayElementAtIndex(0).rectValue = new Rect(-4f, -4f, 8f, 8f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = spawner.gameObject;
        Debug.Log($"Debris spawner ready in {scene.path}: 60x40 area around the origin, {DebrisSpecs.Length} entries.");
    }

    /// <summary>给 WorldDebrisSpawner 的 SerializedObject 填入 DebrisSpecs 与推荐的密度参数。</summary>
    public static void ApplyDebrisEntries(SerializedObject spawnerSerialized)
    {
        SerializedProperty entries = spawnerSerialized.FindProperty("entries");
        entries.arraySize = 0;
        foreach (DebrisSpec spec in DebrisSpecs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Debris prefab missing: {spec.PrefabPath} (run Build Obstacle Prefabs first).");
                continue;
            }

            entries.arraySize++;
            SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("weight").floatValue = spec.Weight;
            entry.FindPropertyRelative("clearance").floatValue = spec.Clearance;
        }

        // 新素材体积比原来的 64px 小花大得多，密度相应降低；再加一个全局最小间距兜底。
        spawnerSerialized.FindProperty("densityPer100SquareUnits").floatValue = 0.8f;
        spawnerSerialized.FindProperty("maximumCount").intValue = 300;
        SerializedProperty spacing = spawnerSerialized.FindProperty("minimumSpacing");
        if (spacing != null)
            spacing.floatValue = 2.2f;
    }

    /// <summary>围栏：Blocking 层实体碰撞体挡路 + 稍大的 Trigger 供羊群交互。</summary>
    private static void BuildFencePrefab(
        string path,
        Sprite sprite,
        ObstacleDefinition definition,
        GameObject fragmentPrefab,
        int fragmentCount,
        Vector2 fragmentSpawnOffset,
        float fragmentSpawnRadius,
        AudioClip breakClip,
        float breakVolume)
    {
        GameObject root = new GameObject("Obstacle_Fence");
        try
        {
            int blockingLayer = LayerMask.NameToLayer(MovementBlocking.BlockingLayerName);
            if (blockingLayer >= 0)
                root.layer = blockingLayer;
            else
                Debug.LogWarning("Layer 'Blocking' does not exist; fences will not block movement.");

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 5;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            BoxCollider2D solid = root.AddComponent<BoxCollider2D>();
            solid.isTrigger = false;
            solid.size = new Vector2(2f, 0.7f);
            solid.offset = new Vector2(0f, -0.1f);

            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(2.6f, 1.8f);

            BreakableObstacle breakable = root.AddComponent<BreakableObstacle>();
            SerializedObject breakableSerialized = new SerializedObject(breakable);
            breakableSerialized.FindProperty("definition").objectReferenceValue = definition;
            breakableSerialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            breakableSerialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureBreakVfx(
                root,
                fragmentPrefab,
                fragmentCount,
                fragmentSpawnOffset,
                fragmentSpawnRadius);

            FenceObstacle fence = root.AddComponent<FenceObstacle>();
            SerializedObject fenceSerialized = new SerializedObject(fence);
            fenceSerialized.FindProperty("breakable").objectReferenceValue = breakable;
            // 羊圈 / 外围围栏不靠普通接触破坏，只响应 E 整群冲刺。
            SerializedProperty breakOnContact = fenceSerialized.FindProperty("breakOnContact");
            if (breakOnContact != null)
                breakOnContact.boolValue = false;
            fenceSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (breakClip != null)
            {
                BreakableObstacleAudio audio = root.AddComponent<BreakableObstacleAudio>();
                audio.Configure(breakClip, breakVolume);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// 花草 / 石头 / 木桶 / 树……：碰到之前是 Blocking 层的实体碰撞体（羊会被挡一下），
    /// 稍大的 Trigger 侦测到羊群成员后立刻 Break——花草消失，其余换坏图沉到 Background。
    /// </summary>
    private static void BuildDebrisPrefab(
        string path,
        string name,
        Sprite sprite,
        ObstacleDefinition definition,
        float solidRadius,
        Vector2 solidSize,
        Vector2 solidOffset,
        float scale,
        int sortingOrder,
        GameObject fragmentPrefab,
        int fragmentCount,
        Vector2 fragmentSpawnOffset,
        float fragmentSpawnRadius,
        AudioClip breakClip,
        AudioClip[] randomBreakClips,
        float breakVolume)
    {
        GameObject root = new GameObject(name);
        try
        {
            int blockingLayer = LayerMask.NameToLayer(MovementBlocking.BlockingLayerName);
            if (blockingLayer >= 0)
                root.layer = blockingLayer;

            root.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            // 未破坏时在 Default 层：花草 / 小石头 -1（永远在羊下面），树 / 木桶等 1（在羊上面）；破坏后由 ObstacleDefinition 决定沉到 Background。
            renderer.sortingOrder = sortingOrder;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            // 碰撞体都按缩放前的本地单位给，世界尺寸 = 本地值 * scale。
            float inverseScale = 1f / Mathf.Max(0.01f, scale);
            if (solidSize.x > 0.001f && solidSize.y > 0.001f)
            {
                // 高个子的散布物（例如石块）用矩形贴合底座：图整整 5 个世界单位高，
                // 但只有最下面那截是"挡路"的，上半截让羊从背后绕过去才自然。
                BoxCollider2D solidBox = root.AddComponent<BoxCollider2D>();
                solidBox.isTrigger = false;
                solidBox.size = solidSize * inverseScale;
                solidBox.offset = solidOffset * inverseScale;

                BoxCollider2D triggerBox = root.AddComponent<BoxCollider2D>();
                triggerBox.isTrigger = true;
                triggerBox.size = (solidSize + Vector2.one * 0.45f) * inverseScale;
                triggerBox.offset = solidBox.offset;
            }
            else
            {
                float localSolid = solidRadius * inverseScale;
                CircleCollider2D solid = root.AddComponent<CircleCollider2D>();
                solid.isTrigger = false;
                solid.radius = localSolid;

                CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = localSolid + 0.45f * inverseScale;
            }

            BreakableObstacle breakable = root.AddComponent<BreakableObstacle>();
            SerializedObject serialized = new SerializedObject(breakable);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureBreakVfx(
                root,
                fragmentPrefab,
                fragmentCount,
                fragmentSpawnOffset,
                fragmentSpawnRadius);

            if (breakClip != null || randomBreakClips.Length > 0)
            {
                BreakableObstacleAudio audio = root.AddComponent<BreakableObstacleAudio>();
                if (randomBreakClips.Length > 0)
                    audio.ConfigureRandom(randomBreakClips, breakVolume);
                else
                    audio.Configure(breakClip, breakVolume);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureBreakVfx(
        GameObject root,
        GameObject fragmentPrefab,
        int fragmentCount,
        Vector2 fragmentSpawnOffset,
        float fragmentSpawnRadius)
    {
        BreakableObstacleVFX vfx = root.GetComponent<BreakableObstacleVFX>();
        if (vfx == null)
            vfx = root.AddComponent<BreakableObstacleVFX>();

        vfx.Configure(
            fragmentPrefab,
            fragmentCount,
            fragmentSpawnOffset,
            fragmentSpawnRadius);
    }

    private static ObstacleDefinition GetOrCreateDefinition(
        string path,
        string id,
        string displayName,
        ObstacleSizeCategory size,
        ObstacleBreakRule rule,
        int requiredCount,
        ObstacleCountSource countSource,
        ObstacleBrokenBehavior broken,
        Sprite brokenSprite,
        int requiredDashHits = 1,
        bool createOnly = false)
    {
        ObstacleDefinition existing = AssetDatabase.LoadAssetAtPath<ObstacleDefinition>(path);
        if (existing != null && createOnly)
            return existing;

        ObstacleDefinition definition = existing;
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<ObstacleDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        SerializedObject serialized = new SerializedObject(definition);
        serialized.FindProperty("obstacleId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("sizeCategory").enumValueIndex = (int)size;
        serialized.FindProperty("breakRule").enumValueIndex = (int)rule;
        serialized.FindProperty("requiredFlockCount").intValue = requiredCount;
        serialized.FindProperty("requiredDashHits").intValue = Mathf.Max(1, requiredDashHits);
        serialized.FindProperty("countSource").enumValueIndex = (int)countSource;
        serialized.FindProperty("brokenBehavior").enumValueIndex = (int)broken;
        // 花草被踩扁 + 淡出的时长；换坏图的类型是瞬间切换，这个值不再使用。
        serialized.FindProperty("breakAnimationDuration").floatValue =
            broken == ObstacleBrokenBehavior.Disappear ? 0.6f : 0.4f;
        serialized.FindProperty("brokenSortingLayer").stringValue = "Background";
        serialized.FindProperty("brokenSortingOrder").intValue = 10;
        serialized.FindProperty("brokenSprite").objectReferenceValue = brokenSprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static Sprite LoadSprite(string fileName)
    {
        return LoadSpriteAt($"{DebrisFolder}/{fileName}.png", optional: false);
    }

    public static Sprite LoadBuildingSprite(string fileName, bool optional = false)
    {
        return LoadSpriteAt($"{BuildingFolder}/{fileName}.png", optional);
    }

    /// <summary>
    /// 统一按 Sprite / Single / 128 PPU 导入。spriteImportMode 也要检查：美术给的图被自动切成 Multiple 时，
    /// LoadAssetAtPath&lt;Sprite&gt; 只会拿到第一个碎片（例如"房子--坏_0"是 27x25 的一块木屑），破坏后的图就完全不对。
    /// </summary>
    public static Sprite LoadSpriteAt(string path, bool optional)
    {
        if (System.IO.File.Exists(path) == false)
        {
            if (!optional)
                Debug.LogWarning($"Sprite not found: {path}");
            return null;
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null
            && (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.spritePixelsPerUnit != 128f))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null && !optional)
            Debug.LogWarning($"Sprite not found: {path}");
        return sprite;
    }

    private static AudioClip LoadAudioClip(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
            Debug.LogWarning($"Audio clip not found: {path}");
        return clip;
    }

    private static AudioClip[] LoadAudioClips(string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return new AudioClip[0];

        AudioClip[] clips = new AudioClip[paths.Length];
        for (int index = 0; index < paths.Length; index++)
            clips[index] = LoadAudioClip(paths[index]);
        return clips;
    }

    private static GameObject LoadGameObjectPrefab(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            Debug.LogWarning($"Prefab not found: {path}");
        return prefab;
    }

    public static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}

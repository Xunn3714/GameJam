using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the primary AlphaFlockExpansion gameplay scene without modifying the legacy Level_01.
/// 240x140 的草原、外圈围栏、出生羊圈（6 只教程羊 + 地面教程标识）、种子驱动的可破坏物、狼群节奏与 HUD。
/// 可重复执行：只重建由它管理的对象。
/// </summary>
public static class AlphaFlockExpansionSceneSetup
{
    private const string SceneFolder = "Assets/_Game/Scenes";
    private const string ScenePath = SceneFolder + "/AlphaFlockExpansion.unity";
    private const string SceneTemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    private const string GrassBackgroundPath = "Assets/Art/WorldSprites/Tiles/草原_背景.png";
    private const string WarningRectAssetPath = "Assets/_Game/Content/Art/Prototype/WolfWarningRect.asset";
    private const string SheepMemberPrefabPath = "Assets/_Game/Content/Perfabs/Sheep/SheepMember.prefab";
    private const string RecruitableSheepPrefabPath = "Assets/_Game/Content/Perfabs/Sheep/RecruitableSheep.prefab";
    private const string WolfPrefabPath = "Assets/_Game/Content/Perfabs/Wolf/Wolf.prefab";
    private const string NamePoolPath = "Assets/_Game/Content/Data/SheepNamePool.asset";
    private const string BannerPrefabPath = "Assets/_Game/Content/Perfabs/UI/BannerSystem.prefab";
    private const string CollectionPanelPrefabPath = "Assets/_Game/Content/Perfabs/UI/CollectionPanel.prefab";
    private const string PauseSystemPrefabPath = "Assets/_Game/Content/Perfabs/UI/PauseSystem.prefab";
    private const string ResultPanelPrefabPath = "Assets/_Game/Content/Perfabs/UI/ResultPanel.prefab";
    private const string TaskSystemPrefabPath = "Assets/_Game/Content/Perfabs/UI/TaskSystem.prefab";
    private const string PoopPrefabPath = "Assets/_Game/Content/Perfabs/SheepMvp/Poop.prefab";
    private const string PoopActionPath = "Assets/_Game/Content/Data/SheepMvp/Poop.asset";
    private const string GameplayBgmPath = "Assets/_Game/Content/Audio/BGM/sheep-coming.wav";
    private const string WolfSpawnClipPath = "Assets/_Game/Content/Audio/SFX/woof/woof.wav";
    private const string WolfAttack1ClipPath = "Assets/_Game/Content/Audio/SFX/woof/attact1.wav";
    private const string WolfAttack2ClipPath = "Assets/_Game/Content/Audio/SFX/woof/attack2.wav";
    private const string WolfAttack3ClipPath = "Assets/_Game/Content/Audio/SFX/woof/attack3.wav";
    private const string WolfCaptureClipPath = "Assets/_Game/Content/Audio/SFX/sheep/sheep (8).wav";

    private static readonly string[] SheepRecruitClipPaths =
    {
        "Assets/_Game/Content/Audio/SFX/sheep/sheep (1).wav",
        "Assets/_Game/Content/Audio/SFX/sheep/sheep (2).wav",
        "Assets/_Game/Content/Audio/SFX/sheep/sheep (3).wav",
        "Assets/_Game/Content/Audio/SFX/sheep/sheep (4).wav",
        "Assets/_Game/Content/Audio/SFX/sheep/sheep (5).wav",
        "Assets/_Game/Content/Audio/SFX/sheep/sheep (7).wav",
        "Assets/_Game/Content/Audio/SFX/sheep/sheep (9).wav"
    };

    private static readonly string[] GrassFootstepPaths =
    {
        "Assets/_Game/Content/Audio/SFX/footstep/grass/Grass1.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/grass/Grass2.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/grass/Grass3.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/grass/Grass4.wav"
    };

    private static readonly string[] SandFootstepPaths =
    {
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand1.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand2.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand3.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand4.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand5.wav",
        "Assets/_Game/Content/Audio/SFX/footstep/sand/Sand6.wav"
    };

    // 地图与羊圈尺寸（世界单位）。
    private static readonly Rect WorldRect = new Rect(-120f, -70f, 240f, 140f);
    private static readonly Rect PenRect = new Rect(-9f, -5f, 18f, 10f);
    private const float BorderFenceScale = 2f;   // 外围围栏 4x2 单位
    private const float PenFenceScale = 1f;      // 羊圈栅栏 2x1 单位
    private const int ExitUnlockFlockSize = 100;
    private const float EscapeBoundsExpansion = 10f;
    private const int TutorialRequiredFlockSize = 6;
    private const int WolfUnlockFlockSize = 20;

    // 区块网格：3×2 共 6 格，每格 80×70；区块定义资产由 Setup 生成到这个目录。
    private const string BlockDefinitionFolder = "Assets/_Game/Content/Data/World/Blocks";
    private const int MapColumns = 3;
    private const int MapRows = 2;

    /// <summary>散布物条目：DebrisSpecs 里的 Id + 本格权重（间距沿用 spec）。</summary>
    private readonly struct DebrisPick
    {
        public DebrisPick(string id, float weight, float clearance = -1f) { Id = id; Weight = weight; Clearance = clearance; }
        public string Id { get; }
        public float Weight { get; }
        /// <summary>小于 0 时沿用 DebrisSpec 的间距；森林里把树的间距压小才能长得密。</summary>
        public float Clearance { get; }
    }

    private readonly struct BlockSpec
    {
        public BlockSpec(
            string file, string displayName, MapBlockRole role, float weight,
            float density, DebrisPick[] debris,
            int houseMin = 0, int houseMax = 0, int chests = 0, int tractorMin = 0, int tractorMax = 0, int farms = 0, bool bigHouse = false)
        {
            BigHouse = bigHouse;
            File = file;
            DisplayName = displayName;
            Role = role;
            Weight = weight;
            Density = density;
            Debris = debris;
            HouseMin = houseMin;
            HouseMax = houseMax;
            Chests = chests;
            TractorMin = tractorMin;
            TractorMax = tractorMax;
            Farms = farms;
        }

        public string File { get; }
        public string DisplayName { get; }
        public MapBlockRole Role { get; }
        public float Weight { get; }
        public float Density { get; }
        public DebrisPick[] Debris { get; }
        public int HouseMin { get; }
        public int HouseMax { get; }
        public int Chests { get; }
        public int TractorMin { get; }
        public int TractorMax { get; }
        public int Farms { get; }
        public bool BigHouse { get; }
    }

    private static readonly DebrisPick[] SparseGrass =
    {
        new DebrisPick("obstacle.grass_1", 3f), new DebrisPick("obstacle.grass_2", 3f), new DebrisPick("obstacle.flower", 2f),
        // 出生 / 村庄格也零星来几棵树和石头，别像被推平过。
        new DebrisPick("obstacle.tree", 1f, 2.4f), new DebrisPick("obstacle.tree_2", 0.6f, 2.8f), new DebrisPick("obstacle.rock", 0.6f, 2.6f),
    };

    private static readonly DebrisPick[] ForestA =
    {
        // 树的 clearance 决定树与树的最小间距（默认 4.5/6 太稀）；石头用它的默认 3.2，和树拉开。
        new DebrisPick("obstacle.tree", 5f, 2.6f), new DebrisPick("obstacle.tree_2", 4f, 3.2f), new DebrisPick("obstacle.rock", 1f, 3.2f),
        new DebrisPick("obstacle.bush_1", 1.5f, 2.4f), new DebrisPick("obstacle.bush_2", 1.5f, 2.4f),
    };

    private static readonly DebrisPick[] ForestB =
    {
        new DebrisPick("obstacle.tree", 4f, 2.6f), new DebrisPick("obstacle.tree_2", 5f, 3.2f), new DebrisPick("obstacle.rock", 0.8f, 3.2f),
        new DebrisPick("obstacle.pebble", 1f, 2f), new DebrisPick("obstacle.bush_1", 1.5f, 2.4f),
    };

    private static readonly DebrisPick[] PlainsGrass =
    {
        new DebrisPick("obstacle.grass_1", 6f), new DebrisPick("obstacle.grass_2", 6f), new DebrisPick("obstacle.grass_3", 6f),
        new DebrisPick("obstacle.flower", 5f), new DebrisPick("obstacle.flower_daisy", 5f), new DebrisPick("obstacle.flower_cluster", 3f),
        new DebrisPick("obstacle.bush_1", 2f), new DebrisPick("obstacle.bush_2", 2f),
        new DebrisPick("obstacle.tree", 4f, 2.2f), new DebrisPick("obstacle.tree_2", 2.5f, 2.6f),
    };

    private static readonly DebrisPick[] PlainsRock =
    {
        new DebrisPick("obstacle.pebble", 5f), new DebrisPick("obstacle.rock", 3f, 2.4f), new DebrisPick("obstacle.grass_1", 2f),
        new DebrisPick("obstacle.barrel", 0.5f), new DebrisPick("obstacle.tree", 3f, 2.2f), new DebrisPick("obstacle.tree_2", 2f, 2.6f),
    };

    private static readonly BlockSpec[] BlockSpecs =
    {
        new BlockSpec("block.spawn", "出生点", MapBlockRole.Spawn, 1f, 0.45f, SparseGrass),
        new BlockSpec("block.forest_a", "森林 A", MapBlockRole.Forest, 1f, 3.5f, ForestA),
        new BlockSpec("block.forest_b", "森林 B", MapBlockRole.Forest, 1f, 4f, ForestB),
        new BlockSpec("block.plains_grass", "平原·草", MapBlockRole.Plains, 1f, 0.9f, PlainsGrass, farms: 3),
        new BlockSpec("block.plains_rock", "平原·石", MapBlockRole.Plains, 1f, 0.9f, PlainsRock, farms: 2),
        new BlockSpec("block.village_small", "村庄·小房子", MapBlockRole.Village, 1f, 0.6f, SparseGrass, houseMin: 4, houseMax: 5, chests: 3, tractorMin: 1, tractorMax: 2, bigHouse: false),
        new BlockSpec("block.village_big", "村庄·大房子", MapBlockRole.Village, 1f, 0.6f, SparseGrass, houseMin: 3, houseMax: 4, chests: 3, tractorMin: 1, tractorMax: 2, bigHouse: true),
    };

    private static readonly Vector2[] TutorialSheepPositions =
    {
        new Vector2(-6.8f, 3.3f),
        new Vector2(-2.6f, 3.4f),
        new Vector2(4.8f, 3.4f),
        new Vector2(7.2f, 0.4f),
        new Vector2(0.5f, -3.6f),
        new Vector2(-3.8f, -3.4f)
    };

    private static readonly string[] ManagedRootNames =
    {
        "AlphaWorld",
        "SheepFlock",
        "Sheep_Initial",
        "ProgressiveSheepSpawner",
        "WolfSpawner",
        "WolfSystem",
        "WorldSeed",
        "WorldDebrisSpawner",
        "WorldLandmarkSpawner",
        "MapLayoutBuilder",
        "BorderFence",
        "TutorialPen",
        "AlphaCanvas",
        "GameCanvas",
        "PauseManager",
        "EventSystem",
        "SceneAudio",
        "AlphaFlockExpansionController"
    };

    [MenuItem("Game Jam/Alpha Flock Expansion/Setup Scene")]
    public static void SetupScene()
    {
        ConfigureUiPrefabs();
        Scene scene = OpenOrCreateScene();
        BuildScene(scene);
        Selection.activeGameObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "SheepFlock");
    }

    [MenuItem("Game Jam/Alpha Flock Expansion/Apply Sequential Task Flow")]
    public static void ApplySequentialTaskFlow()
    {
        UiVisualPolish.ApplyTaskPrefab();
        ConfigureTaskSystemPrefab();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ClearOrphanedTutorialVisuals(scene);
        TutorialPen pen = FindComponentInScene<TutorialPen>(scene);
        if (pen == null)
            throw new System.InvalidOperationException("Alpha scene is missing TutorialPen.");

        foreach (FenceObstacle fence in pen.Fences)
        {
            if (fence == null)
                continue;

            fence.SetRequiredCountOverride(TutorialRequiredFlockSize);
            EditorUtility.SetDirty(fence);
        }

        GameObject sheepRoot = FindNamedObjectInScene(scene, "TutorialSheep");
        GameObject recruitablePrefab = LoadRequired<GameObject>(RecruitableSheepPrefabPath);
        Transform sixthSheep = sheepRoot != null ? sheepRoot.transform.Find("TutorialSheep_6") : null;
        if (sheepRoot == null)
            throw new System.InvalidOperationException("Alpha scene is missing the TutorialSheep root.");
        if (sixthSheep == null)
        {
            GameObject sheep = (GameObject)PrefabUtility.InstantiatePrefab(recruitablePrefab, sheepRoot.transform);
            sheep.name = "TutorialSheep_6";
            sheep.transform.position = TutorialSheepPositions[5];
        }
        else
        {
            sixthSheep.position = TutorialSheepPositions[5];
            EditorUtility.SetDirty(sixthSheep);
        }

        GameObject existingSigns = FindNamedObjectInScene(scene, "TutorialSigns");
        if (existingSigns != null)
            Object.DestroyImmediate(existingSigns);
        TutorialSignGroups signs = CreateTutorialSigns(pen.transform);
        pen.Configure(PenRect, pen.Fences.ToArray(), signs.Root, signs.Move, signs.Recruit, signs.Fence);
        pen.BindFlock(FindComponentInScene<FlockController>(scene));
        EditorUtility.SetDirty(pen);

        WolfEventDirector director = FindComponentInScene<WolfEventDirector>(scene);
        if (director != null)
        {
            SerializedObject directorData = new SerializedObject(director);
            directorData.FindProperty("requiredMemberCount").intValue = WolfUnlockFlockSize;
            directorData.ApplyModifiedPropertiesWithoutUndo();
        }

        AlphaFlockExpansionController controller = FindComponentInScene<AlphaFlockExpansionController>(scene);
        if (controller != null)
        {
            SerializedObject controllerData = new SerializedObject(controller);
            controllerData.FindProperty("wolfUnlockFlockSize").intValue = WolfUnlockFlockSize;
            controllerData.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Alpha 顺序任务栏、教程羊与阶段门槛已更新。");
    }

    [MenuItem("Game Jam/Alpha Flock Expansion/Apply Audio Integration")]
    public static void ApplyAudioIntegration()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        FlockMovementController movement = FindComponentInScene<FlockMovementController>(scene);
        WolfEventDirector wolfDirector = FindComponentInScene<WolfEventDirector>(scene);
        TutorialPen tutorialPen = FindComponentInScene<TutorialPen>(scene);

        if (movement == null || wolfDirector == null || tutorialPen == null)
            throw new System.InvalidOperationException("Alpha scene is missing its flock, tutorial pen, or wolf system.");

        EnsureSceneAudio(scene);
        ConfigureFootstepAudio(movement);
        ConfigureRecruitAudio(movement, tutorialPen);
        ConfigureWolfAudio(wolfDirector);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Game Jam/Alpha Flock Expansion/Apply Main Menu UI")]
    public static void ApplyMainMenuUi()
    {
        UiVisualPolish.ApplyGameplayPrefabs();
        UiVisualPolish.ApplyMainMenuScene();
        AssetDatabase.SaveAssets();
    }

    /// <summary>把 Alpha 场景加进 Build Settings（主菜单“开始游戏”按名字加载需要它）。</summary>
    [MenuItem("Game Jam/Alpha Flock Expansion/Add Scene To Build Settings")]
    public static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene existing in scenes)
        {
            if (existing.path == ScenePath)
            {
                existing.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("AlphaFlockExpansion 已在 Build Settings 里。");
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("已把 AlphaFlockExpansion 加进 Build Settings。");
    }

    [MenuItem("Game Jam/Alpha Flock Expansion/Open Scene")]
    public static void OpenScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogWarning("Alpha 场景尚未生成，请先运行 Setup Scene。");
            return;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void BuildScene(Scene scene)
    {
        WorldObstaclePrefabBuilder.EnsureFolder(SceneFolder);
        WorldObstaclePrefabBuilder.BuildAll();

        GameObject memberPrefab = LoadRequired<GameObject>(SheepMemberPrefabPath);
        GameObject recruitablePrefab = LoadRequired<GameObject>(RecruitableSheepPrefabPath);
        GameObject wolfPrefab = LoadRequired<GameObject>(WolfPrefabPath);
        GameObject fencePrefab = LoadRequired<GameObject>(WorldObstaclePrefabBuilder.FencePrefabPath);
        SheepNamePool namePool = LoadRequired<SheepNamePool>(NamePoolPath);
        ObstacleDefinition fenceDefinition = LoadRequired<ObstacleDefinition>(WorldObstaclePrefabBuilder.FenceDefinitionPath);
        ObstacleDefinition borderFenceDefinition = LoadRequired<ObstacleDefinition>(WorldObstaclePrefabBuilder.BorderFenceDefinitionPath);

        ClearManagedObjects(scene);
        ClearOrphanedTutorialVisuals(scene);

        EnsureSceneAudio(scene);
        CreateWorld(scene);
        WorldSeed worldSeed = CreateWorldSeed(scene);
        BorderFenceRing borderRing = CreateBorderFence(scene, fencePrefab, borderFenceDefinition);
        TutorialPen tutorialPen = CreateTutorialPen(scene, fencePrefab, fenceDefinition, recruitablePrefab);
        WorldDebrisSpawner debris = CreateDebrisSpawner(scene, worldSeed);

        GameObject flockObject = CreateFlock(
            scene,
            memberPrefab,
            out FlockController flock,
            out FlockMovementController movement,
            out FlockActionController actions,
            out PoopAbility poopAbility);
        tutorialPen.BindFlock(flock);
        ConfigureRecruitAudio(movement, tutorialPen);
        CameraFollow2D cameraFollow = ConfigureCamera(scene, flockObject.transform, out Camera gameplayCamera);
        ConfigureLighting(scene);
        ProgressiveSheepSpawner sheepSpawner = CreateSheepSpawner(scene, flock, namePool, gameplayCamera, worldSeed);
        WorldLandmarkSpawner landmarks = CreateLandmarkSpawner(scene, worldSeed, sheepSpawner, fencePrefab, fenceDefinition);
        CreateMapLayout(scene, worldSeed, borderRing, tutorialPen, flockObject, cameraFollow, debris, landmarks);
        CreateWolfSystem(scene, flock, wolfPrefab.GetComponent<Wolf>(), out WolfSpawner wolfSpawner, out WolfEventDirector director);
        LevelUi ui = CreateLevelUi(scene);
        CreateGameController(
            scene,
            flock,
            movement,
            actions,
            poopAbility,
            sheepSpawner,
            cameraFollow,
            wolfSpawner,
            director,
            borderRing,
            tutorialPen,
            ui);

        // 宝通寺 / 真结局的引用挂在 Setup 重建的对象上，重建后必须重新接一次。
        TrueEndingSetup.Wire();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Alpha 羊群扩张场景已生成：{ScenePath}。" +
            $"地图 {WorldRect.width}x{WorldRect.height}（{MapColumns}x{MapRows} 区块，出生格随机），出生羊圈 6 只教程羊，狼在 {WolfUnlockFlockSize} 只后出现，" +
            $"历史最高 {ExitUnlockFlockSize} 只后解锁冲出地图，冲刺时当前羊数达标才能撞开外围围栏。");
    }

    // ------------------------------------------------------------------ scene

    private static Scene OpenOrCreateScene()
    {
        if (SceneManager.GetActiveScene().path == ScenePath)
            return SceneManager.GetActiveScene();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            throw new System.OperationCanceledException("Alpha scene setup was cancelled.");

        EnsureSceneAssetExists();
        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void EnsureSceneAssetExists()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneTemplatePath) != null)
        {
            if (!AssetDatabase.CopyAsset(SceneTemplatePath, ScenePath))
                throw new System.InvalidOperationException($"Failed to copy {SceneTemplatePath}.");
        }
        else
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene created = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(created, ScenePath);
            EditorSceneManager.CloseScene(created, true);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }

        AssetDatabase.Refresh();
    }

    private static void ClearManagedObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects().ToArray())
        {
            bool managed = ManagedRootNames.Contains(root.name)
                || root.name.StartsWith("Sheep_")
                || root.name.StartsWith("TutorialSheep_")
                // 早期版本把羊圈栅栏直接生成在场景根节点；这些孤儿 PenFence_* 不在任何管理根下，每次 Setup 都会残留。
                || root.name.StartsWith("PenFence")
                || root.name.StartsWith("Wolf_")
                || root.name.StartsWith("AlphaWildSheep_");
            if (managed)
                Object.DestroyImmediate(root);
        }
    }

    private static void ClearOrphanedTutorialVisuals(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects().ToArray())
        {
            bool orphanedKeycapFrame = root.name == "Frame"
                && root.GetComponent<SpriteRenderer>() != null;
            bool orphanedKeycapLetter = root.name == "Letter"
                && root.GetComponent<TextMeshPro>() != null;
            if (orphanedKeycapFrame || orphanedKeycapLetter)
                Object.DestroyImmediate(root);
        }
    }

    // ------------------------------------------------------------------ world

    private static void CreateWorld(Scene scene)
    {
        GameObject world = new GameObject("AlphaWorld");
        SceneManager.MoveGameObjectToScene(world, scene);

        Sprite grass = LoadGrassSprite();
        GameObject background = new GameObject("GrassBackground");
        background.transform.SetParent(world.transform, false);
        Rect grassRect = Expand(WorldRect, EscapeBoundsExpansion);
        background.transform.position = new Vector3(grassRect.center.x, grassRect.center.y, 0f);
        SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = grass;
        // 放在 Background 排序层最底下：碎掉的围栏 / 木桶会切到 Background 层（order 10），要能画在草地上面。
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = -100;
        if (grass != null)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = grassRect.size;
        }
        else
        {
            renderer.color = new Color(0.28f, 0.46f, 0.25f, 1f);
        }
    }

    private static Sprite LoadGrassSprite()
    {
        Sprite grass = LoadTiledSprite(GrassBackgroundPath, optional: false);
        if (grass == null)
            Debug.LogWarning($"找不到草原背景 {GrassBackgroundPath}，改用纯色。");
        return grass;
    }

    /// <summary>可平铺的草地贴图需要 Sprite + Full Rect 网格 + Repeat；不是的话改导入设置后重新导入。</summary>
    private static Sprite LoadTiledSprite(string path, bool optional)
    {
        if (!System.IO.File.Exists(path))
            return null;

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return null;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null && !optional)
            Debug.LogWarning($"Tiled sprite not found: {path}");
        return sprite;
    }

    private static WorldSeed CreateWorldSeed(Scene scene)
    {
        GameObject seedObject = new GameObject("WorldSeed");
        SceneManager.MoveGameObjectToScene(seedObject, scene);
        return seedObject.AddComponent<WorldSeed>();
    }

    // ------------------------------------------------------------------ fences

    private static BorderFenceRing CreateBorderFence(Scene scene, GameObject fencePrefab, ObstacleDefinition definition)
    {
        GameObject root = new GameObject("BorderFence");
        SceneManager.MoveGameObjectToScene(root, scene);
        List<FenceObstacle> fences = BuildFenceRing(root.transform, fencePrefab, definition, WorldRect, BorderFenceScale, "BorderFence");

        BorderFenceRing ring = root.AddComponent<BorderFenceRing>();
        ring.Configure(WorldRect, fences.ToArray());
        return ring;
    }

    /// <summary>沿矩形四边摆一圈围栏；上下边水平，左右边旋转 90°。</summary>
    private static List<FenceObstacle> BuildFenceRing(
        Transform parent,
        GameObject fencePrefab,
        ObstacleDefinition definition,
        Rect rect,
        float scale,
        string namePrefix)
    {
        List<FenceObstacle> fences = new List<FenceObstacle>();
        float segment = 2f * scale;       // 围栏贴图宽 2 单位
        float thickness = 1f * scale;     // 围栏贴图高 1 单位

        int horizontalCount = Mathf.Max(1, Mathf.RoundToInt(rect.width / segment));
        float horizontalStep = rect.width / horizontalCount;
        for (int index = 0; index < horizontalCount; index++)
        {
            float x = rect.xMin + horizontalStep * (index + 0.5f);
            fences.Add(PlaceFence(parent, fencePrefab, definition, new Vector2(x, rect.yMax), 0f, scale, $"{namePrefix}_Top_{index:00}"));
            fences.Add(PlaceFence(parent, fencePrefab, definition, new Vector2(x, rect.yMin), 0f, scale, $"{namePrefix}_Bottom_{index:00}"));
        }

        float sideLength = rect.height - thickness;
        int verticalCount = Mathf.Max(1, Mathf.CeilToInt(sideLength / segment - 0.01f));
        float verticalStep = sideLength / verticalCount;
        for (int index = 0; index < verticalCount; index++)
        {
            float y = rect.yMin + thickness * 0.5f + verticalStep * (index + 0.5f);
            fences.Add(PlaceFence(parent, fencePrefab, definition, new Vector2(rect.xMin, y), 90f, scale, $"{namePrefix}_Left_{index:00}"));
            fences.Add(PlaceFence(parent, fencePrefab, definition, new Vector2(rect.xMax, y), 90f, scale, $"{namePrefix}_Right_{index:00}"));
        }

        return fences;
    }

    private static FenceObstacle PlaceFence(
        Transform parent,
        GameObject fencePrefab,
        ObstacleDefinition definition,
        Vector2 position,
        float rotation,
        float scale,
        string name)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab, parent);
        instance.name = name;
        instance.transform.position = new Vector3(position.x, position.y, 0f);
        instance.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        instance.transform.localScale = new Vector3(scale, scale, 1f);

        BreakableObstacle breakable = instance.GetComponent<BreakableObstacle>();
        SerializedObject serialized = new SerializedObject(breakable);
        serialized.FindProperty("definition").objectReferenceValue = definition;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return instance.GetComponent<FenceObstacle>();
    }

    // ------------------------------------------------------------------ tutorial pen

    private static TutorialPen CreateTutorialPen(
        Scene scene,
        GameObject fencePrefab,
        ObstacleDefinition penDefinition,
        GameObject recruitablePrefab)
    {
        GameObject root = new GameObject("TutorialPen");
        SceneManager.MoveGameObjectToScene(root, scene);

        GameObject fenceRoot = new GameObject("PenFences");
        fenceRoot.transform.SetParent(root.transform, false);
        List<FenceObstacle> fences = BuildFenceRing(fenceRoot.transform, fencePrefab, penDefinition, PenRect, PenFenceScale, "PenFence");
        foreach (FenceObstacle fence in fences)
            fence.SetRequiredCountOverride(TutorialRequiredFlockSize);

        GameObject sheepRoot = new GameObject("TutorialSheep");
        sheepRoot.transform.SetParent(root.transform, false);
        for (int index = 0; index < TutorialSheepPositions.Length; index++)
        {
            GameObject sheep = (GameObject)PrefabUtility.InstantiatePrefab(recruitablePrefab, sheepRoot.transform);
            sheep.name = $"TutorialSheep_{index + 1}";
            Vector2 position = TutorialSheepPositions[index];
            sheep.transform.position = new Vector3(position.x, position.y, 0f);
        }

        TutorialSignGroups signs = CreateTutorialSigns(root.transform);

        TutorialPen pen = root.AddComponent<TutorialPen>();
        pen.Configure(PenRect, fences.ToArray(), signs.Root, signs.Move, signs.Recruit, signs.Fence);
        return pen;
    }

    /// <summary>草地上的教程标识（占位：粉笔色文字 + 键帽方块；美术出图后替换 Sprite 即可）。</summary>
    private static TutorialSignGroups CreateTutorialSigns(Transform parent)
    {
        GameObject signs = new GameObject("TutorialSigns");
        signs.transform.SetParent(parent, false);

        Sprite keycapSprite = AssetDatabase.LoadAllAssetsAtPath(WarningRectAssetPath).OfType<Sprite>().FirstOrDefault();
        Color chalk = new Color(0.16f, 0.14f, 0.08f, 1f);

        GameObject moveGroup = new GameObject("MoveTutorial");
        moveGroup.transform.SetParent(signs.transform, false);
        GameObject recruitGroup = new GameObject("RecruitTutorial");
        recruitGroup.transform.SetParent(signs.transform, false);
        GameObject fenceGroup = new GameObject("FenceTutorial");
        fenceGroup.transform.SetParent(signs.transform, false);

        // 1. 玩家开始移动前，只显示移动操作。
        Vector2 moveOrigin = new Vector2(-5.5f, -1.6f);
        CreateWorldText(moveGroup.transform, "Move_Title", "WASD  移动", moveOrigin + new Vector2(0f, 2.2f), 1.05f, chalk);
        CreateKeycap(moveGroup.transform, keycapSprite, "W", moveOrigin + new Vector2(0f, 0.9f), chalk);
        CreateKeycap(moveGroup.transform, keycapSprite, "A", moveOrigin + new Vector2(-1.1f, -0.2f), chalk);
        CreateKeycap(moveGroup.transform, keycapSprite, "S", moveOrigin + new Vector2(0f, -0.2f), chalk);
        CreateKeycap(moveGroup.transform, keycapSprite, "D", moveOrigin + new Vector2(1.1f, -0.2f), chalk);

        // 2. 开始移动后持续显示寻找目标，累计找到五只才收起。
        Vector2 recruitOrigin = new Vector2(1.5f, 1.1f);
        CreateWorldText(recruitGroup.transform, "Recruit_Title", "找五个新伙伴", recruitOrigin + new Vector2(0f, 1.0f), 1.0f, chalk);

        // 3. 羊群达到撞栏门槛后才显示 E。Q 不再出现在新手教程里。
        Vector2 fenceOrigin = new Vector2(4.7f, -2.7f);
        CreateWorldText(fenceGroup.transform, "Fence_Title", "撞开羊圈！", fenceOrigin + new Vector2(-0.5f, 1.25f), 1.05f, chalk);
        CreateKeycap(fenceGroup.transform, keycapSprite, "E", fenceOrigin + new Vector2(-1.7f, 0f), chalk);
        CreateWorldText(fenceGroup.transform, "Fence_Hint", "整群冲刺  →", fenceOrigin + new Vector2(0.45f, 0f), 0.78f, chalk);

        moveGroup.SetActive(true);
        recruitGroup.SetActive(false);
        fenceGroup.SetActive(false);
        return new TutorialSignGroups(signs, moveGroup, recruitGroup, fenceGroup);
    }

    private readonly struct TutorialSignGroups
    {
        public TutorialSignGroups(GameObject root, GameObject move, GameObject recruit, GameObject fence)
        {
            Root = root;
            Move = move;
            Recruit = recruit;
            Fence = fence;
        }

        public GameObject Root { get; }
        public GameObject Move { get; }
        public GameObject Recruit { get; }
        public GameObject Fence { get; }
    }

    private static void CreateWorldText(Transform parent, string name, string text, Vector2 position, float scale, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        textObject.transform.position = new Vector3(position.x, position.y, 0f);
        textObject.transform.localScale = Vector3.one * scale;

        TextMeshPro label = textObject.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = 4.5f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.fontStyle = FontStyles.Bold;
        label.sortingOrder = -50;
        label.rectTransform.sizeDelta = new Vector2(12f, 2f);
        // 中文字形由 TMP Settings 中随仓库提交的 Noto Sans SC fallback 提供，Edit/Play 模式保持一致。
    }

    private static void CreateKeycap(Transform parent, Sprite sprite, string letter, Vector2 position, Color color)
    {
        GameObject cap = new GameObject($"Key_{letter}");
        cap.transform.SetParent(parent, false);
        cap.transform.position = new Vector3(position.x, position.y, 0f);

        if (sprite != null)
        {
            GameObject frame = new GameObject("Frame");
            frame.transform.SetParent(cap.transform, false);
            frame.transform.localPosition = new Vector3(-0.45f, 0f, 0f); // WolfWarningRect pivot 在左侧中点
            frame.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            SpriteRenderer renderer = frame.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.96f, 0.92f, 0.76f, 0.82f);
            renderer.sortingOrder = -60;
        }

        CreateWorldText(cap.transform, "Letter", letter, position, 1f, color);
    }

    // ------------------------------------------------------------------ debris

    private static WorldDebrisSpawner CreateDebrisSpawner(Scene scene, WorldSeed worldSeed)
    {
        GameObject spawnerObject = new GameObject("WorldDebrisSpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        WorldDebrisSpawner spawner = spawnerObject.AddComponent<WorldDebrisSpawner>();

        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("worldSeed").objectReferenceValue = worldSeed;
        // 散布物种类、权重、间距、密度统一由 WorldObstaclePrefabBuilder.DebrisSpecs 决定。
        WorldObstaclePrefabBuilder.ApplyDebrisEntries(serialized);
        serialized.FindProperty("area").rectValue = WorldRect;
        SerializedProperty zones = serialized.FindProperty("exclusionZones");
        zones.arraySize = 1;
        zones.GetArrayElementAtIndex(0).rectValue = Expand(PenRect, 4f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }

    private static WorldLandmarkSpawner CreateLandmarkSpawner(
        Scene scene,
        WorldSeed worldSeed,
        ProgressiveSheepSpawner sheepSpawner,
        GameObject fencePrefab,
        ObstacleDefinition fenceDefinition)
    {
        GameObject spawnerObject = new GameObject("WorldLandmarkSpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        WorldLandmarkSpawner spawner = spawnerObject.AddComponent<WorldLandmarkSpawner>();

        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("worldSeed").objectReferenceValue = worldSeed;
        serialized.FindProperty("sheepSpawner").objectReferenceValue = sheepSpawner;
        serialized.FindProperty("riceFieldPrefab").objectReferenceValue =
            LoadRequired<GameObject>(WorldObstaclePrefabBuilder.PrefabFolder + "/Obstacle_RiceField.prefab");
        serialized.FindProperty("haystackPrefab").objectReferenceValue =
            LoadRequired<GameObject>(WorldObstaclePrefabBuilder.PrefabFolder + "/Obstacle_Haystack.prefab");
        serialized.FindProperty("barrelPrefab").objectReferenceValue =
            LoadRequired<GameObject>(WorldObstaclePrefabBuilder.PrefabFolder + "/Obstacle_Barrel.prefab");
        serialized.FindProperty("fencePrefab").objectReferenceValue = fencePrefab;
        serialized.FindProperty("fenceDefinition").objectReferenceValue = fenceDefinition;
        serialized.FindProperty("redChestDefinition").objectReferenceValue =
            LoadRequired<ObstacleDefinition>(WorldObstaclePrefabBuilder.RedChestDefinitionPath);
        serialized.FindProperty("houseDefinition").objectReferenceValue =
            LoadRequired<ObstacleDefinition>(WorldObstaclePrefabBuilder.HouseDefinitionPath);
        serialized.FindProperty("redChestSprite").objectReferenceValue =
            WorldObstaclePrefabBuilder.LoadBuildingSprite("红箱子");
        serialized.FindProperty("houseSprite").objectReferenceValue =
            WorldObstaclePrefabBuilder.LoadBuildingSprite("房子");
        serialized.FindProperty("redChestCount").intValue = 7;
        serialized.FindProperty("area").rectValue = WorldRect;
        SerializedProperty zones = serialized.FindProperty("exclusionZones");
        zones.arraySize = 1;
        zones.GetArrayElementAtIndex(0).rectValue = Expand(PenRect, 5f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }

    // ------------------------------------------------------------------ map blocks

    /// <summary>
    /// 区块布局器：运行时按种子分配 3×2 格子并把出生羊圈挪到随机格。
    /// 它依赖的对象（羊圈、羊群、初始羊、相机、两个撒点器）都已在此之前创建。
    /// </summary>
    private static MapLayoutBuilder CreateMapLayout(
        Scene scene,
        WorldSeed worldSeed,
        BorderFenceRing borderRing,
        TutorialPen tutorialPen,
        GameObject flockObject,
        CameraFollow2D cameraFollow,
        WorldDebrisSpawner debris,
        WorldLandmarkSpawner landmarks)
    {
        GameObject builderObject = new GameObject("MapLayoutBuilder");
        SceneManager.MoveGameObjectToScene(builderObject, scene);
        MapLayoutBuilder builder = builderObject.AddComponent<MapLayoutBuilder>();
        MapBlockDefinition[] pool = EnsureBlockDefinitions();
        GameObject initialSheep = FindNamedObjectInScene(scene, "Sheep_Initial");

        SerializedObject serialized = new SerializedObject(builder);
        serialized.FindProperty("worldSeed").objectReferenceValue = worldSeed;
        serialized.FindProperty("borderRing").objectReferenceValue = borderRing;
        serialized.FindProperty("tutorialPen").objectReferenceValue = tutorialPen;
        serialized.FindProperty("flockRoot").objectReferenceValue = flockObject.transform;
        serialized.FindProperty("initialSheep").objectReferenceValue = initialSheep != null ? initialSheep.transform : null;
        serialized.FindProperty("gameplayCamera").objectReferenceValue = cameraFollow != null ? cameraFollow.transform : null;
        serialized.FindProperty("debrisSpawner").objectReferenceValue = debris;
        serialized.FindProperty("landmarkSpawner").objectReferenceValue = landmarks;
        serialized.FindProperty("columns").intValue = MapColumns;
        serialized.FindProperty("rows").intValue = MapRows;
        serialized.FindProperty("worldRect").rectValue = WorldRect;
        SerializedProperty poolProperty = serialized.FindProperty("blockPool");
        poolProperty.arraySize = pool.Length;
        for (int index = 0; index < pool.Length; index++)
            poolProperty.GetArrayElementAtIndex(index).objectReferenceValue = pool[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 两个撒点器改为按格生成；大房子 / 拖拉机没有美术时保持为空，运行时自动退回小房子 / 不放拖拉机。
        SerializedObject debrisSerialized = new SerializedObject(debris);
        debrisSerialized.FindProperty("layout").objectReferenceValue = builder;
        // 保留全局安全上限；按格密度决定目标数量，补撒只恢复到该目标，不会无限增长。
        debrisSerialized.FindProperty("maximumCount").intValue = 520;
        debrisSerialized.FindProperty("minimumSpacing").floatValue = 2.2f;
        // 森林接近满铺时随机落点大多会撞到已放下的树，尝试次数太少会让实际数量远低于目标。
        debrisSerialized.FindProperty("placementAttemptsPerItem").intValue = 32;
        debrisSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject landmarkSerialized = new SerializedObject(landmarks);
        landmarkSerialized.FindProperty("layout").objectReferenceValue = builder;
        landmarkSerialized.FindProperty("bigHouseDefinition").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<ObstacleDefinition>(WorldObstaclePrefabBuilder.BigHouseDefinitionPath);
        landmarkSerialized.FindProperty("bigHouseSprite").objectReferenceValue =
            WorldObstaclePrefabBuilder.LoadBuildingSprite("大房子", optional: true);
        landmarkSerialized.FindProperty("tractorPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(WorldObstaclePrefabBuilder.TractorPrefabPath);
        landmarkSerialized.ApplyModifiedPropertiesWithoutUndo();
        return builder;
    }

    /// <summary>
    /// 按 BlockSpecs 创建缺失的区块定义资产。已经存在的资产视为策划配置源，
    /// 重跑场景 Setup 时不覆盖手工调参，也不删除规格表之外的扩展定义。
    /// </summary>
    private static MapBlockDefinition[] EnsureBlockDefinitions()
    {
        WorldObstaclePrefabBuilder.EnsureFolder(BlockDefinitionFolder);
        List<MapBlockDefinition> result = new List<MapBlockDefinition>(BlockSpecs.Length);
        foreach (BlockSpec spec in BlockSpecs)
        {
            string path = $"{BlockDefinitionFolder}/{spec.File}.asset";
            MapBlockDefinition definition = AssetDatabase.LoadAssetAtPath<MapBlockDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MapBlockDefinition>();
                AssetDatabase.CreateAsset(definition, path);
                SerializedObject serialized = new SerializedObject(definition);
                serialized.FindProperty("blockId").stringValue = spec.File;
                serialized.FindProperty("displayName").stringValue = spec.DisplayName;
                serialized.FindProperty("role").intValue = (int)spec.Role;
                serialized.FindProperty("weight").floatValue = spec.Weight;
                serialized.FindProperty("debrisDensityPer100SquareUnits").floatValue = spec.Density;
                SerializedProperty debris = serialized.FindProperty("debris");
                debris.arraySize = 0;
                foreach (DebrisPick pick in spec.Debris)
                {
                    WorldObstaclePrefabBuilder.DebrisSpec debrisSpec = WorldObstaclePrefabBuilder.DebrisSpecs
                        .FirstOrDefault(item => item.Id == pick.Id);
                    GameObject prefab = debrisSpec != null ? AssetDatabase.LoadAssetAtPath<GameObject>(debrisSpec.PrefabPath) : null;
                    if (prefab == null)
                        continue;   // 对应美术还没到（例如新树），这条先跳过。

                    debris.arraySize++;
                    SerializedProperty entry = debris.GetArrayElementAtIndex(debris.arraySize - 1);
                    entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                    entry.FindPropertyRelative("weight").floatValue = pick.Weight;
                    entry.FindPropertyRelative("clearance").floatValue = pick.Clearance > 0f ? pick.Clearance : debrisSpec.Clearance;
                }
                serialized.FindProperty("minimumHouseCount").intValue = spec.HouseMin;
                serialized.FindProperty("maximumHouseCount").intValue = spec.HouseMax;
                serialized.FindProperty("fixedRedChestCount").intValue = spec.Chests;
                serialized.FindProperty("minimumTractorCount").intValue = spec.TractorMin;
                serialized.FindProperty("maximumTractorCount").intValue = spec.TractorMax;
                serialized.FindProperty("farmClusterCount").intValue = spec.Farms;
                serialized.FindProperty("allowBigHouse").boolValue = spec.BigHouse;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            result.Add(definition);
        }

        AssetDatabase.SaveAssets();
        return result.ToArray();
    }

    // ------------------------------------------------------------------ flock & spawners

    private static GameObject CreateFlock(
        Scene scene,
        GameObject sheepPrefab,
        out FlockController flock,
        out FlockMovementController movement,
        out FlockActionController actions,
        out PoopAbility poopAbility)
    {
        GameObject flockObject = new GameObject("SheepFlock");
        SceneManager.MoveGameObjectToScene(flockObject, scene);

        Rigidbody2D body = flockObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D centerTrigger = flockObject.AddComponent<CircleCollider2D>();
        centerTrigger.isTrigger = true;
        centerTrigger.radius = 0.35f;

        movement = flockObject.AddComponent<FlockMovementController>();
        flock = flockObject.AddComponent<FlockController>();
        actions = flockObject.AddComponent<FlockActionController>();
        actions.Configure(flock, movement);
        poopAbility = flockObject.AddComponent<PoopAbility>();
        poopAbility.Configure(
            LoadRequired<InputActionReference>(PoopActionPath),
            LoadRequired<GameObject>(PoopPrefabPath),
            2f,
            10f,
            100,
            0.2f,
            1.9f,
            0.08f);

        ConfigureFootstepAudio(movement);

        GameObject initialSheep = (GameObject)PrefabUtility.InstantiatePrefab(sheepPrefab, scene);
        initialSheep.name = "Sheep_Initial";
        initialSheep.transform.position = Vector3.zero;
        SheepMember initialMember = initialSheep.GetComponent<SheepMember>();

        SerializedObject serialized = new SerializedObject(flock);
        serialized.FindProperty("movementController").objectReferenceValue = movement;
        SerializedProperty members = serialized.FindProperty("startingMembers");
        members.arraySize = 1;
        members.GetArrayElementAtIndex(0).objectReferenceValue = initialMember;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return flockObject;
    }

    private static ProgressiveSheepSpawner CreateSheepSpawner(
        Scene scene,
        FlockController flock,
        SheepNamePool namePool,
        Camera gameplayCamera,
        WorldSeed worldSeed)
    {
        GameObject spawnerObject = new GameObject("ProgressiveSheepSpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        ProgressiveSheepSpawner spawner = spawnerObject.AddComponent<ProgressiveSheepSpawner>();

        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("flock").objectReferenceValue = flock;
        serialized.FindProperty("namePool").objectReferenceValue = namePool;
        serialized.FindProperty("gameplayCamera").objectReferenceValue = gameplayCamera;
        serialized.FindProperty("worldSeed").objectReferenceValue = worldSeed;
        serialized.FindProperty("specialSheepCatalog").objectReferenceValue =
            SpecialSheepCatalogEditorUtility.LoadOrCreateAndSynchronize();
        serialized.FindProperty("spawnAreaCenter").vector2Value = WorldRect.center;
        serialized.FindProperty("spawnAreaSize").vector2Value = WorldRect.size;

        SerializedProperty zones = serialized.FindProperty("exclusionZones");
        zones.arraySize = 1;
        zones.GetArrayElementAtIndex(0).rectValue = Expand(PenRect, 2f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }

    private static void CreateWolfSystem(
        Scene scene,
        FlockController flock,
        Wolf wolfPrefab,
        out WolfSpawner spawner,
        out WolfEventDirector director)
    {
        GameObject root = new GameObject("WolfSystem");
        SceneManager.MoveGameObjectToScene(root, scene);

        spawner = root.AddComponent<WolfSpawner>();
        SerializedObject spawnerSerialized = new SerializedObject(spawner);
        spawnerSerialized.FindProperty("flock").objectReferenceValue = flock;
        spawnerSerialized.FindProperty("wolfPrefab").objectReferenceValue = wolfPrefab;
        spawnerSerialized.FindProperty("spawnDistance").floatValue = 16f;
        spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

        director = root.AddComponent<WolfEventDirector>();
        SerializedObject directorSerialized = new SerializedObject(director);
        directorSerialized.FindProperty("spawner").objectReferenceValue = spawner;
        directorSerialized.FindProperty("flock").objectReferenceValue = flock;
        directorSerialized.FindProperty("requiredMemberCount").intValue = WolfUnlockFlockSize;
        // 由关卡控制器在羊圈打开后再启动节奏。
        directorSerialized.FindProperty("runOnStart").boolValue = false;
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        ConfigureWolfAudio(director);

        // 正式节奏表：按羊群规模抽取一只狼 / 多只狼 / 本场损失最多的攻击。
        WolfAttackScheduleSetup.ApplyToDirector(director);
    }

    private static void EnsureSceneAudio(Scene scene)
    {
        GameObject audioObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "SceneAudio");
        if (audioObject == null)
        {
            audioObject = new GameObject("SceneAudio");
            SceneManager.MoveGameObjectToScene(audioObject, scene);
        }

        SceneBGM sceneBgm = audioObject.GetComponent<SceneBGM>();
        if (sceneBgm == null)
            sceneBgm = audioObject.AddComponent<SceneBGM>();
        sceneBgm.Configure(LoadRequired<AudioClip>(GameplayBgmPath));
    }

    private static void ConfigureFootstepAudio(FlockMovementController movement)
    {
        FlockFootstepAudio footstepAudio = movement.GetComponent<FlockFootstepAudio>();
        if (footstepAudio == null)
            footstepAudio = movement.gameObject.AddComponent<FlockFootstepAudio>();

        footstepAudio.Configure(
            LoadAudioClips(GrassFootstepPaths),
            LoadAudioClips(SandFootstepPaths),
            0.7f,
            0.35f);
    }

    private static void ConfigureRecruitAudio(
        FlockMovementController movement,
        TutorialPen tutorialPen)
    {
        SheepRecruitAudio recruitAudio = movement.GetComponent<SheepRecruitAudio>();
        if (recruitAudio == null)
            recruitAudio = movement.gameObject.AddComponent<SheepRecruitAudio>();

        recruitAudio.Configure(
            tutorialPen,
            LoadAudioClips(SheepRecruitClipPaths),
            0.5f);
    }

    private static void ConfigureWolfAudio(WolfEventDirector director)
    {
        AudioClip wolfWarningClip = LoadRequired<AudioClip>(WolfSpawnClipPath);
        SerializedObject directorData = new SerializedObject(director);
        directorData.FindProperty("howlClip").objectReferenceValue = wolfWarningClip;
        directorData.ApplyModifiedPropertiesWithoutUndo();

        WolfEventAudio wolfAudio = director.GetComponent<WolfEventAudio>();
        if (wolfAudio == null)
            wolfAudio = director.gameObject.AddComponent<WolfEventAudio>();

        wolfAudio.Configure(
            wolfWarningClip,
            new[]
            {
                LoadRequired<AudioClip>(WolfAttack1ClipPath),
                LoadRequired<AudioClip>(WolfAttack2ClipPath),
                LoadRequired<AudioClip>(WolfAttack3ClipPath)
            },
            LoadRequired<AudioClip>(WolfCaptureClipPath));
    }

    // ------------------------------------------------------------------ UI

    private sealed class LevelUi
    {
        public Canvas Canvas;
        public AlphaBannerView Banner;
        public MvpHudView Hud;
        public JoinToastView JoinToast;
        public PauseManager PauseManager;
        public ResultPanelView ResultPanelPrefab;
    }

    [MenuItem("Game Jam/Alpha Flock Expansion/Configure UI Prefabs")]
    public static void ConfigureUiPrefabs()
    {
        UiVisualPolish.ApplyGameplayPrefabs();
        GameObject pauseRoot = PrefabUtility.LoadPrefabContents(PauseSystemPrefabPath);
        try
        {
            GameObject pausePanel = FindNamedObject(pauseRoot, "PausePanel");
            GameObject pauseWindow = FindNamedObject(pauseRoot, "PauseWindow");
            GameObject settingPanel = FindNamedObject(pauseRoot, "SettingPanel");
            GameObject collectionPanel = FindNamedObject(pauseRoot, "CollectionPanel");

            if (collectionPanel == null && pausePanel != null)
            {
                GameObject collectionPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(CollectionPanelPrefabPath);
                if (collectionPrefab != null)
                {
                    collectionPanel = (GameObject)PrefabUtility.InstantiatePrefab(
                        collectionPrefab,
                        pausePanel.transform);
                    collectionPanel.name = "CollectionPanel";
                }
            }

            if (pausePanel == null || pauseWindow == null || settingPanel == null ||
                collectionPanel == null)
            {
                Debug.LogError("PauseSystem Prefab 缺少必要的 UI 层级。");
                return;
            }

            CollectionPanelController misplacedController =
                pausePanel.GetComponent<CollectionPanelController>();
            if (misplacedController != null)
                Object.DestroyImmediate(misplacedController, true);

            Button continueButton = FindNamedComponent<Button>(pauseRoot, "Btn_Continue");
            Button mainMenuButton = FindNamedComponent<Button>(pauseRoot, "Btn_MainMenu");
            Button settingsButton = FindNamedComponent<Button>(pauseRoot, "Btn_Settings");
            Button collectionButton = FindNamedComponent<Button>(pauseRoot, "Btn_Sheep");
            Button restartButton = FindNamedComponent<Button>(pauseRoot, "Btn_Restart", "Btn_Reasult");
            Button exitButton = FindNamedComponent<Button>(pauseRoot, "Btn_Exit");
            Button settingsBackButton = FindNamedComponent<Button>(settingPanel, "Btn_Back");
            Button collectionBackButton = FindNamedComponent<Button>(collectionPanel, "Btn_Back");

            if (restartButton != null)
                restartButton.gameObject.name = "Btn_Restart";

            Button[] runtimeBoundButtons =
            {
                continueButton,
                mainMenuButton,
                settingsButton,
                collectionButton,
                restartButton,
                exitButton,
                settingsBackButton,
                collectionBackButton
            };
            foreach (Button button in runtimeBoundButtons)
                ClearPersistentListeners(button);

            PauseManager manager = pauseRoot.GetComponent<PauseManager>();
            if (manager == null)
                manager = pauseRoot.AddComponent<PauseManager>();

            SerializedObject serialized = new SerializedObject(manager);
            serialized.FindProperty("pausePanel").objectReferenceValue = pausePanel;
            serialized.FindProperty("pauseWindow").objectReferenceValue = pauseWindow;
            serialized.FindProperty("settingPanel").objectReferenceValue = settingPanel;
            serialized.FindProperty("collectionPanel").objectReferenceValue = collectionPanel;
            serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
            serialized.FindProperty("mainMenuButton").objectReferenceValue = mainMenuButton;
            serialized.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            serialized.FindProperty("collectionButton").objectReferenceValue = collectionButton;
            serialized.FindProperty("restartButton").objectReferenceValue = restartButton;
            serialized.FindProperty("exitButton").objectReferenceValue = exitButton;
            serialized.FindProperty("settingsBackButton").objectReferenceValue = settingsBackButton;
            serialized.FindProperty("collectionBackButton").objectReferenceValue = collectionBackButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            pauseRoot.SetActive(true);
            pausePanel.SetActive(false);
            pauseWindow.SetActive(true);
            settingPanel.SetActive(false);
            collectionPanel.SetActive(false);

            EditorUtility.SetDirty(pauseRoot);
            PrefabUtility.SaveAsPrefabAsset(pauseRoot, PauseSystemPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(pauseRoot);
        }

        ConfigureTaskSystemPrefab();
    }

    private static void ConfigureTaskSystemPrefab()
    {
        GameObject taskRoot = PrefabUtility.LoadPrefabContents(TaskSystemPrefabPath);
        try
        {
            SetText(taskRoot, "Txt_Task", "任务");
            SetText(taskRoot, "Txt_Task_01", "去触碰另一只羊！");

            GameObject row01 = FindNamedObject(taskRoot, "TaskRow_01");
            GameObject row02 = FindNamedObject(taskRoot, "TaskRow_02");
            GameObject row03 = FindNamedObject(taskRoot, "TaskRow_03");
            GameObject row04 = FindNamedObject(taskRoot, "TaskRow_04");
            if (row01 != null) row01.SetActive(true);
            if (row02 != null) row02.SetActive(false);
            if (row03 != null) row03.SetActive(false);
            if (row04 != null) row04.SetActive(false);

            TaskChecklistView checklist = taskRoot.GetComponentInChildren<TaskChecklistView>(true);
            if (checklist != null)
            {
                SerializedObject checklistData = new SerializedObject(checklist);
                checklistData.FindProperty("taskHeaderText").objectReferenceValue =
                    FindNamedComponent<TMP_Text>(taskRoot, "Txt_Task");
                checklistData.FindProperty("taskRow01").objectReferenceValue = row01;
                checklistData.FindProperty("taskTitle01").objectReferenceValue =
                    FindNamedComponent<TMP_Text>(taskRoot, "Txt_Task_01");
                checklistData.FindProperty("progressFill01").objectReferenceValue =
                    FindNamedComponent<Image>(taskRoot, "TaskProgressFill");
                checklistData.FindProperty("taskRow02").objectReferenceValue = row02;
                checklistData.FindProperty("taskTitle02").objectReferenceValue =
                    FindNamedComponent<TMP_Text>(taskRoot, "Txt_Task_02");
                checklistData.FindProperty("taskRow03").objectReferenceValue = row03;
                checklistData.FindProperty("taskTitle03").objectReferenceValue =
                    FindNamedComponent<TMP_Text>(taskRoot, "Txt_Task_03");
                checklistData.FindProperty("taskRow04").objectReferenceValue = row04;
                checklistData.FindProperty("taskTitle04").objectReferenceValue =
                    FindNamedComponent<TMP_Text>(taskRoot, "Txt_Task_04");
                checklistData.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject taskPanel = FindNamedObject(taskRoot, "TaskPanel");
            GameObject taskButton = FindNamedObject(taskRoot, "Btn_TaskIcon");
            if (taskPanel != null)
                taskPanel.SetActive(false);
            if (taskButton != null)
                taskButton.SetActive(true);

            EditorUtility.SetDirty(taskRoot);
            PrefabUtility.SaveAsPrefabAsset(taskRoot, TaskSystemPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(taskRoot);
        }
    }

    private static void SetText(GameObject root, string objectName, string value)
    {
        TMP_Text text = FindNamedComponent<TMP_Text>(root, objectName);
        if (text != null)
            text.text = value;
    }

    private static GameObject FindNamedObject(GameObject root, params string[] names)
    {
        Transform match = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => names.Any(name => item.name.Trim() == name));
        return match != null ? match.gameObject : null;
    }

    private static T FindNamedComponent<T>(GameObject root, params string[] names)
        where T : Component
    {
        return root.GetComponentsInChildren<T>(true)
            .FirstOrDefault(item => names.Any(name => item.gameObject.name.Trim() == name));
    }

    private static void ClearPersistentListeners(Button button)
    {
        if (button == null)
            return;

        for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
            UnityEventTools.RemovePersistentListener(button.onClick, index);
    }

    /// <summary>
    /// 使用正式 UI Prefab 构建 Alpha 界面，不再从归档 Level_01 复制层级。
    /// </summary>
    private static LevelUi CreateLevelUi(Scene scene)
    {
        LevelUi ui = new LevelUi();

        GameObject canvasObject = CreateFallbackCanvas(scene);

        ui.Canvas = canvasObject.GetComponent<Canvas>();

        GameObject taskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TaskSystemPrefabPath);
        TaskPanelToggle taskPanelToggle = null;
        TaskChecklistView taskChecklistView = null;
        if (taskPrefab != null)
        {
            GameObject taskObject = (GameObject)PrefabUtility.InstantiatePrefab(taskPrefab, canvasObject.transform);
            taskObject.name = "TaskSystem";
            taskPanelToggle = taskObject.GetComponent<TaskPanelToggle>();
            taskChecklistView = taskObject.GetComponentInChildren<TaskChecklistView>(true);
        }

        RectTransform sheepHudRect = MvpUiFactory.CreateRect("SheepHUD", canvasObject.transform);
        MvpUiFactory.Stretch(sheepHudRect);
        ui.Hud = sheepHudRect.gameObject.AddComponent<MvpHudView>();
        SerializedObject hudData = new SerializedObject(ui.Hud);
        hudData.FindProperty("taskChecklistView").objectReferenceValue = taskChecklistView;
        hudData.ApplyModifiedPropertiesWithoutUndo();

        RectTransform toastRect = MvpUiFactory.CreateRect("JoinToast", canvasObject.transform);
        MvpUiFactory.Anchor(
            toastRect,
            Vector2.one,
            Vector2.one,
            new Vector2(-24f, -24f),
            new Vector2(520f, 294f));
        TMP_Text toastText = MvpUiFactory.CreateText(
            "Message",
            toastRect,
            string.Empty,
            28f,
            TextAlignmentOptions.Center);
        MvpUiFactory.Stretch(toastText.rectTransform);
        ui.JoinToast = toastRect.gameObject.AddComponent<JoinToastView>();
        SerializedObject toastSerialized = new SerializedObject(ui.JoinToast);
        toastSerialized.FindProperty("messageText").objectReferenceValue = toastText;
        toastSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseSystemPrefabPath);
        if (pausePrefab != null)
        {
            GameObject pauseObject = (GameObject)PrefabUtility.InstantiatePrefab(pausePrefab, canvasObject.transform);
            pauseObject.name = "PauseSystem";
            ui.PauseManager = pauseObject.GetComponent<PauseManager>();
            if (ui.PauseManager != null)
            {
                SerializedObject pauseSerialized = new SerializedObject(ui.PauseManager);
                pauseSerialized.FindProperty("taskPanelToggle").objectReferenceValue = taskPanelToggle;
                pauseSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 狼群仍由 Director 驱动，但 Alpha 不再显示屏幕顶部的阶段 / 倒计时提示。
        WolfEventHudView hud = canvasObject.GetComponentInChildren<WolfEventHudView>(true);
        if (hud != null)
            Object.DestroyImmediate(hud.gameObject);

        // 横幅：优先用仓库的 BannerSystem 预制体。
        GameObject bannerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BannerPrefabPath);
        if (bannerPrefab != null)
        {
            GameObject bannerObject = (GameObject)PrefabUtility.InstantiatePrefab(bannerPrefab, canvasObject.transform);
            bannerObject.name = "AlphaBanner";
            RectTransform bannerRect = bannerObject.GetComponent<RectTransform>();
            if (bannerRect != null)
            {
                bannerRect.anchorMin = new Vector2(0.5f, 1f);
                bannerRect.anchorMax = new Vector2(0.5f, 1f);
                bannerRect.pivot = new Vector2(0.5f, 1f);
                bannerRect.anchoredPosition = new Vector2(0f, -110f);
            }
            ui.Banner = bannerObject.AddComponent<AlphaBannerView>();
            SerializedObject bannerSerialized = new SerializedObject(ui.Banner);
            bannerSerialized.FindProperty("bannerView").objectReferenceValue = bannerObject.GetComponent<BannerView>();
            bannerSerialized.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            RectTransform bannerRect = MvpUiFactory.CreateRect("AlphaBanner", canvasObject.transform);
            MvpUiFactory.Anchor(bannerRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 64f));
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -110f);
            ui.Banner = bannerRect.gameObject.AddComponent<AlphaBannerView>();
        }

        if (ui.PauseManager != null)
        {
            SerializedObject pauseSerialized = new SerializedObject(ui.PauseManager);
            pauseSerialized.FindProperty("bannerView").objectReferenceValue = ui.Banner;
            pauseSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject resultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResultPanelPrefabPath);
        ui.ResultPanelPrefab = resultPrefab != null ? resultPrefab.GetComponent<ResultPanelView>() : null;

        // 暂停遮罩必须盖住任务、横幅和狼事件 HUD。
        if (ui.PauseManager != null)
            ui.PauseManager.transform.SetAsLastSibling();

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        return ui;
    }

    private static GameObject CreateFallbackCanvas(Scene scene)
    {
        GameObject canvasObject = new GameObject("GameCanvas", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject;
    }

    private static void CreateGameController(
        Scene scene,
        FlockController flock,
        FlockMovementController movement,
        FlockActionController actions,
        PoopAbility poopAbility,
        ProgressiveSheepSpawner sheepSpawner,
        CameraFollow2D cameraFollow,
        WolfSpawner wolfSpawner,
        WolfEventDirector director,
        BorderFenceRing borderRing,
        TutorialPen tutorialPen,
        LevelUi ui)
    {
        GameObject controllerObject = new GameObject("AlphaFlockExpansionController");
        SceneManager.MoveGameObjectToScene(controllerObject, scene);
        AlphaFlockExpansionController controller = controllerObject.AddComponent<AlphaFlockExpansionController>();

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("flock").objectReferenceValue = flock;
        serialized.FindProperty("flockMovement").objectReferenceValue = movement;
        serialized.FindProperty("flockActions").objectReferenceValue = actions;
        serialized.FindProperty("poopAbility").objectReferenceValue = poopAbility;
        serialized.FindProperty("sheepSpawner").objectReferenceValue = sheepSpawner;
        serialized.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
        serialized.FindProperty("wolfDirector").objectReferenceValue = director;
        serialized.FindProperty("wolfSpawner").objectReferenceValue = wolfSpawner;
        serialized.FindProperty("borderRing").objectReferenceValue = borderRing;
        serialized.FindProperty("tutorialPen").objectReferenceValue = tutorialPen;
        serialized.FindProperty("bannerView").objectReferenceValue = ui.Banner;
        serialized.FindProperty("uiCanvas").objectReferenceValue = ui.Canvas;
        serialized.FindProperty("hudView").objectReferenceValue = ui.Hud;
        serialized.FindProperty("joinToastView").objectReferenceValue = ui.JoinToast;
        serialized.FindProperty("pauseManager").objectReferenceValue = ui.PauseManager;
        serialized.FindProperty("resultPanelPrefab").objectReferenceValue = ui.ResultPanelPrefab;
        serialized.FindProperty("wolfUnlockFlockSize").intValue = WolfUnlockFlockSize;
        serialized.FindProperty("exitUnlockFlockSize").intValue = ExitUnlockFlockSize;
        serialized.FindProperty("exitBoundsExpansion").floatValue = EscapeBoundsExpansion;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static CameraFollow2D ConfigureCamera(Scene scene, Transform target, out Camera camera)
    {
        camera = scene.GetRootGameObjects()
            .Select(root => root.GetComponentInChildren<Camera>(true))
            .FirstOrDefault(found => found != null);

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.backgroundColor = new Color(0.28f, 0.46f, 0.25f, 1f);

        CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
        if (follow == null)
            follow = camera.gameObject.AddComponent<CameraFollow2D>();

        SerializedObject serialized = new SerializedObject(follow);
        serialized.FindProperty("target").objectReferenceValue = target;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(camera);
        return follow;
    }

    /// <summary>让 Global Light 2D 照亮所有排序层，否则 Background 层上的草地 / 碎片会是黑的。</summary>
    private static void ConfigureLighting(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (UnityEngine.Rendering.Universal.Light2D light in root.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))
            {
                SerializedObject serialized = new SerializedObject(light);
                SerializedProperty layers = serialized.FindProperty("m_ApplyToSortingLayers");
                if (layers == null)
                    continue;

                SortingLayer[] all = SortingLayer.layers;
                layers.arraySize = all.Length;
                for (int index = 0; index < all.Length; index++)
                    layers.GetArrayElementAtIndex(index).intValue = all[index].id;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // ------------------------------------------------------------------ helpers

    private static Rect Expand(Rect rect, float margin)
    {
        return new Rect(rect.xMin - margin, rect.yMin - margin, rect.width + margin * 2f, rect.height + margin * 2f);
    }

    private static T LoadRequired<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new System.InvalidOperationException($"Missing required asset at {path}.");
        return asset;
    }

    private static AudioClip[] LoadAudioClips(string[] paths)
    {
        AudioClip[] clips = new AudioClip[paths.Length];
        for (int index = 0; index < paths.Length; index++)
            clips[index] = LoadRequired<AudioClip>(paths[index]);
        return clips;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static GameObject FindNamedObjectInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == objectName);
            if (match != null)
                return match.gameObject;
        }

        return null;
    }
}

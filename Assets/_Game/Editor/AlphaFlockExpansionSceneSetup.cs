using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the isolated AlphaFlockExpansion development scene without touching Level_01.
/// 240x140 的草原、外圈围栏、出生羊圈（5 只教程羊 + 地面教程标识）、种子驱动的可破坏物、狼群节奏与 HUD。
/// 可重复执行：只重建由它管理的对象。
/// </summary>
public static class AlphaFlockExpansionSceneSetup
{
    private const string SceneFolder = "Assets/_Game/Scenes/Dev";
    private const string ScenePath = SceneFolder + "/AlphaFlockExpansion.unity";
    private const string SceneTemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    private const string GrassBackgroundPath = "Assets/Art/WorldSprites/Tiles/草原_背景.png";
    private const string WarningRectAssetPath = "Assets/_Game/Content/Art/Prototype/WolfWarningRect.asset";
    private const string SheepMemberPrefabPath = "Assets/_Game/Content/Perfabs/Sheep/SheepMember.prefab";
    private const string RecruitableSheepPrefabPath = "Assets/_Game/Content/Perfabs/Sheep/RecruitableSheep.prefab";
    private const string SpecialSheepFolder = "Assets/_Game/Content/Perfabs/Sheep";
    private const string WolfPrefabPath = "Assets/_Game/Content/Perfabs/Wolf/Wolf.prefab";
    private const string NamePoolPath = "Assets/_Game/Content/Data/SheepNamePool.asset";

    // 地图与羊圈尺寸（世界单位）。
    private static readonly Rect WorldRect = new Rect(-120f, -70f, 240f, 140f);
    private static readonly Rect PenRect = new Rect(-9f, -5f, 18f, 10f);
    private const float BorderFenceScale = 2f;   // 外围围栏 4x2 单位
    private const float PenFenceScale = 1f;      // 羊圈栅栏 2x1 单位
    private const int ExitUnlockFlockSize = 100;
    private const int WolfUnlockFlockSize = 6;

    private static readonly Vector2[] TutorialSheepPositions =
    {
        new Vector2(-6.8f, 3.3f),
        new Vector2(-2.6f, 3.4f),
        new Vector2(4.8f, 3.4f),
        new Vector2(7.2f, 0.4f),
        new Vector2(0.5f, -3.6f)
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
        "BorderFence",
        "TutorialPen",
        "AlphaCanvas",
        "EventSystem",
        "AlphaFlockExpansionController"
    };

    [MenuItem("Game Jam/Alpha Flock Expansion/Setup Scene")]
    public static void SetupScene()
    {
        Scene scene = OpenOrCreateScene();
        BuildScene(scene);
        Selection.activeGameObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "SheepFlock");
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
        ObstacleDefinition penFenceDefinition = LoadRequired<ObstacleDefinition>(WorldObstaclePrefabBuilder.PenFenceDefinitionPath);
        ObstacleDefinition borderFenceDefinition = LoadRequired<ObstacleDefinition>(WorldObstaclePrefabBuilder.BorderFenceDefinitionPath);

        ClearManagedObjects(scene);

        CreateWorld(scene);
        WorldSeed worldSeed = CreateWorldSeed(scene);
        BorderFenceRing borderRing = CreateBorderFence(scene, fencePrefab, borderFenceDefinition);
        TutorialPen tutorialPen = CreateTutorialPen(scene, fencePrefab, penFenceDefinition, recruitablePrefab);
        WorldDebrisSpawner debris = CreateDebrisSpawner(scene, worldSeed);

        GameObject flockObject = CreateFlock(scene, memberPrefab, out FlockController flock, out FlockMovementController movement);
        CameraFollow2D cameraFollow = ConfigureCamera(scene, flockObject.transform, out Camera gameplayCamera);
        ConfigureLighting(scene);
        ProgressiveSheepSpawner sheepSpawner = CreateSheepSpawner(scene, flock, recruitablePrefab, namePool, gameplayCamera, worldSeed);
        CreateWolfSystem(scene, flock, wolfPrefab.GetComponent<Wolf>(), out WolfSpawner wolfSpawner, out WolfEventDirector director);
        CreateCanvas(scene, director, out Canvas canvas, out AlphaBannerView banner);
        CreateGameController(scene, flock, movement, sheepSpawner, cameraFollow, wolfSpawner, director, borderRing, tutorialPen, banner, canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Alpha 羊群扩张场景已生成：{ScenePath}。" +
            $"地图 {WorldRect.width}x{WorldRect.height}，出生羊圈 5 只教程羊，狼在 {WolfUnlockFlockSize} 只后出现，" +
            $"历史最高 {ExitUnlockFlockSize} 只后可撞开外围围栏冲出草原。");
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
                || root.name.StartsWith("Wolf_")
                || root.name.StartsWith("AlphaWildSheep_");
            if (managed)
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
        background.transform.position = new Vector3(WorldRect.center.x, WorldRect.center.y, 0f);
        SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = grass;
        // 放在 Background 排序层最底下：碎掉的围栏 / 木桶会切到 Background 层（order 10），要能画在草地上面。
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = -100;
        if (grass != null)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = WorldRect.size;
        }
        else
        {
            renderer.color = new Color(0.28f, 0.46f, 0.25f, 1f);
        }
    }

    /// <summary>草原背景需要 Full Rect 网格才能平铺；不是的话改导入设置后重新导入。</summary>
    private static Sprite LoadGrassSprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(GrassBackgroundPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"找不到草原背景 {GrassBackgroundPath}，改用纯色。");
            return null;
        }

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

        if (changed)
        {
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(GrassBackgroundPath);
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

        GameObject sheepRoot = new GameObject("TutorialSheep");
        sheepRoot.transform.SetParent(root.transform, false);
        for (int index = 0; index < TutorialSheepPositions.Length; index++)
        {
            GameObject sheep = (GameObject)PrefabUtility.InstantiatePrefab(recruitablePrefab, sheepRoot.transform);
            sheep.name = $"TutorialSheep_{index + 1}";
            Vector2 position = TutorialSheepPositions[index];
            sheep.transform.position = new Vector3(position.x, position.y, 0f);
        }

        GameObject signs = CreateTutorialSigns(root.transform);

        TutorialPen pen = root.AddComponent<TutorialPen>();
        pen.Configure(PenRect, fences.ToArray(), signs);
        return pen;
    }

    /// <summary>草地上的教程标识（占位：粉笔色文字 + 键帽方块；美术出图后替换 Sprite 即可）。</summary>
    private static GameObject CreateTutorialSigns(Transform parent)
    {
        GameObject signs = new GameObject("TutorialSigns");
        signs.transform.SetParent(parent, false);

        Sprite keycapSprite = AssetDatabase.LoadAllAssetsAtPath(WarningRectAssetPath).OfType<Sprite>().FirstOrDefault();
        Color chalk = new Color(0.96f, 0.94f, 0.85f, 0.85f);

        // 1. 移动（放在左下，避开左上角的调试 HUD）
        Vector2 moveOrigin = new Vector2(-5.5f, -1.6f);
        CreateWorldText(signs.transform, "Move_Title", "移动", moveOrigin + new Vector2(0f, 2.2f), 1.1f, chalk);
        CreateKeycap(signs.transform, keycapSprite, "W", moveOrigin + new Vector2(0f, 0.9f), chalk);
        CreateKeycap(signs.transform, keycapSprite, "A", moveOrigin + new Vector2(-1.1f, -0.2f), chalk);
        CreateKeycap(signs.transform, keycapSprite, "S", moveOrigin + new Vector2(0f, -0.2f), chalk);
        CreateKeycap(signs.transform, keycapSprite, "D", moveOrigin + new Vector2(1.1f, -0.2f), chalk);

        // 2. 招募
        Vector2 recruitOrigin = new Vector2(1.5f, 1.0f);
        CreateWorldText(signs.transform, "Recruit_Title", "碰到羊 → 加入羊群", recruitOrigin + new Vector2(0f, 1.6f), 0.9f, chalk);
        CreateWorldText(signs.transform, "Recruit_Hint", "把它们都收进来", recruitOrigin + new Vector2(0f, 0.7f), 0.6f, chalk);

        // 3. 撞栅栏
        Vector2 fenceOrigin = new Vector2(5.2f, -3.2f);
        CreateWorldText(signs.transform, "Fence_Title", "羊够 6 只 → 撞开栅栏", fenceOrigin + new Vector2(-1.2f, 1.2f), 0.85f, chalk);
        CreateKeycap(signs.transform, keycapSprite, "E", fenceOrigin + new Vector2(0.4f, 0f), chalk);
        CreateWorldText(signs.transform, "Fence_Arrow", "→", fenceOrigin + new Vector2(2.0f, 0f), 1.4f, chalk);

        return signs;
    }

    private static void CreateWorldText(Transform parent, string name, string text, Vector2 position, float scale, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        textObject.transform.position = new Vector3(position.x, position.y, 0f);
        textObject.transform.localScale = Vector3.one * scale;

        TextMeshPro label = textObject.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.sortingOrder = -50;
        label.rectTransform.sizeDelta = new Vector2(12f, 2f);
        // 中文字形靠 MvpTmpUiFont 在运行时注册的全局 fallback，这里不直接指定运行时字体（不可序列化）。
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
            renderer.color = new Color(color.r, color.g, color.b, 0.35f);
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

        GameObject flower = AssetDatabase.LoadAssetAtPath<GameObject>(WorldObstaclePrefabBuilder.FlowerPrefabPath);
        GameObject rock = AssetDatabase.LoadAssetAtPath<GameObject>(WorldObstaclePrefabBuilder.RockPrefabPath);
        GameObject barrel = AssetDatabase.LoadAssetAtPath<GameObject>(WorldObstaclePrefabBuilder.BarrelPrefabPath);

        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("worldSeed").objectReferenceValue = worldSeed;
        SerializedProperty entries = serialized.FindProperty("entries");
        entries.arraySize = 3;
        SetDebrisEntry(entries.GetArrayElementAtIndex(0), flower, 5f, 1.0f);
        SetDebrisEntry(entries.GetArrayElementAtIndex(1), rock, 2f, 1.5f);
        SetDebrisEntry(entries.GetArrayElementAtIndex(2), barrel, 1.5f, 1.5f);
        serialized.FindProperty("area").rectValue = WorldRect;
        SerializedProperty zones = serialized.FindProperty("exclusionZones");
        zones.arraySize = 1;
        zones.GetArrayElementAtIndex(0).rectValue = Expand(PenRect, 4f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }

    private static void SetDebrisEntry(SerializedProperty entry, GameObject prefab, float weight, float clearance)
    {
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("weight").floatValue = weight;
        entry.FindPropertyRelative("clearance").floatValue = clearance;
    }

    // ------------------------------------------------------------------ flock & spawners

    private static GameObject CreateFlock(
        Scene scene,
        GameObject sheepPrefab,
        out FlockController flock,
        out FlockMovementController movement)
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
        GameObject recruitablePrefab,
        SheepNamePool namePool,
        Camera gameplayCamera,
        WorldSeed worldSeed)
    {
        GameObject spawnerObject = new GameObject("ProgressiveSheepSpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        ProgressiveSheepSpawner spawner = spawnerObject.AddComponent<ProgressiveSheepSpawner>();

        RecruitableSheep common = recruitablePrefab.GetComponent<RecruitableSheep>();
        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("flock").objectReferenceValue = flock;
        serialized.FindProperty("sheepPrefab").objectReferenceValue = common;
        serialized.FindProperty("namePool").objectReferenceValue = namePool;
        serialized.FindProperty("gameplayCamera").objectReferenceValue = gameplayCamera;
        serialized.FindProperty("worldSeed").objectReferenceValue = worldSeed;
        serialized.FindProperty("spawnAreaCenter").vector2Value = WorldRect.center;
        serialized.FindProperty("spawnAreaSize").vector2Value = WorldRect.size;

        // 普通 90%，四种特殊羊各 2.5%。
        (string file, string typeId, string displayName, float weight)[] table =
        {
            ("RecruitableSheep", MvpSheepCatalog.DefaultTypeId, "普通羊", 90f),
            ("SpecialSheep_Black", "sheep.special.black", "黑羊", 2.5f),
            ("SpecialSheep_Horned", "sheep.special.horned", "角羊", 2.5f),
            ("SpecialSheep_TopHat", "sheep.special.tophat", "礼帽羊", 2.5f),
            ("SpecialSheep_RedBow", "sheep.special.redbow", "红蝴蝶结羊", 2.5f)
        };

        SerializedProperty types = serialized.FindProperty("sheepTypes");
        types.arraySize = 0;
        foreach ((string file, string typeId, string displayName, float weight) in table)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{SpecialSheepFolder}/{file}.prefab");
            RecruitableSheep recruitable = prefab != null ? prefab.GetComponent<RecruitableSheep>() : null;
            if (recruitable == null)
            {
                Debug.LogWarning($"Sheep prefab {file} not found or has no RecruitableSheep; skipped.");
                continue;
            }

            types.arraySize++;
            SerializedProperty entry = types.GetArrayElementAtIndex(types.arraySize - 1);
            entry.FindPropertyRelative("prefab").objectReferenceValue = recruitable;
            entry.FindPropertyRelative("typeId").stringValue = typeId;
            entry.FindPropertyRelative("displayName").stringValue = displayName;
            entry.FindPropertyRelative("weight").floatValue = weight;
        }

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
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ UI

    private static void CreateCanvas(Scene scene, WolfEventDirector director, out Canvas canvas, out AlphaBannerView banner)
    {
        GameObject canvasObject = new GameObject("AlphaCanvas", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        // Dev Scene 模板里没有 EventSystem，结算页按钮需要它。
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // 狼群节奏 HUD：只在狼嚎 / 攻击 / 跑路期间显示。
        RectTransform hudRect = MvpUiFactory.CreateRect("WolfEventHud", canvasObject.transform);
        MvpUiFactory.Anchor(hudRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(560f, 80f));
        hudRect.pivot = new Vector2(0.5f, 1f);
        hudRect.anchoredPosition = new Vector2(0f, -16f);
        WolfEventHudView hud = hudRect.gameObject.AddComponent<WolfEventHudView>();
        SerializedObject hudSerialized = new SerializedObject(hud);
        hudSerialized.FindProperty("director").objectReferenceValue = director;
        hudSerialized.FindProperty("showOnlyDuringEvent").boolValue = true;
        hudSerialized.ApplyModifiedPropertiesWithoutUndo();

        // 提示横幅：狼群 HUD 下方。
        RectTransform bannerRect = MvpUiFactory.CreateRect("AlphaBanner", canvasObject.transform);
        MvpUiFactory.Anchor(bannerRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 64f));
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -110f);
        banner = bannerRect.gameObject.AddComponent<AlphaBannerView>();
    }

    private static void CreateGameController(
        Scene scene,
        FlockController flock,
        FlockMovementController movement,
        ProgressiveSheepSpawner sheepSpawner,
        CameraFollow2D cameraFollow,
        WolfSpawner wolfSpawner,
        WolfEventDirector director,
        BorderFenceRing borderRing,
        TutorialPen tutorialPen,
        AlphaBannerView banner,
        Canvas canvas)
    {
        GameObject controllerObject = new GameObject("AlphaFlockExpansionController");
        SceneManager.MoveGameObjectToScene(controllerObject, scene);
        AlphaFlockExpansionController controller = controllerObject.AddComponent<AlphaFlockExpansionController>();

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("flock").objectReferenceValue = flock;
        serialized.FindProperty("flockMovement").objectReferenceValue = movement;
        serialized.FindProperty("sheepSpawner").objectReferenceValue = sheepSpawner;
        serialized.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
        serialized.FindProperty("wolfDirector").objectReferenceValue = director;
        serialized.FindProperty("wolfSpawner").objectReferenceValue = wolfSpawner;
        serialized.FindProperty("borderRing").objectReferenceValue = borderRing;
        serialized.FindProperty("tutorialPen").objectReferenceValue = tutorialPen;
        serialized.FindProperty("bannerView").objectReferenceValue = banner;
        serialized.FindProperty("uiCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("wolfUnlockFlockSize").intValue = WolfUnlockFlockSize;
        serialized.FindProperty("exitUnlockFlockSize").intValue = ExitUnlockFlockSize;
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
}

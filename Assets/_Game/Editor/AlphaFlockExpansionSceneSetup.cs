using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the isolated AlphaFlockExpansion development scene without touching Level_01.
/// </summary>
public static class AlphaFlockExpansionSceneSetup
{
    private const string SceneFolder = "Assets/_Game/Scenes/Dev";
    private const string ScenePath = SceneFolder + "/AlphaFlockExpansion.unity";
    private const string SceneTemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    private const string GridAssetPath = "Assets/_Game/Content/Art/Prototype/WorldGrid.asset";
    private const string SheepMemberPrefabPath = "Assets/_Game/Content/Perfabs/Sheep/SheepMember.prefab";
    private const string RecruitableSheepPrefabPath = "Assets/_Game/Content/Perfabs/Sheep/RecruitableSheep.prefab";
    private const string WolfPrefabPath = "Assets/_Game/Content/Perfabs/Wolf/Wolf.prefab";
    private const string NamePoolPath = "Assets/_Game/Content/Data/SheepNamePool.asset";

    private static readonly string[] ManagedRootNames =
    {
        "AlphaWorld",
        "SheepFlock",
        "ProgressiveSheepSpawner",
        "WolfSpawner",
        "AlphaFlockExpansionController"
    };

    [MenuItem("Game Jam/Alpha Flock Expansion/Setup Scene")]
    public static void SetupScene()
    {
        Scene scene = OpenOrCreateScene();
        BuildScene(scene);
        Selection.activeGameObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "SheepFlock");
    }

    private static void BuildScene(Scene scene)
    {
        EnsureFolder(SceneFolder);

        GameObject memberPrefab = LoadRequired<GameObject>(SheepMemberPrefabPath);
        GameObject recruitablePrefab = LoadRequired<GameObject>(RecruitableSheepPrefabPath);
        GameObject wolfPrefab = LoadRequired<GameObject>(WolfPrefabPath);
        SheepNamePool namePool = LoadRequired<SheepNamePool>(NamePoolPath);

        ClearManagedObjects(scene);
        CreateWorld(scene);

        GameObject flockObject = CreateFlock(scene, memberPrefab, out FlockController flock, out FlockMovementController movement);
        CameraFollow2D cameraFollow = ConfigureCamera(scene, flockObject.transform, out Camera gameplayCamera);
        ProgressiveSheepSpawner sheepSpawner = CreateSheepSpawner(
            scene,
            flock,
            recruitablePrefab.GetComponent<RecruitableSheep>(),
            namePool,
            gameplayCamera);
        WolfSpawner wolfSpawner = CreateWolfSpawner(scene, flock, wolfPrefab.GetComponent<Wolf>());
        CreateGameController(scene, flock, movement, sheepSpawner, cameraFollow, wolfSpawner);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Alpha 羊群扩张场景已生成：{ScenePath}。" +
            "开局 1 只羊，阶段批次为 1 / 2-3 / 5-7 / 10-15 / 20-30，狼在 20 只后出现。");
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
            if (ManagedRootNames.Contains(root.name))
                Object.DestroyImmediate(root);
        }
    }

    private static void CreateWorld(Scene scene)
    {
        GameObject world = new("AlphaWorld");
        SceneManager.MoveGameObjectToScene(world, scene);

        Sprite gridSprite = AssetDatabase.LoadAllAssetsAtPath(GridAssetPath).OfType<Sprite>().FirstOrDefault();
        if (gridSprite == null)
            return;

        GameObject grid = new("BackgroundGrid");
        grid.transform.SetParent(world.transform, false);
        grid.transform.localScale = new Vector3(6f, 6f, 1f);
        SpriteRenderer renderer = grid.AddComponent<SpriteRenderer>();
        renderer.sprite = gridSprite;
        renderer.color = new Color(0.72f, 0.9f, 0.68f, 1f);
        renderer.sortingOrder = -200;
    }

    private static GameObject CreateFlock(
        Scene scene,
        GameObject sheepPrefab,
        out FlockController flock,
        out FlockMovementController movement)
    {
        GameObject flockObject = new("SheepFlock");
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

        SerializedObject serialized = new(flock);
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
        RecruitableSheep sheepPrefab,
        SheepNamePool namePool,
        Camera gameplayCamera)
    {
        GameObject spawnerObject = new("ProgressiveSheepSpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        ProgressiveSheepSpawner spawner = spawnerObject.AddComponent<ProgressiveSheepSpawner>();

        SerializedObject serialized = new(spawner);
        serialized.FindProperty("flock").objectReferenceValue = flock;
        serialized.FindProperty("sheepPrefab").objectReferenceValue = sheepPrefab;
        serialized.FindProperty("namePool").objectReferenceValue = namePool;
        serialized.FindProperty("gameplayCamera").objectReferenceValue = gameplayCamera;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }

    private static WolfSpawner CreateWolfSpawner(Scene scene, FlockController flock, Wolf wolfPrefab)
    {
        GameObject spawnerObject = new("WolfSpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        WolfSpawner spawner = spawnerObject.AddComponent<WolfSpawner>();

        SerializedObject serialized = new(spawner);
        serialized.FindProperty("flock").objectReferenceValue = flock;
        serialized.FindProperty("wolfPrefab").objectReferenceValue = wolfPrefab;
        serialized.FindProperty("spawnDistance").floatValue = 16f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }

    private static void CreateGameController(
        Scene scene,
        FlockController flock,
        FlockMovementController movement,
        ProgressiveSheepSpawner sheepSpawner,
        CameraFollow2D cameraFollow,
        WolfSpawner wolfSpawner)
    {
        GameObject controllerObject = new("AlphaFlockExpansionController");
        SceneManager.MoveGameObjectToScene(controllerObject, scene);
        AlphaFlockExpansionController controller = controllerObject.AddComponent<AlphaFlockExpansionController>();

        SerializedObject serialized = new(controller);
        serialized.FindProperty("flock").objectReferenceValue = flock;
        serialized.FindProperty("flockMovement").objectReferenceValue = movement;
        serialized.FindProperty("sheepSpawner").objectReferenceValue = sheepSpawner;
        serialized.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
        serialized.FindProperty("wolfSpawner").objectReferenceValue = wolfSpawner;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static CameraFollow2D ConfigureCamera(Scene scene, Transform target, out Camera camera)
    {
        camera = scene.GetRootGameObjects()
            .Select(root => root.GetComponentInChildren<Camera>(true))
            .FirstOrDefault(found => found != null);

        if (camera == null)
        {
            GameObject cameraObject = new("Main Camera");
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

        SerializedObject serialized = new(follow);
        serialized.FindProperty("target").objectReferenceValue = target;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(camera);
        return follow;
    }

    private static T LoadRequired<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new System.InvalidOperationException($"Missing required asset at {path}.");
        return asset;
    }

    private static void EnsureFolder(string path)
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

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SheepMovementPrototypeSetup
{
    private const string ScenePath = "Assets/_Game/Scenes/TestLevel.unity";
    private const string PrototypeFolder = "Assets/_Game/Content/Art/Prototype";
    private const string CircleAssetPath = PrototypeFolder + "/SheepCircle.asset";
    private const string RectangleAssetPath = PrototypeFolder + "/SheepRectangle.asset";
    private const string GridAssetPath = PrototypeFolder + "/WorldGrid.asset";
    private const string SheepPrefabFolder = "Assets/_Game/Content/Perfabs/Sheep";
    private const string RecruitablePrefabPath = SheepPrefabFolder + "/RecruitableSheep.prefab";
    private const string MemberPrefabPath = SheepPrefabFolder + "/SheepMember.prefab";

    [MenuItem("Game Jam/Sheep MVP/Setup TestLevel Flock")]
    public static void SetupTestLevel()
    {
        EnsureFolder(PrototypeFolder);
        Sprite circleSprite = GetOrCreateCircleSprite();
        Scene scene = OpenTestLevel();

        GameObject flockObject = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "SheepFlock");
        if (flockObject == null)
        {
            flockObject = new GameObject("SheepFlock");
            SceneManager.MoveGameObjectToScene(flockObject, scene);
        }
        flockObject.transform.localScale = Vector3.one;

        GameObject initialSheep = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Sheep_Initial");
        if (initialSheep == null)
        {
            initialSheep = new GameObject("Sheep_Initial");
            SceneManager.MoveGameObjectToScene(initialSheep, scene);
        }

        initialSheep.transform.SetPositionAndRotation(flockObject.transform.position, Quaternion.identity);
        ApplyCircleAppearance(initialSheep, circleSprite, 1.25f, true);

        SpriteRenderer renderer = initialSheep.GetComponent<SpriteRenderer>();
        renderer.color = new Color(0.92f, 0.92f, 0.82f, 1f);

        Rigidbody2D body = GetOrAdd<Rigidbody2D>(initialSheep);
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        SheepMember initialMember = GetOrAdd<SheepMember>(initialSheep);
        GetOrAdd<SheepIdentity>(initialSheep);
        FlockMovementController movement = GetOrAdd<FlockMovementController>(flockObject);
        FlockController flock = GetOrAdd<FlockController>(flockObject);
        ConfigureFlock(flock, movement, initialMember);

        CameraFollow2D cameraFollow = Object.FindAnyObjectByType<CameraFollow2D>();
        if (cameraFollow != null && cameraFollow.gameObject.scene == scene)
        {
            SerializedObject cameraObject = new SerializedObject(cameraFollow);
            cameraObject.FindProperty("target").objectReferenceValue = flockObject.transform;
            cameraObject.ApplyModifiedPropertiesWithoutUndo();
        }

        Selection.activeGameObject = flockObject;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("TestLevel flock is ready: input controls SheepFlock, not an individual sheep.");
    }

    [MenuItem("Game Jam/Sheep MVP/Apply Circle Visuals To Existing Sheep")]
    public static void ApplyCircleVisualsToExistingSheep()
    {
        EnsureFolder(PrototypeFolder);
        Sprite circleSprite = GetOrCreateCircleSprite();

        UpdatePrefabAppearance(MemberPrefabPath, circleSprite, 1.25f, true);
        UpdatePrefabAppearance(RecruitablePrefabPath, circleSprite, 1.1f, true);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SheepMember member in root.GetComponentsInChildren<SheepMember>(true))
                {
                    ApplyCircleAppearance(member.gameObject, circleSprite, 1.25f, true);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Circle visuals were applied to sheep assets and loaded flock members.");
    }

    [MenuItem("Game Jam/Sheep MVP/Add First Recruitable Sheep")]
    public static void AddFirstRecruitableSheep()
    {
        EnsureFolder(PrototypeFolder);
        EnsureFolder(SheepPrefabFolder);

        Sprite circleSprite = GetOrCreateCircleSprite();
        GameObject prefab = CreateRecruitablePrefab(circleSprite);
        Scene scene = OpenTestLevel();
        GameObject sheep = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "RecruitableSheep_01");

        if (sheep == null)
        {
            sheep = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            sheep.name = "RecruitableSheep_01";
        }

        sheep.transform.SetPositionAndRotation(new Vector3(3f, 0f, 0f), Quaternion.identity);
        Selection.activeGameObject = sheep;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("First recruitable sheep is ready. Any flock member can recruit it by contact.");
    }

    [MenuItem("Game Jam/Sheep MVP/Setup Obstacle And Grid")]
    public static void SetupObstacleAndGrid()
    {
        Scene scene = OpenTestLevel();
        Sprite rectangleSprite = AssetDatabase.LoadAllAssetsAtPath(RectangleAssetPath)
            .OfType<Sprite>()
            .FirstOrDefault();
        if (rectangleSprite == null)
        {
            throw new System.InvalidOperationException(
                $"Missing rectangle sprite at {RectangleAssetPath}.");
        }
        Sprite gridSprite = GetOrCreateGridSprite();

        GameObject flockObject = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "SheepFlock");
        if (flockObject == null)
        {
            throw new System.InvalidOperationException("SheepFlock must exist before adding the obstacle.");
        }
        flockObject.transform.localScale = Vector3.one;

        Rigidbody2D flockBody = GetOrAdd<Rigidbody2D>(flockObject);
        flockBody.bodyType = RigidbodyType2D.Kinematic;
        flockBody.gravityScale = 0f;
        flockBody.freezeRotation = true;
        flockBody.useFullKinematicContacts = true;
        flockBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D centerTrigger = GetOrAdd<CircleCollider2D>(flockObject);
        centerTrigger.isTrigger = true;
        centerTrigger.radius = 0.35f;

        ReplaceGeneratedGrid(scene, gridSprite);
        ReplaceSizeObstacle(scene, rectangleSprite);

        Selection.activeGameObject = flockObject;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("TestLevel grid and six-member breakable obstacle are ready.");
    }

    private static Scene OpenTestLevel()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == ScenePath) return activeScene;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            throw new System.OperationCanceledException("TestLevel setup was cancelled.");
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static Sprite GetOrCreateCircleSprite()
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(CircleAssetPath).OfType<Sprite>().FirstOrDefault();
        if (sprite != null) return sprite;

        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = "SheepCircleTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[16 * 16];
        const float center = 7.5f;
        const float radiusSquared = center * center;
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float deltaX = x - center;
                float deltaY = y - center;
                pixels[y * 16 + x] = deltaX * deltaX + deltaY * deltaY <= radiusSquared
                    ? Color.white
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, CircleAssetPath);

        sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
        sprite.name = "SheepCircleSprite";
        AssetDatabase.AddObjectToAsset(sprite, texture);
        AssetDatabase.ImportAsset(CircleAssetPath);
        return sprite;
    }

    private static Sprite GetOrCreateGridSprite()
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(GridAssetPath)
            .OfType<Sprite>()
            .FirstOrDefault();
        if (sprite != null)
            return sprite;

        const int pixelsPerUnit = 8;
        const int gridStep = pixelsPerUnit * 2;
        const int width = 28 * pixelsPerUnit;
        const int height = 20 * pixelsPerUnit;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "WorldGridTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[width * height];
        Color background = new Color(0.18f, 0.28f, 0.2f, 1f);
        Color gridLine = new Color(0.25f, 0.36f, 0.28f, 1f);
        Color axisLine = new Color(0.38f, 0.5f, 0.41f, 1f);
        int centerX = width / 2;
        int centerY = height / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isAxis = x == centerX || y == centerY;
                bool isGridLine = x % gridStep == 0 || y % gridStep == 0;
                pixels[y * width + x] = isAxis
                    ? axisLine
                    : isGridLine ? gridLine : background;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, GridAssetPath);

        sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
        sprite.name = "WorldGridSprite";
        AssetDatabase.AddObjectToAsset(sprite, texture);
        AssetDatabase.ImportAsset(GridAssetPath);
        return sprite;
    }

    private static void ReplaceGeneratedGrid(Scene scene, Sprite sprite)
    {
        const string gridRootName = "SheepMvp_WorldGrid";
        GameObject existing = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == gridRootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var gridRoot = new GameObject(gridRootName);
        SceneManager.MoveGameObjectToScene(gridRoot, scene);

        CreateSpriteObject(
            "BackgroundGrid",
            gridRoot.transform,
            sprite,
            new Vector3(0f, 0f, 0f),
            Vector3.one,
            Color.white,
            -200);
    }

    private static void ReplaceSizeObstacle(Scene scene, Sprite sprite)
    {
        const string obstacleName = "SheepMvp_SizeGate";
        GameObject existing = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == obstacleName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject obstacle = CreateSpriteObject(
            obstacleName,
            null,
            sprite,
            new Vector3(7.5f, 0f, 0f),
            new Vector3(0.8f, 6f, 1f),
            new Color(0.55f, 0.28f, 0.12f, 1f),
            10);
        SceneManager.MoveGameObjectToScene(obstacle, scene);

        BoxCollider2D trigger = obstacle.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;

        Rigidbody2D body = obstacle.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;

        obstacle.AddComponent<FlockSizeObstacle>();
    }

    private static GameObject CreateSpriteObject(
        string name,
        Transform parent,
        Sprite sprite,
        Vector3 localPosition,
        Vector3 localScale,
        Color color,
        int sortingOrder)
    {
        var target = new GameObject(name);
        target.transform.SetParent(parent, false);
        target.transform.localPosition = localPosition;
        target.transform.localScale = localScale;

        SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return target;
    }

    private static GameObject CreateRecruitablePrefab(Sprite sprite)
    {
        var root = new GameObject("RecruitableSheep");
        try
        {
            ApplyCircleAppearance(root, sprite, 1.1f, true);
            root.GetComponent<SpriteRenderer>().color = new Color(0.72f, 0.82f, 1f, 1f);
            root.AddComponent<RecruitableSheep>();
            root.AddComponent<SheepIdentity>();
            return PrefabUtility.SaveAsPrefabAsset(root, RecruitablePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void UpdatePrefabAppearance(
        string prefabPath,
        Sprite sprite,
        float uniformScale,
        bool isTrigger)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return;

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ApplyCircleAppearance(prefabRoot, sprite, uniformScale, isTrigger);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ApplyCircleAppearance(
        GameObject sheep,
        Sprite sprite,
        float uniformScale,
        bool isTrigger)
    {
        SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(sheep);
        renderer.sprite = sprite;
        sheep.transform.localScale = new Vector3(uniformScale, uniformScale, sheep.transform.localScale.z);

        CircleCollider2D circleCollider = GetOrAdd<CircleCollider2D>(sheep);
        circleCollider.isTrigger = isTrigger;
        circleCollider.radius = 0.5f;

        BoxCollider2D boxCollider = sheep.GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Object.DestroyImmediate(boxCollider);
        }

        EditorUtility.SetDirty(sheep);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(circleCollider);
    }

    private static void ConfigureFlock(
        FlockController flock,
        FlockMovementController movement,
        SheepMember initialMember)
    {
        SerializedObject flockObject = new SerializedObject(flock);
        flockObject.FindProperty("movementController").objectReferenceValue = movement;
        SerializedProperty startingMembers = flockObject.FindProperty("startingMembers");
        startingMembers.arraySize = 1;
        startingMembers.GetArrayElementAtIndex(0).objectReferenceValue = initialMember;
        flockObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}

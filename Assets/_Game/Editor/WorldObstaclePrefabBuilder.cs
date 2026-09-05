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

    public const string FencePrefabPath = PrefabFolder + "/Obstacle_Fence.prefab";
    public const string BarrelPrefabPath = PrefabFolder + "/Obstacle_Barrel.prefab";
    public const string RockPrefabPath = PrefabFolder + "/Obstacle_Rock.prefab";
    public const string FlowerPrefabPath = PrefabFolder + "/Obstacle_Flower.prefab";

    public const string FenceDefinitionPath = DefinitionFolder + "/obstacle.fence.asset";
    public const string PenFenceDefinitionPath = DefinitionFolder + "/obstacle.pen_fence.asset";
    public const string BorderFenceDefinitionPath = DefinitionFolder + "/obstacle.border_fence.asset";
    public const string BarrelDefinitionPath = DefinitionFolder + "/obstacle.barrel.asset";
    public const string RockDefinitionPath = DefinitionFolder + "/obstacle.rock.asset";
    public const string FlowerDefinitionPath = DefinitionFolder + "/obstacle.flower.asset";

    [MenuItem("Game Jam/World/Build Obstacle Prefabs")]
    public static void BuildAll()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(DefinitionFolder);

        Sprite fence = LoadSprite("obstacle_fence_256x128");
        Sprite fenceBroken = LoadSprite("obstacle_fence_broken_256x128");
        Sprite barrel = LoadSprite("obstacle_barrel_128");
        Sprite barrelBroken = LoadSprite("obstacle_barrel_broken_128");
        Sprite rock = LoadSprite("obstacle_rock_128");
        Sprite rockBroken = LoadSprite("obstacle_rock_broken_128");
        Sprite flower = LoadSprite("obstacle_flower_64");

        ObstacleDefinition fenceDefinition = GetOrCreateDefinition(
            FenceDefinitionPath, "obstacle.fence", "围栏", ObstacleSizeCategory.Medium,
            ObstacleBreakRule.RequireCountAndInteract, 5, ObstacleCountSource.CurrentFlockCount,
            ObstacleBrokenBehavior.BecomeBackground, fenceBroken, createOnly: true);
        ObstacleDefinition penFenceDefinition = GetOrCreateDefinition(
            PenFenceDefinitionPath, "obstacle.pen_fence", "羊圈栅栏", ObstacleSizeCategory.Medium,
            ObstacleBreakRule.RequireCountAndInteract, 6, ObstacleCountSource.CurrentFlockCount,
            ObstacleBrokenBehavior.BecomeBackground, fenceBroken);
        ObstacleDefinition borderFenceDefinition = GetOrCreateDefinition(
            BorderFenceDefinitionPath, "obstacle.border_fence", "外围围栏", ObstacleSizeCategory.Large,
            ObstacleBreakRule.RequireCountAndInteract, 100, ObstacleCountSource.HighestFlockCountThisRun,
            ObstacleBrokenBehavior.BecomeBackground, fenceBroken);
        ObstacleDefinition barrelDefinition = GetOrCreateDefinition(
            BarrelDefinitionPath, "obstacle.barrel", "木桶", ObstacleSizeCategory.Small,
            ObstacleBreakRule.OnAnyContact, 1, ObstacleCountSource.CurrentFlockCount,
            ObstacleBrokenBehavior.BecomeBackground, barrelBroken, createOnly: true);
        ObstacleDefinition rockDefinition = GetOrCreateDefinition(
            RockDefinitionPath, "obstacle.rock", "石头", ObstacleSizeCategory.Small,
            ObstacleBreakRule.OnAnyContact, 1, ObstacleCountSource.CurrentFlockCount,
            ObstacleBrokenBehavior.BecomeBackground, rockBroken, createOnly: true);
        ObstacleDefinition flowerDefinition = GetOrCreateDefinition(
            FlowerDefinitionPath, "obstacle.flower", "花", ObstacleSizeCategory.Small,
            ObstacleBreakRule.OnAnyContact, 1, ObstacleCountSource.CurrentFlockCount,
            ObstacleBrokenBehavior.Disappear, null, createOnly: true);

        BuildFencePrefab(FencePrefabPath, fence, fenceDefinition);
        BuildDebrisPrefab(BarrelPrefabPath, "Obstacle_Barrel", barrel, barrelDefinition, 0.42f);
        BuildDebrisPrefab(RockPrefabPath, "Obstacle_Rock", rock, rockDefinition, 0.42f);
        BuildDebrisPrefab(FlowerPrefabPath, "Obstacle_Flower", flower, flowerDefinition, 0.25f);

        AssetDatabase.SaveAssets();
        Debug.Log($"Obstacle prefabs built under {PrefabFolder}; definitions under {DefinitionFolder}.");
    }

    /// <summary>围栏：Blocking 层实体碰撞体挡路 + 稍大的 Trigger 供羊群交互。</summary>
    private static void BuildFencePrefab(string path, Sprite sprite, ObstacleDefinition definition)
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

            FenceObstacle fence = root.AddComponent<FenceObstacle>();
            SerializedObject fenceSerialized = new SerializedObject(fence);
            fenceSerialized.FindProperty("breakable").objectReferenceValue = breakable;
            // 羊圈 / 外围围栏要按 E 才碎（教程里教的就是这个）。
            SerializedProperty breakOnContact = fenceSerialized.FindProperty("breakOnContact");
            if (breakOnContact != null)
                breakOnContact.boolValue = false;
            fenceSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>花 / 石头 / 木桶：碰到即碎，不挡路（Default 层，只有 Trigger）。</summary>
    private static void BuildDebrisPrefab(string path, string name, Sprite sprite, ObstacleDefinition definition, float radius)
    {
        GameObject root = new GameObject(name);
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 2;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = radius;

            BreakableObstacle breakable = root.AddComponent<BreakableObstacle>();
            SerializedObject serialized = new SerializedObject(breakable);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
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
        serialized.FindProperty("countSource").enumValueIndex = (int)countSource;
        serialized.FindProperty("brokenBehavior").enumValueIndex = (int)broken;
        serialized.FindProperty("brokenSortingLayer").stringValue = "Background";
        serialized.FindProperty("brokenSortingOrder").intValue = 10;
        serialized.FindProperty("brokenSprite").objectReferenceValue = brokenSprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = $"{DebrisFolder}/{fileName}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"Sprite not found: {path}");
        return sprite;
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

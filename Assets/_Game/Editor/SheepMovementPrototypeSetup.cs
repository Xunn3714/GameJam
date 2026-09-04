using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SheepMovementPrototypeSetup
{
    private const string ScenePath = "Assets/_Game/Scenes/TestLevel.unity";
    private const string PrototypeFolder = "Assets/_Game/Content/Art/Prototype";
    private const string RectangleAssetPath = PrototypeFolder + "/SheepRectangle.asset";
    private const string SheepPrefabFolder = "Assets/_Game/Content/Perfabs/Sheep";
    private const string RecruitablePrefabPath = SheepPrefabFolder + "/RecruitableSheep.prefab";

    [MenuItem("Game Jam/Sheep MVP/Setup TestLevel Rectangle Player")]
    public static void SetupTestLevel()
    {
        if (!AssetDatabase.IsValidFolder(PrototypeFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Game/Content/Art", "Prototype");
        }

        Sprite rectangleSprite = GetOrCreateRectangleSprite();
        Scene scene = OpenTestLevel();
        GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "PlayerSheep");

        if (player == null)
        {
            player = new GameObject("PlayerSheep");
            SceneManager.MoveGameObjectToScene(player, scene);
        }

        player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        player.transform.localScale = new Vector3(1.5f, 1f, 1f);

        SpriteRenderer spriteRenderer = GetOrAdd<SpriteRenderer>(player);
        spriteRenderer.sprite = rectangleSprite;
        spriteRenderer.color = new Color(0.92f, 0.92f, 0.82f, 1f);

        Rigidbody2D body = GetOrAdd<Rigidbody2D>(player);
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D collider = GetOrAdd<BoxCollider2D>(player);
        collider.isTrigger = false;
        collider.size = Vector2.one;

        GetOrAdd<SheepPlayerController>(player);

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("TestLevel rectangle player is ready: WASD moves PlayerSheep.");
    }

    [MenuItem("Game Jam/Sheep MVP/Add First Recruitable Sheep")]
    public static void AddFirstRecruitableSheep()
    {
        EnsureFolder(PrototypeFolder);
        EnsureFolder(SheepPrefabFolder);

        Sprite rectangleSprite = GetOrCreateRectangleSprite();
        GameObject prefab = CreateRecruitablePrefab(rectangleSprite);
        Scene scene = OpenTestLevel();
        GameObject sheep = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "RecruitableSheep_01");

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
        Debug.Log("First recruitable sheep is ready. Touch it with PlayerSheep to recruit it.");
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

    private static Sprite GetOrCreateRectangleSprite()
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(RectangleAssetPath).OfType<Sprite>().FirstOrDefault();
        if (sprite != null) return sprite;

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RectangleAssetPath);
        if (texture == null)
        {
            texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = "SheepRectangleTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(Enumerable.Repeat(Color.white, 16 * 16).ToArray());
            texture.Apply();
            AssetDatabase.CreateAsset(texture, RectangleAssetPath);
        }

        sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
        sprite.name = "SheepRectangleSprite";
        AssetDatabase.AddObjectToAsset(sprite, texture);
        AssetDatabase.ImportAsset(RectangleAssetPath);
        return sprite;
    }

    private static GameObject CreateRecruitablePrefab(Sprite sprite)
    {
        var root = new GameObject("RecruitableSheep");
        try
        {
            root.transform.localScale = new Vector3(1.2f, 0.8f, 1f);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.72f, 0.82f, 1f, 1f);

            CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.65f;

            root.AddComponent<RecruitableSheep>();
            return PrefabUtility.SaveAsPrefabAsset(root, RecruitablePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
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

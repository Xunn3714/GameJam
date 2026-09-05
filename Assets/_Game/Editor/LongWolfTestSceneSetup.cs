using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates a separate long-wolf Dev Scene without rebuilding TestWolf or its shared prefab.</summary>
public static class LongWolfTestSceneSetup
{
    public const string ScenePath = "Assets/_Game/Scenes/Dev/TestLongWolf.unity";
    public const string PrefabPath = "Assets/_Game/Content/Perfabs/Wolf/LongWolf.prefab";
    private const string SourceScene = "Assets/_Game/Scenes/Dev/TestWolf.unity";
    private const string SourcePrefab = "Assets/_Game/Content/Perfabs/Wolf/Wolf.prefab";

    [MenuItem("Game Jam/Wolf Test/Create TestLongWolf Scene")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play before creating TestLongWolf.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            Open();
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            throw new InvalidOperationException("TestWolf scene must exist first.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? CreatePrefab();
        if (!AssetDatabase.CopyAsset(SourceScene, ScenePath))
            throw new InvalidOperationException("Cannot copy TestWolf to TestLongWolf.");

        // Additive preserves any unsaved work in the user's current scene.
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        WolfSpawner[] spawners = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WolfSpawner>(true)).ToArray();
        if (spawners.Length != 1)
            throw new InvalidOperationException("Expected exactly one WolfSpawner in the copied scene.");
        SerializedObject settings = new SerializedObject(spawners[0]);
        settings.FindProperty("wolfPrefab").objectReferenceValue = prefab.GetComponent<Wolf>();
        settings.FindProperty("maxAliveWolves").intValue = 1;
        settings.FindProperty("waitForPreviousWolf").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SceneManager.SetActiveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = spawners[0].gameObject;
        Debug.Log("TestLongWolf created: long-body sweep captures every touched member; no scattering or rescue. Open it alone before Play.");
    }

    [MenuItem("Game Jam/Wolf Test/Open TestLongWolf Scene")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    /// <summary>取现有的 LongWolf prefab，没有就按 Wolf.prefab 生成一份。供多狼编队的测试场景复用。</summary>
    public static GameObject GetOrCreatePrefab()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? CreatePrefab();
    }

    private static GameObject CreatePrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (source == null)
            throw new InvalidOperationException("Missing source Wolf prefab.");
        GameObject root = Object.Instantiate(source);
        root.name = "LongWolf";
        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Wolf wolf = root.GetComponent<Wolf>();
            SerializedObject wolfSettings = new SerializedObject(wolf);
            SpriteRenderer originalBody = (SpriteRenderer)wolfSettings.FindProperty("bodyRenderer").objectReferenceValue;
            SpriteRenderer warning = (SpriteRenderer)wolfSettings.FindProperty("warningRenderer").objectReferenceValue;
            Sprite circle = originalBody.sprite;
            Material material = originalBody.sharedMaterial;
            originalBody.gameObject.SetActive(false);

            Transform body = new GameObject("LongBody").transform;
            body.SetParent(root.transform, false);
            // Warning sprite is left-pivoted; this makes a unit body occupying [-1,0] on X.
            MakeSprite("Torso", body, warning.sprite, material, new Vector2(-1f, 0f), Vector2.one,
                new Color(0.65f, 0.16f, 0.2f), 20);
            for (int i = 0; i < 8; i++)
            {
                float x = -0.08f - i * 0.12f;
                MakeSprite("Stripe_" + i, body, warning.sprite, material, new Vector2(x, 0f), new Vector2(0.035f, 0.84f),
                    new Color(0.85f, 0.27f, 0.25f), 21);
            }
            Transform head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            SpriteRenderer headRenderer = MakeSprite("Face", head, circle, material, new Vector2(-0.3f, 0f), new Vector2(0.9f, 1.1f),
                new Color(0.85f, 0.22f, 0.18f), 22);
            MakeSprite("Snout", head, circle, material, new Vector2(-0.05f, 0f), new Vector2(0.4f, 0.55f), new Color(0.24f, 0.10f, 0.13f), 23);
            foreach (float side in new[] { -1f, 1f })
            {
                MakeSprite("Ear", head, circle, material, new Vector2(-0.55f, side * 0.45f), new Vector2(0.4f, 0.3f),
                    new Color(0.4f, 0.08f, 0.14f), 21);
                MakeSprite("Eye", head, circle, material, new Vector2(-0.2f, side * 0.3f), Vector2.one * 0.18f, Color.yellow, 24);
            }
            LongWolfSweep sweep = root.AddComponent<LongWolfSweep>();
            SerializedObject sweepSettings = new SerializedObject(sweep);
            sweepSettings.FindProperty("bodyVisual").objectReferenceValue = body;
            sweepSettings.FindProperty("headVisual").objectReferenceValue = head;
            sweepSettings.ApplyModifiedPropertiesWithoutUndo();
            sweep.SetDirection(Vector2.right);
            wolfSettings.FindProperty("bodyRenderer").objectReferenceValue = headRenderer;
            wolfSettings.FindProperty("aimFollowsFlockDuringWarning").boolValue = false;
            wolfSettings.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Material material,
        Vector2 position, Vector2 scale, Color color, int order)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }
}

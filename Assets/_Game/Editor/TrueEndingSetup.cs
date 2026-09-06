using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 把洪山宝通寺和真结局需要的美术 / 数据接到当前打开的场景上：
/// WorldLandmarkSpawner 的宝塔定义与贴图、AlphaFlockExpansionController 的两张洞贴图和任务页引用。
/// 可重复执行。
/// </summary>
public static class TrueEndingSetup
{
    private const string EnvironmentFolder = "Assets/_Game/Content/Art/Environment";
    private const string PagodaDefinitionPath =
        "Assets/_Game/Content/Data/World/Obstacles/obstacle.pagoda.asset";

    [MenuItem("Game Jam/World/Wire Pagoda And True Ending To Open Scene")]
    public static void Wire()
    {
        Scene scene = SceneManager.GetActiveScene();
        WorldLandmarkSpawner spawner = Object.FindFirstObjectByType<WorldLandmarkSpawner>();
        AlphaFlockExpansionController controller = Object.FindFirstObjectByType<AlphaFlockExpansionController>();
        if (spawner == null || controller == null)
        {
            Debug.LogError("当前场景里没有 WorldLandmarkSpawner 或 AlphaFlockExpansionController。");
            return;
        }

        ObstacleDefinition pagodaDefinition =
            AssetDatabase.LoadAssetAtPath<ObstacleDefinition>(PagodaDefinitionPath);
        Sprite pagoda = LoadSprite("洪山宝通寺");
        Sprite holeFirst = LoadSprite("洞 第一次跳跃");
        Sprite holeSecond = LoadSprite("洞 第二次跳跃");
        if (pagodaDefinition == null || pagoda == null || holeFirst == null || holeSecond == null)
        {
            Debug.LogError($"缺资源：定义 {(pagodaDefinition != null)} / 宝塔 {(pagoda != null)} / " +
                           $"洞一 {(holeFirst != null)} / 洞二 {(holeSecond != null)}。");
            return;
        }

        SerializedObject spawnerObject = new SerializedObject(spawner);
        spawnerObject.FindProperty("pagodaDefinition").objectReferenceValue = pagodaDefinition;
        spawnerObject.FindProperty("pagodaSprite").objectReferenceValue = pagoda;
        spawnerObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerObject = new SerializedObject(controller);
        controllerObject.FindProperty("holeFirstJumpSprite").objectReferenceValue = holeFirst;
        controllerObject.FindProperty("holeSecondJumpSprite").objectReferenceValue = holeSecond;
        TaskPanelToggle toggle = Object.FindFirstObjectByType<TaskPanelToggle>(FindObjectsInactive.Include);
        if (toggle != null)
            controllerObject.FindProperty("taskPanelToggle").objectReferenceValue = toggle;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"宝通寺已接好：门槛 {pagodaDefinition.RequiredFlockCount} 只羊，" +
                  $"任务页引用 {(toggle != null ? "已连" : "没找到")}，两张洞贴图已连。");
    }

    private static Sprite LoadSprite(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Texture2D", new[] { EnvironmentFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != name)
                continue;

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;
        }

        Debug.LogWarning($"{EnvironmentFolder} 里没找到 {name}。");
        return null;
    }
}

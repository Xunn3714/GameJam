using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AlphaUiIntegrationTests
{
    private const string AlphaScenePath =
        "Assets/_Game/Scenes/AlphaFlockExpansion.unity";
    private const string PauseSystemPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/PauseSystem.prefab";
    private const string TaskSystemPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/TaskSystem.prefab";
    private const string MainMenuScenePath =
        "Assets/_Game/Scenes/MainMenu.unity";
    private const string ResultPanelPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/ResultPanel.prefab";
    private const string FenceSpritePath =
        "Assets/Art/Debris/obstacle_fence_256x128.png";
    private const string CatalogPath =
        "Assets/_Game/Content/Data/Sheep/SpecialSheepCatalog.asset";

    [Test]
    public void PauseSystemPrefabHasOneConfiguredManager()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseSystemPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        PauseManager[] managers = prefab.GetComponentsInChildren<PauseManager>(true);
        Assert.That(managers, Has.Length.EqualTo(1));

        SerializedObject serialized = new SerializedObject(managers[0]);
        string[] requiredReferences =
        {
            "pausePanel",
            "pauseWindow",
            "settingPanel",
            "collectionPanel",
            "continueButton",
            "mainMenuButton",
            "settingsButton",
            "collectionButton",
            "restartButton",
            "exitButton",
            "settingsBackButton",
            "collectionBackButton"
        };

        foreach (string propertyName in requiredReferences)
        {
            Object value = serialized.FindProperty(propertyName).objectReferenceValue;
            Assert.That(value, Is.Not.Null, $"PauseManager.{propertyName} is not configured.");
        }
    }

    [Test]
    public void PauseManagerCoordinatesPauseSettingsAndCollectionPanels()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseSystemPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            PauseManager manager = instance.GetComponent<PauseManager>();
            SerializedObject serialized = new SerializedObject(manager);
            GameObject pausePanel =
                (GameObject)serialized.FindProperty("pausePanel").objectReferenceValue;
            GameObject pauseWindow =
                (GameObject)serialized.FindProperty("pauseWindow").objectReferenceValue;
            GameObject settingPanel =
                (GameObject)serialized.FindProperty("settingPanel").objectReferenceValue;
            GameObject collectionPanel =
                (GameObject)serialized.FindProperty("collectionPanel").objectReferenceValue;

            manager.OpenPause();
            Assert.That(manager.IsPaused, Is.True);
            Assert.That(pausePanel.activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            manager.ShowSettings();
            Assert.That(settingPanel.activeSelf, Is.True);
            Assert.That(pauseWindow.activeSelf, Is.False);

            manager.BackToPause();
            manager.ShowCollection();
            Assert.That(collectionPanel.activeSelf, Is.True);
            Assert.That(pauseWindow.activeSelf, Is.False);

            manager.BackFromCollection();
            Assert.That(collectionPanel.activeSelf, Is.False);
            Assert.That(pauseWindow.activeSelf, Is.True);

            manager.ResumeGame();
            Assert.That(manager.IsPaused, Is.False);
            Assert.That(pausePanel.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            manager.SetResultLocked(true);
            manager.OpenPause();
            Assert.That(manager.IsPaused, Is.False);
            Assert.That(pausePanel.activeSelf, Is.False);
        }
        finally
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void AlphaSceneKeepsMainGameplayAndUsesSingleUiStack()
    {
        Scene scene = EditorSceneManager.OpenScene(AlphaScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject[] objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();

            Assert.That(objects.SelectMany(item => item.GetComponents<PauseManager>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<EventSystem>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<FlockActionController>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<TaskPanelToggle>()).ToArray(),
                Has.Length.EqualTo(1));

            AlphaFlockExpansionController controller = objects
                .Select(item => item.GetComponent<AlphaFlockExpansionController>())
                .FirstOrDefault(item => item != null);
            Assert.That(controller, Is.Not.Null);

            SerializedObject controllerData = new SerializedObject(controller);
            Assert.That(controllerData.FindProperty("flockActions").objectReferenceValue, Is.Not.Null);
            Assert.That(controllerData.FindProperty("pauseManager").objectReferenceValue, Is.Not.Null);
            Assert.That(controllerData.FindProperty("bannerView").objectReferenceValue, Is.Not.Null);

            MvpHudView hud = objects
                .Select(item => item.GetComponent<MvpHudView>())
                .FirstOrDefault(item => item != null);
            Assert.That(hud, Is.Not.Null);
            SerializedObject hudData = new SerializedObject(hud);
            Assert.That(hudData.FindProperty("taskChecklistView").objectReferenceValue, Is.Not.Null);

            PauseManager pauseManager = objects
                .Select(item => item.GetComponent<PauseManager>())
                .FirstOrDefault(item => item != null);
            SerializedObject pauseData = new SerializedObject(pauseManager);
            Assert.That(pauseData.FindProperty("taskPanelToggle").objectReferenceValue, Is.Not.Null);
            Assert.That(pauseData.FindProperty("bannerView").objectReferenceValue, Is.Not.Null);

            TMP_Text gatherHint = objects
                .Select(item => item.GetComponent<TMP_Text>())
                .FirstOrDefault(item => item != null && item.gameObject.name == "Gather_Hint");
            Assert.That(gatherHint, Is.Not.Null);
            Assert.That(gatherHint.text, Does.Contain("收拢"));

            FenceObstacle[] fences = objects
                .Select(item => item.GetComponent<FenceObstacle>())
                .Where(item => item != null)
                .ToArray();
            Assert.That(fences.Length, Is.GreaterThan(20));
            foreach (FenceObstacle fence in fences)
            {
                SpriteRenderer renderer = fence.GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.EqualTo(FenceSpritePath));
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void PolishedUiKeepsTaskAspectAndConnectsMainMenuCatalog()
    {
        GameObject taskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TaskSystemPrefabPath);
        Transform taskPanel = taskPrefab.GetComponentsInChildren<Transform>(true)
            .First(item => item.name == "TaskPanel");
        RectTransform taskRect = taskPanel.GetComponent<RectTransform>();
        Assert.That(taskRect.sizeDelta.x, Is.EqualTo(410f).Within(0.01f));
        Assert.That(taskRect.sizeDelta.y, Is.EqualTo(520f).Within(0.01f));
        Assert.That(taskPanel.GetComponent<Image>().sprite, Is.Not.Null);

        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
        try
        {
            SheepCollectionManager manager = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SheepCollectionManager>(true))
                .FirstOrDefault();
            Assert.That(manager, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(manager);
            Assert.That(serialized.FindProperty("specialSheepCatalog").objectReferenceValue, Is.Not.Null);

            StatisticsPanelController statistics = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<StatisticsPanelController>(true))
                .FirstOrDefault();
            Assert.That(statistics, Is.Not.Null);
            SerializedObject statisticsData = new SerializedObject(statistics);
            Assert.That(statisticsData.FindProperty("emptyStateText").objectReferenceValue, Is.Not.Null);

            MainMenuController menu = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true))
                .FirstOrDefault();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.creditsPanel, Is.Not.Null);
            Button developers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .First(button => button.gameObject.name == "Btn_Developers");
            Assert.That(developers.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(developers.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(MainMenuController.ShowCredits)));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ResultPanelUsesChineseRuntimeLabels()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResultPanelPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            ResultPanelView view = instance.GetComponent<ResultPanelView>();
            view.ShowVictory("测试说明", 12, 34, 11, 2, 83f);
            TMP_Text[] texts = instance.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(texts.First(text => text.gameObject.name == "ResultTitle").text, Is.EqualTo("冲出草原！"));
            Assert.That(texts.First(text => text.gameObject.name == "SheepCountLabel").text, Is.EqualTo("当前羊数：12"));
            Assert.That(texts.First(text => text.gameObject.name == "ScoreLabel").text, Is.EqualTo("当前得分：34"));
            Assert.That(texts.First(text => text.gameObject.name == "RecruitCountLabel").text, Is.EqualTo("成功招募：11"));
            Assert.That(texts.First(text => text.gameObject.name == "LostCountLabel").text, Is.EqualTo("损失羊数：2"));
            Assert.That(texts.First(text => text.gameObject.name == "TimeLabel").text, Is.EqualTo("游戏用时：01:23"));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void CollectionUnlockedCountIgnoresStaleSaveEntries()
    {
        GameObject owner = new GameObject("CollectionManagerTest");
        try
        {
            SheepCollectionManager manager = owner.AddComponent<SheepCollectionManager>();
            SerializedObject serialized = new SerializedObject(manager);
            serialized.FindProperty("specialSheepCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SpecialSheepCatalog>(CatalogPath);
            SerializedProperty progress = serialized.FindProperty("progressList");
            progress.arraySize = 2;
            progress.GetArrayElementAtIndex(0).FindPropertyRelative("sheepId").stringValue = MvpSheepCatalog.DefaultTypeId;
            progress.GetArrayElementAtIndex(0).FindPropertyRelative("unlocked").boolValue = true;
            progress.GetArrayElementAtIndex(1).FindPropertyRelative("sheepId").stringValue = "removed.sheep";
            progress.GetArrayElementAtIndex(1).FindPropertyRelative("unlocked").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            MethodInfo rebuild = typeof(SheepCollectionManager)
                .GetMethod("RebuildCatalogEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuild, Is.Not.Null);
            rebuild.Invoke(manager, null);

            Assert.That(manager.GetUnlockedCount(), Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}

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
    private const string SettingPanelPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/SettingPanel.prefab";
    private const string CollectionPanelPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/CollectionPanel.prefab";
    private const string FenceSpritePath =
        "Assets/Art/Debris/obstacle_fence_256x128.png";
    private const string CatalogPath =
        "Assets/_Game/Content/Data/Sheep/SpecialSheepCatalog.asset";
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
            Assert.That(objects.SelectMany(item => item.GetComponents<AlphaFlockExpansionController>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<WorldDebrisSpawner>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<WorldSeed>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<ProgressiveSheepSpawner>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<BorderFenceRing>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<TutorialPen>()).ToArray(),
                Has.Length.EqualTo(1));
            SheepRecruitAudio[] recruitAudio = objects
                .SelectMany(item => item.GetComponents<SheepRecruitAudio>())
                .ToArray();
            Assert.That(recruitAudio, Has.Length.EqualTo(1));
            SerializedObject recruitAudioData = new SerializedObject(recruitAudio[0]);
            Assert.That(
                recruitAudioData.FindProperty("tutorialPen").objectReferenceValue,
                Is.Not.Null);
            SerializedProperty sheepClips = recruitAudioData.FindProperty("sheepClips");
            Assert.That(sheepClips.arraySize, Is.EqualTo(7));
            for (int index = 0; index < sheepClips.arraySize; index++)
            {
                Assert.That(
                    sheepClips.GetArrayElementAtIndex(index).objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<AudioClip>(SheepRecruitClipPaths[index])),
                    $"SheepRecruitAudio clip {index + 1} differs from main.");
            }
            Assert.That(
                recruitAudioData.FindProperty("recruitBleatChance").floatValue,
                Is.EqualTo(0.5f).Within(0.001f));
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
            Assert.That(gatherHint, Is.Null, "新手教程不应再介绍 Q 收拢");
            Assert.That(objects.Any(item => item.name == "Key_Q"), Is.False);

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
        Assert.That(taskRect.sizeDelta.x, Is.EqualTo(440f).Within(0.01f));
        Assert.That(taskRect.sizeDelta.y, Is.EqualTo(220f).Within(0.01f));
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
    public void PolishedUiPreservesMainAudioLabelsAndCollectionImageSize()
    {
        GameObject settings = AssetDatabase.LoadAssetAtPath<GameObject>(SettingPanelPrefabPath);
        Assert.That(settings, Is.Not.Null);

        string[] labelNames = { "Main_Label", "Music_Label", "Sheep_Label" };
        string[] expectedLabels = { "总音量", "音乐音量", "音效音量" };
        string[] sliderNames = { "Main_Slider", "Music_Slider", "Sheep_Slider" };
        string[] expectedCallbacks = { "SetMasterVolume", "SetBGMVolume", "SetSheepVolume" };
        for (int index = 0; index < labelNames.Length; index++)
        {
            TMP_Text label = settings.GetComponentsInChildren<TMP_Text>(true)
                .Single(item => item.gameObject.name == labelNames[index]);
            Assert.That(label.text, Is.EqualTo(expectedLabels[index]));
            Assert.That(label.rectTransform.sizeDelta, Is.EqualTo(new Vector2(160f, 50f)));

            Slider slider = settings.GetComponentsInChildren<Slider>(true)
                .Single(item => item.gameObject.name == sliderNames[index]);
            Assert.That(slider.GetComponent<RectTransform>().sizeDelta,
                Is.EqualTo(new Vector2(480f, 40f)));
            Assert.That(slider.onValueChanged.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(slider.onValueChanged.GetPersistentMethodName(0),
                Is.EqualTo(expectedCallbacks[index]));
        }

        GameObject collection = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionPanelPrefabPath);
        Assert.That(collection, Is.Not.Null);
        RectTransform sheepImage = collection.GetComponentsInChildren<RectTransform>(true)
            .Single(item => item.gameObject.name == "SheepImage");
        Assert.That(sheepImage.sizeDelta, Is.EqualTo(new Vector2(220f, 190f)));
    }

    [Test]
    public void SequentialTaskPrefabShowsOnlyCurrentObjective()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TaskSystemPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            TaskChecklistView checklist = instance.GetComponentInChildren<TaskChecklistView>(true);
            Assert.That(checklist, Is.Not.Null);

            SerializedObject checklistData = new SerializedObject(checklist);
            Assert.That(checklistData.FindProperty("taskRow01").objectReferenceValue, Is.Not.Null);
            Assert.That(checklistData.FindProperty("taskTitle01").objectReferenceValue, Is.Not.Null);

            checklist.ApplyObjectives(new[]
            {
                new MvpObjectiveSnapshot(
                    "alpha.recruit_five",
                    "找五个新伙伴",
                    true,
                    false,
                    false,
                    2,
                    5)
            }, 4);

            Transform taskPanel = instance.GetComponentsInChildren<Transform>(true)
                .First(item => item.name == "TaskPanel");
            Transform[] taskRows = taskPanel.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith("TaskRow_"))
                .ToArray();
            Assert.That(taskRows, Has.Length.EqualTo(4));
            Assert.That(taskRows.Count(item => item.gameObject.activeSelf), Is.EqualTo(1));
            Assert.That(taskRows.Single(item => item.gameObject.activeSelf).name, Is.EqualTo("TaskRow_01"));
            Assert.That(taskPanel.GetComponentsInChildren<TMP_Text>(true)
                .First(item => item.name == "Txt_Task_01").text, Is.EqualTo("找五个新伙伴"));
            Assert.That(taskPanel.GetComponentsInChildren<TMP_Text>(true)
                .First(item => item.name == "Progress_01").text, Is.EqualTo("2/5"));

            RectTransform check = taskPanel.GetComponentsInChildren<Image>(true)
                .First(item => item.name.StartsWith("Check_ICon")).rectTransform;
            RectTransform title = taskPanel.GetComponentsInChildren<TMP_Text>(true)
                .First(item => item.name == "Txt_Task_01").rectTransform;
            RectTransform counter = taskPanel.GetComponentsInChildren<TMP_Text>(true)
                .First(item => item.name == "Progress_01").rectTransform;
            Assert.That(check.anchoredPosition.y, Is.EqualTo(title.anchoredPosition.y).Within(0.01f));
            Assert.That(counter.anchoredPosition.y, Is.EqualTo(title.anchoredPosition.y).Within(0.01f));
            Assert.That(title.GetComponent<TMP_Text>().fontSize, Is.EqualTo(26f).Within(0.01f));

            Image progressFill = taskPanel.GetComponentsInChildren<Image>(true)
                .First(item => item.name == "TaskProgressFill");
            Assert.That(progressFill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(progressFill.fillAmount, Is.EqualTo(0.4f).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void SequentialTaskSceneUsesSixTutorialSheepAndUpdatedThresholds()
    {
        Scene scene = EditorSceneManager.OpenScene(AlphaScenePath, OpenSceneMode.Additive);
        try
        {
            Transform[] transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();
            Transform tutorialRoot = transforms.Single(item => item.name == "TutorialSheep");
            Assert.That(tutorialRoot.Cast<Transform>().Count(), Is.EqualTo(6));
            Assert.That(tutorialRoot.Find("TutorialSheep_6"), Is.Not.Null);

            TutorialPen pen = transforms.Select(item => item.GetComponent<TutorialPen>())
                .First(item => item != null);
            Assert.That(pen.Fences, Is.Not.Empty);
            Assert.That(pen.RequiredFlockCount, Is.EqualTo(6));

            WolfEventDirector director = transforms.Select(item => item.GetComponent<WolfEventDirector>())
                .First(item => item != null);
            Assert.That(director.RequiredMemberCount, Is.EqualTo(20));

            TMP_Text fenceTitle = transforms.Select(item => item.GetComponent<TMP_Text>())
                .First(item => item != null && item.gameObject.name == "Fence_Title");
            Assert.That(fenceTitle.text, Is.EqualTo("撞开羊圈！"));

            Assert.That(transforms.Any(item => item.name == "Key_Q"), Is.False);
            Transform moveTutorial = transforms.Single(item => item.name == "MoveTutorial");
            Transform recruitTutorial = transforms.Single(item => item.name == "RecruitTutorial");
            Transform fenceTutorial = transforms.Single(item => item.name == "FenceTutorial");
            Assert.That(moveTutorial.gameObject.activeSelf, Is.True);
            Assert.That(recruitTutorial.gameObject.activeSelf, Is.False);
            Assert.That(fenceTutorial.gameObject.activeSelf, Is.False);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void SideTaskExpandsPanelAndStaysInsideItsBackground()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TaskSystemPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            TaskChecklistView checklist = instance.GetComponentInChildren<TaskChecklistView>(true);
            RectTransform taskPanel = instance.GetComponentsInChildren<RectTransform>(true)
                .Single(item => item.name == "TaskPanel");
            float originalHeight = taskPanel.sizeDelta.y;

            checklist.ApplyObjectives(new[]
            {
                new MvpObjectiveSnapshot("alpha.grow", "壮大羊群！", true, false, false, 17, 20),
                new MvpObjectiveSnapshot("alpha.pagoda", "寻找？？", false, true, false, 30, 150)
            }, 17);

            RectTransform sideTask = taskPanel.GetComponentsInChildren<RectTransform>(true)
                .Single(item => item.name == "TaskRow_02");
            Assert.That(sideTask.gameObject.activeSelf, Is.True);
            Assert.That(taskPanel.sizeDelta.y, Is.GreaterThan(originalHeight));
            float rowBottomFromPanelTop = -sideTask.anchoredPosition.y + sideTask.rect.height * 0.5f;
            Assert.That(rowBottomFromPanelTop, Is.LessThanOrEqualTo(taskPanel.rect.height));
            Assert.That(sideTask.GetComponentsInChildren<TMP_Text>(true)
                .Single(item => item.name == "Txt_Task_02").text, Is.EqualTo("寻找？？"));
            TMP_Text counter = sideTask.GetComponentsInChildren<TMP_Text>(true)
                .Single(item => item.name == "Progress_02");
            Assert.That(counter.text, Is.EqualTo("30/150"));
            Assert.That(counter.rectTransform.sizeDelta.x, Is.GreaterThanOrEqualTo(96f));
            Assert.That(counter.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
        }
        finally
        {
            Object.DestroyImmediate(instance);
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
    public void ResultPanelAcceptsTrueEndingTitle()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResultPanelPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            ResultPanelView view = instance.GetComponent<ResultPanelView>();
            view.ShowVictory("结算说明", 150, 150, 149, 0, 83f, "寻得美食");

            TMP_Text title = instance.GetComponentsInChildren<TMP_Text>(true)
                .First(text => text.gameObject.name == "ResultTitle");
            Assert.That(title.text, Is.EqualTo("寻得美食"));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void EndingIllustrationTexturesAreAvailableThroughResources()
    {
        Texture2D fakeEnding = Resources.Load<Texture2D>(EndingIllustrationSequence.FakeEndingResourcePath);
        Texture2D trueEnding = Resources.Load<Texture2D>(EndingIllustrationSequence.TrueEndingResourcePath);

        Assert.That(fakeEnding, Is.Not.Null);
        Assert.That(fakeEnding.width, Is.EqualTo(1920));
        Assert.That(fakeEnding.height, Is.EqualTo(478));
        Assert.That(trueEnding, Is.Not.Null);
        Assert.That(trueEnding.width, Is.EqualTo(1920));
        Assert.That(trueEnding.height, Is.EqualTo(1080));
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

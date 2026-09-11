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
    private const string PoopPrefabPath =
        "Assets/_Game/Content/Perfabs/SheepMvp/Poop.prefab";
    private const string BreakParticlesPrefabPath =
        "Assets/_Game/Content/Perfabs/World/VFX/BreakParticles.prefab";
    private const string MainMenuScenePath =
        "Assets/_Game/Scenes/MainMenu.unity";
    private const string ResultPanelPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/ResultPanel.prefab";
    private const string SettingPanelPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/SettingPanel.prefab";
    private const string CollectionPanelPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/CollectionPanel.prefab";
    private const string SheepCardPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/SheepCard.prefab";
    private const string FenceSpritePath =
        "Assets/Art/Debris/obstacle_fence_256x128.png";
    private const string CatalogPath =
        "Assets/_Game/Content/Data/Sheep/SpecialSheepCatalog.asset";
    private const string MainMenuButtonArtFolder =
        "Assets/_Game/Content/Art/UI/MainMenuButtons";
    private const string TutorialKeyArtFolder =
        "Assets/_Game/Content/Art/UI/TutorialKeys";
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
            FlockActionController[] flockActions = objects
                .SelectMany(item => item.GetComponents<FlockActionController>())
                .ToArray();
            Assert.That(flockActions, Has.Length.EqualTo(1));
            SerializedObject actionData = new SerializedObject(flockActions[0]);
            Assert.That(actionData.FindProperty("dashSpeed").floatValue,
                Is.EqualTo(9f).Within(0.001f));
            Assert.That(actionData.FindProperty("dashDistance").floatValue,
                Is.EqualTo(3.2f).Within(0.001f));
            Assert.That(actionData.FindProperty("impactFollowThroughDuration").floatValue,
                Is.EqualTo(0.28f).Within(0.001f));
            Assert.That(actionData.FindProperty("impactFollowThroughSpeedFactor").floatValue,
                Is.EqualTo(0.78f).Within(0.001f));
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

            CollectionPanelController inGameCollection = objects
                .Select(item => item.GetComponent<CollectionPanelController>())
                .FirstOrDefault(item => item != null);
            Assert.That(inGameCollection, Is.Not.Null);
            RectTransform inGameDetailPanel =
                inGameCollection.detailImage.transform.parent as RectTransform;
            Assert.That(inGameDetailPanel, Is.Not.Null);
            Assert.That(inGameDetailPanel.sizeDelta, Is.EqualTo(new Vector2(590f, 620f)));
            Assert.That(inGameCollection.detailImage.rectTransform.sizeDelta,
                Is.EqualTo(new Vector2(320f, 320f)));

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
            SerializedObject menuData = new SerializedObject(menu);
            Object sharedCollectionPrefab =
                menuData.FindProperty("collectionPanelPrefab").objectReferenceValue;
            Assert.That(sharedCollectionPrefab, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sharedCollectionPrefab),
                Is.EqualTo(CollectionPanelPrefabPath));
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
    public void MainMenuUsesNewArtworkAndPressedSprites()
    {
        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
        try
        {
            (string ObjectName, string AssetName)[] expected =
            {
                ("Btn_Start", "Start"),
                ("Btn_Collection", "Collection"),
                ("Btn_Statistics", "Statistics"),
                ("Btn_Developers", "Credits"),
            };

            Button[] buttons = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .ToArray();
            foreach ((string objectName, string assetName) in expected)
            {
                Button button = buttons.Single(item => item.name == objectName);
                Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
                Assert.That(
                    AssetDatabase.GetAssetPath(button.GetComponent<Image>().sprite),
                    Is.EqualTo($"{MainMenuButtonArtFolder}/{assetName}.png"));
                Assert.That(
                    AssetDatabase.GetAssetPath(button.spriteState.pressedSprite),
                    Is.EqualTo($"{MainMenuButtonArtFolder}/{assetName}Pressed.png"));
                Assert.That(
                    button.GetComponentsInChildren<TMP_Text>(true).All(label => !label.gameObject.activeSelf),
                    Is.True,
                    $"{objectName} should not draw a duplicate TMP label over its artwork.");
            }

            Image menuPanel = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Image>(true))
                .Single(image => image.name == "MenuPanel");
            Assert.That(menuPanel.enabled, Is.False);
            Transform backdropTint = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(item => item.name == "BackdropTint");
            Assert.That(backdropTint.gameObject.activeSelf, Is.False);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void MainMenuReplacesItsLegacyCatalogWithSharedGameplayPrefab()
    {
        GameObject root = new GameObject("MainMenuCatalogBindingTest");
        root.SetActive(false);
        try
        {
            MainMenuController menu = root.AddComponent<MainMenuController>();
            menu.menuPanel = new GameObject("MenuPanel");
            menu.menuPanel.transform.SetParent(root.transform, false);
            menu.collectionPanel = new GameObject("LegacyCollectionPanel");
            menu.collectionPanel.transform.SetParent(root.transform, false);
            menu.collectionPanel.SetActive(false);

            SerializedObject menuData = new SerializedObject(menu);
            menuData.FindProperty("collectionPanelPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(CollectionPanelPrefabPath);
            menuData.ApplyModifiedPropertiesWithoutUndo();

            MethodInfo bindSharedCollection = typeof(MainMenuController).GetMethod(
                "BindSharedCollectionPanel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bindSharedCollection, Is.Not.Null);
            bindSharedCollection.Invoke(menu, null);

            Assert.That(menu.collectionPanel, Is.Not.Null);
            Assert.That(menu.collectionPanel.name, Is.EqualTo("CollectionPanel"));
            Assert.That(menu.collectionPanel.GetComponent<CollectionPanelController>(), Is.Not.Null);

            Button backButton = menu.collectionPanel.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.name == "Btn_Back");
            menu.menuPanel.SetActive(false);
            menu.collectionPanel.SetActive(true);
            backButton.onClick.Invoke();
            Assert.That(menu.menuPanel.activeSelf, Is.True);
            Assert.That(menu.collectionPanel.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
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
        Assert.That(sheepImage.sizeDelta, Is.EqualTo(new Vector2(320f, 320f)));
        Assert.That(sheepImage.GetComponent<Image>().preserveAspect, Is.True);

        RectTransform detailPanel = collection.GetComponentsInChildren<RectTransform>(true)
            .Single(item => item.gameObject.name == "DetailPanel");
        Assert.That(detailPanel.sizeDelta, Is.EqualTo(new Vector2(590f, 620f)));
    }

    [Test]
    public void CollectionCardShowsQualityBeyondItsOutline()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SheepCardPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            SheepCardView card = instance.GetComponent<SheepCardView>();
            Assert.That(card, Is.Not.Null);

            card.Setup(null, "一只名字特别特别长的测试羊", 12,
                SheepQuality.Gold, true, null);

            Assert.That(card.sheepImage.preserveAspect, Is.True);
            Assert.That(card.nameText.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(card.countText.text, Is.EqualTo("发现 12 次"));
            Assert.That(card.QualityWash, Is.Not.Null);
            Assert.That(card.QualityWash.color.a, Is.GreaterThan(0.1f));
            Assert.That(card.QualityBadge, Is.Not.Null);
            Assert.That(card.QualityBadge.color, Is.EqualTo(
                SheepCardView.GetQualityColor(SheepQuality.Gold)));
            Assert.That(card.QualityBadgeText.text, Is.EqualTo("金色"));

            card.Setup(null, "彩色测试羊", 1,
                SheepQuality.EasterEgg, true, null);
            Transform rainbow = card.QualityBadge.transform.Find("RainbowSegments");
            Assert.That(rainbow, Is.Not.Null);
            Assert.That(rainbow.gameObject.activeSelf, Is.True);
            Assert.That(rainbow.childCount, Is.EqualTo(5));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void CollectionDetailPreservesImageAndEllipsizesOverflow()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionPanelPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            CollectionPanelController controller = instance.GetComponent<CollectionPanelController>();
            SheepDetailCardView detail = controller.GetOrCreateDetailCard();
            SheepCollectionEntry entry = new()
            {
                displayName = "测试羊",
                quality = SheepQuality.Purple,
                description = new string('长', 200)
            };

            detail.Show(entry, 7);

            Assert.That(detail.SheepImage.preserveAspect, Is.True);
            Assert.That(detail.CountText.text, Is.EqualTo("发现次数：7"));
            Assert.That(detail.DescriptionText.overflowMode,
                Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(detail.DescriptionText.maxVisibleLines, Is.EqualTo(12));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
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
            PoopAbility poopAbility = transforms.Select(item => item.GetComponent<PoopAbility>())
                .First(item => item != null);
            Assert.That(poopAbility.CooldownSeconds, Is.EqualTo(2f));
            Assert.That(poopAbility.LifetimeSeconds, Is.EqualTo(10f));
            Assert.That(poopAbility.MaxActivePoops, Is.EqualTo(100));
            Assert.That(poopAbility.RingIntervalSeconds, Is.EqualTo(0.2f));
            Assert.That(poopAbility.RingWidth, Is.EqualTo(1.9f));
            SerializedObject poopData = new SerializedObject(poopAbility);
            Assert.That(poopData.FindProperty("poopAction").objectReferenceValue, Is.Not.Null);
            Assert.That(poopData.FindProperty("poopPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(poopData.FindProperty("footOffset").floatValue, Is.EqualTo(0.08f));
            Transform moveTutorial = transforms.Single(item => item.name == "MoveTutorial");
            Transform recruitTutorial = transforms.Single(item => item.name == "RecruitTutorial");
            Transform fenceTutorial = transforms.Single(item => item.name == "FenceTutorial");
            Assert.That(moveTutorial.gameObject.activeSelf, Is.True);
            Assert.That(recruitTutorial.gameObject.activeSelf, Is.False);
            Assert.That(fenceTutorial.gameObject.activeSelf, Is.False);

            TutorialKeyVisual[] keyVisuals = transforms
                .Select(item => item.GetComponent<TutorialKeyVisual>())
                .Where(item => item != null)
                .ToArray();
            Assert.That(keyVisuals, Has.Length.EqualTo(6));
            foreach (TutorialKeyVisual visual in keyVisuals)
            {
                string assetName = visual.Key.ToString();
                Assert.That(
                    AssetDatabase.GetAssetPath(visual.NormalSprite),
                    Is.EqualTo($"{TutorialKeyArtFolder}/{assetName}.png"));
                Assert.That(
                    AssetDatabase.GetAssetPath(visual.PressedSprite),
                    Is.EqualTo($"{TutorialKeyArtFolder}/{assetName}Pressed.png"));
            }

            Assert.That(recruitTutorial.Find("Key_Space"), Is.Not.Null);
            Assert.That(recruitTutorial.Find("Poop_Hint"), Is.Not.Null);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void PoopPrefabReusesExistingBreakParticlesEffect()
    {
        GameObject poopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PoopPrefabPath);
        GameObject breakParticles = AssetDatabase.LoadAssetAtPath<GameObject>(BreakParticlesPrefabPath);
        Assert.That(poopPrefab, Is.Not.Null);
        Assert.That(breakParticles, Is.Not.Null);

        PoopVisual visual = poopPrefab.GetComponent<PoopVisual>();
        Assert.That(visual, Is.Not.Null);
        SerializedObject serialized = new SerializedObject(visual);
        Assert.That(
            serialized.FindProperty("despawnEffectPrefab").objectReferenceValue,
            Is.SameAs(breakParticles));
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
    public void PoopCounterAndPagodaTaskOccupySeparateRows()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TaskSystemPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            TaskChecklistView checklist = instance.GetComponentInChildren<TaskChecklistView>(true);
            checklist.ApplyObjectives(new[]
            {
                new MvpObjectiveSnapshot("alpha.grow", "壮大羊群！", true, false, false, 17, 20),
                AlphaTaskSequence.PoopCounter(7),
                AlphaTaskSequence.Pagoda(30, 150, false)
            }, 17);

            Transform taskPanel = instance.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "TaskPanel");
            Transform[] visibleRows = taskPanel.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith("TaskRow_") && item.gameObject.activeSelf)
                .ToArray();
            Assert.That(visibleRows, Has.Length.EqualTo(3));

            TMP_Text poopTitle = taskPanel.GetComponentsInChildren<TMP_Text>(true)
                .Single(item => item.name == "Txt_Task_02");
            TMP_Text poopCount = taskPanel.GetComponentsInChildren<TMP_Text>(true)
                .Single(item => item.name == "Progress_02");
            Assert.That(poopTitle.text, Is.EqualTo("Space 拉屎"));
            Assert.That(poopCount.text, Is.EqualTo("7 次"));

            TMP_Text pagodaTitle = taskPanel.GetComponentsInChildren<TMP_Text>(true)
                .Single(item => item.name == "Txt_Task_03");
            TMP_Text pagodaProgress = taskPanel.GetComponentsInChildren<TMP_Text>(true)
                .Single(item => item.name == "Progress_03");
            Assert.That(pagodaTitle.text, Is.EqualTo("寻找？？"));
            Assert.That(pagodaProgress.text, Is.EqualTo("30/150"));
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

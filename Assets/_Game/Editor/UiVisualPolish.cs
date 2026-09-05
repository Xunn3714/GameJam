using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies the project's hand-painted UI assets to the reusable UI prefabs and main menu.
/// Keeping the layout here makes the generated Alpha scene repeatable and reviewable.
/// </summary>
public static class UiVisualPolish
{
    private const string MainMenuScenePath = "Assets/_Game/Scenes/MainMenu.unity";
    private const string MainMenuBgmPath = "Assets/_Game/Content/Audio/BGM/SheepMvp/sheep-coming(city).wav";
    private const string SettingPrefabPath = "Assets/_Game/Content/Perfabs/UI/SettingPanel.prefab";
    private const string CollectionPrefabPath = "Assets/_Game/Content/Perfabs/UI/CollectionPanel.prefab";
    private const string SheepCardPrefabPath = "Assets/_Game/Content/Perfabs/UI/SheepCard.prefab";
    private const string StatRowPrefabPath = "Assets/_Game/Content/Perfabs/UI/StatRow.prefab";
    private const string ResultPrefabPath = "Assets/_Game/Content/Perfabs/UI/ResultPanel.prefab";
    private const string TaskPrefabPath = "Assets/_Game/Content/Perfabs/UI/TaskSystem.prefab";
    private const string PausePrefabPath = "Assets/_Game/Content/Perfabs/UI/PauseSystem.prefab";
    private const string BannerPrefabPath = "Assets/_Game/Content/Perfabs/UI/BannerSystem.prefab";
    private const string CatalogPath = "Assets/_Game/Content/Data/Sheep/SpecialSheepCatalog.asset";

    private const string GrassPath = "Assets/Art/WorldSprites/Tiles/草原_背景.png";
    private const string StartButtonPath = "Assets/Art/UI/Buttons/530_195开始按钮.png";
    private const string MediumButtonPath = "Assets/Art/UI/Buttons/225_85中小型按钮.png";
    private const string ExitButtonPath = "Assets/Art/UI/Buttons/225_85各类退出按钮.png";
    private const string SmallButtonPath = "Assets/Art/UI/Buttons/190_75小型图标.png";
    private const string PausePanelPath = "Assets/Art/UI/Panels/910_620暂停菜单.png";
    private const string LargePanelPath = "Assets/Art/UI/Panels/统计菜单图鉴菜单.png";
    private const string TaskPanelPath = "Assets/Art/UI/Panels/410_520px 任务面板.png";
    private const string BannerPanelPath = "Assets/Art/UI/Panels/520_90新羊通知弹窗.png";
    private const string StatRowPath = "Assets/Art/UI/Panels/710_90统计页面词条.png";
    private const string SheepCardPath = "Assets/Art/UI/Panels/1.png";
    private const string SheepCardSelectedPath = "Assets/Art/UI/Panels/2.png";
    private const string TaskIconPath = "Assets/Art/UI/Icon/Icon_01.png";
    private const string RestartIconPath = "Assets/Art/UI/Icon/Icon_02.png";
    private const string SettingsIconPath = "Assets/Art/UI/Icon/Icon_03.png";

    private static readonly Color Ink = new Color32(65, 57, 39, 255);
    private static readonly Color MutedInk = new Color32(101, 88, 59, 255);
    private static readonly Color Paper = new Color32(246, 240, 216, 255);
    private static readonly Color SoftPaper = new Color32(238, 229, 195, 205);
    private static readonly Color Overlay = new Color32(24, 28, 42, 195);
    private static readonly Color Olive = new Color32(116, 124, 73, 255);

    [MenuItem("Game Jam/UI/Apply Visual Polish")]
    public static void ApplyAll()
    {
        ApplyGameplayPrefabs();
        ApplyMainMenuScene();
        AssetDatabase.SaveAssets();
        Debug.Log("手绘 UI 风格已应用到主菜单和 Alpha 共用界面。");
    }

    public static void ApplyGameplayPrefabs()
    {
        StyleSettingPrefab();
        StyleCollectionPrefab();
        StyleSheepCardPrefab();
        StyleStatRowPrefab();
        StyleResultPrefab();
        StyleTaskPrefab();
        StyleBannerPrefab();
        StylePausePrefab();
    }

    public static void ApplyMainMenuScene()
    {
        Scene scene = SceneManager.GetSceneByPath(MainMenuScenePath);
        bool openedForEdit = !scene.IsValid() || !scene.isLoaded;
        if (openedForEdit)
            scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);

        GameObject canvasObject = Find(scene, "Canvas");
        if (canvasObject == null)
        {
            Debug.LogError("主菜单缺少 Canvas，无法应用 UI 风格。");
            return;
        }
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            RemoveMissingScripts(sceneRoot);

        GameObject sceneAudio = Find(scene, "SceneAudio");
        if (sceneAudio == null)
        {
            sceneAudio = new GameObject("SceneAudio");
            SceneManager.MoveGameObjectToScene(sceneAudio, scene);
        }

        SceneBGM sceneBgm = sceneAudio.GetComponent<SceneBGM>();
        if (sceneBgm == null)
            sceneBgm = sceneAudio.AddComponent<SceneBGM>();
        AudioClip mainMenuBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(MainMenuBgmPath);
        if (mainMenuBgm == null)
            throw new System.InvalidOperationException($"Missing main menu BGM at {MainMenuBgmPath}.");
        sceneBgm.Configure(mainMenuBgm);
        EditorUtility.SetDirty(sceneBgm);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject background = Find(scene, "Background");
        SetStretch(background);
        SetImage(background, GrassPath, new Color32(205, 226, 165, 255));

        GameObject tint = EnsureImage(canvasObject.transform, "BackdropTint");
        SetStretch(tint);
        tint.GetComponent<Image>().color = new Color32(23, 38, 42, 42);
        tint.GetComponent<Image>().raycastTarget = false;
        if (background != null)
            tint.transform.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);

        GameObject menu = Find(scene, "MenuPanel");
        if (menu != null)
        {
            DisableLayout(menu);
            SetRect(menu, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(910f, 620f));
            SetImage(menu, PausePanelPath, Color.white);
            menu.transform.SetAsLastSibling();

            TMP_Text title = EnsureText(menu.transform, "GameTitle", "羊群暴力扩张");
            SetRect(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 218f), new Vector2(720f, 72f));
            StyleText(title, 48f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            TMP_Text subtitle = EnsureText(menu.transform, "Subtitle", "聚拢伙伴 · 冲出草原");
            SetRect(subtitle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 166f), new Vector2(620f, 42f));
            StyleText(subtitle, 22f, MutedInk, TextAlignmentOptions.Center);

            StyleButton(Find(menu, "Btn_Start"), StartButtonPath, "开始游戏", new Vector2(0f, 58f), new Vector2(440f, 162f), 32f);
            StyleButton(Find(menu, "Btn_Collection"), MediumButtonPath, "羊群图鉴", new Vector2(-132f, -78f), new Vector2(225f, 85f), 23f);
            StyleButton(Find(menu, "Btn_Statistics"), MediumButtonPath, "游戏统计", new Vector2(132f, -78f), new Vector2(225f, 85f), 23f);
            StyleButton(Find(menu, "Btn_Quit"), ExitButtonPath, "退出游戏", new Vector2(0f, -190f), new Vector2(225f, 85f), 23f);
        }

        StyleIconButton(Find(scene, "Btn_Settings"), SettingsIconPath, new Vector2(1f, 1f), new Vector2(-70f, -66f), new Vector2(82f, 82f));
        GameObject developersButton = Find(scene, "Btn_Developers");
        StyleButton(developersButton, SmallButtonPath, "制作人员", new Vector2(116f, 62f), new Vector2(190f, 75f), 20f, new Vector2(0f, 0f));

        GameObject collection = Find(scene, "CollectionPanel");
        StyleCollection(collection);
        GameObject statistics = Find(scene, "StatisticsPanel");
        StyleStatistics(statistics);

        MainMenuController menuController = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true))
            .FirstOrDefault();
        GameObject credits = BuildCreditsPanel(canvasObject.transform, menuController);
        if (menuController != null)
        {
            menuController.creditsPanel = credits;
            Button openCredits = developersButton != null ? developersButton.GetComponent<Button>() : null;
            ReplacePersistentListener(openCredits, menuController.ShowCredits);
            EditorUtility.SetDirty(menuController);
        }

        SheepCollectionManager manager = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<SheepCollectionManager>(true))
            .FirstOrDefault();
        SpecialSheepCatalog catalog = AssetDatabase.LoadAssetAtPath<SpecialSheepCatalog>(CatalogPath);
        if (manager != null && catalog != null)
        {
            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty property = serialized.FindProperty("specialSheepCatalog");
            if (property != null)
                property.objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedForEdit)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static GameObject BuildCreditsPanel(Transform canvas, MainMenuController controller)
    {
        GameObject panel = EnsureImage(canvas, "CreditsPanel");
        SetStretch(panel);
        SetOverlay(panel);
        panel.transform.SetAsLastSibling();

        GameObject window = EnsureImage(panel.transform, "CreditsWindow");
        SetRect(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(910f, 620f));
        SetImage(window, PausePanelPath, Color.white);

        TMP_Text title = EnsureText(window.transform, "CreditsTitle", "制作人员");
        SetRect(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(620f, 62f), new Vector2(0.5f, 1f));
        StyleText(title, 42f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);

        const string creditsText =
            "项目管理  PKAT · KSLJ · XUNN\n" +
            "游戏设计  PKAT · KSLJ\n\n" +
            "程序  XUNN · W1K · DARCY\n" +
            "3WATER · JIMMY · KSLJ\n\n" +
            "2D 美术  ANKI0_0 · OAKT · GAILTY · XUNN\n" +
            "音乐  WHILIST · JIMMY · GAILTY\n" +
            "音效  WHILIST";
        TMP_Text body = EnsureText(window.transform, "CreditsBody", creditsText);
        SetRect(body.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(760f, 365f));
        StyleText(body, 23f, Ink, TextAlignmentOptions.Center);
        body.lineSpacing = 12f;

        GameObject backObject = EnsureImage(window.transform, "Btn_CreditsBack");
        Button back = backObject.GetComponent<Button>();
        if (back == null) back = backObject.AddComponent<Button>();
        EnsureText(backObject.transform, "Text (TMP)", "返回");
        StyleButton(backObject, ExitButtonPath, "返回", new Vector2(0f, -245f), new Vector2(190f, 72f), 22f);
        if (controller != null)
            ReplacePersistentListener(back, controller.BackToMenu);

        panel.SetActive(false);
        return panel;
    }

    private static void StyleSettingPrefab()
    {
        EditPrefab(SettingPrefabPath, root =>
        {
            SetRect(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(910f, 620f));
            SetImage(root, PausePanelPath, Color.white);

            TMP_Text title = FindComponent<TMP_Text>(root, "Title");
            if (title != null)
            {
                title.text = "设置";
                SetRect(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 205f), new Vector2(420f, 66f));
                StyleText(title, 44f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            }

            string[] labels = { "Main_Label", "Music_Label", "Sheep_Label" };
            string[] values = { "主音量", "音乐音量", "羊叫音量" };
            string[] sliders = { "Main_Slider", "Music_Slider", "Sheep_Slider" };
            float[] y = { 75f, -20f, -115f };
            for (int index = 0; index < labels.Length; index++)
            {
                TMP_Text label = FindComponent<TMP_Text>(root, labels[index]);
                if (label != null)
                {
                    label.text = values[index];
                    SetRect(label.gameObject, new Vector2(0.5f, 0.5f), new Vector2(-235f, y[index]), new Vector2(180f, 48f));
                    StyleText(label, 24f, Ink, TextAlignmentOptions.Right, FontStyles.Bold);
                }

                GameObject sliderObject = Find(root, sliders[index]);
                SetRect(sliderObject, new Vector2(0.5f, 0.5f), new Vector2(85f, y[index]), new Vector2(430f, 34f));
                StyleSlider(sliderObject);
            }

            StyleButton(Find(root, "Btn_Back"), ExitButtonPath, "返回", new Vector2(-128f, -94f), new Vector2(178f, 68f), 22f, new Vector2(1f, 1f));
        });
    }

    private static void StyleCollectionPrefab()
    {
        EditPrefab(CollectionPrefabPath, root =>
        {
            StyleCollection(root);
        });
    }

    private static void StyleCollection(GameObject root)
    {
        if (root == null)
            return;
        SetStretch(root);
        SetOverlay(root);
        GameObject window = Find(root, "CollectionWindow");
        if (window == null)
            return;
        SetRect(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 860f));
        SetImage(window, LargePanelPath, Color.white);

        TMP_Text title = FindComponent<TMP_Text>(window, "Txt_Title");
        if (title != null)
        {
            title.text = "羊群图鉴";
            SetRect(title.gameObject, new Vector2(0f, 1f), new Vector2(130f, -92f), new Vector2(480f, 64f), new Vector2(0f, 1f));
            StyleText(title, 44f, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
        }
        TMP_Text progress = FindComponent<TMP_Text>(window, "Txt_Progress");
        if (progress != null)
        {
            progress.text = $"已解锁  0 / {GetCatalogTotal()}";
            SetRect(progress.gameObject, new Vector2(0f, 1f), new Vector2(132f, -154f), new Vector2(460f, 42f), new Vector2(0f, 1f));
            StyleText(progress, 22f, MutedInk, TextAlignmentOptions.Left, FontStyles.Bold);
        }
        StyleButton(Find(window, "Btn_Back"), ExitButtonPath, "返回", new Vector2(-150f, -101f), new Vector2(190f, 72f), 22f, new Vector2(1f, 1f));

        GameObject scroll = Find(window, "SheepScrollView");
        SetRect(scroll, new Vector2(0.5f, 0.5f), new Vector2(-325f, -58f), new Vector2(800f, 570f));
        SetSoftPanel(scroll);
        StyleScrollView(scroll);
        GameObject content = Find(scroll, "Content");
        GridLayoutGroup grid = content != null ? content.GetComponent<GridLayoutGroup>() : null;
        if (grid != null)
        {
            grid.cellSize = new Vector2(220f, 250f);
            grid.spacing = new Vector2(22f, 20f);
            grid.padding = new RectOffset(28, 20, 24, 24);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        GameObject detail = Find(window, "DetailPanel");
        SetRect(detail, new Vector2(0.5f, 0.5f), new Vector2(465f, -58f), new Vector2(470f, 570f));
        SetSoftPanel(detail);
        if (detail != null)
        {
            TMP_Text sheepName = FindComponent<TMP_Text>(detail, "Txt_SheepName");
            TMP_Text count = FindComponent<TMP_Text>(detail, "Txt_Count");
            TMP_Text description = FindComponent<TMP_Text>(detail, "Txt_Description");
            TMP_Text abilityName = FindComponent<TMP_Text>(detail, "Txt_AbilityName");
            TMP_Text ability = FindComponent<TMP_Text>(detail, "Txt_AbilityDescription");
            GameObject sheepImage = Find(detail, "SheepImage");
            Image sheepGraphic = sheepImage != null ? sheepImage.GetComponent<Image>() : null;
            if (sheepGraphic != null && sheepGraphic.sprite == null)
                sheepGraphic.enabled = false;
            Image sentenceGraphic = FindComponent<Image>(detail, "Sheep_Sentence");
            if (sentenceGraphic != null && sentenceGraphic.sprite == null)
                sentenceGraphic.enabled = false;
            SetRect(sheepImage, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(190f, 190f));
            PlaceDetailText(sheepName, new Vector2(0f, -245f), new Vector2(400f, 48f), 30f, FontStyles.Bold);
            PlaceDetailText(count, new Vector2(0f, -287f), new Vector2(400f, 34f), 18f);
            PlaceDetailText(description, new Vector2(0f, -354f), new Vector2(392f, 82f), 19f);
            PlaceDetailText(abilityName, new Vector2(0f, -425f), new Vector2(392f, 36f), 21f, FontStyles.Bold);
            PlaceDetailText(ability, new Vector2(0f, -482f), new Vector2(392f, 72f), 18f);
            if (sheepName != null) sheepName.text = "选择一只羊";
            if (count != null) count.text = string.Empty;
            if (description != null) description.text = "在草原上遇见新的羊，\n它的资料就会记录在这里。";
            if (abilityName != null) abilityName.text = string.Empty;
            if (ability != null) ability.text = string.Empty;
        }
    }

    private static void StyleStatistics(GameObject root)
    {
        if (root == null)
            return;
        SetStretch(root);
        SetOverlay(root);
        GameObject window = Find(root, "StatisticsWindow");
        if (window == null)
            return;
        SetRect(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 860f));
        SetImage(window, LargePanelPath, Color.white);
        TMP_Text title = FindComponent<TMP_Text>(window, "Txt_Title");
        if (title != null)
        {
            title.text = "游戏统计";
            SetRect(title.gameObject, new Vector2(0f, 1f), new Vector2(130f, -92f), new Vector2(500f, 64f), new Vector2(0f, 1f));
            StyleText(title, 44f, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
        }
        StyleButton(Find(window, "Btn_Back"), ExitButtonPath, "返回", new Vector2(-150f, -101f), new Vector2(190f, 72f), 22f, new Vector2(1f, 1f));
        GameObject scroll = Find(window, "StatsScrollView");
        SetRect(scroll, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(1120f, 585f));
        SetSoftPanel(scroll);
        StyleScrollView(scroll);
        GameObject content = Find(scroll, "Content");
        VerticalLayoutGroup layout = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
        if (layout != null)
        {
            layout.padding = new RectOffset(195, 195, 28, 28);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
        }
        GameObject unusedDetail = Find(window, "DetailPanel");
        if (unusedDetail != null)
            unusedDetail.SetActive(false);

        TMP_Text emptyState = EnsureText(window.transform, "EmptyState", "完成一局游戏后，统计会显示在这里。");
        SetRect(emptyState.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(760f, 64f));
        StyleText(emptyState, 24f, MutedInk, TextAlignmentOptions.Center);
        StatisticsPanelController controller = root.GetComponent<StatisticsPanelController>();
        if (controller != null)
        {
            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty property = serialized.FindProperty("emptyStateText");
            if (property != null) property.objectReferenceValue = emptyState;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void StyleSheepCardPrefab()
    {
        EditPrefab(SheepCardPrefabPath, root =>
        {
            SetRect(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 250f));
            SetImage(root, SheepCardPath, Color.white);
            TMP_Text name = FindComponent<TMP_Text>(root, "Txt_Name");
            TMP_Text count = FindComponent<TMP_Text>(root, "Txt_Count");
            PlaceDetailText(name, new Vector2(0f, -82f), new Vector2(190f, 38f), 21f, FontStyles.Bold);
            PlaceDetailText(count, new Vector2(0f, -110f), new Vector2(190f, 28f), 15f);
            SetRect(Find(root, "SheepImage"), new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(132f, 132f));
            SheepCardView view = root.GetComponent<SheepCardView>();
            if (view != null)
            {
                SerializedObject serialized = new SerializedObject(view);
                serialized.FindProperty("normalBackground").objectReferenceValue = SpriteAt(SheepCardPath);
                serialized.FindProperty("selectedBackground").objectReferenceValue = SpriteAt(SheepCardSelectedPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        });
    }

    private static void StyleStatRowPrefab()
    {
        EditPrefab(StatRowPrefabPath, root =>
        {
            SetRect(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(710f, 90f));
            SetImage(root, StatRowPath, Color.white);
            StyleText(FindComponent<TMP_Text>(root, "Txt_Name"), 23f, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
            StyleText(FindComponent<TMP_Text>(root, "Txt_Value"), 23f, Ink, TextAlignmentOptions.Right, FontStyles.Bold);
            LayoutElement element = root.GetComponent<LayoutElement>();
            if (element == null) element = root.AddComponent<LayoutElement>();
            element.preferredHeight = 90f;
            element.minHeight = 90f;
        });
    }

    private static void StyleResultPrefab()
    {
        EditPrefab(ResultPrefabPath, root =>
        {
            SetStretch(root);
            SetOverlay(root);
            GameObject window = Find(root, "ResultWindow");
            SetRect(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 700f));
            SetImage(window, LargePanelPath, Color.white);
            if (window != null)
            {
                TMP_Text resultTitle = FindComponent<TMP_Text>(window, "ResultTitle");
                TMP_Text description = FindComponent<TMP_Text>(window, "ResultDescription");
                SetText(window, "ResultTitle", "冲出草原！");
                SetRect(resultTitle != null ? resultTitle.gameObject : null, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(860f, 72f), new Vector2(0.5f, 1f));
                StyleText(resultTitle, 46f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);

                SetText(window, "ResultDescription", "带着羊群冲出了围栏\n\n本局详情会显示在这里。\n\n按 R 可以再来一局");
                SetRect(description != null ? description.gameObject : null, new Vector2(0.5f, 0.5f), new Vector2(265f, -28f), new Vector2(560f, 400f));
                StyleText(description, 20f, MutedInk, TextAlignmentOptions.TopLeft);

                string[] names = { "SheepCountLabel", "ScoreLabel", "RecruitCountLabel", "LostCountLabel", "TimeLabel" };
                string[] values = { "当前羊数：0", "当前得分：0", "成功招募：0", "损失羊数：0", "游戏用时：00:00" };
                float[] y = { 120f, 48f, -24f, -96f, -168f };
                for (int index = 0; index < names.Length; index++)
                {
                    TMP_Text label = FindComponent<TMP_Text>(window, names[index]);
                    SetText(window, names[index], values[index]);
                    SetRect(label != null ? label.gameObject : null, new Vector2(0.5f, 0.5f), new Vector2(-300f, y[index]), new Vector2(400f, 54f));
                    StyleText(label, 24f, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
                }
            }
            StyleButton(Find(root, "Btn_ReturnTitle"), ExitButtonPath, "返回主菜单", new Vector2(0f, -280f), new Vector2(245f, 85f), 22f);
        });
    }

    private static void StyleTaskPrefab()
    {
        EditPrefab(TaskPrefabPath, root =>
        {
            SetStretch(root);
            GameObject panel = Find(root, "TaskPanel");
            SetRect(panel, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(410f, 520f), new Vector2(0f, 1f));
            SetImage(panel, TaskPanelPath, Color.white);
            Transform duplicateIcon = panel != null ? panel.transform.Find("Image") : null;
            if (duplicateIcon != null)
                duplicateIcon.gameObject.SetActive(false);

            TMP_Text title = FindComponent<TMP_Text>(panel, "Txt_Task");
            if (title != null)
            {
                title.text = "任务";
                SetRect(title.gameObject, new Vector2(0f, 1f), new Vector2(52f, -72f), new Vector2(150f, 46f), new Vector2(0f, 1f));
                StyleText(title, 30f, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
            }
            TMP_Text count = FindComponent<TMP_Text>(panel, "GroupCountText");
            if (count != null)
            {
                count.text = "羊群：1";
                SetRect(count.gameObject, new Vector2(1f, 1f), new Vector2(-58f, -75f), new Vector2(170f, 40f), new Vector2(1f, 1f));
                StyleText(count, 21f, Ink, TextAlignmentOptions.Right, FontStyles.Bold);
            }

            string[] rowNames = { "TaskRow_01", "TaskRow_02", "TaskRow_03", "TaskRow_04" };
            string[] labelNames = { "Txt_Task_01", "Txt_Task_02", "Txt_Task_03", "Txt_Task_04" };
            string[] progressNames = { "Progress_01", "Progress_02", "Progress_03", "Progress_04" };
            string[] labels = { "撞开出生羊圈", "壮大羊群并解锁出口", "撞开外围围栏并逃离", "招募一只特殊羊" };
            string[] defaultProgress = { "0/1", "0/6", "0/1", "0/1" };
            float[] y = { -132f, -222f, -312f, -402f };
            for (int index = 0; index < rowNames.Length; index++)
            {
                GameObject row = Find(panel, rowNames[index]);
                SetRect(row, new Vector2(0.5f, 1f), new Vector2(0f, y[index]), new Vector2(348f, 74f));
                TMP_Text label = FindComponent<TMP_Text>(row, labelNames[index]);
                if (label != null)
                {
                    label.text = labels[index];
                    SetRect(label.gameObject, new Vector2(0f, 0.5f), new Vector2(50f, 10f), new Vector2(245f, 36f), new Vector2(0f, 0.5f));
                    StyleText(label, 21f, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
                }
                TMP_Text progress = FindComponent<TMP_Text>(row, progressNames[index]);
                if (progress != null)
                {
                    progress.text = defaultProgress[index];
                    SetRect(progress.gameObject, new Vector2(1f, 0.5f), new Vector2(-8f, -22f), new Vector2(145f, 28f), new Vector2(1f, 0.5f));
                    StyleText(progress, 19f, Ink, TextAlignmentOptions.Right, FontStyles.Bold);
                }
                Image check = row != null ? row.GetComponentsInChildren<Image>(true)
                    .FirstOrDefault(image => image.gameObject.name.StartsWith("Check_ICon")) : null;
                if (check != null)
                    SetRect(check.gameObject, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(40f, 40f), new Vector2(0f, 0.5f));
            }

            StyleButton(Find(panel, "Btn_Close"), ExitButtonPath, "X", new Vector2(-27f, -27f), new Vector2(52f, 52f), 24f, new Vector2(1f, 1f));
            StyleIconButton(Find(root, "Btn_TaskIcon"), TaskIconPath, new Vector2(0f, 1f), new Vector2(64f, -66f), new Vector2(84f, 84f));
            if (panel != null) panel.SetActive(false);
            GameObject icon = Find(root, "Btn_TaskIcon");
            if (icon != null) icon.SetActive(true);
        });
    }

    private static void StyleBannerPrefab()
    {
        EditPrefab(BannerPrefabPath, root =>
        {
            SetRect(root, new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(520f, 90f), new Vector2(0.5f, 1f));
            SetImage(root, BannerPanelPath, Color.white);
            TMP_Text text = FindComponent<TMP_Text>(root, "BannerText");
            if (text != null)
            {
                text.text = "新的伙伴加入了羊群！";
                SetStretch(text.gameObject, new Vector2(76f, 14f));
                StyleText(text, 22f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            }
            Image icon = FindComponent<Image>(root, "Icon");
            if (icon != null && icon.sprite == null)
                icon.enabled = false;
        });
    }

    private static void StylePausePrefab()
    {
        EditPrefab(PausePrefabPath, root =>
        {
            SetStretch(root);
            GameObject panel = Find(root, "PausePanel");
            SetStretch(panel);
            SetOverlay(panel);
            GameObject window = Find(root, "PauseWindow");
            SetRect(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(910f, 620f));
            SetImage(window, PausePanelPath, Color.white);
            TMP_Text title = FindComponent<TMP_Text>(window, "PauseTitle");
            if (title != null)
            {
                title.text = "暂停";
                SetRect(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 205f), new Vector2(400f, 66f));
                StyleText(title, 44f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            }
            StyleButton(Find(window, "Btn_Continue"), MediumButtonPath, "继续游戏", new Vector2(0f, 82f), new Vector2(300f, 106f), 27f);
            StyleButton(Find(window, "Btn_Settings", "Btn_Settings "), MediumButtonPath, "设置", new Vector2(-250f, -58f), new Vector2(225f, 85f), 22f);
            StyleButton(Find(window, "Btn_MainMenu"), MediumButtonPath, "主菜单", new Vector2(0f, -58f), new Vector2(225f, 85f), 22f);
            StyleButton(Find(window, "Btn_Sheep"), MediumButtonPath, "羊群图鉴", new Vector2(250f, -58f), new Vector2(225f, 85f), 22f);
            StyleButton(Find(window, "Btn_Exit"), ExitButtonPath, "退出游戏", new Vector2(0f, -190f), new Vector2(225f, 85f), 22f);
            StyleIconButton(Find(window, "Btn_Restart", "Btn_Reasult"), RestartIconPath, new Vector2(0f, 1f), new Vector2(115f, -112f), new Vector2(72f, 72f));
        });
    }

    private static void StyleSlider(GameObject sliderObject)
    {
        if (sliderObject == null)
            return;
        GameObject background = Find(sliderObject, "Background");
        Image backgroundImage = background != null ? background.GetComponent<Image>() : null;
        if (backgroundImage != null)
        {
            backgroundImage.sprite = null;
            backgroundImage.color = new Color32(92, 82, 54, 125);
            SetHorizontalBar(background.GetComponent<RectTransform>(), 12f, 7f);
        }

        GameObject fill = Find(sliderObject, "Fill");
        Image fillImage = fill != null ? fill.GetComponent<Image>() : null;
        if (fillImage != null)
        {
            fillImage.sprite = null;
            fillImage.color = Olive;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.offsetMin = new Vector2(fillRect.offsetMin.x, -6f);
            fillRect.offsetMax = new Vector2(fillRect.offsetMax.x, 6f);
        }

        GameObject handle = Find(sliderObject, "Handle");
        Image handleImage = handle != null ? handle.GetComponent<Image>() : null;
        if (handleImage != null)
        {
            handleImage.color = new Color32(245, 235, 197, 255);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(30f, 30f);
            Outline outline = handle.GetComponent<Outline>();
            if (outline == null) outline = handle.AddComponent<Outline>();
            outline.effectColor = new Color32(83, 72, 44, 210);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    private static void StyleScrollView(GameObject scrollObject)
    {
        ScrollRect scroll = scrollObject != null ? scrollObject.GetComponent<ScrollRect>() : null;
        if (scroll == null || scroll.verticalScrollbar == null)
            return;

        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        GameObject scrollbarObject = scroll.verticalScrollbar.gameObject;
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        float height = Mathf.Max(120f, scrollRect.sizeDelta.y - 34f);
        SetRect(scrollbarObject, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(16f, height), new Vector2(1f, 0.5f));

        Image track = scrollbarObject.GetComponent<Image>();
        if (track != null)
        {
            track.sprite = null;
            track.color = new Color32(92, 82, 54, 48);
        }

        Image handle = scroll.verticalScrollbar.targetGraphic as Image;
        if (handle != null)
        {
            handle.sprite = null;
            handle.color = new Color32(116, 124, 73, 220);
            Outline outline = handle.GetComponent<Outline>();
            if (outline == null) outline = handle.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(65, 57, 39, 150);
            outline.effectDistance = new Vector2(1f, -1f);
        }
    }

    private static void SetHorizontalBar(RectTransform rect, float height, float horizontalInset)
    {
        if (rect == null)
            return;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(horizontalInset, -height * 0.5f);
        rect.offsetMax = new Vector2(-horizontalInset, height * 0.5f);
    }

    private static void PlaceDetailText(TMP_Text text, Vector2 position, Vector2 size, float fontSize, FontStyles style = FontStyles.Normal)
    {
        if (text == null)
            return;
        SetRect(text.gameObject, new Vector2(0.5f, 1f), position, size, new Vector2(0.5f, 1f));
        StyleText(text, fontSize, style == FontStyles.Bold ? Ink : MutedInk, TextAlignmentOptions.Center, style);
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            RemoveMissingScripts(root);
            edit(root);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveMissingScripts(GameObject root)
    {
        if (root == null)
            return;
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
        }
    }

    private static Sprite SpriteAt(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    private static int GetCatalogTotal()
    {
        SpecialSheepCatalog catalog = AssetDatabase.LoadAssetAtPath<SpecialSheepCatalog>(CatalogPath);
        if (catalog == null)
            return 0;
        HashSet<string> ids = new HashSet<string> { MvpSheepCatalog.DefaultTypeId };
        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            if (tier == null) continue;
            foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
                if (entry != null && entry.Sprite != null && !string.IsNullOrWhiteSpace(entry.TypeId)) ids.Add(entry.TypeId);
        }
        return ids.Count;
    }

    private static void SetImage(GameObject target, string path, Color color)
    {
        if (target == null)
            return;
        Image image = target.GetComponent<Image>();
        if (image == null) image = target.AddComponent<Image>();
        image.sprite = SpriteAt(path);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
    }

    private static void SetOverlay(GameObject target)
    {
        if (target == null)
            return;
        Image image = target.GetComponent<Image>();
        if (image == null) image = target.AddComponent<Image>();
        image.sprite = null;
        image.color = Overlay;
        image.raycastTarget = true;
    }

    private static void SetSoftPanel(GameObject target)
    {
        if (target == null)
            return;
        Image image = target.GetComponent<Image>();
        if (image == null) image = target.AddComponent<Image>();
        image.sprite = null;
        image.color = SoftPaper;
    }

    private static void ReplacePersistentListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
            UnityEventTools.RemovePersistentListener(button.onClick, index);
        if (action != null)
            UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    private static void SetText(GameObject root, string name, string value)
    {
        TMP_Text text = FindComponent<TMP_Text>(root, name);
        if (text != null)
            text.text = value;
    }

    private static void StyleButton(GameObject target, string spritePath, string label, Vector2 position, Vector2 size, float fontSize, Vector2? anchor = null)
    {
        if (target == null)
            return;
        SetRect(target, anchor ?? new Vector2(0.5f, 0.5f), position, size);
        SetImage(target, spritePath, Color.white);
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;
        Button button = target.GetComponent<Button>();
        if (button != null)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 247, 213, 255);
            colors.pressedColor = new Color32(208, 193, 145, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color32(160, 153, 130, 190);
            button.colors = colors;
        }
        TMP_Text text = target.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.gameObject.SetActive(true);
            text.text = label;
            SetStretch(text.gameObject, new Vector2(14f, 8f));
            StyleText(text, fontSize, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
        }
    }

    private static void StyleIconButton(GameObject target, string spritePath, Vector2 anchor, Vector2 position, Vector2 size)
    {
        if (target == null)
            return;
        SetRect(target, anchor, position, size);
        SetImage(target, spritePath, Color.white);
        Image image = target.GetComponent<Image>();
        image.preserveAspect = true;
        TMP_Text text = target.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.gameObject.SetActive(false);
    }

    private static void StyleText(TMP_Text text, float size, Color color, TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
    {
        if (text == null)
            return;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.margin = Vector4.zero;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private static void SetRect(GameObject target, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        RectTransform rect = target != null ? target.GetComponent<RectTransform>() : null;
        if (rect == null)
            return;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetStretch(GameObject target, Vector2? inset = null)
    {
        RectTransform rect = target != null ? target.GetComponent<RectTransform>() : null;
        if (rect == null)
            return;
        Vector2 padding = inset ?? Vector2.zero;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = padding;
        rect.offsetMax = -padding;
    }

    private static void DisableLayout(GameObject root)
    {
        foreach (LayoutGroup group in root.GetComponentsInChildren<LayoutGroup>(true))
            group.enabled = false;
        foreach (ContentSizeFitter fitter in root.GetComponentsInChildren<ContentSizeFitter>(true))
            fitter.enabled = false;
    }

    private static GameObject EnsureImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;
        GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static TMP_Text EnsureText(Transform parent, string name, string value)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI text;
        if (existing == null)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);
            text = created.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            text = existing.GetComponent<TextMeshProUGUI>();
            if (text == null) text = existing.gameObject.AddComponent<TextMeshProUGUI>();
        }
        text.text = value;
        return text;
    }

    private static GameObject Find(Scene scene, params string[] names)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject result = Find(root, names);
            if (result != null) return result;
        }
        return null;
    }

    private static GameObject Find(GameObject root, params string[] names)
    {
        if (root == null)
            return null;
        Transform result = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => names.Any(name => item.name.Trim() == name));
        return result != null ? result.gameObject : null;
    }

    private static T FindComponent<T>(GameObject root, params string[] names) where T : Component
    {
        if (root == null)
            return null;
        return root.GetComponentsInChildren<T>(true)
            .FirstOrDefault(item => names.Any(name => item.gameObject.name.Trim() == name));
    }
}

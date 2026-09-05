using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 一键生成 TestWolf 开发场景：10 只羊的羊群 + 定时生成的狼 + 调试 HUD。
/// 菜单：Game Jam / Wolf Test / Setup TestWolf Scene。可重复执行，会重建场景内的相关对象。
/// TestSmartWolf 场景用同一套流程，多两个“学习率 / 预测角度”调试窗口和更多的羊。
/// Dev 下的 TestWolf 系列都用缩短后的狼群节奏（见 ApplyFastWolfRhythm），正式关卡不受影响。
/// </summary>
public static class WolfTestSceneSetup
{
    private const string SceneFolder = "Assets/_Game/Scenes/Dev";
    private const string ScenePath = SceneFolder + "/TestWolf.unity";
    private const string SmartScenePath = SceneFolder + "/TestSmartWolf.unity";
    private const string SceneTemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";

    private const string PrototypeFolder = "Assets/_Game/Content/Art/Prototype";
    private const string WarningRectAssetPath = PrototypeFolder + "/WolfWarningRect.asset";
    private const string GridAssetPath = PrototypeFolder + "/WorldGrid.asset";

    private const string SheepMemberPrefabPath = "Assets/_Game/Content/Perfabs/Sheep/SheepMember.prefab";
    private const string WolfPrefabFolder = "Assets/_Game/Content/Perfabs/Wolf";
    private const string WolfPrefabPath = WolfPrefabFolder + "/Wolf.prefab";

    private const int StartingSheepCount = 10;
    private const int SmartStartingSheepCount = 20;

    // 测试场景专用的快节奏：空挡 3~5 秒（第一轮 3 秒）、狼嚎 2 秒、跑路 1 秒。正式关卡仍是 15~20 / 3 / 1.5。
    private const float FastCalmMin = 3f;
    private const float FastCalmMax = 5f;
    private const float FastFirstCalm = 3f;
    private const float FastHowl = 2f;
    private const float FastRetreat = 1f;

    private static readonly string[] ManagedRootNames =
    {
        "SheepFlock",
        "WolfSpawner",
        "WolfEventDirector",
        "WolfTestGameController",
        "WolfEventCanvas",
        "SmartWolfDebugView",
        "TestWolf_WorldGrid"
    };

    [MenuItem("Game Jam/Wolf Test/Setup TestWolf Scene")]
    public static void SetupTestWolfScene()
    {
        BuildTestScene(ScenePath, StartingSheepCount, false);
    }

    /// <summary>TestSmartWolf：20 只羊 + 聪明狼 + 两个调试窗口（当前学习率 / 当前预测角度）。</summary>
    [MenuItem("Game Jam/Wolf Test/Setup TestSmartWolf Scene")]
    public static void SetupTestSmartWolfScene()
    {
        BuildTestScene(SmartScenePath, SmartStartingSheepCount, true);
    }

    private static void BuildTestScene(string scenePath, int sheepCount, bool smartWolfWindows)
    {
        EnsureFolder(SceneFolder);
        EnsureFolder(PrototypeFolder);
        EnsureFolder(WolfPrefabFolder);

        Sprite circleSprite = SheepMovementPrototypeSetup.GetOrCreateCircleSprite();
        Sprite warningSprite = GetOrCreateWarningRectSprite();
        GameObject wolfPrefab = CreateOrUpdateWolfPrefab(circleSprite, warningSprite);

        GameObject sheepPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SheepMemberPrefabPath);
        if (sheepPrefab == null)
        {
            throw new System.InvalidOperationException($"Missing sheep prefab at {SheepMemberPrefabPath}.");
        }

        Scene scene = OpenOrCreateScene(scenePath);
        ClearManagedObjects(scene);

        CreateGrid(scene);
        GameObject flockObject = CreateFlock(scene, sheepPrefab, sheepCount, out FlockController flock, out FlockMovementController movement);
        WolfSpawner spawner = CreateSpawner(scene, flock, wolfPrefab);
        WolfEventDirector director = CreateEventDirector(scene, spawner);
        CreateGameController(scene, flock, movement, spawner, director);
        CreateEventHud(scene, director);
        if (smartWolfWindows)
        {
            CreateSmartWolfDebugView(scene, spawner, director);
        }
        ConfigureCamera(scene, flockObject.transform);

        Selection.activeGameObject = flockObject;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"{System.IO.Path.GetFileNameWithoutExtension(scenePath)} scene is ready at {scenePath}: {sheepCount} sheep, " +
            $"fast dev rhythm (calm {FastCalmMin}~{FastCalmMax}s, howl {FastHowl}s, retreat {FastRetreat}s). " +
            (smartWolfWindows ? "Two debug windows show the wolves' learning rate and predicted angle (F1 toggles them). " : "") +
            "Press Play, move with WASD, press R to restart after Game Over.");
    }

    /// <summary>
    /// 给当前打开的 Dev 测试场景（例如 TestLongWolf）套用缩短后的狼群节奏。只改 WolfEventDirector 的时间参数。
    /// </summary>
    [MenuItem("Game Jam/Wolf Test/Apply Fast Wolf Rhythm To Open Scene")]
    public static void ApplyFastWolfRhythmToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.path.StartsWith(SceneFolder))
        {
            Debug.LogWarning($"Fast wolf rhythm is only for scenes under {SceneFolder}; the open scene is {scene.path}.");
            return;
        }

        WolfEventDirector[] directors = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<WolfEventDirector>(true))
            .ToArray();
        if (directors.Length == 0)
        {
            Debug.LogWarning("No WolfEventDirector in the open scene.");
            return;
        }

        foreach (WolfEventDirector director in directors)
        {
            SerializedObject serialized = new SerializedObject(director);
            ApplyFastWolfRhythm(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Applied fast wolf rhythm to {directors.Length} director(s) in {scene.path}: " +
                  $"calm {FastCalmMin}~{FastCalmMax}s (first {FastFirstCalm}s), howl {FastHowl}s, retreat {FastRetreat}s.");
    }

    private static void ApplyFastWolfRhythm(SerializedObject director)
    {
        director.FindProperty("calmDurationMin").floatValue = FastCalmMin;
        director.FindProperty("calmDurationMax").floatValue = FastCalmMax;
        director.FindProperty("firstCalmDurationOverride").floatValue = FastFirstCalm;
        director.FindProperty("howlDuration").floatValue = FastHowl;
        director.FindProperty("retreatDuration").floatValue = FastRetreat;
    }

    private const string Level01ScenePath = "Assets/_Game/Scenes/Level_01.unity";
    private const string ResultPanelPrefabPath = "Assets/_Game/Content/Perfabs/UI/ResultPanel.prefab";
    private const string Level01WolfRootName = "WolfSystem";
    private const string Level01WolfHudName = "WolfEventHud";

    /// <summary>
    /// 把狼群节奏接进 Level_01：WolfSystem（生成器 + 节奏 + 失败结算）+ GameCanvas 下的节奏 HUD。
    /// 可重复执行，会先删掉上一次生成的对象。
    /// </summary>
    [MenuItem("Game Jam/Wolf Test/Integrate Wolves Into Level_01")]
    public static void IntegrateIntoLevel01()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != Level01ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            scene = EditorSceneManager.OpenScene(Level01ScenePath, OpenSceneMode.Single);
        }

        Sprite circleSprite = SheepMovementPrototypeSetup.GetOrCreateCircleSprite();
        Sprite warningSprite = GetOrCreateWarningRectSprite();
        EnsureFolder(WolfPrefabFolder);
        GameObject wolfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WolfPrefabPath);
        if (wolfPrefab == null)
        {
            wolfPrefab = CreateOrUpdateWolfPrefab(circleSprite, warningSprite);
        }

        GameObject[] roots = scene.GetRootGameObjects();
        FlockController flock = roots.Select(r => r.GetComponentInChildren<FlockController>(true)).FirstOrDefault(f => f != null);
        Canvas canvas = roots.Where(r => r.name == "GameCanvas").Select(r => r.GetComponent<Canvas>()).FirstOrDefault(c => c != null)
            ?? roots.Select(r => r.GetComponentInChildren<Canvas>(true)).FirstOrDefault(c => c != null);
        PauseManager pauseManager = roots.Select(r => r.GetComponentInChildren<PauseManager>(true)).FirstOrDefault(p => p != null);

        if (flock == null || canvas == null)
        {
            throw new System.InvalidOperationException("Level_01 needs a FlockController and a GameCanvas before wolves can be integrated.");
        }

        // 清理上一次集成的对象。
        foreach (GameObject root in roots)
        {
            if (root.name == Level01WolfRootName)
            {
                Object.DestroyImmediate(root);
            }
        }
        Transform oldHud = canvas.transform.Find(Level01WolfHudName);
        if (oldHud != null)
        {
            Object.DestroyImmediate(oldHud.gameObject);
        }

        GameObject wolfRoot = new GameObject(Level01WolfRootName);
        SceneManager.MoveGameObjectToScene(wolfRoot, scene);

        WolfSpawner spawner = wolfRoot.AddComponent<WolfSpawner>();
        SerializedObject spawnerSerialized = new SerializedObject(spawner);
        spawnerSerialized.FindProperty("flock").objectReferenceValue = flock;
        spawnerSerialized.FindProperty("wolfPrefab").objectReferenceValue = wolfPrefab.GetComponent<Wolf>();
        spawnerSerialized.FindProperty("spawnDistance").floatValue = 12f;
        spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

        WolfEventDirector director = wolfRoot.AddComponent<WolfEventDirector>();
        SerializedObject directorSerialized = new SerializedObject(director);
        directorSerialized.FindProperty("spawner").objectReferenceValue = spawner;
        directorSerialized.FindProperty("flock").objectReferenceValue = flock;
        directorSerialized.FindProperty("requiredMemberCount").intValue = 6;
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        ResultPanelView resultPanelPrefab = null;
        GameObject resultPanelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ResultPanelPrefabPath);
        if (resultPanelAsset != null)
        {
            resultPanelPrefab = resultPanelAsset.GetComponent<ResultPanelView>();
        }

        WolfDefeatHandler defeat = wolfRoot.AddComponent<WolfDefeatHandler>();
        SerializedObject defeatSerialized = new SerializedObject(defeat);
        defeatSerialized.FindProperty("flock").objectReferenceValue = flock;
        defeatSerialized.FindProperty("flockMovement").objectReferenceValue = flock.GetComponent<FlockMovementController>();
        defeatSerialized.FindProperty("director").objectReferenceValue = director;
        defeatSerialized.FindProperty("pauseManager").objectReferenceValue = pauseManager;
        defeatSerialized.FindProperty("resultPanelPrefab").objectReferenceValue = resultPanelPrefab;
        defeatSerialized.FindProperty("canvas").objectReferenceValue = canvas;
        defeatSerialized.ApplyModifiedPropertiesWithoutUndo();

        // 顶部居中的节奏 HUD（左上是任务列表，右上是入队提示，避开它们）。
        RectTransform hudRect = MvpUiFactory.CreateRect(Level01WolfHudName, canvas.transform);
        MvpUiFactory.Anchor(
            hudRect,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -16f),
            new Vector2(520f, 80f));
        hudRect.pivot = new Vector2(0.5f, 1f);
        hudRect.anchoredPosition = new Vector2(0f, -16f);
        WolfEventHudView hud = hudRect.gameObject.AddComponent<WolfEventHudView>();
        SerializedObject hudSerialized = new SerializedObject(hud);
        hudSerialized.FindProperty("director").objectReferenceValue = director;
        hudSerialized.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = wolfRoot;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Wolves integrated into Level_01: WolfSystem (spawner + rhythm director + defeat handler) and WolfEventHud under GameCanvas. " +
                  "Countdown starts once the flock reaches 6 sheep; the flock huddles during the howl warning.");
    }

    [MenuItem("Game Jam/Wolf Test/Open TestWolf Scene")]
    public static void OpenTestWolfScene()
    {
        OpenExistingScene(ScenePath, "Setup TestWolf Scene");
    }

    [MenuItem("Game Jam/Wolf Test/Open TestSmartWolf Scene")]
    public static void OpenTestSmartWolfScene()
    {
        OpenExistingScene(SmartScenePath, "Setup TestSmartWolf Scene");
    }

    private static void OpenExistingScene(string scenePath, string setupMenuName)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            Debug.LogWarning($"{scenePath} does not exist yet. Run '{setupMenuName}' first.");
            return;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
    }

    private static Scene OpenOrCreateScene(string scenePath)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == scenePath)
            return activeScene;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            throw new System.OperationCanceledException("Wolf test scene setup was cancelled.");
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneTemplatePath) != null)
            {
                // 复制 URP 2D 模板，保留 Main Camera + Global Light 2D。
                if (!AssetDatabase.CopyAsset(SceneTemplatePath, scenePath))
                {
                    throw new System.InvalidOperationException($"Failed to copy {SceneTemplatePath} to {scenePath}.");
                }
            }
            else
            {
                Scene created = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(created, scenePath);
            }

            AssetDatabase.Refresh();
        }

        return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }

    private static void ClearManagedObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects().ToArray())
        {
            bool managed = ManagedRootNames.Contains(root.name)
                || root.name.StartsWith("Sheep_")
                || root.name.StartsWith("Wolf_");
            if (managed)
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static void CreateGrid(Scene scene)
    {
        Sprite gridSprite = AssetDatabase.LoadAllAssetsAtPath(GridAssetPath).OfType<Sprite>().FirstOrDefault();
        if (gridSprite == null)
            return;

        GameObject gridRoot = new GameObject("TestWolf_WorldGrid");
        SceneManager.MoveGameObjectToScene(gridRoot, scene);

        GameObject grid = new GameObject("BackgroundGrid");
        grid.transform.SetParent(gridRoot.transform, false);
        grid.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
        SpriteRenderer renderer = grid.AddComponent<SpriteRenderer>();
        renderer.sprite = gridSprite;
        renderer.sortingOrder = -200;
    }

    private static GameObject CreateFlock(
        Scene scene,
        GameObject sheepPrefab,
        int sheepCount,
        out FlockController flock,
        out FlockMovementController movement)
    {
        GameObject flockObject = new GameObject("SheepFlock");
        SceneManager.MoveGameObjectToScene(flockObject, scene);
        flockObject.transform.position = Vector3.zero;

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

        // 内圈 3 只、中圈 7 只，再多的羊排到外圈，围绕羊群中心。
        int[] ringCapacity = { 3, 7, Mathf.Max(0, sheepCount - 10) };
        float[] ringRadius = { 0.8f, 1.6f, 2.6f };

        List<SheepMember> members = new List<SheepMember>();
        for (int index = 0; index < sheepCount; index++)
        {
            GameObject sheep = (GameObject)PrefabUtility.InstantiatePrefab(sheepPrefab, scene);
            sheep.name = $"Sheep_{index + 1:00}";

            int ring = index < 3 ? 0 : index < 10 ? 1 : 2;
            int ringIndex = index - (ring == 0 ? 0 : ring == 1 ? 3 : 10);
            int ringCount = Mathf.Max(1, ringCapacity[ring]);
            float radius = ringRadius[ring];
            float angle = ringIndex * Mathf.PI * 2f / ringCount;
            sheep.transform.position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            SheepMember member = sheep.GetComponent<SheepMember>();
            if (member == null)
            {
                member = sheep.AddComponent<SheepMember>();
            }
            members.Add(member);
        }

        SerializedObject flockSerialized = new SerializedObject(flock);
        flockSerialized.FindProperty("movementController").objectReferenceValue = movement;
        SerializedProperty startingMembers = flockSerialized.FindProperty("startingMembers");
        startingMembers.arraySize = members.Count;
        for (int index = 0; index < members.Count; index++)
        {
            startingMembers.GetArrayElementAtIndex(index).objectReferenceValue = members[index];
        }
        flockSerialized.ApplyModifiedPropertiesWithoutUndo();

        return flockObject;
    }

    private static WolfSpawner CreateSpawner(Scene scene, FlockController flock, GameObject wolfPrefab)
    {
        GameObject spawnerObject = new GameObject("WolfSpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        WolfSpawner spawner = spawnerObject.AddComponent<WolfSpawner>();

        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("flock").objectReferenceValue = flock;
        serialized.FindProperty("wolfPrefab").objectReferenceValue = wolfPrefab.GetComponent<Wolf>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }

    private static WolfEventDirector CreateEventDirector(Scene scene, WolfSpawner spawner)
    {
        GameObject directorObject = new GameObject("WolfEventDirector");
        SceneManager.MoveGameObjectToScene(directorObject, scene);
        WolfEventDirector director = directorObject.AddComponent<WolfEventDirector>();

        SerializedObject serialized = new SerializedObject(director);
        serialized.FindProperty("spawner").objectReferenceValue = spawner;
        // Dev 测试场景用缩短的节奏，正式关卡（Level_01 / Alpha）保持 15~20 秒。
        ApplyFastWolfRhythm(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return director;
    }

    /// <summary>TestSmartWolf 的两个调试窗口：当前学习率 + 当前预测角度（IMGUI，可拖动，F1 隐藏）。</summary>
    private static void CreateSmartWolfDebugView(Scene scene, WolfSpawner spawner, WolfEventDirector director)
    {
        GameObject viewObject = new GameObject("SmartWolfDebugView");
        SceneManager.MoveGameObjectToScene(viewObject, scene);
        SmartWolfDebugView view = viewObject.AddComponent<SmartWolfDebugView>();

        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("spawner").objectReferenceValue = spawner;
        serialized.FindProperty("director").objectReferenceValue = director;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateGameController(
        Scene scene,
        FlockController flock,
        FlockMovementController movement,
        WolfSpawner spawner,
        WolfEventDirector director)
    {
        GameObject controllerObject = new GameObject("WolfTestGameController");
        SceneManager.MoveGameObjectToScene(controllerObject, scene);
        WolfTestGameController controller = controllerObject.AddComponent<WolfTestGameController>();

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("flock").objectReferenceValue = flock;
        serialized.FindProperty("flockMovement").objectReferenceValue = movement;
        serialized.FindProperty("spawner").objectReferenceValue = spawner;
        serialized.FindProperty("eventDirector").objectReferenceValue = director;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>右上角的狼群节奏 HUD（图标 + 文案 + 倒计时）。</summary>
    private static void CreateEventHud(Scene scene, WolfEventDirector director)
    {
        GameObject canvasObject = new GameObject("WolfEventCanvas", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform hudRect = MvpUiFactory.CreateRect("WolfEventHud", canvasObject.transform);
        MvpUiFactory.Anchor(
            hudRect,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-260f, -40f),
            new Vector2(520f, 80f));
        hudRect.pivot = new Vector2(1f, 1f);
        hudRect.anchoredPosition = new Vector2(-20f, -20f);

        WolfEventHudView hud = hudRect.gameObject.AddComponent<WolfEventHudView>();
        SerializedObject serialized = new SerializedObject(hud);
        serialized.FindProperty("director").objectReferenceValue = director;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCamera(Scene scene, Transform target)
    {
        Camera camera = scene.GetRootGameObjects()
            .Select(root => root.GetComponentInChildren<Camera>(true))
            .FirstOrDefault(found => found != null);

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        camera.orthographic = true;
        camera.orthographicSize = 8f;

        CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<CameraFollow2D>();
        }

        SerializedObject serialized = new SerializedObject(follow);
        serialized.FindProperty("target").objectReferenceValue = target;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(camera);
    }

    private static GameObject CreateOrUpdateWolfPrefab(Sprite circleSprite, Sprite warningSprite)
    {
        GameObject root = new GameObject("Wolf");
        try
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.useFullKinematicContacts = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.7f;

            // 狼身：红色圆形。
            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(root.transform, false);
            bodyObject.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            SpriteRenderer bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = circleSprite;
            bodyRenderer.color = new Color(0.9f, 0.15f, 0.12f, 1f);
            bodyRenderer.sortingOrder = 20;

            // 冲锋预警：从狼的位置沿冲锋方向拉伸的红色长方形。
            GameObject warningObject = new GameObject("ChargeWarning");
            warningObject.transform.SetParent(root.transform, false);
            warningObject.transform.localScale = new Vector3(6f, 1.5f, 1f);
            SpriteRenderer warningRenderer = warningObject.AddComponent<SpriteRenderer>();
            warningRenderer.sprite = warningSprite;
            warningRenderer.color = new Color(1f, 0.2f, 0.1f, 0.3f);
            warningRenderer.sortingOrder = -50;
            warningRenderer.enabled = false;

            Wolf wolf = root.AddComponent<Wolf>();
            SerializedObject serialized = new SerializedObject(wolf);
            serialized.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
            serialized.FindProperty("warningRenderer").objectReferenceValue = warningRenderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, WolfPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>1x1 单位的白色方块，pivot 在左侧中点，方便沿冲锋方向拉伸。</summary>
    private static Sprite GetOrCreateWarningRectSprite()
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(WarningRectAssetPath).OfType<Sprite>().FirstOrDefault();
        if (sprite != null)
            return sprite;

        const int size = 4;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "WolfWarningRectTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = Enumerable.Repeat(Color.white, size * size).ToArray();
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, WarningRectAssetPath);

        sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0f, 0.5f), size);
        sprite.name = "WolfWarningRectSprite";
        AssetDatabase.AddObjectToAsset(sprite, texture);
        AssetDatabase.ImportAsset(WarningRectAssetPath);
        return sprite;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }
            current = next;
        }
    }
}

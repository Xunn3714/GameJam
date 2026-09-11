using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class CreditsSceneSetup
{
    public const string PrefabPath =
        "Assets/_Game/Content/Perfabs/UI/CreditsScene.prefab";
    public const string PreviewScenePath =
        "Assets/_Game/Scenes/Credits.unity";

    private const string BuilderVersion = "credits-scene-v16-longer-opening";

    private const string MainMenuScenePath =
        "Assets/_Game/Scenes/MainMenu.unity";
    public const string FontPath =
        "Assets/_Game/Content/Fonts/CreditsChinese SDF.asset";
    private const string SourceFontPath =
        "Assets/_Game/Content/Fonts/Resources/NotoSansSC-Regular.otf";
    private const string GrassPath =
        "Assets/Art/WorldSprites/Tiles/草原_背景.png";
    private const string MainMenuTitlePath =
        "Assets/_Game/Content/Art/UI/MainMenuTitle.png";
    private const string NormalSheepPath =
        "Assets/Art/SheepSprites/Sheep_Normal.png";
    private const string BlackSheepPath =
        "Assets/Art/SheepSprites/Sheep_Special_Black.png";
    private const string TopHatSheepPath =
        "Assets/Art/SheepSprites/Sheep_Special_TopHat.png";
    private const string BowSheepPath =
        "Assets/Art/SheepSprites/Sheep_Special_RedBow.png";
    private const string HornedSheepPath =
        "Assets/Art/SheepSprites/Sheep_Special_Horned.png";
    private const string PoopPath =
        "Assets/Art/SkillSprites/Poop/Poop_Cartoon.png";
    private const string BackButtonPath =
        "Assets/Art/UI/Buttons/225_85各类退出按钮.png";

    private const string CreditsCharacters =
        "制作与设计技术开发美术设计音乐与音效测试与鸣谢负责人项目统筹游戏程序二维剧情文案关卡曲目提供内部全体成员特别感谢每一位聚拢羊群的玩家" +
        "出品翼光计划武汉全球高校游戏创作挑战赛参赛作品按住空格加速返回游玩" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_·—\n ";

    private const float WorldTextScale = 0.18f;
    private static readonly Color SectionColor = new Color32(92, 76, 45, 255);
    private static readonly Color RoleColor = new Color32(255, 250, 224, 255);
    private static readonly Color NameColor = new Color32(255, 255, 255, 245);
    [InitializeOnLoadMethod]
    private static void BuildMissingAssetsAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            bool prefabMissing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            bool sceneMissing = AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScenePath) == null;
            AssetImporter prefabImporter = AssetImporter.GetAtPath(PrefabPath);
            bool versionChanged = prefabImporter == null || prefabImporter.userData != BuilderVersion;
            if (prefabMissing || sceneMissing || versionChanged)
                BuildAndIntegrate();
        };
    }


    [MenuItem("Game Jam/UI/Build Credits Scene")]
    public static void BuildAndIntegrate()
    {
        EnsureCreditsFont();
        GameObject prefab = BuildScenePrefab();
        BuildPreviewScene(prefab);
        IntegrateMainMenu(prefab);
        AssetDatabase.SaveAssets();
        UiScreenshotCapture.CaptureCreditsScenePreview();
        AssetDatabase.Refresh();
        Debug.Log("Credits scene, runtime prefab, and main-menu entry were rebuilt.");
    }


    private static GameObject BuildScenePrefab()
    {
        GameObject root = new GameObject("CreditsScene");
        root.SetActive(false);

        try
        {
            CreditsSceneController controller = root.AddComponent<CreditsSceneController>();

            GameObject cameraObject = CreateChild(root.transform, "Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(178, 210, 128, 255);
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            cameraObject.AddComponent<AudioListener>();

            Transform startMarker = CreateMarker(root.transform, "ScrollStart", Vector2.zero);
            Transform endMarker = CreateMarker(root.transform, "ScrollEnd", new Vector2(0f, -115f));
            controller.Configure(camera, startMarker, endMarker);

            Transform world = CreateChild(root.transform, "CreditsWorld").transform;
            BuildBackground(world);
            BuildCreditsText(world);
            BuildDecorations(world);
            BuildOverlay(root.transform, controller);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException($"Could not save credits prefab at {PrefabPath}.");

            AssetImporter importer = AssetImporter.GetAtPath(PrefabPath);
            if (importer != null)
            {
                importer.userData = BuilderVersion;
                importer.SaveAndReimport();
            }

            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }


    private static void BuildBackground(Transform parent)
    {
        Sprite grass = AssetDatabase.LoadAssetAtPath<Sprite>(GrassPath);
        if (grass == null)
            throw new FileNotFoundException("Credits grass background is missing.", GrassPath);

        float targetWidth = 39.4f;
        float scale = targetWidth / grass.bounds.size.x;
        float tileHeight = grass.bounds.size.y * scale;
        for (int index = 0; index < 5; index++)
        {
            GameObject tile = CreateChild(parent, $"Grass_{index + 1:00}");
            tile.transform.localPosition = new Vector3(0f, -tileHeight * index, 2f);
            tile.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = grass;
            renderer.sortingOrder = -100;
        }
    }


    private static void BuildCreditsText(Transform parent)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new FileNotFoundException("Credits TMP font is missing.", FontPath);

        CreateWorldText(parent, "StudioCredit", "Pkat Studio 出品", new Vector2(0f, 7.4f),
            40f, 150f, 36f, font, SectionColor, FontStyles.Bold);
        CreateWorldText(parent, "EventCredit",
            "2026 翼光计划  ·  武汉全球高校游戏创作挑战赛参赛作品",
            new Vector2(0f, -0.5f), 26f, 180f, 32f, font, SectionColor);

        CreateSectionHeading(parent, "ProductionDesignSection", "—  制作与设计  —", -4.2f, font);
        CreateCreditEntry(parent, "GameDesignLead", "游戏设计负责人", "KSLJ  ·  PKAT", -8f, font);
        CreateCreditEntry(parent, "ProjectManager", "项目统筹", "DARCY  ·  KSLJ  ·  PKAT  ·  XUNN", -14.5f, font);
        CreateCreditEntry(parent, "GameDesigner", "游戏设计", "DARCY  ·  GAILTY  ·  KSLJ  ·  PKAT", -21f, font);
        CreateCreditEntry(parent, "LevelDesigner", "关卡设计", "3WATER  ·  GAILTY  ·  PKAT", -27.5f, font);
        CreateCreditEntry(parent, "ScriptWriter", "剧情文案", "GAILTY  ·  PKAT  ·  WHILIST", -34f, font);

        CreateSectionHeading(parent, "TechnologySection", "—  技术开发  —", -41.5f, font);
        CreateCreditEntry(parent, "TechnicalLead", "技术负责人", "DARCY", -45.5f, font);
        CreateCreditEntry(parent, "Programmer", "程序开发",
            "3WATER  ·  DARCY  ·  JIMMY\nKSLJ  ·  W1K  ·  XUNN", -52f, font);

        CreateSectionHeading(parent, "ArtSection", "—  美术设计  —", -59.5f, font);
        CreateCreditEntry(parent, "ArtLead", "美术负责人", "ANKI0_0", -63.5f, font);
        CreateCreditEntry(parent, "2DArtist", "2D 美术", "ANKI0_0  ·  GAILTY  ·  PKAT  ·  XUNN", -70f, font);

        CreateSectionHeading(parent, "AudioSection", "—  音乐与音效  —", -77.5f, font);
        CreateCreditEntry(parent, "MusicDesigner", "音乐设计", "WHILIST", -81.5f, font);
        CreateCreditEntry(parent, "SoundEffectDesigner", "音效设计", "GAILTY  ·  JIMMY  ·  WHILIST", -88f, font);
        CreateCreditEntry(parent, "MusicCredit", "曲目提供", "O2I3", -94.5f, font);

        CreateSectionHeading(parent, "ThanksSection", "—  测试与鸣谢  —", -102f, font);
        CreateCreditEntry(parent, "PlayTester", "内部测试", "Pkat Studio 全体成员", -106f, font);
        CreateCreditEntry(parent, "SpecialThanks", "特别感谢",
            "每一位聚拢羊群的玩家", -112.5f, font);
        CreateWorldText(parent, "ThankYou", "感谢游玩", new Vector2(0f, -121.5f),
            52f, 150f, 48f, font, RoleColor, FontStyles.Bold);
    }


    private static void BuildDecorations(Transform parent)
    {
        CreateDecoration(parent, "GameTitle", MainMenuTitlePath, new Vector2(0f, 3.4f), 9.2f, false, 0f, false);
        CreateDecoration(parent, "Sheep_Normal_01", NormalSheepPath, new Vector2(8.2f, -13f), 4.5f, false, 0f);
        CreateDecoration(parent, "Sheep_Black", BlackSheepPath, new Vector2(-8.4f, -35f), 4.4f, true, 0.8f);
        CreateDecoration(parent, "Sheep_TopHat", TopHatSheepPath, new Vector2(8f, -52f), 4.5f, false, 1.6f);
        CreateDecoration(parent, "Sheep_RedBow", BowSheepPath, new Vector2(-8f, -70f), 4.5f, true, 2.4f);
        CreateDecoration(parent, "Sheep_Horned", HornedSheepPath, new Vector2(8.2f, -89f), 4.5f, false, 3.2f);
        CreateDecoration(parent, "Sheep_Normal_02", NormalSheepPath, new Vector2(-8.2f, -113f), 4.5f, true, 4f);

        CreateDecoration(parent, "Poop_01", PoopPath, new Vector2(-7.4f, -8f), 1.25f, false, 0.4f, false);
        CreateDecoration(parent, "Poop_02", PoopPath, new Vector2(7.5f, -42f), 1.15f, false, 1.2f, false);
        CreateDecoration(parent, "Poop_03", PoopPath, new Vector2(-7.2f, -82f), 1.2f, false, 2f, false);
        CreateDecoration(parent, "Poop_04", PoopPath, new Vector2(7.4f, -117f), 1.1f, false, 2.8f, false);
    }


    private static void BuildOverlay(Transform parent, CreditsSceneController controller)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite backSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackButtonPath);

        GameObject overlay = new GameObject(
            "ControlsCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        overlay.transform.SetParent(parent, false);

        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        TextMeshProUGUI hint = CreateUiText(overlay.transform, "Hint", font);
        hint.text = "按住空格加速  ·  Esc 返回";
        hint.fontSize = 22f;
        hint.alignment = TextAlignmentOptions.Left;
        hint.color = new Color32(255, 253, 236, 225);
        SetRect(hint.rectTransform, new Vector2(0f, 1f), new Vector2(42f, -34f), new Vector2(460f, 52f), new Vector2(0f, 1f));

        GameObject buttonObject = new GameObject(
            "Btn_Back",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        buttonObject.transform.SetParent(overlay.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        SetRect(buttonRect, Vector2.one, new Vector2(-120f, -58f), new Vector2(190f, 72f), Vector2.one);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = backSprite;
        buttonImage.color = Color.white;
        buttonImage.raycastTarget = false;
        controller.ConfigureBackButton(buttonRect, buttonImage);

        TextMeshProUGUI label = CreateUiText(buttonObject.transform, "Label", font);
        label.text = "返回";
        label.fontSize = 24f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color32(65, 57, 39, 255);
        SetStretch(label.rectTransform, new Vector2(12f, 8f));
    }


    private static void BuildPreviewScene(GameObject prefab)
    {
        Scene previousActive = SceneManager.GetActiveScene();
        Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            instance.SetActive(true);
            EditorSceneManager.SaveScene(preview, PreviewScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(preview, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);
        }
    }


    private static void IntegrateMainMenu(GameObject creditsPrefab)
    {
        Scene scene = SceneManager.GetSceneByPath(MainMenuScenePath);
        bool openedForEdit = !scene.IsValid() || !scene.isLoaded;
        if (openedForEdit)
            scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject legacyDevelopers = Find(scene, "DevelopersPanel");
            GameObject legacyCredits = Find(scene, "CreditsPanel");
            if (legacyDevelopers != null)
                Object.DestroyImmediate(legacyDevelopers);
            if (legacyCredits != null)
                Object.DestroyImmediate(legacyCredits);

            MainMenuController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true))
                .FirstOrDefault();
            if (controller == null)
                throw new System.InvalidOperationException("MainMenuController was not found in MainMenu.");

            controller.creditsPanel = null;
            controller.developersPanel = null;
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("creditsScenePrefab").objectReferenceValue = creditsPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Button developersButton = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .FirstOrDefault(button => button.gameObject.name == "Btn_Developers");
            if (developersButton == null)
                throw new System.InvalidOperationException("Btn_Developers was not found in MainMenu.");

            for (int index = developersButton.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
                UnityEventTools.RemovePersistentListener(developersButton.onClick, index);
            UnityEventTools.AddPersistentListener(developersButton.onClick, controller.ShowCredits);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(developersButton);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedForEdit)
                EditorSceneManager.CloseScene(scene, true);
        }
    }


    private static void CreateSectionHeading(
        Transform parent,
        string name,
        string value,
        float y,
        TMP_FontAsset font)
    {
        CreateWorldText(parent, name, value, new Vector2(0f, y),
            52f, 150f, 44f, font, SectionColor, FontStyles.Bold);
    }


    private static void CreateCreditEntry(
        Transform parent,
        string name,
        string role,
        string members,
        float y,
        TMP_FontAsset font)
    {
        CreateWorldText(parent, $"{name}_Role", role, new Vector2(0f, y + 0.9f),
            44f, 150f, 40f, font, RoleColor, FontStyles.Bold);
        TextMeshPro names = CreateWorldText(parent, $"{name}_Names", members, new Vector2(0f, y - 1.1f),
            36f, 160f, 72f, font, NameColor, FontStyles.Normal);
        names.lineSpacing = 10f;
    }


    private static TextMeshPro CreateWorldText(
        Transform parent,
        string name,
        string value,
        Vector2 position,
        float fontSize,
        float width,
        float height,
        TMP_FontAsset font,
        Color color,
        FontStyles style = FontStyles.Normal)
    {
        GameObject textObject = CreateChild(parent, name);
        textObject.transform.localPosition = new Vector3(position.x, position.y, 0f);
        textObject.transform.localScale = Vector3.one * WorldTextScale;
        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.sortingOrder = 10;
        text.rectTransform.sizeDelta = new Vector2(width, height);
        return text;
    }


    private static void CreateDecoration(
        Transform parent,
        string name,
        string assetPath,
        Vector2 position,
        float targetWidth,
        bool flipX,
        float phase,
        bool animate = true)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
            throw new FileNotFoundException($"Credits decoration is missing: {assetPath}", assetPath);

        GameObject decoration = CreateChild(parent, name);
        decoration.transform.localPosition = new Vector3(position.x, position.y, 0f);
        float scale = targetWidth / sprite.bounds.size.x;
        decoration.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer renderer = decoration.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.flipX = flipX;
        renderer.sortingOrder = 0;

        if (animate)
        {
            CreditsDecorationMotion motion = decoration.AddComponent<CreditsDecorationMotion>();
            motion.Configure(0.18f, 0.1f, 0.75f, phase);
        }
    }


    private static void EnsureCreditsFont()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
            throw new FileNotFoundException("Credits source font is missing.", SourceFontPath);

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                72,
                7,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                false);
            if (font == null)
                throw new System.InvalidOperationException("Could not create the dedicated credits font.");

            font.name = "CreditsChinese SDF";
            AssetDatabase.CreateAsset(font, FontPath);
            AddFontSubAssets(font);
        }

        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        if (!font.TryAddCharacters(CreditsCharacters, out string missingCharacters))
            Debug.LogWarning($"Credits font is missing characters: {missingCharacters}");
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
    }


    private static void AddFontSubAssets(TMP_FontAsset font)
    {
        if (font.material != null && !AssetDatabase.Contains(font.material))
        {
            font.material.name = $"{font.name} Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
        }

        IReadOnlyList<Texture2D> atlases = font.atlasTextures;
        if (atlases == null)
            return;

        for (int index = 0; index < atlases.Count; index++)
        {
            Texture2D atlas = atlases[index];
            if (atlas == null || AssetDatabase.Contains(atlas))
                continue;
            atlas.name = $"{font.name} Atlas {index}";
            AssetDatabase.AddObjectToAsset(atlas, font);
        }
    }


    private static Transform CreateMarker(Transform parent, string name, Vector2 position)
    {
        Transform marker = CreateChild(parent, name).transform;
        marker.localPosition = new Vector3(position.x, position.y, 0f);
        return marker;
    }


    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }


    private static TextMeshProUGUI CreateUiText(Transform parent, string name, TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.raycastTarget = false;
        return text;
    }


    private static void SetRect(
        RectTransform rect,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }


    private static void SetStretch(RectTransform rect, Vector2 inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = inset;
        rect.offsetMax = -inset;
    }


    private static GameObject Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform result = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == name);
            if (result != null)
                return result.gameObject;
        }

        return null;
    }
}

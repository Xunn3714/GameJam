#if UNITY_EDITOR
using System.IO;
using GameJam.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace GameJam.Game.UI.Editor
{
    /// <summary>
    /// Creates the menu through Unity's scene API, preserving GUID/reference safety instead of
    /// requiring hand-authored Scene or Prefab YAML.
    /// </summary>
    public static class GameMenuUICreator
    {
        private const string DevScenePath = "Assets/_Game/Scenes/Dev/MenuUiDev.unity";

        private static readonly Color CanvasBackground = new Color(0.035f, 0.055f, 0.12f, 1f);
        private static readonly Color PanelColor = new Color(0.075f, 0.11f, 0.22f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.17f, 0.32f, 0.62f, 1f);
        private static readonly Color SecondaryButtonColor = new Color(0.15f, 0.2f, 0.35f, 1f);
        private static readonly Color AccentColor = new Color(0.38f, 0.82f, 1f, 1f);
        private static readonly Color MainTextColor = new Color(0.93f, 0.96f, 1f, 1f);
        private static readonly Color SubtleTextColor = new Color(0.68f, 0.77f, 0.92f, 1f);

        [MenuItem("GameJam/UI/Create Menu UI In Active Scene", priority = 10)]
        private static void CreateMenuUiInActiveScene()
        {
            if (Object.FindFirstObjectByType<MenuUIController>() != null)
            {
                EditorUtility.DisplayDialog("Menu UI already exists", "The active scene already contains a MenuUIController.", "OK");
                return;
            }

            var root = CreateMenuUi();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
        }

        [MenuItem("GameJam/UI/Create Menu UI Dev Scene", priority = 11)]
        public static void CreateDevScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DevScenePath) != null)
            {
                EditorUtility.DisplayDialog(
                    "Dev scene already exists",
                    "MenuUiDev.unity already exists. Open it and use GameJam/UI/Create Menu UI In Active Scene only if it has no menu yet.",
                    "OK");
                return;
            }

            EnsureFolder("Assets/_Game");
            EnsureFolder("Assets/_Game/Scenes");
            EnsureFolder("Assets/_Game/Scenes/Dev");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            var root = CreateMenuUi();
            EditorSceneManager.SaveScene(scene, DevScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log($"Created menu UI development scene at {DevScenePath}.");
        }

        private static GameObject CreateMenuUi()
        {
            Undo.SetCurrentGroupName("Create Game Menu UI");
            var undoGroup = Undo.GetCurrentGroup();

            EnsureEventSystem();

            var root = CreateUiObject("GameMenuUI", null);
            var canvas = Undo.AddComponent<Canvas>(root);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = Undo.AddComponent<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Undo.AddComponent<GraphicRaycaster>(root);

            var controller = Undo.AddComponent<MenuUIController>(root);
            var bgmSource = Undo.AddComponent<AudioSource>(root);
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;

            var shake = EnsureCameraShake();
            var motionLayer = CreateRawImage("BackgroundMotion", root.transform, CanvasBackground);
            SetStretch(motionLayer.rectTransform, -80f, -80f, -80f, -80f);
            motionLayer.raycastTarget = false;
            motionLayer.texture = Texture2D.whiteTexture;

            var spriteLayer = CreateImage("SpriteBackground", motionLayer.transform, Color.white);
            SetStretch(spriteLayer.rectTransform, 0f, 0f, 0f, 0f);
            spriteLayer.raycastTarget = false;
            spriteLayer.enabled = false;

            CreateAmbientShape("Ambient Glow Left", motionLayer.transform, new Vector2(-670f, 230f), new Vector2(880f, 620f), new Color(0.11f, 0.47f, 0.85f, 0.16f), -18f);
            CreateAmbientShape("Ambient Glow Right", motionLayer.transform, new Vector2(700f, -310f), new Vector2(950f, 620f), new Color(0.54f, 0.24f, 0.86f, 0.16f), 19f);
            CreateAmbientShape("Accent Bar", motionLayer.transform, new Vector2(0f, 430f), new Vector2(1500f, 7f), new Color(0.38f, 0.82f, 1f, 0.45f), 0f);

            var backgroundMotion = Undo.AddComponent<MenuBackgroundMotion>(motionLayer.gameObject);
            backgroundMotion.Configure(motionLayer.rectTransform, motionLayer, spriteLayer, shake);

            var heading = CreateText("Game Title", root.transform, "GAME JAM", 66, FontStyle.Bold, TextAnchor.MiddleCenter, AccentColor);
            SetCentered(heading.rectTransform, 900f, 96f, new Vector2(0f, 390f));
            var subtitle = CreateText("Game Subtitle", root.transform, "2D ADVENTURE", 22, FontStyle.Bold, TextAnchor.MiddleCenter, SubtleTextColor);
            SetCentered(subtitle.rectTransform, 900f, 36f, new Vector2(0f, 328f));

            var mainPanel = CreatePanel("Main Panel", root.transform, new Vector2(560f, 470f));
            SetCentered(mainPanel.GetComponent<RectTransform>(), 560f, 470f, new Vector2(0f, -5f));
            var mainTitle = CreateText("Title", mainPanel.transform, "主菜单", 38, FontStyle.Bold, TextAnchor.MiddleCenter, MainTextColor);
            SetCentered(mainTitle.rectTransform, 440f, 62f, new Vector2(0f, 155f));
            var mainDescription = CreateText("Description", mainPanel.transform, "选择一个选项，开始你的旅程。", 20, FontStyle.Normal, TextAnchor.MiddleCenter, SubtleTextColor);
            SetCentered(mainDescription.rectTransform, 440f, 52f, new Vector2(0f, 95f));
            CreateButton("Start Button", mainPanel.transform, "开始游戏", new Vector2(0f, 25f), ButtonColor, controller, MenuButtonAction.ShowStart, out _);
            CreateButton("Settings Button", mainPanel.transform, "设置", new Vector2(0f, -55f), SecondaryButtonColor, controller, MenuButtonAction.ShowSettings, out _);
            CreateButton("Popup Button", mainPanel.transform, "弹窗示例", new Vector2(0f, -135f), SecondaryButtonColor, controller, MenuButtonAction.ShowPopup, out _);

            var startPanel = CreatePanel("Start Panel", root.transform, new Vector2(600f, 440f));
            SetCentered(startPanel.GetComponent<RectTransform>(), 600f, 440f, new Vector2(0f, -5f));
            var startTitle = CreateText("Title", startPanel.transform, "开始游戏", 38, FontStyle.Bold, TextAnchor.MiddleCenter, MainTextColor);
            SetCentered(startTitle.rectTransform, 500f, 60f, new Vector2(0f, 145f));
            var startDescription = CreateText("Description", startPanel.transform, "确认后将调用 MenuUIController 的 On Start Game 事件。\n将你的关卡加载逻辑绑定到这个事件即可。", 21, FontStyle.Normal, TextAnchor.MiddleCenter, SubtleTextColor);
            SetCentered(startDescription.rectTransform, 490f, 110f, new Vector2(0f, 52f));
            CreateButton("Confirm Start Button", startPanel.transform, "进入游戏", new Vector2(0f, -75f), ButtonColor, controller, MenuButtonAction.StartGame, out _);
            CreateButton("Back Button", startPanel.transform, "返回", new Vector2(0f, -155f), SecondaryButtonColor, controller, MenuButtonAction.ShowMain, out _);
            startPanel.SetActive(false);

            var settingsPanel = CreatePanel("Settings Panel", root.transform, new Vector2(600f, 400f));
            SetCentered(settingsPanel.GetComponent<RectTransform>(), 600f, 400f, new Vector2(0f, -5f));
            var settingsTitle = CreateText("Title", settingsPanel.transform, "设置", 38, FontStyle.Bold, TextAnchor.MiddleCenter, MainTextColor);
            SetCentered(settingsTitle.rectTransform, 500f, 60f, new Vector2(0f, 120f));
            var settingsDescription = CreateText("Description", settingsPanel.transform, "把 AudioClip 拖到 GameMenuUI 的 BGM Audio Source，\n此按钮会静音/恢复播放。", 19, FontStyle.Normal, TextAnchor.MiddleCenter, SubtleTextColor);
            SetCentered(settingsDescription.rectTransform, 500f, 80f, new Vector2(0f, 47f));
            CreateButton("BGM Toggle Button", settingsPanel.transform, "BGM：开", new Vector2(0f, -45f), ButtonColor, controller, MenuButtonAction.ToggleBgm, out var bgmLabel);
            CreateButton("Back Button", settingsPanel.transform, "返回", new Vector2(0f, -125f), SecondaryButtonColor, controller, MenuButtonAction.ShowMain, out _);
            settingsPanel.SetActive(false);

            var popup = CreatePopup(root.transform);
            popup.SetDefaultContent(new PopupContent(
                "游戏提示",
                "这是一个可复用弹窗。请在 Popup Dialog 组件中直接编辑标题、正文和 Options 的文本，或在运行时调用 Show(PopupContent)。",
                "知道了",
                "稍后再说"));
            popup.gameObject.SetActive(false);

            controller.Configure(mainPanel, startPanel, settingsPanel, popup, backgroundMotion, shake, bgmSource, bgmLabel);
            Undo.CollapseUndoOperations(undoGroup);
            return root;
        }

        private static PopupDialog CreatePopup(Transform parent)
        {
            var popupRoot = CreateUiObject("Popup Dialog", parent);
            SetStretch(popupRoot.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            var dialog = Undo.AddComponent<PopupDialog>(popupRoot);

            var dimmer = CreateImage("Dimmer", popupRoot.transform, new Color(0.005f, 0.01f, 0.03f, 0.76f));
            SetStretch(dimmer.rectTransform, 0f, 0f, 0f, 0f);

            var card = CreatePanel("Card", popupRoot.transform, new Vector2(680f, 440f));
            SetCentered(card.GetComponent<RectTransform>(), 680f, 440f, Vector2.zero);
            var title = CreateText("Title", card.transform, "标题", 34, FontStyle.Bold, TextAnchor.MiddleCenter, MainTextColor);
            SetCentered(title.rectTransform, 560f, 58f, new Vector2(0f, 150f));
            var body = CreateText("Body", card.transform, "正文", 20, FontStyle.Normal, TextAnchor.UpperLeft, SubtleTextColor);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            SetCentered(body.rectTransform, 540f, 128f, new Vector2(0f, 48f));

            var optionsRoot = CreateUiObject("Options", card.transform);
            var optionsRect = optionsRoot.GetComponent<RectTransform>();
            SetCentered(optionsRect, 510f, 145f, new Vector2(0f, -118f));
            var layout = Undo.AddComponent<VerticalLayoutGroup>(optionsRoot);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var template = CreatePopupOptionTemplate(optionsRoot.transform);
            dialog.Configure(title, body, optionsRoot.transform, template);
            return dialog;
        }

        private static Button CreatePopupOptionTemplate(Transform parent)
        {
            var root = CreateImage("Option Button Template", parent, ButtonColor);
            var button = Undo.AddComponent<Button>(root.gameObject);
            ConfigureButtonColors(button);
            var layoutElement = Undo.AddComponent<LayoutElement>(root.gameObject);
            layoutElement.preferredHeight = 58f;
            var label = CreateText("Label", root.transform, "选项", 21, FontStyle.Bold, TextAnchor.MiddleCenter, MainTextColor);
            SetStretch(label.rectTransform, 12f, 6f, 12f, 6f);
            root.gameObject.SetActive(false);
            return button;
        }

        private static void CreateCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.backgroundColor = CanvasBackground;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static CameraShake2D EnsureCameraShake()
        {
            CreateCamera();
            var mainCamera = Camera.main;
            var shake = mainCamera.GetComponent<CameraShake2D>();
            return shake != null ? shake : Undo.AddComponent<CameraShake2D>(mainCamera.gameObject);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create Event System");
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 size)
        {
            var image = CreateImage(name, parent, PanelColor);
            image.rectTransform.sizeDelta = size;
            return image.gameObject;
        }

        private static void CreateAmbientShape(string name, Transform parent, Vector2 position, Vector2 size, Color color, float rotation)
        {
            var shape = CreateRawImage(name, parent, color);
            SetCentered(shape.rectTransform, size.x, size.y, position);
            shape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            shape.raycastTarget = false;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string text,
            Vector2 position,
            Color color,
            MenuUIController controller,
            MenuButtonAction action,
            out Text label)
        {
            var image = CreateImage(name, parent, color);
            SetCentered(image.rectTransform, 390f, 62f, position);
            var button = Undo.AddComponent<Button>(image.gameObject);
            ConfigureButtonColors(button);
            var command = Undo.AddComponent<MenuButtonCommand>(image.gameObject);
            command.Configure(controller, action);

            label = CreateText("Label", image.transform, text, 23, FontStyle.Bold, TextAnchor.MiddleCenter, MainTextColor);
            SetStretch(label.rectTransform, 10f, 4f, 10f, 4f);
            return button;
        }

        private static void ConfigureButtonColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
            colors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var root = CreateUiObject(name, parent);
            var image = Undo.AddComponent<Image>(root);
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(string name, Transform parent, Color color)
        {
            var root = CreateUiObject(name, parent);
            var image = Undo.AddComponent<RawImage>(root);
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            var root = CreateUiObject(name, parent);
            var label = Undo.AddComponent<Text>(root);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color;
            label.supportRichText = true;
            label.raycastTarget = false;
            return label;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static void SetCentered(RectTransform rectTransform, float width, float height, Vector2 anchoredPosition)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(width, height);
            rectTransform.anchoredPosition = anchoredPosition;
        }

        private static void SetStretch(RectTransform rectTransform, float left, float bottom, float right, float top)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif

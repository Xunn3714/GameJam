using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Renders deterministic UI review images without entering Play Mode.</summary>
public static class UiScreenshotCapture
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 720;

    [MenuItem("Game Jam/UI/Capture Main Menu")]
    public static void CaptureMainMenu() => Capture("Assets/_Game/Scenes/MainMenu.unity", "main", "ui-main-menu.png");

    [MenuItem("Game Jam/UI/Capture Collection")]
    public static void CaptureCollection() => Capture("Assets/_Game/Scenes/MainMenu.unity", "collection", "ui-collection.png");

    [MenuItem("Game Jam/UI/Capture Statistics")]
    public static void CaptureStatistics() => Capture("Assets/_Game/Scenes/MainMenu.unity", "statistics", "ui-statistics.png");

    [MenuItem("Game Jam/UI/Capture Credits")]
    public static void CaptureCredits() => Capture("Assets/_Game/Scenes/MainMenu.unity", "credits", "ui-credits.png");

    [MenuItem("Game Jam/UI/Capture Main Menu Ultrawide")]
    public static void CaptureMainMenuUltrawide() => Capture("Assets/_Game/Scenes/MainMenu.unity", "main", "ui-main-menu-ultrawide.png", 1600, 720);

    [MenuItem("Game Jam/UI/Capture Main Menu 4x3")]
    public static void CaptureMainMenuFourThree() => Capture("Assets/_Game/Scenes/MainMenu.unity", "main", "ui-main-menu-4x3.png", 1024, 768);

    [MenuItem("Game Jam/UI/Capture Alpha Pause")]
    public static void CaptureAlphaPause() => Capture("Assets/_Game/Scenes/AlphaFlockExpansion.unity", "pause", "ui-alpha-pause.png");

    [MenuItem("Game Jam/UI/Capture Alpha Task")]
    public static void CaptureAlphaTask() => Capture("Assets/_Game/Scenes/AlphaFlockExpansion.unity", "task", "ui-alpha-task.png");

    [MenuItem("Game Jam/UI/Capture Alpha Settings")]
    public static void CaptureAlphaSettings() => Capture("Assets/_Game/Scenes/AlphaFlockExpansion.unity", "settings", "ui-alpha-settings.png");

    [MenuItem("Game Jam/UI/Capture Alpha Result")]
    public static void CaptureAlphaResult() => Capture("Assets/_Game/Scenes/AlphaFlockExpansion.unity", "result", "ui-alpha-result.png");

    private static void Capture(string scenePath, string mode, string fileName, int width = DefaultWidth, int height = DefaultHeight)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        PrepareView(scene, mode);

        Camera camera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault();
        GameObject temporaryCamera = null;
        if (camera == null)
        {
            temporaryCamera = new GameObject("UiCaptureCamera", typeof(Camera));
            camera = temporaryCamera.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(43, 50, 75, 255);
        }

        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previousTarget = camera.targetTexture;
        List<CanvasState> canvasStates = new List<CanvasState>();
        List<FontState> fontStates = new List<FontState>();
        try
        {
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>()
                         .Where(text => text.gameObject.scene == scene))
            {
                fontStates.Add(new FontState(text, text.font));
                MvpTmpUiFont.Apply(text);
            }

            foreach (Canvas canvas in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
            {
                canvasStates.Add(new CanvasState(canvas));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }

            Canvas.ForceUpdateCanvases();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();

            string output = Path.Combine(Path.GetTempPath(), "GameJamUiReviews", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, image.EncodeToPNG());
            Debug.Log($"UI screenshot captured: {output}");
        }
        finally
        {
            foreach (FontState state in fontStates)
                if (state.Text != null) state.Text.font = state.Font;
            foreach (CanvasState state in canvasStates)
                state.Restore();
            camera.targetTexture = previousTarget;
            RenderTexture.active = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            if (temporaryCamera != null)
                Object.DestroyImmediate(temporaryCamera);
        }
    }

    private static void PrepareView(Scene scene, string mode)
    {
        if (mode == "main" || mode == "collection" || mode == "statistics" || mode == "credits")
        {
            MainMenuController menu = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true))
                .FirstOrDefault();
            if (menu != null)
            {
                if (mode == "collection") menu.ShowCollection();
                else if (mode == "statistics") menu.ShowStatistics();
                else if (mode == "credits") menu.ShowCredits();
                else menu.ShowMenu();
            }

            if (mode == "statistics")
            {
                GameObject statsScroll = Find(scene, "StatsScrollView");
                ScrollRect scroll = statsScroll != null ? statsScroll.GetComponent<ScrollRect>() : null;
                if (scroll != null && scroll.verticalScrollbar != null)
                    scroll.verticalScrollbar.gameObject.SetActive(false);
            }
        }
        else if (mode == "pause" || mode == "settings")
        {
            GameObject pausePanel = Find(scene, "PausePanel");
            GameObject pauseWindow = Find(scene, "PauseWindow");
            GameObject settingPanel = Find(scene, "SettingPanel");
            GameObject task = Find(scene, "TaskSystem");
            if (pausePanel != null) pausePanel.SetActive(true);
            if (pauseWindow != null) pauseWindow.SetActive(mode == "pause");
            if (settingPanel != null) settingPanel.SetActive(mode == "settings");
            if (task != null) task.SetActive(false);
            GameObject banner = Find(scene, "AlphaBanner");
            CanvasGroup bannerGroup = banner != null ? banner.GetComponent<CanvasGroup>() : null;
            if (bannerGroup != null) bannerGroup.alpha = 0f;
            else if (banner != null) banner.SetActive(false);
        }
        else if (mode == "task")
        {
            GameObject pausePanel = Find(scene, "PausePanel");
            GameObject task = Find(scene, "TaskSystem");
            GameObject taskPanel = Find(scene, "TaskPanel");
            GameObject taskIcon = Find(scene, "Btn_TaskIcon");
            if (pausePanel != null) pausePanel.SetActive(false);
            if (task != null) task.SetActive(true);
            if (taskPanel != null) taskPanel.SetActive(true);
            if (taskIcon != null) taskIcon.SetActive(false);
        }
        else if (mode == "result")
        {
            GameObject canvas = Find(scene, "GameCanvas");
            GameObject task = Find(scene, "TaskSystem");
            GameObject pausePanel = Find(scene, "PausePanel");
            GameObject banner = Find(scene, "AlphaBanner");
            if (task != null) task.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (banner != null) banner.SetActive(false);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Perfabs/UI/ResultPanel.prefab");
            if (canvas != null && prefab != null)
            {
                GameObject result = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                result.GetComponent<ResultPanelView>()?.ShowVictory(
                    "带着 108 只羊成功冲出了草原！\n\n" +
                    "生存时间：12:34　历史最高：143 只\n" +
                    "当前羊群：普通羊 92，礼帽羊 8，蝴蝶结羊 8\n" +
                    "累计招募：普通羊 121，特殊羊 21\n" +
                    "被狼抓走：普通羊 30，特殊羊 5\n" +
                    "峰值构成：普通羊 122，特殊羊 21\n\n" +
                    "按 R 可以再来一局",
                    108, 3260, 142, 35, 754f);
            }
        }
    }

    private static GameObject Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name.Trim() == name);
            if (match != null) return match.gameObject;
        }
        return null;
    }

    private readonly struct FontState
    {
        public FontState(TMP_Text text, TMP_FontAsset font) { Text = text; Font = font; }
        public TMP_Text Text { get; }
        public TMP_FontAsset Font { get; }
    }

    private readonly struct CanvasState
    {
        private readonly Canvas canvas;
        private readonly RenderMode mode;
        private readonly Camera camera;
        private readonly float distance;
        public CanvasState(Canvas value)
        {
            canvas = value;
            mode = value.renderMode;
            camera = value.worldCamera;
            distance = value.planeDistance;
        }
        public void Restore()
        {
            if (canvas == null) return;
            canvas.renderMode = mode;
            canvas.worldCamera = camera;
            canvas.planeDistance = distance;
        }
    }
}

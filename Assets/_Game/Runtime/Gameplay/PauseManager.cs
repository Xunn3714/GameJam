using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("Pause UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject pauseWindow;
    [SerializeField] private GameObject settingPanel;

    [Header("Collection")]
    [SerializeField] private GameObject collectionPanel;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button collectionButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button collectionBackButton;

    [Header("Related UI")]
    [SerializeField] private TaskPanelToggle taskPanelToggle;
    [SerializeField] private AlphaBannerView bannerView;

    private bool isPaused;
    private bool resultLocked;
    private MvpCodexView codexView;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        BindButtons();

        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (pauseWindow != null)
            pauseWindow.SetActive(true);
        if (settingPanel != null)
            settingPanel.SetActive(false);
        if (collectionPanel != null)
            collectionPanel.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;
    }


    private void Update()
    {
        if (resultLocked)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (codexView != null && codexView.IsOpen)
            {
                BackFromCodex();
                return;
            }

            if (settingPanel != null && settingPanel.activeSelf)
            {
                BackToPause();
                return;
            }

            if (collectionPanel != null && collectionPanel.activeSelf)
            {
                BackFromCollection();
                return;
            }

            if (taskPanelToggle != null && taskPanelToggle.IsOpen)
            {
                taskPanelToggle.CloseTaskPanel();
                return;
            }

            TogglePause();
        }
    }


    // ESC 切换暂停状态
    public void TogglePause()
    {
        if (resultLocked)
            return;

        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            OpenPause();
        }
    }


    // 打开暂停菜单
    public void OpenPause()
    {
        if (resultLocked)
            return;

        if (taskPanelToggle != null && taskPanelToggle.IsOpen)
            taskPanelToggle.CloseTaskPanel();
        if (taskPanelToggle != null)
            taskPanelToggle.gameObject.SetActive(false);
        if (bannerView != null)
            bannerView.SetSuppressed(true);

        isPaused = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);
        if (pauseWindow != null)
            pauseWindow.SetActive(true);
        if (settingPanel != null)
            settingPanel.SetActive(false);
        if (collectionPanel != null)
            collectionPanel.SetActive(false);

        Time.timeScale = 0f;
    }

    public void PauseGame()
    {
        OpenPause();
    }


    // 打开 Settings
    public void ShowSettings()
    {
        if (!isPaused)
            return;

        if (pauseWindow != null)
            pauseWindow.SetActive(false);
        if (settingPanel != null)
            settingPanel.SetActive(true);
    }


    // Settings 返回暂停主页
    public void BackToPause()
    {
        if (!isPaused)
            return;

        if (settingPanel != null)
            settingPanel.SetActive(false);
        if (collectionPanel != null)
            collectionPanel.SetActive(false);
        if (taskPanelToggle != null)
            taskPanelToggle.gameObject.SetActive(false);
        if (pauseWindow != null)
            pauseWindow.SetActive(true);
    }


    // 继续游戏
    public void ResumeGame()
    {
        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (settingPanel != null)
            settingPanel.SetActive(false);
        if (collectionPanel != null)
            collectionPanel.SetActive(false);
        if (codexView != null)
            codexView.Hide();
        if (taskPanelToggle != null)
            taskPanelToggle.gameObject.SetActive(true);
        if (bannerView != null)
            bannerView.SetSuppressed(false);

        Time.timeScale = 1f;
    }

    public void ContinueGame()
    {
        ResumeGame();
    }

    public void ShowCollection()
    {
        if (!isPaused || collectionPanel == null)
            return;

        if (pauseWindow != null)
            pauseWindow.SetActive(false);
        if (settingPanel != null)
            settingPanel.SetActive(false);
        if (taskPanelToggle != null)
            taskPanelToggle.gameObject.SetActive(false);

        collectionPanel.SetActive(true);
    }

    public void BackFromCollection()
    {
        if (!isPaused)
            return;

        if (collectionPanel != null)
            collectionPanel.SetActive(false);
        if (pauseWindow != null)
            pauseWindow.SetActive(true);
    }


    // 返回 MainMenu
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadMainMenu();
        }
        else
        {
            Debug.LogError("SceneLoader Instance not found.");
        }
    }

    public void ConfigureCodex(MvpCodexView view)
    {
        codexView = view;

        // 新暂停菜单已经有图鉴按钮时，不再动态生成旧按钮。
        if (collectionButton != null)
            return;

        if (pauseWindow == null || pauseWindow.transform.Find("MvpCodexButton") != null)
            return;

        UnityEngine.UI.Button codexButton = MvpUiFactory.CreateButton(
            "MvpCodexButton",
            pauseWindow.transform,
            "同伴名册 / 图鉴",
            ShowCodex,
            new Vector2(260f, 64f));
        MvpUiFactory.Anchor(
            codexButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-145f, 72f),
            new Vector2(260f, 64f));

        if (pauseWindow.transform.Find("MvpRestartButton") == null)
        {
            UnityEngine.UI.Button restartButton = MvpUiFactory.CreateButton(
                "MvpRestartButton",
                pauseWindow.transform,
                "重新开始",
                RestartGame,
                new Vector2(260f, 64f));
            MvpUiFactory.Anchor(
                restartButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(145f, 72f),
                new Vector2(260f, 64f));
        }
    }

    public void ShowCodex()
    {
        if (!isPaused || codexView == null)
            return;

        pauseWindow.SetActive(false);
        if (settingPanel != null)
            settingPanel.SetActive(false);
        codexView.Show(BackFromCodex);
    }

    public void BackFromCodex()
    {
        if (codexView != null)
            codexView.Hide();

        if (isPaused && pauseWindow != null)
            pauseWindow.SetActive(true);
    }

    public void SetResultLocked(bool value)
    {
        resultLocked = value;
        if (!value)
        {
            if (taskPanelToggle != null)
                taskPanelToggle.gameObject.SetActive(true);
            return;
        }

        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (settingPanel != null)
            settingPanel.SetActive(false);
        if (collectionPanel != null)
            collectionPanel.SetActive(false);
        if (codexView != null)
            codexView.Hide();
        if (taskPanelToggle != null)
        {
            taskPanelToggle.CloseTaskPanel();
            taskPanelToggle.gameObject.SetActive(false);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.ReloadCurrentScene();
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
        isPaused = false;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BindButtons()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueGame);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(ShowSettings);
        if (collectionButton != null)
            collectionButton.onClick.AddListener(ShowCollection);
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (exitButton != null)
            exitButton.onClick.AddListener(ExitGame);
        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(BackToPause);
        if (collectionBackButton != null)
            collectionBackButton.onClick.AddListener(BackFromCollection);
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(ContinueGame);
        if (mainMenuButton != null)
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(ShowSettings);
        if (collectionButton != null)
            collectionButton.onClick.RemoveListener(ShowCollection);
        if (restartButton != null)
            restartButton.onClick.RemoveListener(RestartGame);
        if (exitButton != null)
            exitButton.onClick.RemoveListener(ExitGame);
        if (settingsBackButton != null)
            settingsBackButton.onClick.RemoveListener(BackToPause);
        if (collectionBackButton != null)
            collectionBackButton.onClick.RemoveListener(BackFromCollection);
    }
}

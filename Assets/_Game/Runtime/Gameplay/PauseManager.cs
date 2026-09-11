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
        resultLocked = false;
    }


    private void Update()
    {
        if (resultLocked)
            return;

        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;


        // Codex 打开时 ESC 返回 Pause
        if (codexView != null &&
            codexView.IsOpen)
        {
            BackFromCodex();
            return;
        }


        // Settings 打开时 ESC 返回 Pause
        if (settingPanel != null &&
            settingPanel.activeSelf)
        {
            BackToPause();
            return;
        }


        // Collection 打开时 ESC 返回 Pause
        if (collectionPanel != null &&
            collectionPanel.activeSelf)
        {
            BackFromCollection();
            return;
        }


        TogglePause();
    }


    // ============================================================
    // PAUSE
    // ============================================================

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


    public void OpenPause()
    {
        if (resultLocked)
            return;


        // 暂停时只隐藏 TaskSystem，不改变任务栏的展开状态。
        // 恢复游戏后仍保持原状态，任务栏只能通过自己的关闭按钮收起。
        if (taskPanelToggle != null)
        {
            taskPanelToggle.gameObject.SetActive(false);
        }


        // 暂停期间不让 AlphaBanner 跳出来
        if (bannerView != null)
        {
            bannerView.SetSuppressed(true);
        }


        isPaused = true;


        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }


        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }


        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }


        Time.timeScale = 0f;
    }


    public void PauseGame()
    {
        OpenPause();
    }


    // ============================================================
    // CONTINUE
    // ============================================================

    public void ResumeGame()
    {
        isPaused = false;


        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }


        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }


        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }


        if (codexView != null)
        {
            codexView.Hide();
        }


        // 恢复 TaskSystem
        if (taskPanelToggle != null)
        {
            taskPanelToggle.gameObject.SetActive(true);
        }


        // 恢复 AlphaBanner
        if (bannerView != null)
        {
            bannerView.SetSuppressed(false);
        }


        Time.timeScale = 1f;
    }


    public void ContinueGame()
    {
        ResumeGame();
    }


    // ============================================================
    // SETTINGS
    // ============================================================

    public void ShowSettings()
    {
        if (!isPaused)
            return;


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(false);
        }


        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }


        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
        }
    }


    public void BackToPause()
    {
        if (!isPaused)
            return;


        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }


        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }


        // 暂停状态下继续隐藏 TaskSystem
        if (taskPanelToggle != null)
        {
            taskPanelToggle.gameObject.SetActive(false);
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }
    }


    // ============================================================
    // COLLECTION
    // ============================================================

    public void ShowCollection()
    {
        if (!isPaused ||
            collectionPanel == null)
        {
            return;
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(false);
        }


        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }


        if (taskPanelToggle != null)
        {
            taskPanelToggle.gameObject.SetActive(false);
        }


        collectionPanel.SetActive(true);
    }


    public void BackFromCollection()
    {
        if (!isPaused)
            return;


        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }


        // 仍处于 Pause，因此 TaskSystem 保持隐藏
        if (taskPanelToggle != null)
        {
            taskPanelToggle.gameObject.SetActive(false);
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }
    }


    // ============================================================
    // MAIN MENU
    // ============================================================

    public void ReturnToMainMenu()
    {
        // --------------------------------------------------------
        // 方案 1：
        // 场景里有项目自己的 SceneLoader，就优先使用。
        // --------------------------------------------------------

        if (SceneLoader.Instance != null)
        {
            Time.timeScale = 1f;
            isPaused = false;

            SceneLoader.Instance.LoadMainMenu();

            return;
        }


        // --------------------------------------------------------
        // 方案 2：
        // Alpha 场景里没有 SceneLoader 时，
        // 直接使用 Unity SceneManager。
        // --------------------------------------------------------

        const string mainMenuSceneName =
            "MainMenu";


        if (Application.CanStreamedLevelBeLoaded(
            mainMenuSceneName))
        {
            Time.timeScale = 1f;
            isPaused = false;

            SceneManager.LoadScene(
                mainMenuSceneName
            );

            return;
        }


        // --------------------------------------------------------
        // 两种方式都无法加载时：
        // 不解除暂停。
        //
        // 避免出现：
        // 点 Main Menu
        // → 游戏恢复
        // → Pause UI 还在
        // → Settings / Collection 全部失效
        // --------------------------------------------------------

        Debug.LogError(
            "PauseManager: 无法加载 MainMenu。请检查 MainMenu 是否已加入 Build Settings / Build Profile。",
            this
        );


        // 保持正确暂停状态
        isPaused = true;
        Time.timeScale = 0f;


        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }
    }


    // ============================================================
    // CODEX COMPATIBILITY
    // ============================================================

    public void ConfigureCodex(
        MvpCodexView view)
    {
        codexView = view;


        // 新 Pause 菜单已经有 Sheep 按钮，
        // 就不再动态创建旧 Codex 按钮。
        if (collectionButton != null)
            return;


        if (pauseWindow == null)
            return;


        if (pauseWindow.transform.Find(
            "MvpCodexButton") != null)
        {
            return;
        }


        Button codexButton =
            MvpUiFactory.CreateButton(
                "MvpCodexButton",
                pauseWindow.transform,
                "同伴名册 / 图鉴",
                ShowCodex,
                new Vector2(
                    260f,
                    64f
                )
            );


        MvpUiFactory.Anchor(
            codexButton
                .GetComponent<RectTransform>(),
            new Vector2(
                0.5f,
                0f
            ),
            new Vector2(
                0.5f,
                0f
            ),
            new Vector2(
                -145f,
                72f
            ),
            new Vector2(
                260f,
                64f
            )
        );


        // 老系统兼容：
        // 只有当前 PauseWindow 没 Restart 时
        // 才动态生成。
        if (pauseWindow.transform.Find(
            "MvpRestartButton") == null &&
            restartButton == null)
        {
            Button runtimeRestartButton =
                MvpUiFactory.CreateButton(
                    "MvpRestartButton",
                    pauseWindow.transform,
                    "重新开始",
                    RestartGame,
                    new Vector2(
                        260f,
                        64f
                    )
                );


            MvpUiFactory.Anchor(
                runtimeRestartButton
                    .GetComponent<RectTransform>(),
                new Vector2(
                    0.5f,
                    0f
                ),
                new Vector2(
                    0.5f,
                    0f
                ),
                new Vector2(
                    145f,
                    72f
                ),
                new Vector2(
                    260f,
                    64f
                )
            );
        }
    }


    public void ShowCodex()
    {
        if (!isPaused ||
            codexView == null)
        {
            return;
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(false);
        }


        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }


        codexView.Show(
            BackFromCodex
        );
    }


    public void BackFromCodex()
    {
        if (codexView != null)
        {
            codexView.Hide();
        }


        if (isPaused &&
            pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }
    }


    // ============================================================
    // RESULT LOCK
    // ============================================================

    public void SetResultLocked(
        bool value)
    {
        resultLocked = value;


        if (!value)
        {
            if (taskPanelToggle != null)
            {
                taskPanelToggle.gameObject.SetActive(true);
            }

            return;
        }


        isPaused = false;

        Time.timeScale = 1f;


        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }


        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }


        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }


        if (codexView != null)
        {
            codexView.Hide();
        }


        if (taskPanelToggle != null)
        {
            taskPanelToggle.CloseTaskPanel();
            taskPanelToggle.gameObject.SetActive(false);
        }


        if (bannerView != null)
        {
            bannerView.SetSuppressed(false);
        }
    }


    // ============================================================
    // RESTART
    // ============================================================

    public void RestartGame()
    {
        Time.timeScale = 1f;
        isPaused = false;


        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.ReloadCurrentScene();

            return;
        }


        SceneManager.LoadScene(
            SceneManager
                .GetActiveScene()
                .name
        );
    }


    // ============================================================
    // EXIT
    // ============================================================

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


    // ============================================================
    // BUTTON BINDING
    // ============================================================

    private void BindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(
                ContinueGame
            );
        }


        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(
                ReturnToMainMenu
            );
        }


        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(
                ShowSettings
            );
        }


        if (collectionButton != null)
        {
            collectionButton.onClick.AddListener(
                ShowCollection
            );
        }


        if (restartButton != null)
        {
            restartButton.onClick.AddListener(
                RestartGame
            );
        }


        if (exitButton != null)
        {
            exitButton.onClick.AddListener(
                ExitGame
            );
        }


        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.AddListener(
                BackToPause
            );
        }


        if (collectionBackButton != null)
        {
            collectionBackButton.onClick.AddListener(
                BackFromCollection
            );
        }
    }


    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDestroy()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(
                ContinueGame
            );
        }


        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(
                ReturnToMainMenu
            );
        }


        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(
                ShowSettings
            );
        }


        if (collectionButton != null)
        {
            collectionButton.onClick.RemoveListener(
                ShowCollection
            );
        }


        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(
                RestartGame
            );
        }


        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(
                ExitGame
            );
        }


        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.RemoveListener(
                BackToPause
            );
        }


        if (collectionBackButton != null)
        {
            collectionBackButton.onClick.RemoveListener(
                BackFromCollection
            );
        }
    }
}

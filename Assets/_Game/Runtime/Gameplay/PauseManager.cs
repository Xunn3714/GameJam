using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject pauseWindow;
    [SerializeField] private GameObject settingPanel;

    [Header("Collection")]
    [SerializeField] private GameObject collectionPanel;

    [Header("Pause Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button collectionButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button exitButton;

    [Header("Settings")]
    [SerializeField] private Button settingsBackButton;

    [Header("Collection")]
    [SerializeField] private Button collectionBackButton;


    private bool isPaused;
    private bool resultLocked;

    private MvpCodexView codexView;

    // TaskSystem 是另一个 Prefab。
    // 运行时找到它，用于：
    // 1. TaskPanel 打开时阻止 Pause
    // 2. Collection 打开时隐藏整个 TaskSystem
    private TaskPanelToggle taskPanelToggle;


    private void Awake()
    {
        // ========================================================
        // BUTTON BINDINGS
        // ========================================================

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


        // ========================================================
        // FIND TASK SYSTEM
        // ========================================================

        taskPanelToggle =
            FindFirstObjectByType<TaskPanelToggle>();


        // ========================================================
        // INITIAL UI STATE
        // ========================================================

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


        // ========================================================
        // INITIAL GAME STATE
        // ========================================================

        Time.timeScale = 1f;

        isPaused = false;
        resultLocked = false;
    }


    private void Update()
    {
        // Result 已经出现后，
        // 不再允许 ESC 操作 Pause。
        if (resultLocked)
        {
            return;
        }


        if (Keyboard.current == null)
        {
            return;
        }


        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }


        // ========================================================
        // OTHER MODAL UI
        // ========================================================

        // Codex 已打开：
        // 不允许 ESC 再开 Pause。
        if (codexView != null &&
            codexView.IsOpen)
        {
            return;
        }


        // Settings 已打开：
        // 使用设置页面自己的返回按钮。
        if (settingPanel != null &&
            settingPanel.activeSelf)
        {
            return;
        }


        // Collection 已打开：
        // 使用图鉴自己的 Return。
        if (collectionPanel != null &&
            collectionPanel.activeSelf)
        {
            return;
        }


        // TaskPanel 已打开：
        // 不允许 Pause 盖在 TaskPanel 上面。
        FindTaskPanelToggle();

        if (taskPanelToggle != null &&
            taskPanelToggle.IsOpen)
        {
            return;
        }


        TogglePause();
    }


    // ============================================================
    // PAUSE TOGGLE
    // ============================================================

    public void TogglePause()
    {
        if (resultLocked)
        {
            return;
        }


        if (isPaused)
        {
            ContinueGame();
        }
        else
        {
            PauseGame();
        }
    }


    // ============================================================
    // PAUSE
    // ============================================================

    public void PauseGame()
    {
        if (resultLocked)
        {
            return;
        }


        FindTaskPanelToggle();


        // TaskPanel 正开着时，
        // 不允许打开 Pause。
        if (taskPanelToggle != null &&
            taskPanelToggle.IsOpen)
        {
            return;
        }


        // Codex 正开着时，
        // 不允许打开 Pause。
        if (codexView != null &&
            codexView.IsOpen)
        {
            return;
        }


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

        isPaused = true;
    }


    // ============================================================
    // CONTINUE
    // ============================================================

    public void ContinueGame()
    {
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


        // 如果之前从 Collection 隐藏了 TaskSystem，
        // 恢复它。
        SetTaskSystemVisible(true);


        Time.timeScale = 1f;

        isPaused = false;
    }


    // ============================================================
    // SETTINGS
    // ============================================================

    public void ShowSettings()
    {
        // Settings 只能在 Pause 状态下打开。
        if (!isPaused)
        {
            return;
        }


        if (codexView != null &&
            codexView.IsOpen)
        {
            return;
        }


        if (collectionPanel != null &&
            collectionPanel.activeSelf)
        {
            return;
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(false);
        }


        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
        }
    }


    public void BackToPause()
    {
        if (!isPaused)
        {
            return;
        }


        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }


        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }


        SetTaskSystemVisible(true);


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }
    }


    // ============================================================
    // SHEEP COLLECTION
    // ============================================================

    public void ShowCollection()
    {
        // Collection 只能从 Pause 打开。
        if (!isPaused)
        {
            return;
        }


        // Settings 正在打开时不能切到 Collection。
        if (settingPanel != null &&
            settingPanel.activeSelf)
        {
            return;
        }


        if (codexView != null &&
            codexView.IsOpen)
        {
            return;
        }


        // 隐藏暂停主菜单。
        if (pauseWindow != null)
        {
            pauseWindow.SetActive(false);
        }


        // 图鉴打开时隐藏左上角 TaskSystem。
        SetTaskSystemVisible(false);


        // CollectionPanelController 的 OnEnable()
        // 会自动 RefreshCollection()。
        if (collectionPanel != null)
        {
            collectionPanel.SetActive(true);
        }
    }


    public void BackFromCollection()
    {
        if (!isPaused)
        {
            return;
        }


        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }


        // 图鉴关闭后恢复 TaskSystem。
        SetTaskSystemVisible(true);


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
        Time.timeScale = 1f;

        isPaused = false;


        SceneManager.LoadScene(
            "MainMenu"
        );
    }


    // ============================================================
    // RESTART CURRENT LEVEL
    // ============================================================

    public void RestartGame()
    {
        Time.timeScale = 1f;

        isPaused = false;


        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.ReloadCurrentScene();
        }
        else
        {
            SceneManager.LoadScene(
                SceneManager
                    .GetActiveScene()
                    .name
            );
        }
    }


    // ============================================================
    // EXIT
    // 你已经接好的退出逻辑继续保留。
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
    // CODEX COMPATIBILITY
    // ============================================================

    public void ConfigureCodex(
        MvpCodexView view)
    {
        // 只保留旧系统引用。
        //
        // 不再运行时自动生成
        // MvpCodexButton / MvpRestartButton，
        // 因为现在 PauseWindow 已经有
        // Btn_Sheep 和 Btn_Restart。
        codexView = view;
    }


    public void ShowCodex()
    {
        if (!isPaused)
        {
            return;
        }


        if (codexView == null)
        {
            return;
        }


        if (settingPanel != null &&
            settingPanel.activeSelf)
        {
            return;
        }


        if (collectionPanel != null &&
            collectionPanel.activeSelf)
        {
            return;
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(false);
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


        // 防止结算后 TaskSystem 因为
        // Collection 曾经打开而一直被隐藏。
        SetTaskSystemVisible(true);


        FindTaskPanelToggle();


        if (taskPanelToggle != null &&
            taskPanelToggle.IsOpen)
        {
            taskPanelToggle.CloseTaskPanel();
        }
    }


    // ============================================================
    // TASK SYSTEM HELPERS
    // ============================================================

    private void FindTaskPanelToggle()
    {
        if (taskPanelToggle != null)
        {
            return;
        }


        taskPanelToggle =
            FindFirstObjectByType<TaskPanelToggle>();
    }


    private void SetTaskSystemVisible(
        bool visible)
    {
        FindTaskPanelToggle();


        if (taskPanelToggle == null)
        {
            return;
        }


        // TaskPanelToggle 挂在 TaskSystem 根对象上，
        // 所以直接控制这个根对象。
        taskPanelToggle.gameObject.SetActive(
            visible
        );
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

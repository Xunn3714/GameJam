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


    [Header("Pause Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button settingsButton;


    [Header("Settings")]
    [SerializeField] private Button settingsBackButton;


    private bool isPaused = false;
    private bool resultLocked = false;

    private MvpCodexView codexView;

    // TaskSystem 属于另一个 Prefab，
    // 所以这里运行时自动寻找。
    private TaskPanelToggle taskPanelToggle;


    private void Awake()
    {

        // Pause Prefab 按钮自动绑定

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(ContinueGame);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(ShowSettings);
        }

        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.AddListener(BackToPause);
        }



        // 找 TaskSystem

        taskPanelToggle =
            FindFirstObjectByType<TaskPanelToggle>();



        // 初始 UI 状态


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


        // 初始游戏状态

        Time.timeScale = 1f;

        isPaused = false;
        resultLocked = false;
    }


    private void Update()
    {

        // Result 已出现
        // 禁止再通过 ESC 操作暂停系统

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


        // Codex / 图鉴打开
        //
        // ESC 什么都不做。
        // 必须使用图鉴自己的返回按钮。

        if (codexView != null &&
            codexView.IsOpen)
        {
            return;
        }



        // Settings 打开
        //
        // ESC 什么都不做。
        // 必须使用 Settings 自己的返回按钮。

        if (settingPanel != null &&
            settingPanel.activeSelf)
        {
            return;
        }


        // TaskPanel 打开
        //
        // ESC 什么都不做。
        // 必须使用 TaskPanel 自己的 Close。
        if (taskPanelToggle == null)
        {
            taskPanelToggle =
                FindFirstObjectByType<TaskPanelToggle>();
        }

        if (taskPanelToggle != null &&
            taskPanelToggle.IsOpen)
        {
            return;
        }


        // 没有其他 Modal UI 占用输入
        // 才允许 Pause Toggle
        TogglePause();
    }


    // Pause Toggle
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

    // Pause
    public void PauseGame()
    {
        if (resultLocked)
        {
            return;
        }


        // TaskPanel 如果当前打开，
        // 不允许 Pause 覆盖在它上面。
        if (taskPanelToggle == null)
        {
            taskPanelToggle =
                FindFirstObjectByType<TaskPanelToggle>();
        }

        if (taskPanelToggle != null &&
            taskPanelToggle.IsOpen)
        {
            return;
        }


        // Codex 已打开时不允许打开 Pause
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


        Time.timeScale = 0f;

        isPaused = true;
    }

    // Continue
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


        if (codexView != null)
        {
            codexView.Hide();
        }


        Time.timeScale = 1f;

        isPaused = false;
    }


    // Settings
    public void ShowSettings()
    {
        // Settings 只能从 Pause 菜单打开
        if (!isPaused)
        {
            return;
        }


        // 如果 Codex 已打开，不允许切 Settings
        if (codexView != null &&
            codexView.IsOpen)
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


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }
    }


    // Main Menu
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        isPaused = false;

        SceneManager.LoadScene("MainMenu");
    }


    // Codex
    public void ConfigureCodex(MvpCodexView view)
    {
        codexView = view;


        if (pauseWindow == null)
        {
            return;
        }


        // Codex Button
        if (pauseWindow.transform.Find("MvpCodexButton") == null)
        {
            Button codexButton =
                MvpUiFactory.CreateButton(
                    "MvpCodexButton",
                    pauseWindow.transform,
                    "同伴名册 / 图鉴",
                    ShowCodex,
                    new Vector2(260f, 64f)
                );


            MvpUiFactory.Anchor(
                codexButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-145f, 72f),
                new Vector2(260f, 64f)
            );
        }

        // Restart Button
        if (pauseWindow.transform.Find("MvpRestartButton") == null)
        {
            Button restartButton =
                MvpUiFactory.CreateButton(
                    "MvpRestartButton",
                    pauseWindow.transform,
                    "重新开始",
                    RestartGame,
                    new Vector2(260f, 64f)
                );


            MvpUiFactory.Anchor(
                restartButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(145f, 72f),
                new Vector2(260f, 64f)
            );
        }
    }


    public void ShowCodex()
    {
        // Codex 只能从 Pause 打开
        if (!isPaused)
        {
            return;
        }


        if (codexView == null)
        {
            return;
        }


        // Settings 打开时不能切到 Codex
        if (settingPanel != null &&
            settingPanel.activeSelf)
        {
            return;
        }


        if (pauseWindow != null)
        {
            pauseWindow.SetActive(false);
        }


        codexView.Show(BackFromCodex);
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

    // Result Lock
    public void SetResultLocked(bool value)
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


        if (codexView != null)
        {
            codexView.Hide();
        }


        if (taskPanelToggle == null)
        {
            taskPanelToggle =
                FindFirstObjectByType<TaskPanelToggle>();
        }


        if (taskPanelToggle != null &&
            taskPanelToggle.IsOpen)
        {
            taskPanelToggle.CloseTaskPanel();
        }
    }

    // Restart
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
                SceneManager.GetActiveScene().name
            );
        }
    }

    // Cleanup
    private void OnDestroy()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueGame);
        }


        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        }


        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(ShowSettings);
        }


        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.RemoveListener(BackToPause);
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [Header("Pause UI")]
    public GameObject pausePanel;
    public GameObject pauseWindow;
    public GameObject settingPanel;

    private bool isPaused = false;
    private bool resultLocked;
    private MvpCodexView codexView;


    private void Start()
    {
        // 进入关卡时默认不暂停
        if (pausePanel != null)
            pausePanel.SetActive(false);

        // 暂停主页默认准备好
        if (pauseWindow != null)
            pauseWindow.SetActive(true);

        // 设置页面默认关闭
        if (settingPanel != null)
            settingPanel.SetActive(false);

        Time.timeScale = 1f;
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

            TogglePause();
        }
    }


    // ESC 切换暂停状态
    public void TogglePause()
    {
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
        isPaused = true;

        pausePanel.SetActive(true);

        pauseWindow.SetActive(true);
        settingPanel.SetActive(false);

        Time.timeScale = 0f;
    }


    // 打开 Settings
    public void ShowSettings()
    {
        pauseWindow.SetActive(false);
        settingPanel.SetActive(true);
    }


    // Settings 返回暂停主页
    public void BackToPause()
    {
        settingPanel.SetActive(false);
        pauseWindow.SetActive(true);
    }


    // 继续游戏
    public void ResumeGame()
    {
        isPaused = false;

        pausePanel.SetActive(false);

        Time.timeScale = 1f;
    }


    // 返回 MainMenu
    public void ReturnToMainMenu()
    {
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
            return;

        isPaused = false;
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (codexView != null)
            codexView.Hide();
    }

    public void RestartGame()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.ReloadCurrentScene();
        else
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}

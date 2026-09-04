using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [Header("Pause UI")]
    public GameObject pausePanel;
    public GameObject pauseWindow;
    public GameObject settingPanel;

    private bool isPaused = false;


    private void Start()
    {
        // 进入关卡时默认不暂停
        pausePanel.SetActive(false);

        // 暂停主页默认准备好
        pauseWindow.SetActive(true);

        // 设置页面默认关闭
        settingPanel.SetActive(false);

        Time.timeScale = 1f;
    }


    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
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
}

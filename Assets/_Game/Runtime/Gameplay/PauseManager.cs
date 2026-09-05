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

    private bool isPaused;


    private void Awake()
    {
        // 按钮事件全部由 Prefab 内部自动绑定
        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueGame);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(ShowSettings);

        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(BackToPause);

        // 游戏开始时确保暂停界面关闭
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (pauseWindow != null)
            pauseWindow.SetActive(true);

        if (settingPanel != null)
            settingPanel.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;
    }


    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (settingPanel != null && settingPanel.activeSelf)
            {
                BackToPause();
                return;
            }

            if (isPaused)
                ContinueGame();
            else
                PauseGame();
        }
    }


    public void PauseGame()
    {
        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (pauseWindow != null)
            pauseWindow.SetActive(true);

        if (settingPanel != null)
            settingPanel.SetActive(false);

        Time.timeScale = 0f;
        isPaused = true;
    }


    public void ContinueGame()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;
    }


    public void ShowSettings()
    {
        if (pauseWindow != null)
            pauseWindow.SetActive(false);

        if (settingPanel != null)
            settingPanel.SetActive(true);
    }


    public void BackToPause()
    {
        if (settingPanel != null)
            settingPanel.SetActive(false);

        if (pauseWindow != null)
            pauseWindow.SetActive(true);
    }


    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;

        SceneManager.LoadScene("MainMenu");
    }


    private void OnDestroy()
    {
        // 防止重复监听
        if (continueButton != null)
            continueButton.onClick.RemoveListener(ContinueGame);

        if (mainMenuButton != null)
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(ShowSettings);

        if (settingsBackButton != null)
            settingsBackButton.onClick.RemoveListener(BackToPause);
    }
}

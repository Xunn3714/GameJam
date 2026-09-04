using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Panel")]
    public GameObject menuPanel;
    public GameObject settingPanel;

    [Header("Main Menu UI")]
    public GameObject gameTitle;
    public GameObject settingButton;


    private void Start()
    {
        ShowMenu();
    }


    // 显示主菜单
    public void ShowMenu()
    {
        menuPanel.SetActive(true);
        settingPanel.SetActive(false);

        if (gameTitle != null)
            gameTitle.SetActive(true);

        if (settingButton != null)
            settingButton.SetActive(true);
    }


    // 打开设置
    public void ShowSettings()
    {
        menuPanel.SetActive(false);
        settingPanel.SetActive(true);

        if (gameTitle != null)
            gameTitle.SetActive(false);

        if (settingButton != null)
            settingButton.SetActive(false);
    }


    // 返回主菜单
    public void BackToMenu()
    {
        settingPanel.SetActive(false);
        menuPanel.SetActive(true);

        if (gameTitle != null)
            gameTitle.SetActive(true);

        if (settingButton != null)
            settingButton.SetActive(true);
    }


    // 开始游戏
    public void StartGame()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadLevel01();
        }
        else
        {
            Debug.LogError("SceneLoader Instance not found.");
        }
    }


    // 退出游戏
    public void QuitGame()
    {
        Debug.Log("Quit Game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

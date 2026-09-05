using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [Header("Main Menu")]
    public GameObject menuPanel;

    [Header("Sub Panels")]
    public GameObject settingPanel;
    public GameObject collectionPanel;
    public GameObject statisticsPanel;

    [Header("Independent Main Menu UI")]
    public GameObject gameTitle;
    public GameObject settingButton;
    public GameObject developersButton;


    private void Start()
    {
        ShowMenu();
    }


    public void ShowMenu()
    {
        if (menuPanel != null)
            menuPanel.SetActive(true);

        if (settingPanel != null)
            settingPanel.SetActive(false);

        if (collectionPanel != null)
            collectionPanel.SetActive(false);

        if (statisticsPanel != null)
            statisticsPanel.SetActive(false);

        if (gameTitle != null)
            gameTitle.SetActive(true);

        if (settingButton != null)
            settingButton.SetActive(true);

        if (developersButton != null)
            developersButton.SetActive(true);
    }


    public void ShowSettings()
    {
        HideMainMenu();

        if (settingPanel != null)
            settingPanel.SetActive(true);
    }


    public void ShowCollection()
    {
        HideMainMenu();

        if (collectionPanel != null)
            collectionPanel.SetActive(true);
    }


    public void ShowStatistics()
    {
        HideMainMenu();

        if (statisticsPanel != null)
            statisticsPanel.SetActive(true);
    }


    public void BackToMenu()
    {
        ShowMenu();
    }


    private void HideMainMenu()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (settingPanel != null)
            settingPanel.SetActive(false);

        if (collectionPanel != null)
            collectionPanel.SetActive(false);

        if (statisticsPanel != null)
            statisticsPanel.SetActive(false);

        if (gameTitle != null)
            gameTitle.SetActive(false);

        if (settingButton != null)
            settingButton.SetActive(false);

        if (developersButton != null)
            developersButton.SetActive(false);
    }


    public void StartGame()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadGameplayScene();
        }
        else
        {
            Debug.LogError("SceneLoader Instance not found.");
        }
    }


    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

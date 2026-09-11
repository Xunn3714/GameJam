using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Main Menu")]
    public GameObject menuPanel;

    [Header("Sub Panels")]
    public GameObject settingPanel;
    public GameObject collectionPanel;
    [SerializeField] private GameObject collectionPanelPrefab;
    public GameObject statisticsPanel;
    public GameObject creditsPanel;

    [Header("Developers")]
    public GameObject developersPanel;

    [Header("Independent Main Menu UI")]
    public GameObject gameTitle;
    public GameObject settingButton;
    public GameObject developersButton;


    private void Start()
    {
        BindSharedCollectionPanel();
        ShowMenu();
    }


    private void BindSharedCollectionPanel()
    {
        if (collectionPanelPrefab == null)
            return;

        GameObject legacyPanel = collectionPanel;
        Transform targetParent = legacyPanel != null
            ? legacyPanel.transform.parent
            : transform.parent;
        int siblingIndex = legacyPanel != null
            ? legacyPanel.transform.GetSiblingIndex()
            : -1;

        // The scene copy is inactive, so using it as the temporary parent prevents
        // the shared panel from refreshing before all main-menu systems have started.
        Transform instantiateParent = legacyPanel != null && !legacyPanel.activeInHierarchy
            ? legacyPanel.transform
            : targetParent;
        GameObject sharedPanel = Instantiate(collectionPanelPrefab, instantiateParent, false);
        sharedPanel.name = collectionPanelPrefab.name;
        sharedPanel.SetActive(false);
        if (sharedPanel.transform.parent != targetParent)
            sharedPanel.transform.SetParent(targetParent, false);
        if (siblingIndex >= 0)
            sharedPanel.transform.SetSiblingIndex(siblingIndex);

        Button[] buttons = sharedPanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.gameObject.name != "Btn_Back")
                continue;

            button.onClick.AddListener(BackToMenu);
            break;
        }

        collectionPanel = sharedPanel;
        if (legacyPanel != null)
        {
            if (Application.isPlaying)
                Destroy(legacyPanel);
            else
                DestroyImmediate(legacyPanel);
        }
    }


    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current
            .escapeKey
            .wasPressedThisFrame)
        {
            return;
        }


        // 制作人员页面打开时
        // ESC 返回主菜单
        if (developersPanel != null &&
            developersPanel.activeSelf)
        {
            ShowMenu();
        }
    }


    // ============================================================
    // MAIN MENU
    // ============================================================

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

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (developersPanel != null)
            developersPanel.SetActive(false);

        if (gameTitle != null)
            gameTitle.SetActive(true);

        if (settingButton != null)
            settingButton.SetActive(true);

        if (developersButton != null)
            developersButton.SetActive(true);
    }


    // ============================================================
    // SETTINGS
    // ============================================================

    public void ShowSettings()
    {
        HideMainMenu();

        if (settingPanel != null)
            settingPanel.SetActive(true);
    }


    // ============================================================
    // COLLECTION
    // ============================================================

    public void ShowCollection()
    {
        HideMainMenu();

        if (collectionPanel != null)
            collectionPanel.SetActive(true);
    }


    // ============================================================
    // STATISTICS
    // ============================================================

    public void ShowStatistics()
    {
        HideMainMenu();

        if (statisticsPanel != null)
            statisticsPanel.SetActive(true);
    }


    // ============================================================
    // CREDITS
    // ============================================================

    public void ShowCredits()
    {
        HideMainMenu();

        if (creditsPanel != null)
            creditsPanel.SetActive(true);
    }


    // ============================================================
    // DEVELOPERS
    // ============================================================

    public void ShowDevelopers()
    {
        HideMainMenu();

        if (developersPanel != null)
            developersPanel.SetActive(true);
    }


    // ============================================================
    // BACK
    // ============================================================

    public void BackToMenu()
    {
        ShowMenu();
    }


    // ============================================================
    // HIDE MAIN MENU
    // ============================================================

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

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (developersPanel != null)
            developersPanel.SetActive(false);

        if (gameTitle != null)
            gameTitle.SetActive(false);

        if (settingButton != null)
            settingButton.SetActive(false);

        if (developersButton != null)
            developersButton.SetActive(false);
    }


    // ============================================================
    // START GAME
    // ============================================================

    public void StartGame()
    {
        if (SceneLoader.Instance != null)
        {
            // 不再直接进入 Gameplay
            // 先进入插画 Intro
            SceneLoader.Instance
                .LoadIllustrationIntro();
        }
        else
        {
            Debug.LogError(
                "MainMenuController: SceneLoader Instance not found.",
                this
            );
        }
    }


    // ============================================================
    // QUIT GAME
    // ============================================================

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication
            .isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

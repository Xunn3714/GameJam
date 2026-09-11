using System.Collections;
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


    private bool developersCanCloseByClick = false;
    private Coroutine developersClickGuardCoroutine;


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
        // 只有制作人员页面打开时，
        // 才处理 ESC / 左键返回。
        if (developersPanel == null ||
            !developersPanel.activeSelf)
        {
            return;
        }


        // ========================================================
        // ESC 返回
        // ========================================================

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ShowMenu();
            return;
        }


        // ========================================================
        // 鼠标左键返回
        // ========================================================

        if (!developersCanCloseByClick)
            return;


        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            ShowMenu();
        }
    }


    // ============================================================
    // MAIN MENU
    // ============================================================

    public void ShowMenu()
    {
        developersCanCloseByClick = false;

        if (developersClickGuardCoroutine != null)
        {
            StopCoroutine(developersClickGuardCoroutine);
            developersClickGuardCoroutine = null;
        }


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


        // 防止点击“制作人员”按钮的这一击
        // 同时又被识别成“左键返回”。
        developersCanCloseByClick = false;


        if (developersClickGuardCoroutine != null)
        {
            StopCoroutine(developersClickGuardCoroutine);
        }


        developersClickGuardCoroutine =
            StartCoroutine(
                EnableDevelopersClickAfterMouseRelease()
            );
    }


    private IEnumerator EnableDevelopersClickAfterMouseRelease()
    {
        // 等待进入页面时按下的左键完全松开。
        if (Mouse.current != null)
        {
            while (Mouse.current.leftButton.isPressed)
            {
                yield return null;
            }
        }


        // 再等一帧，防止同一帧输入残留。
        yield return null;


        if (developersPanel != null &&
            developersPanel.activeSelf)
        {
            developersCanCloseByClick = true;
        }


        developersClickGuardCoroutine = null;
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
        developersCanCloseByClick = false;


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
            SceneLoader.Instance.LoadIllustrationIntro();
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
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

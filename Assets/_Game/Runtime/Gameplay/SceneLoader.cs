using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }


    [Header("Intro")]
    [Tooltip("主菜单点击开始游戏后进入的插画过场场景。")]
    [SerializeField]
    private string illustrationIntroSceneName = "IllustrationIntro";


    [Header("Gameplay")]
    [Tooltip("插画过场结束后进入的正式游戏场景。")]
    [SerializeField]
    private string gameplaySceneName = "AlphaFlockExpansion";


    public string GameplaySceneName => gameplaySceneName;


    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }


    // ============================================================
    // MAIN MENU
    // ============================================================

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            "MainMenu"
        );
    }


    // ============================================================
    // ILLUSTRATION INTRO
    // ============================================================

    public void LoadIllustrationIntro()
    {
        Time.timeScale = 1f;

        string sceneName =
            string.IsNullOrWhiteSpace(
                illustrationIntroSceneName
            )
            ? "IllustrationIntro"
            : illustrationIntroSceneName;


        if (!Application.CanStreamedLevelBeLoaded(
            sceneName))
        {
            Debug.LogError(
                $"SceneLoader: 场景 {sceneName} 不在 Build Profile / Scene List 中。",
                this
            );

            return;
        }


        SceneManager.LoadScene(
            sceneName
        );
    }


    // ============================================================
    // GAMEPLAY
    // ============================================================

    public void LoadGameplayScene()
    {
        Time.timeScale = 1f;

        string sceneName =
            string.IsNullOrWhiteSpace(
                gameplaySceneName
            )
            ? "AlphaFlockExpansion"
            : gameplaySceneName;


        if (!Application.CanStreamedLevelBeLoaded(
            sceneName))
        {
            Debug.LogWarning(
                $"场景 {sceneName} 不在 Build Profile / Scene List 中，回退到 Level_01。",
                this
            );

            sceneName = "Level_01";
        }


        SceneManager.LoadScene(
            sceneName
        );
    }


    // ============================================================
    // LEVEL 01
    // ============================================================

    public void LoadLevel01()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            "Level_01"
        );
    }


    // ============================================================
    // GENERIC LOAD
    // ============================================================

    public void LoadScene(
        string sceneName)
    {
        if (string.IsNullOrWhiteSpace(
            sceneName))
        {
            Debug.LogError(
                "SceneLoader: Scene name is empty.",
                this
            );

            return;
        }


        Time.timeScale = 1f;

        SceneManager.LoadScene(
            sceneName
        );
    }


    // ============================================================
    // RELOAD
    // ============================================================

    public void ReloadCurrentScene()
    {
        Time.timeScale = 1f;

        string currentScene =
            SceneManager
                .GetActiveScene()
                .name;

        SceneManager.LoadScene(
            currentScene
        );
    }
}

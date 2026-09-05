using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    // 进入主菜单
    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }


    [Header("Gameplay")]
    [Tooltip("主菜单“开始游戏”进入的场景名（需要在 Build Settings 里）。")]
    [SerializeField] private string gameplaySceneName = "AlphaFlockExpansion";

    public string GameplaySceneName => gameplaySceneName;

    // 进入主玩法场景（当前是羊群暴力扩张）
    public void LoadGameplayScene()
    {
        Time.timeScale = 1f;
        string sceneName = string.IsNullOrWhiteSpace(gameplaySceneName) ? "Level_01" : gameplaySceneName;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"场景 {sceneName} 不在 Build Settings 里，回退到 Level_01。");
            sceneName = "Level_01";
        }

        SceneManager.LoadScene(sceneName);
    }


    // 进入第一关
    public void LoadLevel01()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Level_01");
    }


    // 通用场景加载
    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }


    // 重新加载当前场景
    public void ReloadCurrentScene()
    {
        Time.timeScale = 1f;

        string currentScene =
            SceneManager.GetActiveScene().name;

        SceneManager.LoadScene(currentScene);
    }
}

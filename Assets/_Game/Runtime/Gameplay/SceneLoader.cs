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

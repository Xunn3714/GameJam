using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingController : MonoBehaviour
{
    // 返回主菜单
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        // 正常游戏流程中优先使用 SceneLoader
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadMainMenu();
        }
        else
        {
            // 防止直接从 Ending Scene 测试时没有 SceneLoader
            SceneManager.LoadScene("MainMenu");
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

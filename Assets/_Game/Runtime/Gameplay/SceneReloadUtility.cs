using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>重新加载当前场景；Dev Scene 不在 Build Settings 里时，编辑器下按路径加载。</summary>
public static class SceneReloadUtility
{
    public static void ReloadActiveScene()
    {
        Time.timeScale = 1f;
        Scene active = SceneManager.GetActiveScene();

        if (Application.CanStreamedLevelBeLoaded(active.name))
        {
            SceneManager.LoadScene(active.name);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            active.path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogWarning($"Scene {active.name} is not in Build Settings; cannot reload.");
#endif
    }
}

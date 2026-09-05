using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 开发辅助：项目根目录下存在 Temp/alpha_setup_request.txt 时，脚本重载后自动执行一次
/// “Game Jam / Alpha Flock Expansion / Setup Scene”，然后删除该文件。方便在无法点菜单时远程触发。
/// </summary>
public static class AlphaAutoSetupRunner
{
    private const string RequestFile = "Temp/alpha_setup_request.txt";

    [InitializeOnLoadMethod]
    private static void CheckRequest()
    {
        if (!File.Exists(RequestFile))
            return;

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestFile))
                return;

            File.Delete(RequestFile);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Alpha auto setup skipped: editor is in play mode.");
                return;
            }

            Debug.Log("Alpha auto setup request found; running Setup Scene.");
            AlphaFlockExpansionSceneSetup.AddSceneToBuildSettings();
            AlphaFlockExpansionSceneSetup.SetupScene();
        };
    }
}

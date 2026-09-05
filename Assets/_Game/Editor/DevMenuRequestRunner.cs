using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 开发辅助：项目根目录下存在 Temp/menu_request.txt 时，脚本重载后按行执行其中的菜单项
/// （例如 "Game Jam/Wolf Test/Setup TestSmartWolf Scene"），然后删除该文件。方便无法点菜单时远程触发。
/// </summary>
public static class DevMenuRequestRunner
{
    private const string RequestFile = "Temp/menu_request.txt";

    [InitializeOnLoadMethod]
    private static void CheckRequest()
    {
        if (!File.Exists(RequestFile))
            return;

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestFile))
                return;

            string[] lines = File.ReadAllLines(RequestFile);
            File.Delete(RequestFile);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Dev menu request skipped: editor is in play mode.");
                return;
            }

            foreach (string rawLine in lines)
            {
                string menuPath = rawLine.Trim();
                if (menuPath.Length == 0 || menuPath.StartsWith("#"))
                    continue;

                Debug.Log($"Dev menu request: executing '{menuPath}'.");
                if (!EditorApplication.ExecuteMenuItem(menuPath))
                {
                    Debug.LogWarning($"Dev menu request: menu item '{menuPath}' was not found.");
                }
            }
        };
    }
}

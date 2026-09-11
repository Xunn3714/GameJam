using UnityEditor;
using UnityEngine;

internal static class SheepCollectionResetEditor
{
    private const string SaveKey = "SheepCollectionProgress";

    [MenuItem("Game Jam/Debug/Reset Sheep Collection Progress")]
    private static void Reset()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();

        foreach (SheepCollectionManager manager in
                 Resources.FindObjectsOfTypeAll<SheepCollectionManager>())
        {
            if (manager != null && !EditorUtility.IsPersistent(manager))
                manager.ResetProgress();
        }

        Debug.Log("羊群图鉴的 PlayerPrefs 与当前运行时进度已清空。");
    }
}

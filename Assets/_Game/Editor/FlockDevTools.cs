using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 调试用：往当前场景的羊群里一次性补几只羊，方便观察间距和跟随效果。
/// 只改场景里的对象，不保存；不想保留就关掉场景时选 Don't Save。
/// </summary>
public static class FlockDevTools
{
    private const string SheepMemberPrefabPath = "Assets/_Game/Content/Perfabs/Sheep/SheepMember.prefab";

    [MenuItem("Game Jam/Sheep MVP/Dev: Fill Flock To 10 Sheep")]
    public static void FillFlockToTen()
    {
        FillFlock(10);
    }

    [MenuItem("Game Jam/Sheep MVP/Dev: Fill Flock To 20 Sheep")]
    public static void FillFlockToTwenty()
    {
        FillFlock(20);
    }

    private static void FillFlock(int targetCount)
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("请在非 Play 状态下使用。");
            return;
        }

        FlockController flock = Object.FindAnyObjectByType<FlockController>();
        if (flock == null)
        {
            Debug.LogWarning("当前场景里没有 FlockController。");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SheepMemberPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"找不到 {SheepMemberPrefabPath}。");
            return;
        }

        SerializedObject serialized = new SerializedObject(flock);
        SerializedProperty startingMembers = serialized.FindProperty("startingMembers");
        List<SheepMember> members = new List<SheepMember>();
        for (int index = 0; index < startingMembers.arraySize; index++)
        {
            SheepMember existing = startingMembers.GetArrayElementAtIndex(index).objectReferenceValue as SheepMember;
            if (existing != null)
            {
                members.Add(existing);
            }
        }

        Vector2 center = flock.transform.position;
        int toAdd = targetCount - members.Count;
        for (int index = 0; index < toAdd; index++)
        {
            GameObject sheep = (GameObject)PrefabUtility.InstantiatePrefab(prefab, flock.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(sheep, "Add test sheep");
            sheep.name = $"Sheep_Test_{members.Count + 1:00}";
            float angle = index * Mathf.PI * 2f / Mathf.Max(1, toAdd);
            float radius = 1.5f + 1.2f * (index % 2);
            sheep.transform.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            members.Add(sheep.GetComponent<SheepMember>());
        }

        startingMembers.arraySize = members.Count;
        for (int index = 0; index < members.Count; index++)
        {
            startingMembers.GetArrayElementAtIndex(index).objectReferenceValue = members[index];
        }
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(flock.gameObject.scene);
        Debug.Log($"羊群起始成员已补到 {members.Count} 只（新增 {Mathf.Max(0, toAdd)} 只）。");
    }
}

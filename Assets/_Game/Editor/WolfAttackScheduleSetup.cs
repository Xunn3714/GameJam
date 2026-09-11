using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 正式节奏表的编辑器工具：生成 WolfAttackSchedule 资产，并把它接到打开场景的 WolfEventDirector 上
/// （顺带挂 WolfFormationRunner、填好普通狼 / 长狼 prefab）。
/// </summary>
public static class WolfAttackScheduleSetup
{
    public const string ScheduleFolder = "Assets/_Game/Content/Data/Wolf";
    public const string SchedulePath = ScheduleFolder + "/WolfAttackSchedule.asset";
    private const string WolfPrefabPath = "Assets/_Game/Content/Perfabs/Wolf/Wolf.prefab";

    [MenuItem("Game Jam/Wolf Test/Schedule/Create Or Reset Wolf Attack Schedule Asset")]
    public static WolfAttackSchedule CreateOrResetSchedule()
    {
        WorldObstaclePrefabBuilder.EnsureFolder(ScheduleFolder);
        WolfAttackSchedule schedule = AssetDatabase.LoadAssetAtPath<WolfAttackSchedule>(SchedulePath);
        if (schedule == null)
        {
            schedule = ScriptableObject.CreateInstance<WolfAttackSchedule>();
            AssetDatabase.CreateAsset(schedule, SchedulePath);
        }

        SerializedObject serialized = new SerializedObject(schedule);
        WolfAttackSchedule.Stage[] defaults = WolfAttackSchedule.CreateDefaultStages();
        SerializedProperty stages = serialized.FindProperty("stages");
        stages.arraySize = defaults.Length;
        for (int index = 0; index < defaults.Length; index++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(index);
            WolfAttackSchedule.Stage source = defaults[index];
            stage.FindPropertyRelative("displayName").stringValue = source.displayName;
            stage.FindPropertyRelative("minMemberCount").intValue = source.minMemberCount;
            stage.FindPropertyRelative("calmDurationMin").floatValue = source.calmDurationMin;
            stage.FindPropertyRelative("calmDurationMax").floatValue = source.calmDurationMax;
            stage.FindPropertyRelative("longWolfWidthMultiplier").floatValue = source.longWolfWidthMultiplier;
            stage.FindPropertyRelative("basicIntensityWeight").floatValue = source.basicIntensityWeight;
            stage.FindPropertyRelative("formationIntensityWeight").floatValue = source.formationIntensityWeight;
            stage.FindPropertyRelative("majorIntensityWeight").floatValue = source.majorIntensityWeight;
        }

        WolfAttackSchedule.AttackDefinition[] defaultAttacks = WolfAttackSchedule.CreateDefaultAttacks();
        SerializedProperty attacks = serialized.FindProperty("attacks");
        attacks.arraySize = defaultAttacks.Length;
        for (int index = 0; index < defaultAttacks.Length; index++)
        {
            SerializedProperty attack = attacks.GetArrayElementAtIndex(index);
            WolfAttackSchedule.AttackDefinition source = defaultAttacks[index];
            attack.FindPropertyRelative("type").enumValueIndex = (int)source.type;
            attack.FindPropertyRelative("unlockStageIndex").intValue = source.unlockStageIndex;
            attack.FindPropertyRelative("intensity").enumValueIndex = (int)source.intensity;
            attack.FindPropertyRelative("baseWeight").floatValue = source.baseWeight;
            attack.FindPropertyRelative("enabled").boolValue = source.enabled;
        }
        serialized.FindProperty("repeatedAttackWeightMultiplier").floatValue = 0.3f;
        serialized.FindProperty("freshnessWeightPerMiss").floatValue = 0.3f;
        serialized.FindProperty("maxFreshnessRounds").intValue = 4;
        serialized.FindProperty("forceBasicAfterMajor").boolValue = true;
        serialized.FindProperty("scareThreshold").intValue = 50;
        serialized.FindProperty("wolfSpeedExponent").floatValue = 1.3f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(schedule);
        AssetDatabase.SaveAssets();
        Debug.Log($"Wolf attack schedule ready at {SchedulePath} ({defaults.Length} stages, {defaultAttacks.Length} attacks).");
        return schedule;
    }

    /// <summary>把节奏表接到打开场景的 WolfEventDirector 上（Alpha / Level_01 都可以）。</summary>
    [MenuItem("Game Jam/Wolf Test/Schedule/Apply Wolf Attack Schedule To Open Scene")]
    public static void ApplyToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        WolfEventDirector director = scene.GetRootGameObjects()
            .Select(root => root.GetComponentInChildren<WolfEventDirector>(true))
            .FirstOrDefault(found => found != null);
        if (director == null)
        {
            Debug.LogWarning("Open scene has no WolfEventDirector.");
            return;
        }

        ApplyToDirector(director);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Wolf attack schedule applied to {director.name} in {scene.path}.");
    }

    /// <summary>给 director 挂上节奏表：schedule 资产 + WolfFormationRunner + 编队模板里的两种 prefab。</summary>
    public static void ApplyToDirector(WolfEventDirector director)
    {
        WolfAttackSchedule schedule = AssetDatabase.LoadAssetAtPath<WolfAttackSchedule>(SchedulePath) ?? CreateOrResetSchedule();

        WolfSpawner spawner = director.GetComponent<WolfSpawner>();
        SerializedObject directorSerialized = new SerializedObject(director);
        if (spawner == null)
            spawner = directorSerialized.FindProperty("spawner").objectReferenceValue as WolfSpawner;
        if (spawner == null)
        {
            Debug.LogWarning("WolfEventDirector has no WolfSpawner; cannot apply the schedule.", director);
            return;
        }

        WolfFormationRunner runner = spawner.GetComponent<WolfFormationRunner>();
        if (runner == null)
            runner = spawner.gameObject.AddComponent<WolfFormationRunner>();
        SerializedObject runnerSerialized = new SerializedObject(runner);
        runnerSerialized.FindProperty("spawner").objectReferenceValue = spawner;
        runnerSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject wolfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WolfPrefabPath);
        GameObject longWolfPrefab = LongWolfTestSceneSetup.GetOrCreatePrefab();

        directorSerialized.FindProperty("schedule").objectReferenceValue = schedule;
        directorSerialized.FindProperty("formationRunner").objectReferenceValue = runner;
        SerializedProperty template = directorSerialized.FindProperty("formationTemplate");
        template.FindPropertyRelative("wolfPrefab").objectReferenceValue = wolfPrefab != null ? wolfPrefab.GetComponent<Wolf>() : null;
        template.FindPropertyRelative("longWolfPrefab").objectReferenceValue = longWolfPrefab != null ? longWolfPrefab.GetComponent<Wolf>() : null;
        template.FindPropertyRelative("count").intValue = 3;
        template.FindPropertyRelative("laneSpacing").floatValue = 2.6f;
        template.FindPropertyRelative("sequentialDelay").floatValue = 0.7f;
        template.FindPropertyRelative("escortDelay").floatValue = 0.8f;
        template.FindPropertyRelative("escortLongWolfWarningDuration").floatValue = 2.4f;
        template.FindPropertyRelative("escortSameSideChance").floatValue = 0.7f;
        template.FindPropertyRelative("escortFanSpread").floatValue = 35f;
        template.FindPropertyRelative("pentagramRadius").floatValue = 16f;
        template.FindPropertyRelative("pentagramUsesLongWolves").boolValue = true;
        template.FindPropertyRelative("pentagramWarningDuration").floatValue = 1.5f;
        template.FindPropertyRelative("pentagramStagger").floatValue = 0.3f;
        template.FindPropertyRelative("pentagramChargeSpeed").floatValue = 9f;
        template.FindPropertyRelative("chainHandoffDistance").floatValue = 14f;
        // 多狼同时在场，攻击阶段兜底放宽。
        directorSerialized.FindProperty("attackTimeout").floatValue = 30f;
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

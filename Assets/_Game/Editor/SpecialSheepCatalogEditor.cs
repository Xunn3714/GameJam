using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(SpecialSheepCatalog))]
public sealed class SpecialSheepCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SpecialSheepCatalog catalog = (SpecialSheepCatalog)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("普通羊概率", $"{catalog.CommonProbabilityPercent:0.###}%");
        if (catalog.BaseSheepPrefab == null)
            EditorGUILayout.HelpBox("缺少共用基础羊 Prefab，运行时不能生成羊。", MessageType.Error);
        if (!AssetDatabase.IsValidFolder(catalog.SourceRootFolder))
            EditorGUILayout.HelpBox($"特殊羊根目录不存在：{catalog.SourceRootFolder}", MessageType.Error);
        if (catalog.SpecialProbabilityPercent > 100.0001f)
            EditorGUILayout.HelpBox("特殊品质概率总和不能超过 100%。", MessageType.Error);

        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            if (tier == null)
                continue;

            int spawnableCount = 0;
            int missingCount = 0;
            foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
            {
                if (entry == null)
                    continue;
                if (entry.CanSpawn) spawnableCount++;
                else if (entry.Sprite == null) missingCount++;
            }

            if (tier.ProbabilityPercent > 0f && spawnableCount == 0)
            {
                EditorGUILayout.HelpBox(
                    $"{tier.FolderName} 概率为 {tier.ProbabilityPercent:0.###}%，但没有可刷新的羊；命中时会回退为普通羊。",
                    MessageType.Error);
            }
            if (missingCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{tier.FolderName} 有 {missingCount} 条记录缺少 PNG 引用，已保留 ID 并禁用刷新。",
                    MessageType.Warning);
            }
        }

        if (GUILayout.Button("从品质文件夹刷新羊种"))
        {
            SpecialSheepCatalogEditorUtility.Synchronize(catalog, saveAssets: true);
            Repaint();
        }
    }
}

/// <summary>创建并同步当前 Alpha 唯一的特殊羊目录资产。</summary>
public static class SpecialSheepCatalogEditorUtility
{
    public const string CatalogFolder = "Assets/_Game/Content/Data/Sheep";
    public const string CatalogPath = CatalogFolder + "/SpecialSheepCatalog.asset";
    public const string DefaultBaseSheepPrefabPath =
        "Assets/_Game/Content/Perfabs/Sheep/RecruitableSheep.prefab";

    private static bool syncScheduled;

    [InitializeOnLoadMethod]
    private static void ScheduleInitialSynchronization()
    {
        ScheduleSynchronization();
    }

    [MenuItem("Game Jam/Sheep/Sync Special Sheep Catalog")]
    public static void SynchronizeFromMenu()
    {
        SpecialSheepCatalog catalog = LoadOrCreate();
        Synchronize(catalog, saveAssets: true);
        Selection.activeObject = catalog;
    }

    public static SpecialSheepCatalog LoadOrCreateAndSynchronize()
    {
        SpecialSheepCatalog catalog = LoadOrCreate();
        Synchronize(catalog, saveAssets: true);
        return catalog;
    }

    public static SpecialSheepCatalog LoadOrCreate()
    {
        SpecialSheepCatalog catalog = AssetDatabase.LoadAssetAtPath<SpecialSheepCatalog>(CatalogPath);
        if (catalog != null)
            return catalog;

        EnsureFolder(CatalogFolder);
        catalog = ScriptableObject.CreateInstance<SpecialSheepCatalog>();
        catalog.EditorEnsureDefaults();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    public static bool Synchronize(SpecialSheepCatalog catalog, bool saveAssets)
    {
        if (catalog == null)
            return false;

        string before = EditorJsonUtility.ToJson(catalog);
        catalog.EditorEnsureDefaults();
        if (catalog.BaseSheepPrefab == null)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBaseSheepPrefabPath);
            RecruitableSheep recruitable = prefabAsset != null
                ? prefabAsset.GetComponent<RecruitableSheep>()
                : null;
            if (recruitable != null)
                catalog.EditorSetBaseSheepPrefab(recruitable);
        }
        Dictionary<string, SpecialSheepCatalog.Entry> existingByGuid = new(StringComparer.Ordinal);
        Dictionary<SpecialSheepCatalog.Entry, SpecialSheepCatalog.Tier> originalTierByEntry = new();
        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            if (tier == null)
                continue;
            foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
            {
                if (entry == null)
                    continue;
                originalTierByEntry[entry] = tier;
                if (!string.IsNullOrWhiteSpace(entry.AssetGuid) && !existingByGuid.ContainsKey(entry.AssetGuid))
                    existingByGuid.Add(entry.AssetGuid, entry);
            }
        }

        Dictionary<SpecialSheepCatalog.Tier, List<SpecialSheepCatalog.Entry>> synchronized = new();
        HashSet<SpecialSheepCatalog.Entry> seen = new();
        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            if (tier == null)
                continue;

            List<SpecialSheepCatalog.Entry> entries = new();
            synchronized[tier] = entries;
            string folderPath = CombineAssetPath(catalog.SourceRootFolder, tier.FolderName);
            if (!AssetDatabase.IsValidFolder(folderPath))
                continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsDirectPngChild(folderPath, assetPath))
                    continue;

                NormalizeTextureImporter(assetPath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                    continue;

                if (!existingByGuid.TryGetValue(guid, out SpecialSheepCatalog.Entry entry))
                    entry = new SpecialSheepCatalog.Entry();

                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                string typeId = string.IsNullOrWhiteSpace(entry.TypeId)
                    ? ResolveStableTypeId(fileName, guid)
                    : entry.TypeId;
                entry.EditorBindImportedSprite(guid, typeId, fileName, sprite);
                entries.Add(entry);
                seen.Add(entry);
            }

            entries.Sort((left, right) => string.CompareOrdinal(left.TypeId, right.TypeId));
        }

        foreach (KeyValuePair<SpecialSheepCatalog.Entry, SpecialSheepCatalog.Tier> pair in originalTierByEntry)
        {
            if (seen.Contains(pair.Key))
                continue;

            pair.Key.EditorMarkMissing();
            synchronized[pair.Value].Add(pair.Key);
            synchronized[pair.Value].Sort((left, right) => string.CompareOrdinal(left.TypeId, right.TypeId));
        }

        foreach (KeyValuePair<SpecialSheepCatalog.Tier, List<SpecialSheepCatalog.Entry>> pair in synchronized)
        {
            pair.Key.EditorEntries.Clear();
            pair.Key.EditorEntries.AddRange(pair.Value);
        }

        string after = EditorJsonUtility.ToJson(catalog);
        bool changed = !string.Equals(before, after, StringComparison.Ordinal);
        if (!changed)
            return false;

        EditorUtility.SetDirty(catalog);
        if (saveAssets)
            AssetDatabase.SaveAssets();
        Debug.Log($"特殊羊目录已同步：{CatalogPath}", catalog);
        return true;
    }

    private static void NormalizeTextureImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out _);
        float pixelsPerUnit = Mathf.Max(1f, sourceWidth);
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool changed = importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit)
            || importer.filterMode != FilterMode.Bilinear
            || importer.mipmapEnabled
            || !importer.alphaIsTransparency
            || importer.wrapMode != TextureWrapMode.Clamp
            || importer.textureCompression != TextureImporterCompression.Compressed
            || settings.spriteGenerateFallbackPhysicsShape;
        if (!changed)
            return;

        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    public static void ScheduleSynchronization()
    {
        if (syncScheduled)
            return;

        syncScheduled = true;
        EditorApplication.delayCall += () =>
        {
            syncScheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleSynchronization();
                return;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Synchronize(LoadOrCreate(), saveAssets: true);
        };
    }

    private static string ResolveStableTypeId(string displayName, string guid)
    {
        switch (displayName?.Trim())
        {
            case "礼帽羊":
            case "礼帽小羊":
                return "sheep.special.tophat";
            case "领结羊":
            case "蝴蝶结羊":
            case "蝴蝶结小羊":
                return "sheep.special.redbow";
            case "山羊":
                return "sheep.special.horned";
            case "黑羊":
            case "黑色小羊":
                return "sheep.special.black";
            default:
                return "sheep.special." + guid.ToLowerInvariant();
        }
    }

    private static bool IsDirectPngChild(string folderPath, string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)
            || !string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        return string.Equals(parent, folderPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineAssetPath(string left, string right)
    {
        return (left.TrimEnd('/', '\\') + "/" + right.Trim('/', '\\')).Replace('\\', '/');
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}

public sealed class SpecialSheepCatalogAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (ContainsSpecialSheepPath(importedAssets)
            || ContainsSpecialSheepPath(deletedAssets)
            || ContainsSpecialSheepPath(movedAssets)
            || ContainsSpecialSheepPath(movedFromAssetPaths))
        {
            SpecialSheepCatalogEditorUtility.ScheduleSynchronization();
        }
    }

    private static bool ContainsSpecialSheepPath(IEnumerable<string> paths)
    {
        if (paths == null)
            return false;

        string defaultRoot = SpecialSheepCatalog.DefaultSourceRootFolder.TrimEnd('/', '\\') + "/";
        SpecialSheepCatalog catalog = AssetDatabase.LoadAssetAtPath<SpecialSheepCatalog>(
            SpecialSheepCatalogEditorUtility.CatalogPath);
        string configuredRoot = catalog != null
            ? catalog.SourceRootFolder.TrimEnd('/', '\\') + "/"
            : defaultRoot;

        foreach (string path in paths)
        {
            if (path.StartsWith(defaultRoot, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(configuredRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>只更新 Alpha 场景里的特殊羊目录引用，不重建其他场景内容。</summary>
public static class SpecialSheepCatalogSceneBinder
{
    public const string AlphaScenePath = "Assets/_Game/Scenes/AlphaFlockExpansion.unity";

    [MenuItem("Game Jam/Sheep/Bind Catalog To Alpha Scene")]
    public static void BindAlphaScene()
    {
        Scene scene = EditorSceneManager.OpenScene(AlphaScenePath, OpenSceneMode.Single);
        ProgressiveSheepSpawner spawner = UnityEngine.Object.FindAnyObjectByType<ProgressiveSheepSpawner>(
            FindObjectsInactive.Include);
        if (spawner == null)
            throw new InvalidOperationException($"{AlphaScenePath} 中没有 ProgressiveSheepSpawner。");

        SpecialSheepCatalog catalog =
            SpecialSheepCatalogEditorUtility.LoadOrCreateAndSynchronize();
        SerializedObject serialized = new(spawner);
        serialized.FindProperty("specialSheepCatalog").objectReferenceValue = catalog;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"保存场景失败：{AlphaScenePath}");

        AssetDatabase.SaveAssets();
        Debug.Log($"Alpha 场景已绑定特殊羊目录：{SpecialSheepCatalogEditorUtility.CatalogPath}");
    }
}

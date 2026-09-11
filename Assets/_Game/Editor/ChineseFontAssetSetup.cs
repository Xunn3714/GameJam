using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Builds the repository-owned TMP fallback used by both Edit Mode previews and runtime UI.
/// </summary>
public static class ChineseFontAssetSetup
{
    public const string SourceFontPath =
        "Assets/_Game/Content/Fonts/Resources/NotoSansSC-Regular.otf";
    public const string FontAssetPath =
        "Assets/_Game/Content/Fonts/Resources/NotoSansSC-Regular SDF.asset";

    private const string TmpSettingsPath =
        "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    private const string PreviewCharacters =
        "羊群暴力扩张聚拢伙伴冲出草原开始游戏图鉴统计退出设置制作人员暂停继续重新返回主菜单" +
        "任务列表当前成功招募损失得分用时新的加入尚未解锁暂无记录普通稀有完成一局后显示这里" +
        "主音量音乐叫声音量碰到把它们都收进来按住够只撞开栅栏全军覆没胜利失败" +
        "棉花糖云朵小卷豆豆奶盖白团咩毛球盐巴小雪软糖月亮礼帽蝴蝶结山黑色" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
        "：，。！？·→/（）()[]+-“”\n ";

    [MenuItem("Game Jam/UI/Build Chinese Font Asset")]
    public static void BuildAndConfigure()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"无法加载项目中文字体：{SourceFontPath}");
            return;
        }

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError("无法从 Noto Sans SC 创建 TMP 中文字体资源。");
                return;
            }

            fontAsset.name = "NotoSansSC-Regular SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AddGeneratedSubAssets(fontAsset);
        }

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;
        if (!fontAsset.TryAddCharacters(PreviewCharacters, out string missingCharacters))
            Debug.LogWarning($"Noto Sans SC 缺少以下预览字符：{missingCharacters}");

        ConfigureGlobalFallback(fontAsset);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
        RefreshLoadedText();

        Debug.Log($"中文 TMP 字体已生成并配置为全局 fallback：{FontAssetPath}");
    }

    private static void AddGeneratedSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
        {
            fontAsset.material.name = $"{fontAsset.name} Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        IReadOnlyList<Texture2D> atlases = fontAsset.atlasTextures;
        if (atlases == null)
            return;

        for (int index = 0; index < atlases.Count; index++)
        {
            Texture2D atlas = atlases[index];
            if (atlas == null || AssetDatabase.Contains(atlas))
                continue;

            atlas.name = $"{fontAsset.name} Atlas {index}";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        }
    }

    private static void ConfigureGlobalFallback(TMP_FontAsset fontAsset)
    {
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings == null)
        {
            Debug.LogError($"无法加载 TMP Settings：{TmpSettingsPath}");
            return;
        }

        SerializedObject serialized = new SerializedObject(settings);
        SerializedProperty fallbacks = serialized.FindProperty("m_fallbackFontAssets");
        for (int index = 0; index < fallbacks.arraySize; index++)
        {
            if (fallbacks.GetArrayElementAtIndex(index).objectReferenceValue == fontAsset)
                return;
        }

        int newIndex = fallbacks.arraySize;
        fallbacks.InsertArrayElementAtIndex(newIndex);
        fallbacks.GetArrayElementAtIndex(newIndex).objectReferenceValue = fontAsset;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static void RefreshLoadedText()
    {
        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null)
                continue;

            text.SetAllDirty();
        }

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}

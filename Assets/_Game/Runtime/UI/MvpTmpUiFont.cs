using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Creates one dynamic TMP font asset from the bundled CJK font and reuses it.
/// </summary>
public static class MvpTmpUiFont
{
    private const string BundledFontResourcePath = "NotoSansSC-Regular";
    private const string RuntimeFontAssetName = "NotoSansSC MVP Runtime Fallback";
    private const string MvpCharacters =
        "找到羊族群加入了棉花糖云朵小卷豆豆奶盖白团咩毛球盐巴雪软月亮任务列表迎接第一位同伴把壮大到只另外带着留下记号支线新当前成功招募拉屎次数游戏用时本局未命名集合完毕返回标题图鉴登记类型绵稀有度普通携带分数动画组原型资源外观以后新增内容条目场景实例重复成员领队真实随机别名区分变化后打开读取最新设置主音量音乐叫声开始退出暂停继续重新“”（）！：，。、☑☐★●•0123456789/";

    private static Font sourceFont;
    private static TMP_FontAsset cachedFontAsset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        // With Enter Play Mode domain reload disabled, TMP_Settings can keep a
        // reference to the previous run's dynamic font after its atlas and
        // material have been destroyed. Remove it before any text is rebuilt.
        RemoveInvalidRuntimeFallbacks();
        cachedFontAsset = null;
        sourceFont = null;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditorCleanup()
    {
        // 退出 Play 模式后，运行时创建的字体图集和材质已被销毁，但 TMP_Settings 的
        // fallback 列表仍指向那个字体资源；编辑器里任何 TMP 文本重建都会走到它，
        // 抛出 "MissingReferenceException: Material has been destroyed"。这里在回到
        // Edit 模式时把它摘掉并销毁干净。
        UnityEditor.EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        UnityEditor.EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        RemoveInvalidRuntimeFallbacks();
    }

    private static void HandlePlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state != UnityEditor.PlayModeStateChange.EnteredEditMode)
            return;

        RemoveInvalidRuntimeFallbacks();
        DestroyRuntimeFont();
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterGlobalFallback()
    {
        RemoveInvalidRuntimeFallbacks();

        TMP_FontAsset fontAsset = GetOrCreate();
        if (fontAsset == null)
        {
            return;
        }

        List<TMP_FontAsset> fallbackFonts = TMP_Settings.fallbackFontAssets;
        if (fallbackFonts != null && !fallbackFonts.Contains(fontAsset))
        {
            fallbackFonts.Add(fontAsset);
        }
    }

    public static void Apply(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        TMP_FontAsset fontAsset = GetOrCreate();
        if (fontAsset == null)
        {
            return;
        }

        // MVP 的动态中文内容直接使用内置字体，避免落入旧的
        // Liberation Sans fallback 链后再次显示缺字方框。
        text.font = fontAsset;
    }

    private static TMP_FontAsset GetOrCreate()
    {
        if (cachedFontAsset != null)
        {
            return cachedFontAsset;
        }

        sourceFont = Resources.Load<Font>(BundledFontResourcePath);
        if (sourceFont == null)
        {
            Debug.LogWarning(
                $"找不到内置中文字体 Resources/{BundledFontResourcePath}，TMP 中文文本可能显示为方框。");
            return null;
        }

        cachedFontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            2048,
            2048,
            AtlasPopulationMode.Dynamic,
            true);

        if (cachedFontAsset == null)
        {
            Debug.LogWarning("无法从内置中文字体创建动态 TMP 字体资源。");
            return null;
        }

        cachedFontAsset.name = RuntimeFontAssetName;
        cachedFontAsset.hideFlags = HideFlags.DontSave;

        if (!cachedFontAsset.TryAddCharacters(MvpCharacters, out string missingCharacters))
        {
            Debug.LogWarning($"内置中文字体缺少以下字符：{missingCharacters}");
        }

        return cachedFontAsset;
    }

    private static void RemoveInvalidRuntimeFallbacks()
    {
        List<TMP_FontAsset> fallbackFonts = TMP_Settings.fallbackFontAssets;
        if (fallbackFonts == null)
            return;

        for (int i = fallbackFonts.Count - 1; i >= 0; i--)
        {
            TMP_FontAsset fallback = fallbackFonts[i];
            if (fallback == null ||
                fallback.material == null ||
                string.Equals(fallback.name, RuntimeFontAssetName, StringComparison.Ordinal))
            {
                fallbackFonts.RemoveAt(i);
            }
        }
    }

    private static void DestroyRuntimeFont()
    {
        if (cachedFontAsset != null)
        {
            if (cachedFontAsset.material != null)
                UnityEngine.Object.DestroyImmediate(cachedFontAsset.material);

            if (cachedFontAsset.atlasTextures != null)
            {
                foreach (Texture2D atlas in cachedFontAsset.atlasTextures)
                {
                    if (atlas != null)
                        UnityEngine.Object.DestroyImmediate(atlas);
                }
            }

            UnityEngine.Object.DestroyImmediate(cachedFontAsset);
        }

        cachedFontAsset = null;
        sourceFont = null;
    }
}

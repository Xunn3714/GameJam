using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Creates one dynamic TMP font asset from an installed Windows CJK font and reuses it.
/// </summary>
public static class MvpTmpUiFont
{
    private const string BundledFontResourcePath = "NotoSansSC-Regular";
    private const string MvpCharacters =
        "找到羊族群加入了棉花糖云朵小卷豆豆奶盖白团咩毛球盐巴雪软月亮“”！：0123456789/";

    private static Font sourceFont;
    private static TMP_FontAsset cachedFontAsset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterGlobalFallback()
    {
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
            Debug.LogWarning("无法从系统中文字体创建动态 TMP 字体资源。");
            return null;
        }

        cachedFontAsset.name = $"{sourceFont.name} Dynamic TMP Fallback";
        cachedFontAsset.hideFlags = HideFlags.DontSave;

        if (!cachedFontAsset.TryAddCharacters(MvpCharacters, out string missingCharacters))
        {
            Debug.LogWarning($"内置中文字体缺少以下字符：{missingCharacters}");
        }

        return cachedFontAsset;
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Applies the repository-owned TMP Chinese font to dynamic UI text.
/// </summary>
public static class MvpTmpUiFont
{
    private const string BundledFontResourcePath = "NotoSansSC-Regular SDF";

    private static TMP_FontAsset cachedFontAsset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        cachedFontAsset = null;
    }

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

        cachedFontAsset = Resources.Load<TMP_FontAsset>(BundledFontResourcePath);

        if (cachedFontAsset == null)
        {
            Debug.LogWarning(
                $"找不到内置 TMP 中文字体 Resources/{BundledFontResourcePath}，中文文本可能显示为方框。");
        }

        return cachedFontAsset;
    }
}

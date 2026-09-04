using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Creates one dynamic TMP font asset from an installed Windows CJK font and reuses it.
/// </summary>
public static class MvpTmpUiFont
{
    private static readonly string[] PreferredFontNames =
    {
        "Microsoft YaHei",
        "SimHei",
        "Noto Sans CJK SC",
        "Arial"
    };

    private static Font sourceFont;
    private static TMP_FontAsset cachedFontAsset;

    public static void Apply(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        TMP_FontAsset fontAsset = GetOrCreate();
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }
    }

    private static TMP_FontAsset GetOrCreate()
    {
        if (cachedFontAsset != null)
        {
            return cachedFontAsset;
        }

        sourceFont = Font.CreateDynamicFontFromOSFont(PreferredFontNames, 48);
        if (sourceFont == null)
        {
            Debug.LogWarning("No preferred system font was available for TMP Chinese text.");
            return null;
        }

        cachedFontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        if (cachedFontAsset == null)
        {
            Debug.LogWarning("Failed to create a dynamic TMP font asset from the system font.");
        }

        return cachedFontAsset;
    }
}

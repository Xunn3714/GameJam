using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class ChineseFontAssetTests
{
    // This guards the repository-level fallback that makes Chinese visible before Play mode.
    [Test]
    public void RepositoryChineseFontIsConfiguredForEditorAndRuntime()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            ChineseFontAssetSetup.FontAssetPath);

        Assert.That(fontAsset, Is.Not.Null, "The repository TMP Chinese font asset is missing.");
        Assert.That(fontAsset.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
        Assert.That(fontAsset.sourceFontFile, Is.Not.Null, "The source OTF is not embedded or referenced.");
        Assert.That(fontAsset.HasCharacter('羊'), Is.True, "The preview atlas does not contain Chinese glyphs.");
        Assert.That(TMP_Settings.fallbackFontAssets.Contains(fontAsset), Is.True,
            "TMP Settings does not include the repository Chinese fallback.");

        TMP_FontAsset runtimeAsset = Resources.Load<TMP_FontAsset>("NotoSansSC-Regular SDF");
        Assert.That(runtimeAsset, Is.SameAs(fontAsset),
            "The runtime font loader does not resolve to the persisted font asset.");
    }
}

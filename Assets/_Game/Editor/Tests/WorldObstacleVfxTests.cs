using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WorldObstacleVfxTests
{
    private const string WoodBreakClipPath =
        "Assets/_Game/Content/Audio/SFX/wood.mp3";

    [Test]
    public void GeneratedObstaclePrefabsHaveExactlyOneConfiguredVfxComponent()
    {
        AssertPrefabVfx(
            WorldObstaclePrefabBuilder.FencePrefabPath,
            WorldObstaclePrefabBuilder.WoodFragmentPrefabPath,
            8,
            Vector2.zero,
            0f);

        foreach (WorldObstaclePrefabBuilder.DebrisSpec spec in WorldObstaclePrefabBuilder.DebrisSpecs)
        {
            AssertPrefabVfx(
                spec.PrefabPath,
                spec.FragmentPrefabPath,
                spec.FragmentCount,
                spec.FragmentSpawnOffset,
                spec.FragmentSpawnRadius);
        }
    }

    [Test]
    public void GeneratedObstaclePrefabsKeepBreakAudioFromMain()
    {
        AssertPrefabAudio(
            WorldObstaclePrefabBuilder.FencePrefabPath,
            WoodBreakClipPath,
            null,
            1f);

        foreach (WorldObstaclePrefabBuilder.DebrisSpec spec in WorldObstaclePrefabBuilder.DebrisSpecs)
        {
            AssertPrefabAudio(
                spec.PrefabPath,
                spec.BreakClipPath,
                spec.RandomBreakClipPaths,
                spec.BreakVolume);
        }
    }

    [Test]
    public void TreeDefinitionsRequireTwentySheepAndAnExplicitDash()
    {
        foreach (WorldObstaclePrefabBuilder.DebrisSpec spec in WorldObstaclePrefabBuilder.DebrisSpecs)
        {
            if (spec.Id != "obstacle.tree" && spec.Id != "obstacle.tree_2")
                continue;

            ObstacleDefinition definition = AssetDatabase.LoadAssetAtPath<ObstacleDefinition>(spec.DefinitionPath);
            Assert.IsNotNull(definition, spec.DefinitionPath);
            Assert.AreEqual(ObstacleBreakRule.RequireCountAndInteract, definition.BreakRule, spec.DefinitionPath);
            Assert.AreEqual(20, definition.RequiredFlockCount, spec.DefinitionPath);
            Assert.AreEqual(1, definition.RequiredDashHits, spec.DefinitionPath);
        }
    }

    private static void AssertPrefabVfx(
        string prefabPath,
        string expectedFragmentPath,
        int expectedFragmentCount,
        Vector2 expectedOffset,
        float expectedRadius)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(prefab, $"Missing obstacle prefab: {prefabPath}");

        BreakableObstacleVFX[] components = prefab.GetComponents<BreakableObstacleVFX>();
        Assert.AreEqual(1, components.Length, $"{prefabPath} must have exactly one VFX component.");

        SerializedObject serialized = new SerializedObject(components[0]);
        Assert.IsNull(serialized.FindProperty("breakParticlesPrefab"),
            $"{prefabPath} must no longer expose the legacy white-particle effect.");

        GameObject expectedFragment = string.IsNullOrWhiteSpace(expectedFragmentPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(expectedFragmentPath);
        Assert.AreSame(
            expectedFragment,
            serialized.FindProperty("fragmentPrefab").objectReferenceValue,
            $"{prefabPath} has the wrong fragment prefab.");
        Assert.AreEqual(
            expectedFragmentCount,
            serialized.FindProperty("fragmentCount").intValue,
            $"{prefabPath} has the wrong fragment count.");
        Assert.AreEqual(
            expectedOffset,
            serialized.FindProperty("spawnOffset").vector2Value,
            $"{prefabPath} has the wrong spawn offset.");
        Assert.AreEqual(
            expectedRadius,
            serialized.FindProperty("fragmentSpawnRadius").floatValue,
            0.0001f,
            $"{prefabPath} has the wrong spawn radius.");
    }

    private static void AssertPrefabAudio(
        string prefabPath,
        string expectedClipPath,
        string[] expectedRandomClipPaths,
        float expectedVolume)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(prefab, $"Missing obstacle prefab: {prefabPath}");

        bool expectsAudio = !string.IsNullOrWhiteSpace(expectedClipPath)
            || (expectedRandomClipPaths != null && expectedRandomClipPaths.Length > 0);
        BreakableObstacleAudio[] components = prefab.GetComponents<BreakableObstacleAudio>();
        Assert.AreEqual(
            expectsAudio ? 1 : 0,
            components.Length,
            $"{prefabPath} has the wrong number of break-audio components.");

        if (!expectsAudio)
            return;

        SerializedObject serialized = new SerializedObject(components[0]);
        AudioClip expectedClip = string.IsNullOrWhiteSpace(expectedClipPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<AudioClip>(expectedClipPath);
        Assert.AreSame(
            expectedClip,
            serialized.FindProperty("breakClip").objectReferenceValue,
            $"{prefabPath} has the wrong fixed break clip.");

        SerializedProperty randomClips = serialized.FindProperty("randomBreakClips");
        int expectedRandomCount = expectedRandomClipPaths?.Length ?? 0;
        Assert.AreEqual(
            expectedRandomCount,
            randomClips.arraySize,
            $"{prefabPath} has the wrong number of random break clips.");
        for (int index = 0; index < expectedRandomCount; index++)
        {
            Assert.AreSame(
                AssetDatabase.LoadAssetAtPath<AudioClip>(expectedRandomClipPaths[index]),
                randomClips.GetArrayElementAtIndex(index).objectReferenceValue,
                $"{prefabPath} has the wrong random break clip at index {index}.");
        }

        Assert.AreEqual(
            expectedVolume,
            serialized.FindProperty("volumeScale").floatValue,
            0.0001f,
            $"{prefabPath} has the wrong break volume.");
    }
}

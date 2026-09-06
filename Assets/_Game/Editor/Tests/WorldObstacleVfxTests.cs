using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WorldObstacleVfxTests
{
    [Test]
    public void GeneratedObstaclePrefabsHaveExactlyOneConfiguredVfxComponent()
    {
        GameObject particlesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            WorldObstaclePrefabBuilder.BreakParticlesPrefabPath);
        Assert.IsNotNull(particlesPrefab, "Missing common break-particle prefab.");

        ParticleSystem breakParticles = particlesPrefab.GetComponent<ParticleSystem>();
        Assert.IsNotNull(breakParticles, "Common break-particle prefab has no ParticleSystem on its root.");

        AssertPrefabVfx(
            WorldObstaclePrefabBuilder.FencePrefabPath,
            breakParticles,
            WorldObstaclePrefabBuilder.WoodFragmentPrefabPath,
            8,
            Vector2.zero,
            0f);

        foreach (WorldObstaclePrefabBuilder.DebrisSpec spec in WorldObstaclePrefabBuilder.DebrisSpecs)
        {
            AssertPrefabVfx(
                spec.PrefabPath,
                breakParticles,
                spec.FragmentPrefabPath,
                spec.FragmentCount,
                spec.FragmentSpawnOffset,
                spec.FragmentSpawnRadius);
        }
    }

    private static void AssertPrefabVfx(
        string prefabPath,
        ParticleSystem expectedParticles,
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
        Assert.AreSame(
            expectedParticles,
            serialized.FindProperty("breakParticlesPrefab").objectReferenceValue,
            $"{prefabPath} has the wrong common particle prefab.");

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
}

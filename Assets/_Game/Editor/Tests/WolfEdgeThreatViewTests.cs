using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WolfEdgeThreatViewTests
{
    private const string WolfPrefabPath = "Assets/_Game/Content/Perfabs/Wolf/Wolf.prefab";

    [TestCase(0f, 0f)]
    [TestCase(0.5f, 0.5f)]
    [TestCase(1f, 1f)]
    public void IsInsideViewport_IncludesVisibleBounds(float x, float y)
    {
        Assert.That(WolfEdgeThreatView.IsInsideViewport(new Vector2(x, y)), Is.True);
    }

    [TestCase(-0.1f, 0.5f)]
    [TestCase(1.1f, 0.5f)]
    [TestCase(0.5f, -0.1f)]
    [TestCase(0.5f, 1.1f)]
    public void IsInsideViewport_RejectsPointsBeyondVisibleBounds(float x, float y)
    {
        Assert.That(WolfEdgeThreatView.IsInsideViewport(new Vector2(x, y)), Is.False);
    }

    [TestCase(-0.25f, 0.5f, -886f, 0f, 1f, 0f)]
    [TestCase(1.25f, 0.5f, 886f, 0f, -1f, 0f)]
    [TestCase(0.5f, -0.25f, 0f, -466f, 0f, 1f)]
    [TestCase(0.5f, 1.25f, 0f, 466f, 0f, -1f)]
    public void TryCalculateEdgePosition_ClampsToMatchingScreenEdge(
        float viewportX,
        float viewportY,
        float expectedX,
        float expectedY,
        float expectedInwardX,
        float expectedInwardY)
    {
        bool found = WolfEdgeThreatView.TryCalculateEdgePosition(
            new Rect(0f, 0f, 1920f, 1080f),
            new Vector2(viewportX, viewportY),
            74f,
            out Vector2 position,
            out Vector2 inward);

        Assert.That(found, Is.True);
        Assert.That(position.x, Is.EqualTo(expectedX).Within(0.01f));
        Assert.That(position.y, Is.EqualTo(expectedY).Within(0.01f));
        Assert.That(inward.x, Is.EqualTo(expectedInwardX).Within(0.01f));
        Assert.That(inward.y, Is.EqualTo(expectedInwardY).Within(0.01f));
    }

    [Test]
    public void TryCalculateEdgePosition_DiagonalPointStaysWithinSafeBounds()
    {
        bool found = WolfEdgeThreatView.TryCalculateEdgePosition(
            new Rect(0f, 0f, 1920f, 1080f),
            new Vector2(1.4f, 1.2f),
            74f,
            out Vector2 position,
            out Vector2 inward);

        Assert.That(found, Is.True);
        Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(886.01f));
        Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(466.01f));
        Assert.That(
            Mathf.Approximately(Mathf.Abs(position.x), 886f)
            || Mathf.Approximately(Mathf.Abs(position.y), 466f),
            Is.True);
        Assert.That(Vector2.Dot(position.normalized, inward), Is.EqualTo(-1f).Within(0.001f));
    }

    [Test]
    public void TryCalculateDirectionEdgePosition_UsesDirectionOfVisibleLogicalSpawnPoint()
    {
        bool found = WolfEdgeThreatView.TryCalculateDirectionEdgePosition(
            new Rect(0f, 0f, 1920f, 1080f),
            new Vector2(0.75f, 0.5f),
            74f,
            out Vector2 position,
            out Vector2 inward);

        Assert.That(found, Is.True);
        Assert.That(position.x, Is.EqualTo(886f).Within(0.01f));
        Assert.That(position.y, Is.EqualTo(0f).Within(0.01f));
        Assert.That(inward.x, Is.EqualTo(-1f).Within(0.001f));
        Assert.That(inward.y, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void AdvancePulsePhase_FrequencyChangeAdvancesWithoutAbsoluteTimeJump()
    {
        float phase = WolfEdgeThreatView.AdvancePulsePhase(0.2f, 1f, 0.1f);
        phase = WolfEdgeThreatView.AdvancePulsePhase(phase, 8f, 0.01f);

        Assert.That(phase, Is.EqualTo(0.38f).Within(0.0001f));
    }

    [Test]
    public void WolfPrefab_ProvidesThreatIndicatorPortrait()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WolfPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        Wolf wolf = prefab.GetComponent<Wolf>();
        Assert.That(wolf, Is.Not.Null);
        Assert.That(wolf.ThreatIndicatorSprite, Is.Not.Null);
    }

    [Test]
    public void Wolf_AdditionalWarningLeadTimeExtendsBaseWarning()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WolfPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            Wolf wolf = instance.GetComponent<Wolf>();
            float baseDuration = wolf.WarningDuration;

            wolf.SetAdditionalWarningLeadTime(0.8f);

            Assert.That(wolf.WarningDuration, Is.EqualTo(baseDuration + 0.8f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void Wolf_WarningDurationCompensatesForHigherStageSpeed()
    {
        float openingDuration = Wolf.CalculateWarningDuration(1.2f, 0.8f, 2f, 0.35f);
        float lateDuration = Wolf.CalculateWarningDuration(1.2f, 0.8f, 4f, 0.35f);

        Assert.That(openingDuration, Is.GreaterThan(lateDuration));
        Assert.That(lateDuration, Is.GreaterThan(1f),
            "Speed compensation should preserve a readable warning instead of removing reaction time.");
    }
}

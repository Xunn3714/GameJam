using NUnit.Framework;
using UnityEngine;

public sealed class FlockActionControllerTests
{
    [TestCase(0f, FlockDashChargeTier.Tap)]
    [TestCase(0.49f, FlockDashChargeTier.Tap)]
    [TestCase(0.5f, FlockDashChargeTier.HalfSecond)]
    [TestCase(0.99f, FlockDashChargeTier.HalfSecond)]
    [TestCase(1f, FlockDashChargeTier.OneSecond)]
    [TestCase(1.49f, FlockDashChargeTier.OneSecond)]
    [TestCase(1.5f, FlockDashChargeTier.OnePointFiveSeconds)]
    [TestCase(1.99f, FlockDashChargeTier.OnePointFiveSeconds)]
    [TestCase(2f, FlockDashChargeTier.TwoSeconds)]
    [TestCase(10f, FlockDashChargeTier.TwoSeconds)]
    public void ChargeDurationSelectsExpectedTier(float duration, FlockDashChargeTier expected)
    {
        Assert.AreEqual(expected, FlockActionController.ResolveChargeTier(duration));
    }

    [Test]
    public void DefaultChargeProfilesBecomeFasterAndTravelFarther()
    {
        GameObject owner = new GameObject("FlockActionTest");
        try
        {
            FlockActionController actions = owner.AddComponent<FlockActionController>();
            FlockDashProfile half = actions.GetDashProfile(0.5f);
            FlockDashProfile one = actions.GetDashProfile(1f);
            FlockDashProfile onePointFive = actions.GetDashProfile(1.5f);
            FlockDashProfile two = actions.GetDashProfile(2f);

            Assert.That(one.Speed, Is.GreaterThan(half.Speed));
            Assert.That(onePointFive.Speed, Is.GreaterThan(one.Speed));
            Assert.That(two.Speed, Is.GreaterThan(onePointFive.Speed));
            Assert.That(one.Distance, Is.GreaterThan(half.Distance));
            Assert.That(onePointFive.Distance, Is.GreaterThan(one.Distance));
            Assert.That(two.Distance, Is.GreaterThan(onePointFive.Distance));
            Assert.AreEqual(1f, one.ImpactMultiplier);
            Assert.AreEqual(1f, onePointFive.ImpactMultiplier);
            Assert.AreEqual(1f, two.ImpactMultiplier);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void ImpactForceUsesCurrentMemberCountAndMultiplier()
    {
        Assert.AreEqual(12f, FlockActionController.CalculateImpactForce(12, 1f));
        Assert.AreEqual(18f, FlockActionController.CalculateImpactForce(12, 1.5f));
        Assert.IsTrue(FlockActionController.MeetsBreakThreshold(6f, 6));
        Assert.IsFalse(FlockActionController.MeetsBreakThreshold(5.99f, 6));
    }

    [Test]
    public void CompressionSpeedScaleTracksFormationContinuously()
    {
        float normal = FlockActionController.CalculateCompressionSpeedScale(1f, 0.45f, 0.58f);
        float halfway = FlockActionController.CalculateCompressionSpeedScale(0.725f, 0.45f, 0.58f);
        float compact = FlockActionController.CalculateCompressionSpeedScale(0.45f, 0.45f, 0.58f);

        Assert.AreEqual(1f, normal, 0.0001f);
        Assert.That(halfway, Is.LessThan(normal).And.GreaterThan(compact));
        Assert.AreEqual(0.58f, compact, 0.0001f);
    }
}

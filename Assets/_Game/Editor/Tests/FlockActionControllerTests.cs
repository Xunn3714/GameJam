using NUnit.Framework;

public sealed class FlockActionControllerTests
{
    [Test]
    public void ImpactForceUsesCurrentMemberCount()
    {
        Assert.AreEqual(12f, FlockActionController.CalculateImpactForce(12));
        Assert.AreEqual(0f, FlockActionController.CalculateImpactForce(-2));
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

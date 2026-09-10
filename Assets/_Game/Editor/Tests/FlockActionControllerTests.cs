using NUnit.Framework;
using UnityEngine;

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
    public void RetreatingMemberBlockDoesNotStopGroupAction()
    {
        Assert.IsFalse(FlockActionController.ShouldMemberBlockStopAction(
            Vector2.zero,
            Vector2.right,
            Vector2.left * 0.1f,
            Vector2.left));
    }

    [Test]
    public void RearMemberBlockDoesNotStopForwardDash()
    {
        Assert.IsFalse(FlockActionController.ShouldMemberBlockStopAction(
            Vector2.zero,
            Vector2.right,
            Vector2.right * 0.1f,
            Vector2.left));
    }

    [Test]
    public void FrontMemberBlockStopsForwardDash()
    {
        Assert.IsTrue(FlockActionController.ShouldMemberBlockStopAction(
            Vector2.zero,
            Vector2.right,
            Vector2.right * 0.1f,
            Vector2.right));
    }

    [Test]
    public void RetreatSpeedEasesAtBothEnds()
    {
        Assert.That(FlockActionController.CalculateRetreatSpeedFactor(0f, 0.45f),
            Is.EqualTo(0.45f).Within(0.001f));
        Assert.That(FlockActionController.CalculateRetreatSpeedFactor(0.5f, 0.45f),
            Is.EqualTo(1f).Within(0.001f));
        Assert.That(FlockActionController.CalculateRetreatSpeedFactor(1f, 0.45f),
            Is.EqualTo(0.45f).Within(0.001f));
    }

    [Test]
    public void DashSpeedBuildsThenSettlesAtTheEnd()
    {
        Assert.That(FlockActionController.CalculateDashSpeedFactor(0f, 0.65f, 0.75f),
            Is.EqualTo(0.65f).Within(0.001f));
        Assert.That(FlockActionController.CalculateDashSpeedFactor(0.5f, 0.65f, 0.75f),
            Is.EqualTo(1f).Within(0.001f));
        Assert.That(FlockActionController.CalculateDashSpeedFactor(1f, 0.65f, 0.75f),
            Is.EqualTo(0.75f).Within(0.001f));
    }

    [Test]
    public void ImpactFollowThroughPreservesThenReleasesMomentum()
    {
        Assert.That(FlockActionController.CalculateImpactFollowThroughSpeedFactor(1f, 0.72f),
            Is.EqualTo(0.72f).Within(0.001f));
        Assert.That(FlockActionController.CalculateImpactFollowThroughSpeedFactor(0.5f, 0.72f),
            Is.GreaterThan(0f).And.LessThan(0.72f));
        Assert.That(FlockActionController.CalculateImpactFollowThroughSpeedFactor(0f, 0.72f),
            Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void DashSweepStopsBeforeThinColliderThatEndpointCheckMisses()
    {
        GameObject blockerObject = new GameObject("ThinDashBlocker");
        try
        {
            blockerObject.transform.position = new Vector3(0f, 1234f, 0f);
            BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
            blocker.size = new Vector2(0.02f, 2f);
            Physics2D.SyncTransforms();

            Vector2 from = new Vector2(-1f, 1234f);
            Vector2 to = new Vector2(1f, 1234f);
            int mask = 1 << blockerObject.layer;

            Vector2 endpointResult = MovementBlocking.ResolveMove(from, to, 0.2f, mask);
            Vector2 sweepResult = MovementBlocking.ResolveDashMove(
                from,
                to,
                0.2f,
                mask,
                out MovementBlockResult blockResult);

            Assert.AreEqual(to, endpointResult);
            Assert.IsTrue(blockResult.WasBlocked);
            Assert.AreSame(blocker, blockResult.Blocker);
            Assert.Less(sweepResult.x, -0.19f);
        }
        finally
        {
            Object.DestroyImmediate(blockerObject);
        }
    }

}

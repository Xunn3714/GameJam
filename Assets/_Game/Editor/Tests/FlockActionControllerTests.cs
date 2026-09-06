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

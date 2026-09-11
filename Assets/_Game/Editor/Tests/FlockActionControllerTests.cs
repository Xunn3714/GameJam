using System.Collections.Generic;
using System.Reflection;
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
    public void GroupActionCollectsEveryDistinctMemberBlockerFromThePhysicsStep()
    {
        GameObject flockObject = new GameObject("FlockActionControllerTests_Flock");
        GameObject firstObject = new GameObject("FlockActionControllerTests_BlockerA");
        GameObject secondObject = new GameObject("FlockActionControllerTests_BlockerB");
        try
        {
            FlockController flock = flockObject.AddComponent<FlockController>();
            Collider2D first = firstObject.AddComponent<BoxCollider2D>();
            Collider2D second = secondObject.AddComponent<BoxCollider2D>();

            InvokeFlockMethod(flock, "SetGroupActionState", true, false, Vector2.right);
            InvokeFlockMethod(flock, "ReportGroupActionMemberBlocked", first);
            InvokeFlockMethod(flock, "ReportGroupActionMemberBlocked", second);
            InvokeFlockMethod(flock, "ReportGroupActionMemberBlocked", first);

            List<Collider2D> results = new List<Collider2D>();
            int count = (int)InvokeFlockMethod(
                flock,
                "ConsumeGroupActionMemberBlocks",
                results);

            Assert.That(count, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { first, second }, results);
            Assert.That((int)InvokeFlockMethod(
                flock,
                "ConsumeGroupActionMemberBlocks",
                results), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(secondObject);
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(flockObject);
        }
    }

    [Test]
    public void DashBatchBreaksEveryContactedFenceSegment()
    {
        GameObject actionObject = new GameObject("FlockActionControllerTests_Action");
        GameObject fenceGroup = new GameObject("FlockActionControllerTests_FenceGroup");
        ObstacleDefinition definition = CreateObstacleDefinition(requiredCount: 6, requiredHits: 1);
        try
        {
            FlockController flock = actionObject.AddComponent<FlockController>();
            FlockMovementController movement = actionObject.AddComponent<FlockMovementController>();
            FlockActionController action = actionObject.AddComponent<FlockActionController>();
            action.Configure(flock, movement);
            SetPrivateField(action, "currentImpactForce", 6f);

            Collider2D first = CreateFenceSegment(fenceGroup.transform, definition, "FenceA");
            Collider2D second = CreateFenceSegment(fenceGroup.transform, definition, "FenceB");

            bool canContinue = (bool)InvokeActionMethod(
                action,
                "HandleDashBlocks",
                new List<Collider2D> { first, second });

            Assert.That(canContinue, Is.True);
            Assert.That(first.GetComponent<BreakableObstacle>().IsBroken, Is.True);
            Assert.That(second.GetComponent<BreakableObstacle>().IsBroken, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(fenceGroup);
            Object.DestroyImmediate(actionObject);
        }
    }

    [Test]
    public void DashBatchCountsMultiColliderObstacleOnlyOnce()
    {
        GameObject actionObject = new GameObject("FlockActionControllerTests_Action");
        GameObject obstacleObject = new GameObject("FlockActionControllerTests_MultiColliderRock");
        obstacleObject.SetActive(false);
        ObstacleDefinition definition = CreateObstacleDefinition(requiredCount: 20, requiredHits: 2);
        try
        {
            FlockController flock = actionObject.AddComponent<FlockController>();
            FlockMovementController movement = actionObject.AddComponent<FlockMovementController>();
            FlockActionController action = actionObject.AddComponent<FlockActionController>();
            action.Configure(flock, movement);
            SetPrivateField(action, "currentImpactForce", 20f);

            Collider2D first = obstacleObject.AddComponent<BoxCollider2D>();
            GameObject child = new GameObject("SecondCollider");
            child.transform.SetParent(obstacleObject.transform, false);
            Collider2D second = child.AddComponent<BoxCollider2D>();
            BreakableObstacle obstacle = obstacleObject.AddComponent<BreakableObstacle>();
            obstacle.Configure(definition, null);
            obstacleObject.SetActive(true);

            bool canContinue = (bool)InvokeActionMethod(
                action,
                "HandleDashBlocks",
                new List<Collider2D> { first, second });

            Assert.That(canContinue, Is.False);
            Assert.That(obstacle.ReceivedDashHits, Is.EqualTo(1));
            Assert.That(obstacle.IsDamaged, Is.True);
            Assert.That(obstacle.IsBroken, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(obstacleObject);
            Object.DestroyImmediate(actionObject);
        }
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

    private static object InvokeFlockMethod(
        FlockController flock,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = typeof(FlockController).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing FlockController.{methodName} test hook.");
        return method.Invoke(flock, arguments);
    }

    private static object InvokeActionMethod(
        FlockActionController action,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = typeof(FlockActionController).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing FlockActionController.{methodName} test hook.");
        return method.Invoke(action, arguments);
    }

    private static Collider2D CreateFenceSegment(
        Transform parent,
        ObstacleDefinition definition,
        string name)
    {
        GameObject segment = new GameObject(name);
        segment.SetActive(false);
        segment.transform.SetParent(parent, false);
        Collider2D collider = segment.AddComponent<BoxCollider2D>();
        BreakableObstacle obstacle = segment.AddComponent<BreakableObstacle>();
        segment.AddComponent<FenceObstacle>();
        obstacle.Configure(definition, null);
        segment.SetActive(true);
        return collider;
    }

    private static ObstacleDefinition CreateObstacleDefinition(
        int requiredCount,
        int requiredHits)
    {
        ObstacleDefinition definition = ScriptableObject.CreateInstance<ObstacleDefinition>();
        SetPrivateField(definition, "breakRule", ObstacleBreakRule.RequireCountAndInteract);
        SetPrivateField(definition, "requiredFlockCount", requiredCount);
        SetPrivateField(definition, "requiredDashHits", requiredHits);
        return definition;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing {target.GetType().Name}.{fieldName} test hook.");
        field.SetValue(target, value);
    }
}

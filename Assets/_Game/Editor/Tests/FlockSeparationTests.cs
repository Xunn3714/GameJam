using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class FlockSeparationTests
{
    [Test]
    public void NeighborChainKeepsEveryReachableMemberInTheMainGroup()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateObject("TestFlock", Vector2.zero, objects)
                .AddComponent<FlockController>();
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            SheepMember anchor = CreateMember("Anchor", Vector2.zero, objects);
            SheepMember middle = CreateMember("Middle", new Vector2(2f, 0f), objects);
            SheepMember edge = CreateMember("Edge", new Vector2(4f, 0f), objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { anchor }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { middle }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { edge }));

            SetField(flock, "mainGroupLinkDistance", 2.5f);
            SetField(flock, "detachDistanceFromMainGroup", 3f);
            SetField(flock, "detachDelay", 0f);
            evaluate.Invoke(flock, new object[] { 10f });

            Assert.AreEqual(3, flock.MemberCount);
            Assert.AreSame(flock, edge.Flock);
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void BlockingObstacleBreaksConnectivityBeforeDistanceDetachment()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            int blockingLayer = LayerMask.NameToLayer(MovementBlocking.BlockingLayerName);
            Assert.That(blockingLayer, Is.GreaterThanOrEqualTo(0));

            FlockController flock = CreateObject("TestFlock", Vector2.zero, objects)
                .AddComponent<FlockController>();
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            SheepMember anchor = CreateMember("Anchor", Vector2.zero, objects);
            SheepMember bridge = CreateMember("Bridge", new Vector2(3f, 0f), objects);
            SheepMember distant = CreateMember("Distant", new Vector2(6f, 0f), objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { anchor }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { bridge }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { distant }));

            GameObject wall = CreateObject("BlockingWall", new Vector2(1.5f, 0f), objects);
            wall.layer = blockingLayer;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.5f, 4f);
            Physics2D.SyncTransforms();

            SetField(flock, "mainGroupLinkDistance", 3.5f);
            SetField(flock, "detachDistanceFromMainGroup", 5f);
            SetField(flock, "detachDelay", 0f);
            SetField(flock, "detachScatterSpeed", 0f);
            SetField(flock, "separationBlockingLayers", (LayerMask)(1 << blockingLayer));
            evaluate.Invoke(flock, new object[] { 10f });

            Assert.AreEqual(2, flock.MemberCount);
            Assert.AreSame(flock, bridge.Flock);
            Assert.IsNull(distant.Flock);
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void DistantDisconnectedMemberBecomesWildAndReturnsWithoutNewRecruitCredit()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            GameObject flockObject = CreateObject("TestFlock", Vector2.zero, objects);
            FlockController flock = flockObject.AddComponent<FlockController>();
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            SheepMember anchor = CreateMember("Anchor", Vector2.zero, objects);
            SheepMember near = CreateMember("Near", new Vector2(2.5f, 0f), objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { anchor }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { near }));

            SheepMember distant = CreateMember("Distant", new Vector2(10f, 0f), objects);
            RecruitableSheep recruitable = distant.gameObject.AddComponent<RecruitableSheep>();
            Assert.IsTrue(flock.TryRecruit(recruitable));
            Assert.AreEqual(1, flock.RecruitedCount);

            SetField(flock, "mainGroupLinkDistance", 3.5f);
            SetField(flock, "detachDistanceFromMainGroup", 6f);
            SetField(flock, "detachDelay", 0f);
            SetField(flock, "detachScatterSpeed", 0f);

            int separatedCount = 0;
            flock.MembersSeparated += count => separatedCount += count;
            evaluate.Invoke(flock, new object[] { 10f });

            Assert.AreSame(anchor, flock.MainGroupAnchor);
            Assert.AreEqual(2, flock.MemberCount);
            Assert.AreEqual(1, separatedCount);
            Assert.IsNull(distant.Flock);
            Assert.IsFalse(recruitable.IsRecruited);
            Assert.IsTrue(distant.GetComponent<ScatteredSheep>().IsScattered);

            recruitable.ReleaseForRecruitment(0f);
            Assert.IsTrue(flock.TryRecruit(recruitable));
            Assert.AreEqual(3, flock.MemberCount);
            Assert.AreEqual(1, flock.RecruitedCount);
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void DistantMemberMustRemainDisconnectedForTheConfiguredDelay()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            GameObject flockObject = CreateObject("TestFlock", Vector2.zero, objects);
            FlockController flock = flockObject.AddComponent<FlockController>();
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            SheepMember anchor = CreateMember("Anchor", Vector2.zero, objects);
            SheepMember distant = CreateMember("Distant", new Vector2(10f, 0f), objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { anchor }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { distant }));

            SetField(flock, "mainGroupLinkDistance", 3.5f);
            SetField(flock, "detachDistanceFromMainGroup", 6f);
            SetField(flock, "detachDelay", 1.5f);
            SetField(flock, "detachScatterSpeed", 0f);

            evaluate.Invoke(flock, new object[] { 10f });
            evaluate.Invoke(flock, new object[] { 11.49f });
            Assert.AreEqual(2, flock.MemberCount);
            Assert.AreSame(flock, distant.Flock);

            evaluate.Invoke(flock, new object[] { 11.5f });
            Assert.AreEqual(1, flock.MemberCount);
            Assert.IsNull(distant.Flock);
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [TestCase(100)]
    [TestCase(200)]
    [TestCase(500)]
    public void SpatialConnectivityScaleCheckKeepsConnectedMembers(int memberCount)
    {
        List<GameObject> objects = new List<GameObject>(memberCount + 1);
        try
        {
            FlockController flock = CreateObject("ScaleTestFlock", Vector2.zero, objects)
                .AddComponent<FlockController>();
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            const int membersPerRow = 25;
            const float spacing = 1.5f;
            for (int index = 0; index < memberCount; index++)
            {
                Vector2 position = new Vector2(
                    index % membersPerRow * spacing,
                    index / membersPerRow * spacing);
                SheepMember member = CreateMember($"Member_{index:000}", position, objects);
                Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { member }));
            }

            SetField(flock, "mainGroupLinkDistance", 2f);
            SetField(flock, "detachDistanceFromMainGroup", 3f);
            SetField(flock, "detachDelay", 0f);
            SetField(flock, "separationBlockingLayers", (LayerMask)0);

            Stopwatch stopwatch = Stopwatch.StartNew();
            evaluate.Invoke(flock, new object[] { 10f });
            stopwatch.Stop();

            Assert.AreEqual(memberCount, flock.MemberCount);
            Debug.Log(
                $"Spatial separation check: {memberCount} connected members in " +
                $"{stopwatch.Elapsed.TotalMilliseconds:0.###} ms.");
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    private static MethodInfo GetPrivateMethod(string name)
    {
        MethodInfo method = typeof(FlockController).GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return method;
    }

    private static void DestroyObjects(List<GameObject> objects)
    {
        for (int index = objects.Count - 1; index >= 0; index--)
            Object.DestroyImmediate(objects[index]);
    }

    private static SheepMember CreateMember(string name, Vector2 position, List<GameObject> objects)
    {
        return CreateObject(name, position, objects).AddComponent<SheepMember>();
    }

    private static GameObject CreateObject(string name, Vector2 position, List<GameObject> objects)
    {
        GameObject owner = new GameObject(name);
        owner.transform.position = position;
        objects.Add(owner);
        return owner;
    }

    private static void SetField<T>(FlockController flock, string name, T value)
    {
        FieldInfo field = typeof(FlockController).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(flock, value);
    }
}

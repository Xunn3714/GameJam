using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class FlockSeparationTests
{
    [Test]
    public void CurrentMemberCountDeterminesFieldWithoutMovingSheep()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            List<Vector2> originalPositions = new List<Vector2>();
            for (int index = 0; index < 30; index++)
            {
                Vector2 position = new Vector2(
                    (index % 6 - 2.5f) * 0.8f,
                    (index / 6 - 2f) * 0.8f);
                originalPositions.Add(position);
                Assert.IsTrue((bool)addMember.Invoke(
                    flock,
                    new object[] { CreateMember($"Core_{index:00}", position, objects) }));
            }

            SheepMember edge = CreateMember("Edge", new Vector2(8.5f, 0f), objects);
            originalPositions.Add(edge.transform.position);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { edge }));

            SetField(flock, "detachDelay", 0f);
            SetField(flock, "detachScatterSpeed", 0f);
            evaluate.Invoke(flock, new object[] { 10f });

            float expectedRadius = 3.5f + 1.2f * Mathf.Sqrt(31);
            Assert.That(flock.CurrentMembershipRadius, Is.EqualTo(expectedRadius).Within(0.01f));
            Assert.AreEqual(31, flock.MemberCount);
            for (int index = 0; index < flock.Members.Count; index++)
            {
                Assert.That(
                    (Vector2)flock.Members[index].transform.position,
                    Is.EqualTo(originalPositions[index]));
            }
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [TestCase(30)]
    [TestCase(1000)]
    public void DistantMembersUseSnapshotRadiusAndDetachTogether(int distantMemberCount)
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            SheepMember center = CreateMember("Center", Vector2.zero, objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { center }));

            float expectedRadius = Mathf.Max(
                5.5f,
                3.5f + 1.2f * Mathf.Sqrt(distantMemberCount + 1));
            List<SheepMember> distantMembers = new List<SheepMember>(distantMemberCount);
            for (int index = 0; index < distantMemberCount; index++)
            {
                Vector2 position = new Vector2(
                    expectedRadius + 10f + index % 6 * 0.5f,
                    index / 6 * 0.5f);
                SheepMember member = CreateMember($"Distant_{index:00}", position, objects);
                member.gameObject.AddComponent<RecruitableSheep>();
                distantMembers.Add(member);
                Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { member }));
            }

            SetField(flock, "detachDelay", 0f);
            SetField(flock, "detachScatterSpeed", 0f);
            int separatedCount = 0;
            flock.MembersSeparated += count => separatedCount += count;
            Stopwatch stopwatch = Stopwatch.StartNew();
            evaluate.Invoke(flock, new object[] { 10f });
            stopwatch.Stop();

            Assert.That(flock.CurrentMembershipRadius, Is.EqualTo(expectedRadius).Within(0.01f));
            Assert.AreEqual(1, flock.MemberCount);
            Assert.AreEqual(distantMemberCount, separatedCount);
            for (int index = 0; index < distantMembers.Count; index++)
            {
                Assert.IsNull(distantMembers[index].Flock);
                Assert.IsTrue(distantMembers[index].GetComponent<ScatteredSheep>().IsScattered);
            }

            evaluate.Invoke(flock, new object[] { 11f });
            Assert.That(flock.CurrentMembershipRadius, Is.EqualTo(5.5f).Within(0.01f));

            Debug.Log(
                $"Membership field detached {distantMemberCount} distant members in "
                + $"{stopwatch.Elapsed.TotalMilliseconds:0.###} ms.");
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void LastMemberIsRetainedOutsideMembershipField()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");
            SheepMember member = CreateMember("LastMember", new Vector2(10f, 0f), objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { member }));

            SetField(flock, "detachDelay", 0f);
            SetField(flock, "detachScatterSpeed", 0f);
            evaluate.Invoke(flock, new object[] { 10f });

            Assert.AreEqual(1, flock.MemberCount);
            Assert.AreSame(flock, member.Flock);
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void AllDistantMembersStillRetainNearestMember()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");
            SheepMember nearest = CreateMember("Nearest", new Vector2(10f, 0f), objects);
            SheepMember middle = CreateMember("Middle", new Vector2(12f, 0f), objects);
            SheepMember farthest = CreateMember("Farthest", new Vector2(14f, 0f), objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { nearest }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { middle }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { farthest }));

            SetField(flock, "detachDelay", 0f);
            SetField(flock, "detachScatterSpeed", 0f);
            evaluate.Invoke(flock, new object[] { 10f });

            Assert.AreEqual(1, flock.MemberCount);
            Assert.AreSame(nearest, flock.Members[0]);
            Assert.AreSame(flock, nearest.Flock);
            Assert.IsNull(middle.Flock);
            Assert.IsNull(farthest.Flock);
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void CenterCanAdvanceUntilEveryMemberPathIsBlocked()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo allBlocked = GetPrivateMethod("AreAllMembersBlocked");

            SheepMember blocked = CreateMember("Blocked", Vector2.zero, objects);
            SheepMember free = CreateMember("Free", Vector2.one, objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { blocked }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { free }));
            SetMovementReport(blocked.Agent, Vector2.right, true);
            SetMovementReport(free.Agent, Vector2.right, false);

            Assert.IsFalse((bool)allBlocked.Invoke(flock, new object[] { Vector2.right }));

            SetMovementReport(free.Agent, Vector2.right, true);
            Assert.IsTrue((bool)allBlocked.Invoke(flock, new object[] { Vector2.right }));

            // 转向后旧方向的报告不能把中心误锁一帧。
            Assert.IsFalse((bool)allBlocked.Invoke(flock, new object[] { Vector2.up }));

            Assert.IsTrue(flock.Remove(free));
            Assert.IsTrue((bool)allBlocked.Invoke(flock, new object[] { Vector2.right }));
            SetMovementReport(blocked.Agent, Vector2.right, false);
            Assert.IsFalse((bool)allBlocked.Invoke(flock, new object[] { Vector2.right }));
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void DesiredCenterIsNotAWorldObstacleContact()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            GameObject center = CreateObject("DesiredCenter", Vector2.zero, objects);
            center.AddComponent<FlockController>();
            CircleCollider2D centerCollider = center.AddComponent<CircleCollider2D>();

            Assert.IsFalse(BreakableObstacle.IsFlockContact(centerCollider));

            MethodInfo addMember = GetPrivateMethod("AddMember");
            FlockController flock = center.GetComponent<FlockController>();
            SheepMember member = CreateMember("RealMember", Vector2.zero, objects);
            CircleCollider2D memberCollider = member.gameObject.AddComponent<CircleCollider2D>();
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { member }));

            Assert.IsTrue(BreakableObstacle.IsFlockContact(memberCollider));
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void CenterLeashKeepsMemberInsideSafeEllipse()
    {
        Vector2 halfExtents = new Vector2(4f, 3f);
        Vector2 memberPosition = new Vector2(2f, -1f);

        Assert.AreEqual(
            new Vector2(3f, 0f),
            FlockMovementController.ClampCenterToMemberEllipse(
                new Vector2(3f, 0f),
                memberPosition,
                halfExtents));

        Vector2 clamped = FlockMovementController.ClampCenterToMemberEllipse(
            new Vector2(12f, -1f),
            memberPosition,
            halfExtents);
        Assert.That(clamped.x, Is.EqualTo(6f).Within(0.001f));
        Assert.That(clamped.y, Is.EqualTo(-1f).Within(0.001f));
    }

    [Test]
    public void OutsideMemberMustRemainOutsideForConfiguredDelay()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");
            SheepMember center = CreateMember("Center", Vector2.zero, objects);
            SheepMember distant = CreateMember("Distant", new Vector2(10f, 0f), objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { center }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { distant }));

            SetField(flock, "detachDelay", 1.5f);
            SetField(flock, "detachScatterSpeed", 0f);
            evaluate.Invoke(flock, new object[] { 10f });
            evaluate.Invoke(flock, new object[] { 11.49f });
            Assert.AreEqual(2, flock.MemberCount);

            evaluate.Invoke(flock, new object[] { 11.5f });
            Assert.AreEqual(1, flock.MemberCount);
            Assert.IsNull(distant.Flock);
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void GroupActionClearsPendingOutsideTimer()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");
            MethodInfo setGroupAction = GetPrivateMethod("SetGroupActionState");
            SheepMember center = CreateMember("Center", Vector2.zero, objects);
            SheepMember distant = CreateMember("Distant", new Vector2(10f, 0f), objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { center }));
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { distant }));

            SetField(flock, "detachDelay", 1.5f);
            SetField(flock, "detachScatterSpeed", 0f);
            evaluate.Invoke(flock, new object[] { 10f });
            setGroupAction.Invoke(flock, new object[] { true, true, Vector2.right });
            setGroupAction.Invoke(flock, new object[] { false, false, Vector2.right });

            evaluate.Invoke(flock, new object[] { 11.5f });
            Assert.AreEqual(2, flock.MemberCount);
            evaluate.Invoke(flock, new object[] { 13f });
            Assert.AreEqual(1, flock.MemberCount);
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void ScatteredMemberMustReturnInsideFieldBeforeRecruitment()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");
            SheepMember center = CreateMember("Center", Vector2.zero, objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { center }));

            SheepMember returning = CreateMember("Returning", new Vector2(10f, 0f), objects);
            RecruitableSheep recruitable = returning.gameObject.AddComponent<RecruitableSheep>();
            Assert.IsTrue(flock.TryRecruit(recruitable));
            Assert.AreEqual(1, flock.RecruitedCount);

            SetField(flock, "detachDelay", 0f);
            SetField(flock, "detachScatterSpeed", 0f);
            evaluate.Invoke(flock, new object[] { 10f });
            recruitable.ReleaseForRecruitment(0f);

            Assert.IsFalse(flock.TryRecruit(recruitable));
            returning.transform.position = new Vector2(4f, 0f);
            Assert.IsTrue(flock.TryRecruit(recruitable));
            Assert.AreEqual(2, flock.MemberCount);
            Assert.AreEqual(1, flock.RecruitedCount);
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [TestCase(100)]
    [TestCase(200)]
    [TestCase(500)]
    [TestCase(1000)]
    public void MembershipFieldScaleCheckKeepsDenseMembers(int memberCount)
    {
        List<GameObject> objects = new List<GameObject>(memberCount + 1);
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            float comfortableRadius = 1f + 0.8f * Mathf.Sqrt(memberCount - 1);
            float aspectRoot = Mathf.Sqrt(1.55f);
            float longitudinalRadius = comfortableRadius * aspectRoot * 1.1f;
            float lateralRadius = comfortableRadius / aspectRoot * 1.1f;
            const float goldenAngle = 2.39996323f;
            for (int index = 0; index < memberCount; index++)
            {
                float normalizedRadius = Mathf.Sqrt((index + 0.5f) / memberCount);
                float angle = index * goldenAngle;
                Vector2 position = new Vector2(
                    Mathf.Cos(angle) * longitudinalRadius * normalizedRadius,
                    Mathf.Sin(angle) * lateralRadius * normalizedRadius);
                Assert.IsTrue((bool)addMember.Invoke(
                    flock,
                    new object[] { CreateMember($"Member_{index:000}", position, objects) }));
            }

            SetField(flock, "detachDelay", 0f);
            SetField(flock, "detachScatterSpeed", 0f);
            Stopwatch stopwatch = Stopwatch.StartNew();
            evaluate.Invoke(flock, new object[] { 10f });
            stopwatch.Stop();

            Assert.AreEqual(memberCount, flock.MemberCount);
            Debug.Log(
                $"Membership field check: {memberCount} members in "
                + $"{stopwatch.Elapsed.TotalMilliseconds:0.###} ms, "
                + $"radius {flock.CurrentMembershipRadius:0.##}.");
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    private static FlockController CreateFlock(List<GameObject> objects)
    {
        return CreateObject("TestFlock", Vector2.zero, objects)
            .AddComponent<FlockController>();
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

    private static void SetAgentField<T>(SheepFlockAgent agent, string name, T value)
    {
        FieldInfo field = typeof(SheepFlockAgent).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(agent, value);
    }

    private static void SetMovementReport(
        SheepFlockAgent agent,
        Vector2 direction,
        bool blocked)
    {
        SetAgentField(agent, "hasNormalMovementReport", true);
        SetAgentField(agent, "normalMovementReportDirection", direction.normalized);
        SetAgentField(agent, "wasNormalMovementBlocked", blocked);
    }
}

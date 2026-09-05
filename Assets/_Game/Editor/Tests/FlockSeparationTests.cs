using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class FlockSeparationTests
{
    [Test]
    public void CentralMembersExpandMembershipFieldWithoutMovingSheep()
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

            Assert.That(flock.CurrentMembershipRadius, Is.GreaterThan(8.5f));
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

    [Test]
    public void ThirtyDistantMembersDoNotExpandFieldAndDetachTogether()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            SheepMember center = CreateMember("Center", Vector2.zero, objects);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { center }));

            List<SheepMember> distantMembers = new List<SheepMember>();
            for (int index = 0; index < 30; index++)
            {
                Vector2 position = new Vector2(
                    20f + index % 6 * 0.5f,
                    index / 6 * 0.5f);
                SheepMember member = CreateMember($"Distant_{index:00}", position, objects);
                distantMembers.Add(member);
                Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { member }));
            }

            SetField(flock, "detachDelay", 0f);
            SetField(flock, "detachScatterSpeed", 0f);
            int separatedCount = 0;
            flock.MembersSeparated += count => separatedCount += count;
            evaluate.Invoke(flock, new object[] { 10f });

            Assert.That(flock.CurrentMembershipRadius, Is.EqualTo(5.5f).Within(0.01f));
            Assert.AreEqual(1, flock.MemberCount);
            Assert.AreEqual(30, separatedCount);
            for (int index = 0; index < distantMembers.Count; index++)
            {
                Assert.IsNull(distantMembers[index].Flock);
                Assert.IsTrue(distantMembers[index].GetComponent<ScatteredSheep>().IsScattered);
            }
        }
        finally
        {
            DestroyObjects(objects);
        }
    }

    [Test]
    public void LastMemberCanBeLostOutsideMembershipField()
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

            Assert.AreEqual(0, flock.MemberCount);
            Assert.IsNull(member.Flock);
        }
        finally
        {
            DestroyObjects(objects);
        }
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
    public void MembershipFieldScaleCheckKeepsDenseMembers(int memberCount)
    {
        List<GameObject> objects = new List<GameObject>(memberCount + 1);
        try
        {
            FlockController flock = CreateFlock(objects);
            MethodInfo addMember = GetPrivateMethod("AddMember");
            MethodInfo evaluate = GetPrivateMethod("EvaluateMemberSeparation");

            const int membersPerRow = 25;
            const float spacing = 0.75f;
            int rowCount = Mathf.CeilToInt(memberCount / (float)membersPerRow);
            Vector2 offset = new Vector2(
                (membersPerRow - 1) * spacing * 0.5f,
                (rowCount - 1) * spacing * 0.5f);
            for (int index = 0; index < memberCount; index++)
            {
                Vector2 position = new Vector2(
                    index % membersPerRow * spacing,
                    index / membersPerRow * spacing) - offset;
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
}

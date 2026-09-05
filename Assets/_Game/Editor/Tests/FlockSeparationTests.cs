using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class FlockSeparationTests
{
    [Test]
    public void ConnectivityWalksNeighborChainAndHonorsBlockedLinks()
    {
        Vector2[] positions =
        {
            Vector2.zero,
            new Vector2(2f, 0f),
            new Vector2(4f, 0f),
            new Vector2(10f, 0f),
        };
        HashSet<int> connected = new HashSet<int>();
        Queue<int> traversal = new Queue<int>();

        FlockConnectivity.CollectConnectedIndices(
            positions,
            0,
            2.5f,
            null,
            connected,
            traversal);

        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, connected);

        FlockConnectivity.CollectConnectedIndices(
            positions,
            0,
            2.5f,
            (from, to) => !(from == 0 && to == 1),
            connected,
            traversal);

        CollectionAssert.AreEquivalent(new[] { 0 }, connected);
    }

    [Test]
    public void DistantDisconnectedMemberBecomesWildAndReturnsWithoutNewRecruitCredit()
    {
        List<GameObject> objects = new List<GameObject>();
        try
        {
            GameObject flockObject = CreateObject("TestFlock", Vector2.zero, objects);
            FlockController flock = flockObject.AddComponent<FlockController>();
            MethodInfo addMember = typeof(FlockController).GetMethod(
                "AddMember",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo evaluate = typeof(FlockController).GetMethod(
                "EvaluateMemberSeparation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(addMember);
            Assert.IsNotNull(evaluate);

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
            for (int index = objects.Count - 1; index >= 0; index--)
                Object.DestroyImmediate(objects[index]);
        }
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

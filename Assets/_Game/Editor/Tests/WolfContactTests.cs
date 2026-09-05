using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WolfContactTests
{
    [Test]
    public void LongWolfLengthCoversViewportForRandomDuration()
    {
        var cameraObject = Create("Coverage camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.aspect = 16f / 9f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        var sweep = Create("Long wolf").AddComponent<LongWolfSweep>();
        typeof(LongWolfSweep).GetField("coverageCamera", Private).SetValue(sweep, camera);
        sweep.Prepare();
        float seconds = sweep.CoverageSeconds;
        Assert.That(seconds, Is.InRange(0.8f, 1.2f));
        sweep.ConfigureCharge(new Vector2(-15f, 0f), Vector2.right, 14f);
        float span = 12f * camera.aspect;
        Assert.AreEqual(seconds, sweep.CoverageSeconds, 0.0001f);
        Assert.AreEqual(seconds, (sweep.BodyLength - span) / 14f, 0.0001f);
        float firstLength = sweep.BodyLength;
        sweep.ConfigureCharge(new Vector2(-15f, 0f), Vector2.right, 14f);
        Assert.AreEqual(firstLength, sweep.BodyLength, "Recalculation must not accumulate length.");
        camera.orthographicSize = 12f;
        sweep.ConfigureCharge(new Vector2(-15f, 0f), Vector2.right, 14f);
        Assert.Greater(sweep.BodyLength, firstLength);
        Assert.AreEqual(seconds, (sweep.BodyLength - 24f * camera.aspect) / 14f, 0.0001f);
        Vector2 warning = sweep.GetWarningSpan(Vector2.zero, Vector2.right, 5f);
        Assert.AreEqual(-12f * camera.aspect - 2f, warning.x, 0.0001f);
        Assert.AreEqual(12f * camera.aspect + 2f, warning.y, 0.0001f);
        Vector2 diagonal = new Vector2(1f, 1f).normalized;
        Vector2 diagonalWarning = sweep.GetWarningSpan(Vector2.zero, diagonal, 5f);
        float diagonalHalfSpan = (12f * camera.aspect + 12f) / Mathf.Sqrt(2f);
        Assert.AreEqual(-diagonalHalfSpan - 2f, diagonalWarning.x, 0.0001f);
        Assert.AreEqual(diagonalHalfSpan + 2f, diagonalWarning.y, 0.0001f);
    }

    private readonly List<GameObject> objects = new List<GameObject>();
    private FlockController flock;
    private Wolf wolf;
    private SheepMember captured;
    private int attacks;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void SetUp()
    {
        captured = null;
        attacks = 0;
        var root = Create("Flock control");
        root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        root.AddComponent<CircleCollider2D>().isTrigger = true;
        flock = root.AddComponent<FlockController>();
        wolf = Create("Wolf").AddComponent<Wolf>();
        Call("Awake");
        wolf.Attacked += (_, result) => { attacks++; captured = result.CapturedSheep; };
        wolf.Launch(flock);
        Call("BeginCharge");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
        objects.Clear();
    }

    [Test]
    public void LogicalCenterDoesNotCaptureOrArmDelayedAttack()
    {
        AddSheep(new Vector2(10, 10));
        Contact(flock.GetComponent<Collider2D>());
        Assert.AreEqual(0, attacks);
        Assert.AreEqual(1, flock.MemberCount);
        Call("FixedUpdate");
        Assert.AreEqual(0, attacks);
    }

    [Test]
    public void SeparatedSheepDoesNotTriggerCapture()
    {
        var sheep = AddSheep(new Vector2(5, 0));
        Contact(sheep.GetComponent<Collider2D>());
        Assert.AreEqual(0, attacks, "Stale contact callback must not capture a sheep that moved away.");
        Assert.AreSame(flock, sheep.Flock);
    }

    [Test]
    public void CapturesFirstContactThenScattersSubsequentMembers()
    {
        var nearest = AddSheep(new Vector2(0.05f, 0.1f));
        var touched = AddSheep(new Vector2(0.4f, -0.1f));
        Contact(touched.GetComponent<Collider2D>());
        Assert.AreSame(touched, captured);
        Assert.AreNotSame(nearest, captured);
        Assert.IsNull(touched.Flock);
        Assert.AreSame(wolf.transform, touched.transform.parent);
        Contact(nearest.GetComponent<Collider2D>());
        Assert.AreEqual(2, attacks);
        Assert.IsNull(captured, "The second contact must not capture a second sheep.");
        Assert.IsNull(nearest.Flock);
        Assert.IsTrue(nearest.GetComponent<ScatteredSheep>().IsScattered);
        Assert.IsFalse(nearest.GetComponent<RecruitableSheep>().IsRecruited);
        Assert.AreSame(wolf.transform, touched.transform.parent);
        Contact(nearest.GetComponent<Collider2D>());
        Assert.AreEqual(2, attacks, "Duplicate callbacks for a scattered member do nothing.");
    }

    [Test]
    public void FirstContactAtEdgeStillCaptures()
    {
        flock.transform.position = new Vector2(5f, 0f);
        wolf.Launch(flock);
        Call("BeginCharge");
        var touched = AddSheep(new Vector2(0.4f, 0f));
        Contact(touched.GetComponent<Collider2D>());
        Assert.AreEqual(1, attacks);
        Assert.AreSame(touched, captured);
        Assert.IsTrue(wolf.IsCarryingSheep);
    }

    [Test]
    public void WildSheepDoesNotCountAsFlockContact()
    {
        var wild = Create("Wild sheep").AddComponent<SheepMember>();
        Contact(wild.GetComponent<Collider2D>());
        Assert.AreEqual(0, attacks);
    }

    private GameObject Create(string name)
    {
        var item = new GameObject(name);
        objects.Add(item);
        return item;
    }

    private SheepMember AddSheep(Vector2 position)
    {
        var sheep = Create("Member").AddComponent<SheepMember>();
        sheep.transform.position = position;
        sheep.GetComponent<CircleCollider2D>().radius = 0.25f;
        typeof(SheepMember).GetMethod("Join", Private).Invoke(sheep, new object[] { flock });
        ((List<SheepMember>)typeof(FlockController).GetField("members", Private).GetValue(flock)).Add(sheep);
        return sheep;
    }

    private void Contact(Collider2D collider)
    {
        Physics2D.SyncTransforms();
        typeof(Wolf).GetMethod("OnTriggerEnter2D", Private).Invoke(wolf, new object[] { collider });
    }

    private void Call(string method) => typeof(Wolf).GetMethod(method, Private).Invoke(wolf, null);
}

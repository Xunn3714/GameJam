using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WolfContactTests
{
    [Test]
    public void LongWolfGameplayLengthStaysFixedWhenViewportChanges()
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
        float gameplayLength = sweep.BodyLength;
        sweep.ConfigureCharge(new Vector2(-15f, 0f), Vector2.right, 14f);
        Assert.AreEqual(gameplayLength, sweep.BodyLength, 0.0001f);
        camera.orthographicSize = 12f;
        sweep.ConfigureCharge(new Vector2(-15f, 0f), Vector2.right, 14f);
        Assert.AreEqual(gameplayLength, sweep.BodyLength, 0.0001f,
            "Camera zoom must never change long-wolf capture geometry.");

        // 预警线仍然是纯表现：它可以随镜头延伸到屏幕两端。
        Vector2 warning = sweep.GetWarningSpan(Vector2.zero, Vector2.right, 5f);
        Assert.AreEqual(-12f * camera.aspect - 2f, warning.x, 0.0001f);
        Assert.AreEqual(12f * camera.aspect + 2f, warning.y, 0.0001f);
        Vector2 diagonal = new Vector2(1f, 1f).normalized;
        Vector2 diagonalWarning = sweep.GetWarningSpan(Vector2.zero, diagonal, 5f);
        float diagonalHalfSpan = (12f * camera.aspect + 12f) / Mathf.Sqrt(2f);
        Assert.AreEqual(-diagonalHalfSpan - 2f, diagonalWarning.x, 0.0001f);
        Assert.AreEqual(diagonalHalfSpan + 2f, diagonalWarning.y, 0.0001f);
    }

    [Test]
    public void PentagramVerticesKeepConfiguredWorldRadius()
    {
        Vector2 center = new Vector2(7f, -3f);
        Vector2[] vertices = WolfFormationRunner.CreatePentagramVertices(center, 16f, 23f);

        Assert.AreEqual(5, vertices.Length);
        foreach (Vector2 vertex in vertices)
            Assert.AreEqual(16f, Vector2.Distance(center, vertex), 0.0001f);
    }

    [Test]
    public void LongWolfSweepHitsEveryPartOfItsPhysicalPath()
    {
        Vector2 previousHead = new Vector2(10f, 0f);
        Vector2 nextHead = new Vector2(14f, 0f);

        Assert.IsTrue(LongWolfSweep.TouchesSweep(
            new Vector2(6f, 0f), 0.25f, previousHead, nextHead, Vector2.right, 7f, 1f),
            "The body behind the head must collide.");
        Assert.IsTrue(LongWolfSweep.TouchesSweep(
            new Vector2(12f, 0f), 0.25f, previousHead, nextHead, Vector2.right, 7f, 1f),
            "The distance travelled this frame must collide even after leaving the warning strip.");
        Assert.IsFalse(LongWolfSweep.TouchesSweep(
            new Vector2(12f, 2f), 0.25f, previousHead, nextHead, Vector2.right, 7f, 1f));
    }

    [Test]
    public void HigherViewportProducesALongerFullCrossingRoute()
    {
        Rect nearView = Rect.MinMaxRect(-10f, -6f, 10f, 6f);
        Rect farView = Rect.MinMaxRect(-24f, -14f, 24f, 14f);
        float nearApproach = WolfSpawner.CalculateOffscreenDistance(
            nearView, Vector2.zero, Vector2.right, 16f, 2f);
        float nearDeparture = WolfSpawner.CalculateOffscreenDistance(
            nearView, Vector2.zero, Vector2.left, 16f, 2f);
        float farApproach = WolfSpawner.CalculateOffscreenDistance(
            farView, Vector2.zero, Vector2.right, 16f, 2f);
        float farDeparture = WolfSpawner.CalculateOffscreenDistance(
            farView, Vector2.zero, Vector2.left, 16f, 2f);

        Assert.Greater(farApproach, nearApproach);
        Assert.Greater(farDeparture, nearDeparture);
        Assert.Greater(
            farApproach + farDeparture,
            nearApproach + nearDeparture,
            "A higher view needs a longer physical route so the wolf enters and exits offscreen.");
    }

    [Test]
    public void LogicalRouteStartPreservesTheWarnedLineAfterPrediction()
    {
        Vector2 visualOrigin = new Vector2(-30f, 0f);
        Vector2 aimPoint = Vector2.zero;
        Vector2 predictedDirection = new Vector2(1f, 0.5f).normalized;

        Vector2 start = Wolf.CalculateLogicalRouteStart(
            visualOrigin,
            predictedDirection,
            aimPoint,
            16f);

        Vector2 perpendicular = new Vector2(-predictedDirection.y, predictedDirection.x);
        float originalLineOffset = Vector2.Dot(aimPoint - visualOrigin, perpendicular);
        float logicalLineOffset = Vector2.Dot(aimPoint - start, perpendicular);
        Assert.AreEqual(originalLineOffset, logicalLineOffset, 0.0001f,
            "Shortening the route must not move it back through the flock center.");
        Assert.AreEqual(16f, Vector2.Dot(aimPoint - start, predictedDirection), 0.0001f);
    }

    [Test]
    public void OffscreenDistanceHandlesAPathThatReentersTheViewport()
    {
        Rect viewport = Rect.MinMaxRect(-10f, -6f, 10f, 6f);

        float distance = WolfSpawner.CalculateOffscreenDistance(
            viewport,
            new Vector2(-16f, 0f),
            Vector2.right,
            16f,
            2f);

        Assert.Greater(distance, 28f);
        Vector2 result = new Vector2(-16f, 0f) + Vector2.right * distance;
        Assert.Greater(result.x, 12f);
    }

    [Test]
    public void OffscreenDistanceHandlesAPathThatCrossesTheWholeViewport()
    {
        Rect viewport = Rect.MinMaxRect(-10f, -6f, 10f, 6f);

        float distance = WolfSpawner.CalculateOffscreenDistance(
            viewport,
            new Vector2(-20f, 0f),
            Vector2.right,
            4f,
            2f);

        Assert.Greater(distance, 32f,
            "Starting outside is not enough when the requested outward ray re-enters the view; use the far edge.");
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

    [Test]
    public void LockedWarningAndChargeKeepTheSameRouteWhenFlockMoves()
    {
        Rigidbody2D wolfBody = wolf.GetComponent<Rigidbody2D>();
        wolfBody.position = new Vector2(-30f, 0f);
        wolf.transform.position = wolfBody.position;
        flock.transform.position = Vector2.zero;
        flock.GetComponent<Rigidbody2D>().position = Vector2.zero;
        wolf.SetLogicalLaunchDistance(16f);
        wolf.Launch(flock, null, false);

        Vector2 warnedStart = wolf.ChargeOrigin;
        Vector2 warnedDirection = wolf.ChargeDirection;
        Assert.Greater(
            Vector2.Dot(wolf.ChargeOrigin - wolf.ThreatWorldPosition, wolf.ChargeDirection),
            0f,
            "The edge indicator must stay on the incoming side of the same locked route.");
        flock.transform.position = new Vector2(0f, 8f);
        flock.GetComponent<Rigidbody2D>().position = flock.transform.position;

        Call("BeginCharge");

        Assert.AreEqual(warnedStart.x, wolf.transform.position.x, 0.0001f);
        Assert.AreEqual(warnedStart.y, wolf.transform.position.y, 0.0001f);
        Assert.AreEqual(warnedDirection.x, wolf.ChargeDirection.x, 0.0001f);
        Assert.AreEqual(warnedDirection.y, wolf.ChargeDirection.y, 0.0001f);
    }

    [Test]
    public void ForcedRetreatCannotCaptureOrScatterMoreSheep()
    {
        SheepMember member = AddSheep(new Vector2(0.2f, 0f));

        wolf.ForceRetreat();
        Contact(member.GetComponent<Collider2D>());

        Assert.AreEqual(0, attacks);
        Assert.AreSame(flock, member.Flock);
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

using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C3 — the contact pass (Physics/ContactPass.cs).
//
// A dynamic Rigidbody's solid SphereCollider is resolved against solid colliders:
// push-out, restitution from the combined bounciness (none under the bounce
// threshold), Coulomb friction from the combined dynamic friction, and a
// mass-weighted impulse between two dynamic spheres. OnCollisionEnter/Stay/Exit fire
// after the solve, so a callback reads the RESOLVED velocity (PhysX order). Pairs
// the layer matrix, IgnoreCollision or excludeLayers rule out never touch.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Records every collision message, with the receiver's body velocity at callback time.</summary>
class CollisionRecorder : MonoBehaviour
{
    public readonly List<(string evt, Collision c, Vector3 velocityAtCallback)> Events = new();
    Vector3 V => TryGetComponent<Rigidbody>(out var rb) ? rb.velocity : Vector3.zero;
    void OnCollisionEnter(Collision c) => Events.Add(("enter", c, V));
    void OnCollisionStay(Collision c) => Events.Add(("stay", c, V));
    void OnCollisionExit(Collision c) => Events.Add(("exit", c, V));
}

public class ContactPassTests
{
    const float Dt = 0.02f;
    static readonly Vector3 Up45 = Quaternion.Euler(0f, 0f, 45f) * Vector3.up;   // the rotated box's top-face normal
    static readonly Vector3 Along45 = Quaternion.Euler(0f, 0f, 45f) * Vector3.right;

    /// <summary>A dynamic ball (radius 0.5, no gravity) with the Astro League material by default.</summary>
    static (Rigidbody rb, SphereCollider col, CollisionRecorder rec) Ball(string name, Vector3 at, Vector3 velocity,
        float bounciness = 1f, PhysicsMaterialCombine bounce = PhysicsMaterialCombine.Maximum, float friction = 0f, float mass = 1f)
    {
        var go = new GameObject(name);
        go.transform.position = at;
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.mass = mass;
        rb.angularDamping = 0f;
        rb.velocity = velocity;
        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.5f;
        col.material = new PhysicsMaterial(name)
        {
            bounciness = bounciness, bounceCombine = bounce,
            dynamicFriction = friction, staticFriction = friction,
            frictionCombine = PhysicsMaterialCombine.Minimum,
        };
        return (rb, col, go.AddComponent<CollisionRecorder>());
    }

    /// <summary>A static 10 x 1 x 1 box rotated 45° about Z at the origin.</summary>
    static (BoxCollider box, CollisionRecorder rec) Wall()
    {
        var go = new GameObject("wall");
        go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(10f, 1f, 1f);
        return (box, go.AddComponent<CollisionRecorder>());
    }

    static void AssertNear(Vector3 expected, Vector3 actual, float tol = 1e-3f)
        => Assert.True((expected - actual).magnitude <= tol, $"expected {expected}, got {actual}");

    [Fact]
    public void Sphere_BouncesOffARotatedBox_AlongTheFaceNormal_ElasticAtBounciness1()
    {
        using var loop = new GameLoop();
        var (_, wallRec) = Wall();
        // 1.05 above the face along its normal (0.5 half-thickness + 0.5 radius + 0.05), falling at 5 m/s.
        var (rb, ballCol, rec) = Ball("ball", Up45 * 1.05f, -Up45 * 5f);

        loop.Tick(Dt);

        AssertNear(Up45 * 5f, rb.velocity);
        Assert.Equal(new[] { "enter", "stay" }, rec.Events.ConvertAll(e => e.evt));
        var enter = rec.Events[0];
        AssertNear(Up45 * 5f, enter.velocityAtCallback); // the solver ran before the callback
        Assert.Same(wallRec.GetComponent<BoxCollider>(), enter.c.collider);
        Assert.Equal(1, enter.c.contactCount);
        AssertNear(Up45, enter.c.contacts[0].normal);            // points from the wall to the ball
        AssertNear(Up45 * 0.5f, enter.c.contacts[0].point);      // on the wall's face
        AssertNear(-Up45 * 5f - Vector3.zero, -enter.c.relativeVelocity); // other - this, pre-solve

        // The wall hears it too, with the normal pointing toward the wall.
        Assert.Equal("enter", wallRec.Events[0].evt);
        Assert.Same(ballCol, wallRec.Events[0].c.collider);
        AssertNear(-Up45, wallRec.Events[0].c.contacts[0].normal);

        // The ball leaves: Exit on the next step, on both sides.
        loop.Tick(Dt);
        Assert.Equal("exit", rec.Events[^1].evt);
        Assert.Equal("exit", wallRec.Events[^1].evt);
    }

    [Fact]
    public void Restitution_UsesTheCombinedBounciness_AndKeepsTangentialSpeedAtZeroFriction()
    {
        using var loop = new GameLoop();
        Wall();
        var v = -Up45 * 5f + Along45 * 3f;
        var (rb, _, _) = Ball("ball", Up45 * 1.05f, v, bounciness: 0.72f);

        loop.Tick(Dt);

        Assert.Equal(0.72f * 5f, Vector3.Dot(rb.velocity, Up45), 3);
        Assert.Equal(3f, Vector3.Dot(rb.velocity, Along45), 3);
    }

    [Fact]
    public void BelowTheBounceThreshold_TheNormalVelocityIsCancelled()
    {
        using var loop = new GameLoop();
        Wall();
        // 1.5 m/s < the 2 m/s default threshold. 1.02 above so one step (0.03) reaches the face.
        var (rb, _, _) = Ball("ball", Up45 * 1.02f, -Up45 * 1.5f);

        loop.Tick(Dt);

        Assert.Equal(0f, Vector3.Dot(rb.velocity, Up45), 3);
    }

    [Fact]
    public void Friction_TakesTheCombinedDynamicFriction_BoundedByTheNormalImpulse()
    {
        using var loop = new GameLoop();
        Wall(); // default material: friction 0.6, Average
        // Ball friction 0.4 combined with Minimum (ranks above Average) → 0.4.
        var (rb, _, _) = Ball("ball", Up45 * 1.05f, -Up45 * 5f + Along45 * 10f, bounciness: 0f, bounce: PhysicsMaterialCombine.Average, friction: 0.4f);

        loop.Tick(Dt);

        // e = (0 + 0) / 2 = 0: normal impulse 5, friction removes min(10, 0.4 * 5) = 2 of the slip.
        Assert.Equal(0f, Vector3.Dot(rb.velocity, Up45), 3);
        Assert.Equal(8f, Vector3.Dot(rb.velocity, Along45), 3);
    }

    [Fact]
    public void Sphere_BouncesOffAKinematicCapsuleHull()
    {
        using var loop = new GameLoop();
        var vessel = new GameObject("vessel");
        var vrb = vessel.AddComponent<Rigidbody>();
        vrb.isKinematic = true;
        var hull = new GameObject("hull");
        hull.transform.SetParent(vessel.transform, false);
        var cap = hull.AddComponent<CapsuleCollider>();
        cap.radius = 0.5f; cap.height = 4f; cap.direction = 1;
        var vesselRec = vessel.AddComponent<CollisionRecorder>();

        var (rb, _, rec) = Ball("ball", new Vector3(1.05f, 1f, 0f), new Vector3(-5f, 0f, 0f));
        loop.Tick(Dt);

        AssertNear(new Vector3(5f, 0f, 0f), rb.velocity);
        Assert.Same(cap, rec.Events[0].c.collider);
        Assert.Same(vrb, rec.Events[0].c.rigidbody);
        AssertNear(Vector3.right, rec.Events[0].c.contacts[0].normal);
        // The hull is a child of the kinematic body: the body's GameObject hears it (original routing).
        Assert.Equal("enter", vesselRec.Events[0].evt);
        Assert.Equal(0f, vrb.velocity.magnitude); // kinematic: infinite mass, untouched
    }

    [Fact]
    public void TwoDynamicSpheres_ExchangeMomentum_HeadOn_ElasticEqualMass()
    {
        using var loop = new GameLoop();
        var (a, _, recA) = Ball("a", new Vector3(-0.55f, 0f, 0f), new Vector3(4f, 0f, 0f));
        var (b, _, recB) = Ball("b", new Vector3(0.55f, 0f, 0f), new Vector3(-2f, 0f, 0f));
        Vector3 p0 = a.velocity * a.mass + b.velocity * b.mass;
        float e0 = 0.5f * (a.velocity.sqrMagnitude + b.velocity.sqrMagnitude);

        loop.Tick(Dt);

        AssertNear(new Vector3(-2f, 0f, 0f), a.velocity);
        AssertNear(new Vector3(4f, 0f, 0f), b.velocity);
        AssertNear(p0, a.velocity * a.mass + b.velocity * b.mass);
        Assert.Equal(e0, 0.5f * (a.velocity.sqrMagnitude + b.velocity.sqrMagnitude), 3);
        Assert.Equal("enter", recA.Events[0].evt);
        Assert.Equal("enter", recB.Events[0].evt);
        AssertNear(Vector3.left, recA.Events[0].c.contacts[0].normal);
        AssertNear(Vector3.right, recB.Events[0].c.contacts[0].normal);
    }

    [Fact]
    public void UnequalMasses_ConserveMomentum()
    {
        using var loop = new GameLoop();
        var (a, _, _) = Ball("a", new Vector3(-0.55f, 0f, 0f), new Vector3(6f, 0f, 0f), mass: 3f);
        var (b, _, _) = Ball("b", new Vector3(0.55f, 0f, 0f), Vector3.zero, mass: 1f);
        Vector3 p0 = a.velocity * a.mass + b.velocity * b.mass;

        loop.Tick(Dt);

        AssertNear(p0, a.velocity * a.mass + b.velocity * b.mass);
        AssertNear(new Vector3(3f, 0f, 0f), a.velocity); // (m1 - m2)/(m1 + m2) * 6
        AssertNear(new Vector3(9f, 0f, 0f), b.velocity); // 2 m1/(m1 + m2) * 6
    }

    [Fact]
    public void ExcludeLayers_LetsTheBallPassThrough()
    {
        using var loop = new GameLoop();
        var (wall, wallRec) = Wall();
        wall.gameObject.layer = 8; // TrailBlocks-like
        var (rb, col, rec) = Ball("ball", Up45 * 1.05f, -Up45 * 5f);
        col.excludeLayers = 1 << 8;

        loop.Tick(Dt);

        AssertNear(-Up45 * 5f, rb.velocity);
        Assert.Empty(rec.Events);
        Assert.Empty(wallRec.Events);
    }

    [Fact]
    public void LayerMatrix_And_IgnoreCollision_SkipThePair_IncludeLayersOverridesTheMatrix()
    {
        using var loop = new GameLoop();
        var (wall, _) = Wall();
        wall.gameObject.layer = 9;
        var (rb, col, rec) = Ball("ball", Up45 * 1.05f, -Up45 * 5f);
        col.gameObject.layer = 10;

        Physics.IgnoreLayerCollision(9, 10);
        Assert.True(Physics.GetIgnoreLayerCollision(10, 9));
        loop.Tick(Dt);
        Assert.Empty(rec.Events);

        // includeLayers wins over the matrix.
        rb.transform.position = Up45 * 1.05f;
        rb.velocity = -Up45 * 5f;
        col.includeLayers = 1 << 9;
        loop.Tick(Dt);
        Assert.Equal("enter", rec.Events[0].evt);

        // IgnoreCollision beats everything.
        loop.Tick(Dt); loop.Tick(Dt);
        rec.Events.Clear();
        Physics.IgnoreCollision(col, wall);
        rb.transform.position = Up45 * 1.05f;
        rb.velocity = -Up45 * 5f;
        loop.Tick(Dt);
        Assert.Empty(rec.Events);
    }

    [Fact]
    public void LayerCollisionMatrix_ParsesTheProjectSettingsHex()
    {
        using var loop = new GameLoop();
        // Row 0 = 0xFFFFFFFE (little-endian "feffffff"): layer 0 ignores layer 0 only.
        Physics.SetLayerCollisionMatrix("feffffff" + new string('f', 31 * 8));
        Assert.True(Physics.GetIgnoreLayerCollision(0, 0));
        Assert.False(Physics.GetIgnoreLayerCollision(0, 1));
        Assert.False(Physics.GetIgnoreLayerCollision(5, 7));
    }

    [Fact]
    public void KinematicPairs_AndTriggers_GetNoContact()
    {
        using var loop = new GameLoop();
        var (a, _, recA) = Ball("a", Vector3.zero, Vector3.zero);
        var (b, colB, recB) = Ball("b", new Vector3(0.5f, 0f, 0f), Vector3.zero);
        a.isKinematic = true;
        b.isKinematic = true;
        loop.Tick(Dt);
        Assert.Empty(recA.Events);
        Assert.Empty(recB.Events);

        // A dynamic ball against a TRIGGER is a trigger message, never a contact.
        b.isKinematic = false;
        colB.isTrigger = true;
        b.velocity = new Vector3(1f, 0f, 0f);
        loop.Tick(Dt);
        Assert.Empty(recB.Events);
        AssertNear(new Vector3(1f, 0f, 0f), b.velocity);
    }

    [Fact]
    public void DynamicBox_IsNotSolved_NoGeneralSolver()
    {
        using var loop = new GameLoop();
        Wall();
        var go = new GameObject("crate");
        go.transform.position = Up45 * 1f;
        var rb = go.AddComponent<Rigidbody>();
        rb.velocity = -Up45 * 5f;
        go.AddComponent<BoxCollider>();
        var rec = go.AddComponent<CollisionRecorder>();

        loop.Tick(Dt);

        Assert.Empty(rec.Events);
        AssertNear(-Up45 * 5f, rb.velocity);
    }

    [Fact]
    public void TriggerMessages_ReachTheAttachedRigidbodysGameObject()
    {
        using var loop = new GameLoop();
        var carrier = new GameObject("carrier");
        carrier.AddComponent<Rigidbody>().isKinematic = true;
        var carrierRec = carrier.AddComponent<TriggerRecorder>();
        var hull = new GameObject("hull");
        hull.transform.SetParent(carrier.transform, false);
        hull.AddComponent<BoxCollider>();
        var hullRec = hull.AddComponent<TriggerRecorder>();

        var (prism, prismCol, _) = ContactRig.MakeProbe("prism", new Vector3(0.6f, 0f, 0f), radius: 0.5f, isTrigger: true);
        loop.Tick(Dt);

        Assert.Single(hullRec.Events);
        Assert.Single(carrierRec.Events); // MantaBomb relies on this
        Assert.Same(prismCol, carrierRec.Events[0].other);
        Assert.Same(carrier.GetComponent<Rigidbody>(), hull.GetComponent<BoxCollider>().attachedRigidbody);
    }

    [Fact]
    public void AstroLeagueShapedBall_StrikesARotatedVesselHull_AndHearsOnCollisionEnter()
    {
        using var loop = new GameLoop();
        // The ball as AstroLeagueBall.Awake builds it: scale 14, r 0.5 (world 7), mass 3,
        // bounciness 1 Maximum, friction 0 Minimum, TrailBlocks excluded.
        var (rb, col, rec) = Ball("ball", Vector3.zero, Vector3.zero, mass: 3f);
        rb.transform.localScale = Vector3.one * 14f;
        col.excludeLayers = 1 << 8;

        // A Dolphin-like kinematic vessel root with a rotated solid wing box, plus a trail prism on layer 8.
        var vessel = new GameObject("vessel");
        vessel.AddComponent<Rigidbody>().isKinematic = true;
        vessel.transform.position = new Vector3(0f, -9f, 0f);
        vessel.transform.rotation = Quaternion.Euler(0f, 30f, 20f);
        var wing = new GameObject("wing");
        wing.transform.SetParent(vessel.transform, false);
        var wingBox = wing.AddComponent<BoxCollider>();
        wingBox.size = new Vector3(6f, 0.5f, 2f);
        var prism = new GameObject("prism") { layer = 8 };
        prism.transform.position = new Vector3(0f, -6.5f, 0f);
        prism.AddComponent<BoxCollider>();

        rb.transform.position = new Vector3(0f, 2f, 0f);
        rb.velocity = new Vector3(0f, -100f, 0f);
        for (int i = 0; i < 10 && rec.Events.Count == 0; i++) loop.Tick(Dt);

        Assert.NotEmpty(rec.Events);
        var enter = rec.Events[0];
        Assert.Equal("enter", enter.evt);
        Assert.Same(wingBox, enter.c.collider); // never the prism: excluded layer
        Assert.True(Vector3.Dot(enter.velocityAtCallback, enter.c.contacts[0].normal) > 0f,
            "OnCollisionEnter sees the ball already leaving the hull, as after PhysX's solve");
        Assert.Equal(100f, enter.velocityAtCallback.magnitude, 2); // bounciness 1, zero friction
    }
}

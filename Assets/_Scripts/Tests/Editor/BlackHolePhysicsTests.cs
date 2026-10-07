#if UNITY_EDITOR
using NUnit.Framework;
using Unity.Mathematics;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The black hole's equations, run as the game runs them (Docs/BLACK_HOLE.md §2):
    /// <see cref="BlackHolePhysics"/> is the one statement of the physics the gravity job, the
    /// vessel pull and this file share, so these tests exercise the code, not a copy of it.
    /// Each test is a claim the design makes about what a hole DOES — captured, orbits, escapes,
    /// swirls, settles — stated as the integrator's own output.
    /// </summary>
    public class BlackHolePhysicsTests
    {
        const float GM = 200000f;   // strength 10 at the shipped config
        const float RS = 20f;

        static BlackHolePhysics.Well Well(float3 position, float gm = GM, float rs = RS, float dragging = 0f,
            float3 velocity = default, float influence = 2000f)
        {
            return new BlackHolePhysics.Well
            {
                Position = position,
                Velocity = velocity,
                GM = gm,
                Horizon = BlackHolePhysics.Horizon.Of(rs),
                InfluenceRadius = influence,
                SpinAxis = new float3(0f, 0f, 1f),
                FrameDragging = dragging,
            };
        }

        static BlackHolePhysics.NativeWells Wells(params BlackHolePhysics.Well[] wells)
        {
            var w = new BlackHolePhysics.NativeWells();
            foreach (var well in wells) w.Add(well);
            return w;
        }

        static BlackHolePhysics.StepParams Params(float coupling = 0f, float damping = 0f, float releaseSpeed = 0.5f) =>
            new()
            {
                FrameDragCoupling = coupling,
                ReleaseDamping = damping,
                ReleaseSpeed = releaseSpeed,
                MaxSubsteps = 8,
            };

        [Test]
        public void Acceleration_PointsAtTheHoleAndFallsOffAsThePseudoNewtonianLaw()
        {
            var well = Well(float3.zero);
            var p = new float3(100f, 0f, 0f);
            var a = BlackHolePhysics.Acceleration(p, well);
            Assert.Less(a.x, 0f, "acceleration must point toward the hole");
            Assert.AreEqual(0f, a.y, 1e-6f);
            Assert.AreEqual(0f, a.z, 1e-6f);
            // a = GM / (d - r_s)^2 with d - r_s = 80.
            Assert.AreEqual(GM / (80f * 80f), math.length(a), 1e-3f);

            // Twice the gap, a quarter of the pull (plus the horizon offset, which is why the
            // law is checked against (d - r_s), not d).
            var a2 = BlackHolePhysics.Acceleration(new float3(180f, 0f, 0f), well);
            Assert.AreEqual(math.length(a) / 4f, math.length(a2), 1e-3f);

            // At the centre there is no direction to pull along: zero, not NaN.
            var a0 = BlackHolePhysics.Acceleration(float3.zero, well);
            Assert.IsFalse(math.any(math.isnan(a0)));
            Assert.AreEqual(0f, math.length(a0));
        }

        [Test]
        public void PoleGuard_BoundsTheAccelerationJustOutsideTheHorizon()
        {
            var well = Well(float3.zero);
            var a = BlackHolePhysics.Acceleration(new float3(RS + 0.001f, 0f, 0f), well);
            float gap = RS * BlackHolePhysics.PoleGuardFraction;
            Assert.LessOrEqual(math.length(a), GM / (gap * gap) + 1f,
                "the pole must be floored — a body about to be captured is integrated, not flung to infinity");
            Assert.IsFalse(math.any(math.isnan(a)));
        }

        [Test]
        public void EscapeAndCircularSpeeds_AreTheTextbookNumbersForThePseudoPotential()
        {
            var h = BlackHolePhysics.Horizon.Of(RS);
            // v_esc = sqrt(2GM / (d - r_s)); v_circ = sqrt(GM d) / (d - r_s).
            Assert.AreEqual(math.sqrt(2f * GM / 80f), BlackHolePhysics.EscapeSpeed(100f, GM, h), 1e-3f);
            Assert.AreEqual(math.sqrt(GM * 100f) / 80f, BlackHolePhysics.CircularSpeed(100f, GM, h), 1e-3f);
            Assert.AreEqual(3f * RS, BlackHolePhysics.IscoRadius(h));
            // The design claim in the config tooltip: a cruising Squirrel (54 u/s) is caught at
            // 100 u by a strength-10 hole, a boosting vessel (> 70 u/s) gets away.
            Assert.Greater(BlackHolePhysics.EscapeSpeed(100f, GM, h), 54f);
            Assert.Less(BlackHolePhysics.EscapeSpeed(100f, GM, h), 75f);
        }

        [Test]
        public void Step_ABodyAtRestFallsStraightInAndIsCaptured()
        {
            var wells = Wells(Well(float3.zero));
            var prm = Params();
            float3 p = new float3(150f, 0f, 0f), v = float3.zero;
            var verdict = BlackHolePhysics.Verdict.Free;
            int steps = 0;
            while (verdict == BlackHolePhysics.Verdict.Free && steps++ < 100000)
            {
                verdict = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out _);
                Assert.AreEqual(0f, p.y, 1e-4f, "a radial fall must stay radial");
                Assert.AreEqual(0f, p.z, 1e-4f);
            }
            Assert.AreEqual(BlackHolePhysics.Verdict.Captured, verdict, "a body at rest must fall in and be captured");
            Assert.Less(steps, 60 * 60, "free fall from 150 u took over a minute — the pull is far weaker than the design says");
        }

        [Test]
        public void Step_CapturedBodyNamesTheWellThatTookIt()
        {
            var wells = Wells(Well(new float3(1000f, 0f, 0f)), Well(float3.zero));
            var prm = Params();
            float3 p = new float3(RS - 1f, 0f, 0f), v = float3.zero;
            var verdict = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out int by);
            Assert.AreEqual(BlackHolePhysics.Verdict.Captured, verdict);
            Assert.AreEqual(1, by, "the SECOND well is the one whose horizon the body is inside");
        }

        [Test]
        public void Step_ACircularOrbitOutsideTheIscoStaysBound()
        {
            // Stable circular orbit at 8 r_s: speed from the pseudo-potential, no dragging, no damping.
            var h = BlackHolePhysics.Horizon.Of(RS);
            float d = 8f * RS;
            float vc = BlackHolePhysics.CircularSpeed(d, GM, h);
            var wells = Wells(Well(float3.zero));
            var prm = Params();
            float3 p = new float3(d, 0f, 0f), v = new float3(0f, vc, 0f);

            float period = 2f * math.PI * d / vc;
            float dt = 1f / 120f;
            int steps = (int)(3f * period / dt);
            float minR = float.MaxValue, maxR = 0f;
            for (int i = 0; i < steps; i++)
            {
                var verdict = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, dt, out _);
                Assert.AreEqual(BlackHolePhysics.Verdict.Free, verdict, $"orbit captured or released at step {i}");
                float r = math.length(p);
                minR = math.min(minR, r);
                maxR = math.max(maxR, r);
            }
            // Semi-implicit Euler is symplectic: the radius wobbles but does not drift. Three
            // periods must stay within a few percent of the launch radius.
            Assert.Greater(minR, d * 0.93f, $"orbit decayed to {minR:F1} u from {d:F1} u");
            Assert.Less(maxR, d * 1.07f, $"orbit grew to {maxR:F1} u from {d:F1} u");
        }

        [Test]
        public void Step_ABodyAboveEscapeSpeedGetsAwayBent()
        {
            var h = BlackHolePhysics.Horizon.Of(RS);
            float d = 100f;
            float vEsc = BlackHolePhysics.EscapeSpeed(d, GM, h);
            var wells = Wells(Well(float3.zero));
            var prm = Params();
            // Flying PAST the hole (tangentially) at twice the escape speed: the effective-potential
            // barrier at this angular momentum turns it at ~88 u, a gentle (~15 degree) bend.
            float3 p = new float3(d, -2000f, 0f), v = new float3(0f, 2f * vEsc, 0f);
            float3 v0 = v;
            float minR = float.MaxValue;
            for (int i = 0; i < 20000; i++)
            {
                var verdict = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out _);
                Assert.AreNotEqual(BlackHolePhysics.Verdict.Captured, verdict, "a body above escape speed must not be captured");
                minR = math.min(minR, math.length(p));
                if (p.y > 2000f) break;
            }
            Assert.Greater(p.y, 2000f, "the body never cleared the hole");
            Assert.Less(minR, d * 1.01f, "the pass did not actually go near the hole");
            // Bent: it leaves on a different heading than it arrived on.
            float cosDeflection = math.dot(math.normalize(v), math.normalize(v0));
            Assert.Less(cosDeflection, 0.999f, "the trajectory was not bent at all");
            Assert.Greater(cosDeflection, 0f, "the trajectory was reversed — that is a capture that did not count");
        }

        [Test]
        public void Step_FrameDraggingSweepsABodyAtRestIntoARotation()
        {
            // Dragging at the full circular rate with a strong coupling: a body at rest acquires
            // tangential velocity about the spin axis (+Z), i.e. it starts to ORBIT instead of
            // falling on a radial line.
            var wells = Wells(Well(float3.zero, dragging: 1f));
            var prm = Params(coupling: 2f);
            float3 p = new float3(200f, 0f, 0f), v = float3.zero;
            for (int i = 0; i < 60; i++)
                BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out _);
            // cross(+Z, +X) = +Y: the dragged frame moves +Y at +X.
            Assert.Greater(v.y, 1f, "frame dragging did not give the body tangential velocity");
            Assert.Greater(math.abs(v.y), math.abs(v.x) * 0.5f, "the swirl is too weak next to the radial fall");
            Assert.AreEqual(0f, v.z, 1e-4f, "nothing moves along the spin axis");
        }

        [Test]
        public void FrameVelocity_IsZeroWithNoDraggingAndCarriesTheHolesOwnMotion()
        {
            var moving = Well(float3.zero, velocity: new float3(5f, 0f, 0f));
            var vf = BlackHolePhysics.FrameVelocity(new float3(100f, 0f, 0f), moving);
            Assert.AreEqual(5f, vf.x, 1e-5f, "with no dragging the frame moves with the hole and nothing else");
            Assert.AreEqual(0f, vf.y, 1e-5f);
            // On the spin axis itself there is no tangent: only the hole's motion.
            var spinning = Well(float3.zero, dragging: 1f, velocity: new float3(5f, 0f, 0f));
            var onAxis = BlackHolePhysics.FrameVelocity(new float3(0f, 0f, 100f), spinning);
            Assert.AreEqual(5f, onAxis.x, 1e-5f);
            Assert.AreEqual(0f, onAxis.y, 1e-5f);
            Assert.AreEqual(0f, onAxis.z, 1e-5f);
        }

        [Test]
        public void Step_ABodyOutsideEveryInfluenceSphereDampsAndIsReleased()
        {
            var wells = Wells(Well(float3.zero, influence: 50f));
            var prm = Params(damping: 4f, releaseSpeed: 0.5f);
            float3 p = new float3(500f, 0f, 0f), v = new float3(0f, 30f, 0f);
            var verdict = BlackHolePhysics.Verdict.Free;
            int steps = 0;
            while (verdict == BlackHolePhysics.Verdict.Free && steps++ < 10000)
                verdict = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out _);
            Assert.AreEqual(BlackHolePhysics.Verdict.Released, verdict, "a body coasting outside every influence sphere must settle and be released");
            Assert.Less(steps, 600, "release took over ten seconds at damping 4/s");
        }

        [Test]
        public void Step_WithNoWellsEveryBodyCoastsToRelease()
        {
            var wells = new BlackHolePhysics.NativeWells();
            var prm = Params(damping: 4f, releaseSpeed: 0.5f);
            float3 p = float3.zero, v = new float3(10f, 0f, 0f);
            var verdict = BlackHolePhysics.Verdict.Free;
            int steps = 0;
            while (verdict == BlackHolePhysics.Verdict.Free && steps++ < 10000)
                verdict = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out _);
            Assert.AreEqual(BlackHolePhysics.Verdict.Released, verdict);
            Assert.Greater(p.x, 0f, "the body should have drifted before settling");
        }

        [Test]
        public void NativeWells_HoldsAtMostCapacityAndIndexes()
        {
            var w = new BlackHolePhysics.NativeWells();
            for (int i = 0; i < BlackHolePhysics.NativeWells.Capacity + 2; i++)
                w.Add(Well(new float3(i, 0f, 0f)));
            Assert.AreEqual(BlackHolePhysics.NativeWells.Capacity, w.Count, "a fifth well must be refused, not written over the fourth");
            for (int i = 0; i < w.Count; i++)
                Assert.AreEqual(i, w[i].Position.x, 1e-6f);
        }
    }
}
#endif

#if UNITY_EDITOR
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The black hole's equations, run as the game runs them (Docs/BLACK_HOLE.md §2):
    /// <see cref="BlackHolePhysics"/> is the one statement of the physics the gravity job, the
    /// vessel pull and this file share, so these tests exercise the code, not a copy of it.
    /// Each test is a claim the design makes about what a hole DOES — captured, orbits, escapes,
    /// winds an infall the way it spins, settles — stated as the integrator's own output.
    /// </summary>
    public class BlackHolePhysicsTests
    {
        const float GM = 200000f;   // strength 10 at the shipped config
        const float RS = 20f;

        static BlackHolePhysics.Well Well(float3 position, float gm = GM, float rs = RS, float spin = 0f,
            float influence = 2000f)
        {
            return new BlackHolePhysics.Well
            {
                Position = position,
                GM = gm,
                Horizon = BlackHolePhysics.Horizon.Of(rs),
                InfluenceRadius = influence,
                SpinAxis = new float3(0f, 0f, 1f),
                FrameDrag = BlackHolePhysics.FrameDragCoefficient(gm, rs, spin),
            };
        }

        static BlackHolePhysics.NativeWells Wells(params BlackHolePhysics.Well[] wells)
        {
            var w = new BlackHolePhysics.NativeWells();
            foreach (var well in wells) w.Add(well);
            return w;
        }

        static BlackHolePhysics.StepParams Params(float damping = 0f, float releaseSpeed = 0.5f) =>
            new()
            {
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
            // Stable circular orbit at 8 r_s: speed from the pseudo-potential, no spin, no damping.
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
        public void FrameAngularVelocity_IsLenseThirring()
        {
            // c is the speed whose escape radius is the horizon (r_s = 2GM/c²) ...
            float c = BlackHolePhysics.LightSpeed(GM, RS);
            Assert.AreEqual(math.sqrt(2f * GM / RS), c, 1e-3f);
            Assert.AreEqual(RS, 2f * GM / (c * c), 1e-3f, "the horizon is not 2GM/c² for the derived light speed");

            // ... and the frame turns at ω = 2GJ/(c²r³) = a*·c·r_s²/(2r³): a*·c/(2r_s) at the horizon,
            var w = Well(float3.zero, spin: 0.9f);
            float atHorizon = BlackHolePhysics.FrameAngularVelocity(RS, w);
            Assert.AreEqual(0.9f * c / (2f * RS), atHorizon, atHorizon * 1e-4f);
            // falling as 1/r³ (twice as far turns 8× slower),
            Assert.AreEqual(atHorizon / 8f, BlackHolePhysics.FrameAngularVelocity(2f * RS, w), atHorizon * 1e-5f);
            Assert.AreEqual(atHorizon / 125f, BlackHolePhysics.FrameAngularVelocity(5f * RS, w), atHorizon * 1e-5f);
            // and held at the horizon's value inside it (a body there is captured, not swirled to infinity).
            Assert.AreEqual(atHorizon, BlackHolePhysics.FrameAngularVelocity(0.25f * RS, w), atHorizon * 1e-5f);

            // A non-rotating hole drags nothing; a* is capped at Thorne's 0.998.
            Assert.AreEqual(0f, BlackHolePhysics.FrameDragCoefficient(GM, RS, 0f));
            Assert.AreEqual(0f, BlackHolePhysics.FrameAngularVelocity(RS, Well(float3.zero)));
            Assert.AreEqual(BlackHolePhysics.FrameDragCoefficient(GM, RS, 0.998f),
                BlackHolePhysics.FrameDragCoefficient(GM, RS, 5f), 1e-3f);
        }

        [Test]
        public void FrameVelocity_TurnsAboutTheSpinAxisAndNothingElse()
        {
            var spinning = Well(float3.zero, spin: 0.9f);
            // On the equator: a pure rotation about +Z at ω(r), cross(+Z, +X) = +Y.
            float3 p = new float3(100f, 0f, 0f);
            var vf = BlackHolePhysics.FrameVelocity(p, spinning);
            float expect = BlackHolePhysics.FrameAngularVelocity(100f, spinning) * 100f;
            Assert.AreEqual(0f, vf.x, 1e-6f);
            Assert.AreEqual(expect, vf.y, expect * 1e-4f);
            Assert.AreEqual(0f, vf.z, 1e-6f);
            // On the spin axis there is no tangent, and a hole with no spin turns nothing.
            Assert.AreEqual(0f, math.length(BlackHolePhysics.FrameVelocity(new float3(0f, 0f, 100f), spinning)), 1e-6f);
            Assert.AreEqual(0f, math.length(BlackHolePhysics.FrameVelocity(p, Well(float3.zero))), 1e-6f);
        }

        [Test]
        public void Step_ASpinningHoleTwistsAnInfallButStillSwallowsIt()
        {
            // A body released at rest at 3 r_s. Around a non-rotating hole it falls on a straight
            // radial line; around a spinning one the dragged frame winds it the way the hole turns
            // (+Y at +X for spin about +Z) — and it is captured either way: frame dragging bends
            // the fall, it does not hold a body up.
            float FallAngle(float spin, out bool captured)
            {
                var wells = Wells(Well(float3.zero, spin: spin));
                var prm = Params();
                float3 p = new float3(3f * RS, 0f, 0f), v = float3.zero, lastFree = p;
                captured = false;
                for (int i = 0; i < 600; i++)
                {
                    var verdict = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out _);
                    if (verdict == BlackHolePhysics.Verdict.Captured) { captured = true; break; }
                    lastFree = p;
                    Assert.AreEqual(0f, lastFree.z, 1e-4f, "nothing moves along the spin axis");
                }
                return math.atan2(lastFree.y, lastFree.x);
            }

            float still = FallAngle(0f, out bool capturedStill);
            float spun = FallAngle(0.9f, out bool capturedSpun);
            Assert.IsTrue(capturedStill, "a body at rest beside a non-rotating hole was not swallowed");
            Assert.IsTrue(capturedSpun, "frame dragging held a body up instead of bending its fall");
            Assert.AreEqual(0f, still, 1e-6f, "a non-rotating hole bent a radial fall");
            Assert.Greater(spun, 0.05f, "the spinning hole did not wind the infall in its own sense of rotation");
        }

        static BlackHolePhysics.Well White(float3 position, float gm = GM, float rs = RS, float spin = 0f, float influence = 2000f)
        {
            var w = Well(position, gm, rs, spin, influence);
            w.Polarity = -1f;
            return w;
        }

        [Test]
        public void WhiteHole_RepelsWithTheBlackHolesMagnitudeAndTurnsItsFrameTheOtherWay()
        {
            var p = new float3(100f, 0f, 0f);
            var black = Well(float3.zero, spin: 0.9f);
            var white = White(float3.zero, spin: 0.9f);
            var aB = BlackHolePhysics.Acceleration(p, black);
            var aW = BlackHolePhysics.Acceleration(p, white);
            Assert.Less(aB.x, 0f, "the black hole pulls");
            Assert.Greater(aW.x, 0f, "the white hole pushes");
            Assert.AreEqual(math.length(aB), math.length(aW), 1e-3f, "the same magnitude, reversed");
            var fB = BlackHolePhysics.FrameVelocity(p, black);
            var fW = BlackHolePhysics.FrameVelocity(p, white);
            Assert.AreEqual(-fB.y, fW.y, 1e-4f, "a white hole's frame turns the other way (angular momentum flips under time reversal)");
            // An unset polarity is a black hole: every well authored before white holes existed still pulls.
            var unset = Well(float3.zero);
            unset.Polarity = 0f;
            Assert.AreEqual(1f, BlackHolePhysics.PolaritySign(unset));
        }

        [Test]
        public void WhiteHole_NeverCapturesAndCarriesABodyAtItsHorizonOut()
        {
            // A body born just outside a white horizon — the emitted end of a tunnel — is pushed out
            // past the influence sphere and released; nothing is ever captured by a white hole.
            var wells = Wells(White(float3.zero, influence: 120f));
            var prm = Params(damping: 0f, releaseSpeed: 0.01f);
            float3 p = new float3(RS * 1.05f, 0f, 0f), v = float3.zero;
            var verdict = BlackHolePhysics.Verdict.Free;
            float farthest = 0f;
            for (int i = 0; i < 2000 && verdict == BlackHolePhysics.Verdict.Free; i++)
            {
                verdict = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out _);
                Assert.AreNotEqual(BlackHolePhysics.Verdict.Captured, verdict, "a white hole captured a body");
                farthest = math.max(farthest, math.length(p));
            }
            Assert.Greater(farthest, 120f, "the white hole did not push the body past its influence sphere");
            Assert.Greater(v.x, 0f, "the body is not moving away");
            // Even a body INSIDE the white horizon is not captured: it is being emitted.
            float3 pin = new float3(0.5f * RS, 0f, 0f), vin = float3.zero;
            Assert.AreNotEqual(BlackHolePhysics.Verdict.Captured, BlackHolePhysics.Step(ref pin, ref vin, in wells, in prm, 1f / 60f, out _));
            Assert.Greater(pin.x, 0.5f * RS, "a body inside the white horizon was not pushed outward");
        }

        [Test]
        public void Pair_DriftsApartStopsAtHalfLifeAndMeetsAgainAtTheEnd()
        {
            const float s0 = 80f, drift = 20f, life = 5f;
            Assert.AreEqual(s0, BlackHolePairMath.HalfGap(s0, drift, life, 0f), 1e-5f, "born at the birth gap");
            Assert.AreEqual(s0 + 0.25f * drift * life, BlackHolePairMath.HalfGap(s0, drift, life, 0.5f * life), 1e-4f, "widest at half the lifetime");
            Assert.AreEqual(BlackHolePairMath.MaxHalfGap(s0, drift, life), BlackHolePairMath.HalfGap(s0, drift, life, 0.5f * life), 1e-4f);
            Assert.AreEqual(s0, BlackHolePairMath.HalfGap(s0, drift, life, life), 1e-4f, "back at the birth gap at the end: they meet and annihilate");
            // Monotone out, then monotone back.
            float prev = s0;
            for (float t = 0.1f; t <= 0.5f * life; t += 0.1f) { float g = BlackHolePairMath.HalfGap(s0, drift, life, t); Assert.GreaterOrEqual(g, prev - 1e-5f); prev = g; }
            for (float t = 0.5f * life + 0.1f; t <= life; t += 0.1f) { float g = BlackHolePairMath.HalfGap(s0, drift, life, t); Assert.LessOrEqual(g, prev + 1e-5f); prev = g; }
            Assert.IsFalse(BlackHolePairMath.IsSpent(life, life - 0.01f));
            Assert.IsTrue(BlackHolePairMath.IsSpent(life, life));
            BlackHolePairMath.Positions(new Vector3(10f, 0f, 0f), Vector3.right * 3f, 50f, out var black, out var white);
            Assert.AreEqual(new Vector3(-40f, 0f, 0f), black, "the black hole sits −axis from the midpoint");
            Assert.AreEqual(new Vector3(60f, 0f, 0f), white, "the white hole sits +axis from the midpoint");
        }

        [Test]
        public void Pair_ABodyThatFellInComesOutTheWhiteHoleThePointReflectedWayOutward()
        {
            // Entry at the black horizon at P, moving with v (v·P < 0: inward). Exit at −P from the
            // white centre with the same v — which is outward there — at least 1.05 r_s out.
            var blackC = new Vector3(0f, 0f, 0f);
            var whiteC = new Vector3(500f, 0f, 0f);
            var entry = blackC + new Vector3(0f, RS, 0f);                 // came in from above
            var v = new Vector3(3f, -40f, 0f);                            // moving down into it
            var exit = BlackHolePairMath.ExitPosition(entry, blackC, whiteC, RS, Vector3.right);
            var rel = exit - whiteC;
            Assert.AreEqual(0f, rel.x, 1e-4f);
            Assert.Less(rel.y, 0f, "the point reflection: in from above, out below");
            Assert.AreEqual(BlackHolePairMath.EmitRadiusFraction * RS, rel.magnitude, 1e-3f, "born just outside the white horizon");
            Assert.Greater(Vector3.Dot(v, rel.normalized), 0f, "the entry velocity points OUT of the white hole at the exit");
            // A capture dead on the centre has no direction: the fallback axis is used, at the emit radius.
            var centreExit = BlackHolePairMath.ExitPosition(blackC, blackC, whiteC, RS, Vector3.right);
            Assert.AreEqual(whiteC + Vector3.right * (BlackHolePairMath.EmitRadiusFraction * RS), centreExit);
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

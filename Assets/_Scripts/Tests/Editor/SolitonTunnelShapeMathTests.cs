using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Meerkat's wormhole arithmetic (<see cref="SolitonTunnelShapeMath"/>) held to the Vessel Studio page it was ported
    /// from (<c>Docs/Studios/VesselStudio/meerkat.html</c>). Every golden number here came from running the page's OWN
    /// <c>shapeFor</c> / <c>bendAngle</c> / <c>whAt</c> / <c>whFrame</c> / <c>whRadius</c> / <c>stepTunnel</c> / <c>aiGatePlan</c> in node on
    /// the same inputs, at the page's shipped numbers (2026-10-27). A page change that moves one of these is a port to
    /// redo, not a test to loosen.
    /// </summary>
    [TestFixture]
    public class SolitonTunnelShapeMathTests
    {
        const float Tol = 1e-3f;

        static readonly SolitonTunnelShapeMath.ShapeSettings Shipped = new()
        {
            ShaftMin = 60f, ShaftMax = 600f, BendRadius = 60f, BendMaxDegrees = 150f, ThroatRadius = 4f, MouthRadius = 10f,
            OvalShortest = 0.2f, OvalLongest = 1.1f, OvalMaxSharpness = 8f,
        };

        // round 7 (the umbrella handle): lq, sx, sy -> L, S, R, b, M, theta, fat, dx, dy
        [TestCase(0f, 0f, 0f, 60f, 60f, 60f, 4f, 10f, 0f, 0f, 0f, 0f)]
        [TestCase(0f, 0.3f, 0f, 101.336745f, 60f, 60f, 4f, 10f, 0.688946f, 0.263158f, 1f, 0f)]
        [TestCase(0.4f, 0.7f, -0.2f, 388.107071f, 276f, 60f, 4f, 10f, 1.868451f, 0.713696f, 0.961524f, -0.274721f)]
        [TestCase(1f, 0f, 0f, 600f, 600f, 60f, 4f, 10f, 0f, 0f, 0f, 0f)]
        [TestCase(1f, 1f, 1f, 757.079633f, 600f, 60f, 4f, 10f, 2.617994f, 1f, 0.707107f, 0.707107f)]
        [TestCase(0f, 0.04f, 0f, 60f, 60f, 60f, 4f, 10f, 0f, 0f, 1f, 0f)]
        public void ShapeFor_MatchesThePage(float lq, float sx, float sy, float L, float shaft, float bendRadius, float b,
            float m, float theta, float fat, float dx, float dy)
        {
            var s = SolitonTunnelShapeMath.ShapeFor(Shipped, lq, sx, sy);
            Assert.AreEqual(L, s.L, Tol, "L");
            Assert.AreEqual(shaft, s.S, Tol, "shaft");
            Assert.AreEqual(bendRadius, s.R, Tol, "bend radius");
            Assert.AreEqual(b, s.B, Tol, "throat");
            Assert.AreEqual(m, s.M, Tol, "mouth");
            Assert.AreEqual(theta, s.Theta, 1e-5f, "theta");
            Assert.AreEqual(fat, s.Fat, 1e-5f, "fat");
            Assert.AreEqual(dx, s.Dx, 1e-5f, "dx");
            Assert.AreEqual(dy, s.Dy, 1e-5f, "dy");
        }

        // round 22: one conic arc round its middle. s -> point, tangent, normal, turned (A (1,2,3), t0 +z, side +x, L 196,
        // theta 1.2, its eccentricity 0.37559 from the shipped oval rows): half the turn is done at the middle (s 98)
        [TestCase(-10f, 1f, 2f, -7f, 0f, 0f, 1f, 1f, 0f, 0f, 0f)]
        [TestCase(0f, 1f, 2f, 3f, 0f, 0f, 1f, 1f, 0f, 0f, 0f)]
        [TestCase(50f, 8.400751f, 2f, 52.259136f, 0.296288f, 0f, 0.955099f, 0.955099f, 0f, -0.296288f, 0.300804f)]
        [TestCase(98f, 29.198617f, 2f, 95.320873f, 0.564642f, 0f, 0.825336f, 0.825336f, 0f, -0.564642f, 0.6f)]
        [TestCase(140f, 57.170751f, 2f, 126.489599f, 0.759258f, 0f, 0.650789f, 0.650789f, 0f, -0.759258f, 0.862173f)]
        [TestCase(195f, 104.095679f, 2f, 154.690932f, 0.929899f, 0f, 0.367815f, 0.367815f, 0f, -0.929899f, 1.194139f)]
        [TestCase(196f, 105.027291f, 2f, 155.05627f, 0.932039f, 0f, 0.362358f, 0.362358f, 0f, -0.932039f, 1.2f)]
        public void AtAndFrame_MatchThePage(float s, float px, float py, float pz, float tx, float ty, float tz,
            float nx, float ny, float nz, float turned)
        {
            var path = new SolitonTunnelShapeMath.Conic(196f, 1.2f, SolitonTunnelShapeMath.OvalOf(Shipped, 196f, 1.2f));
            var p = SolitonTunnelShapeMath.At(new Vector3(1f, 2f, 3f), Vector3.forward, Vector3.right, path, s, out var t);
            SolitonTunnelShapeMath.Frame(Vector3.forward, Vector3.right, path, s, out var n, out var bn);
            Assert.AreEqual(px, p.x, Tol); Assert.AreEqual(py, p.y, Tol); Assert.AreEqual(pz, p.z, Tol);
            Assert.AreEqual(tx, t.x, 1e-4f); Assert.AreEqual(ty, t.y, 1e-4f); Assert.AreEqual(tz, t.z, 1e-4f);
            Assert.AreEqual(nx, n.x, 1e-4f); Assert.AreEqual(ny, n.y, 1e-4f); Assert.AreEqual(nz, n.z, 1e-4f);
            // the page's binormal is (0, 1, 0) at every s: t0 x side
            Assert.AreEqual(0f, bn.x, 1e-6f); Assert.AreEqual(1f, bn.y, 1e-6f); Assert.AreEqual(0f, bn.z, 1e-6f);
            if (s >= 0f) Assert.AreEqual(turned, SolitonTunnelShapeMath.BendAngle(path, s), 1e-4f, "bendAngle");
        }

        // round 22: L, theta -> the eccentricity (a short tunnel nearly a circle; a long sharp turn held to an oval; a long
        // gentle one a hyperbola)
        [TestCase(60f, 0f, 0.2f)]
        [TestCase(217.08f, 2.618f, 0.402806f)]
        [TestCase(400f, 1.2f, 0.638974f)]
        [TestCase(757.08f, 2.618f, 0.896575f)]
        [TestCase(757.08f, 1f, 1.1f)]
        [TestCase(300f, 0.5f, 0.509864f)]
        public void OvalOf_MatchesThePage(float L, float theta, float e) =>
            Assert.AreEqual(e, SolitonTunnelShapeMath.OvalOf(Shipped, L, theta), 1e-4f);

        [TestCase(0f, 300f, 0f, 26f)]
        [TestCase(0f, 300f, 150f, 10f)]
        [TestCase(0f, 300f, 60f, 17.531686f)]
        [TestCase(100f, 200f, 150f, 3.333333f)]
        [TestCase(100f, 200f, 110f, 7.216032f)]
        public void Radius_MatchesThePage(float s0, float s1, float s, float expected) =>
            Assert.AreEqual(expected, SolitonTunnelShapeMath.Radius(10f, 26f, 300f, s0, s1, s), Tol);

        /// <summary>
        /// One tunnel's life at dt 0.0625 s (exact in binary, so the step a phase changes on is the page's): it forms over
        /// 0.5 s, stays open while ridden, the rider comes out on step 20, it stays 10 s, collapses at 375 u/s and is gone on
        /// step 192 at its centre.
        /// </summary>
        [Test]
        public void Step_TheTunnelsLifeMatchesThePage()
        {
            const float L = 300f, dt = 0.0625f;
            var w = SolitonTunnelShapeMath.NewWindow(L);
            for (int k = 1; k <= 400; k++)
            {
                if (k == 20) SolitonTunnelShapeMath.RiderOut(ref w);
                bool gone = SolitonTunnelShapeMath.Step(ref w, L, 0.5f, 10f, 0.8f, dt);
                switch (k)
                {
                    case 1: Expect(w, SolitonTunnelShapeMath.Phase.Form, 100.488281f, 199.511719f); break;
                    case 4: Expect(w, SolitonTunnelShapeMath.Phase.Form, 18.75f, 281.25f); break;
                    case 8: case 9: case 19: Expect(w, SolitonTunnelShapeMath.Phase.Open, 0f, 300f); break;
                    case 20: Expect(w, SolitonTunnelShapeMath.Phase.Stay, 0f, 300f); break;
                    case 179: Expect(w, SolitonTunnelShapeMath.Phase.Collapse, 0f, 300f); break;
                    case 180: Expect(w, SolitonTunnelShapeMath.Phase.Collapse, 11.71875f, 288.28125f); break;
                    case 181: Expect(w, SolitonTunnelShapeMath.Phase.Collapse, 23.4375f, 276.5625f); break;
                    case 185: Expect(w, SolitonTunnelShapeMath.Phase.Collapse, 70.3125f, 229.6875f); break;
                }
                if (k < 192) Assert.IsFalse(gone, $"gone early, on step {k}");
                if (k != 192) continue;
                Assert.IsTrue(gone, "the page fades it on step 192");
                Expect(w, SolitonTunnelShapeMath.Phase.Gone, 150f, 150f);
                return;
            }
            Assert.Fail("never collapsed");
        }

        static void Expect(SolitonTunnelShapeMath.Window w, SolitonTunnelShapeMath.Phase phase, float s0, float s1)
        {
            Assert.AreEqual(phase, w.Phase);
            Assert.AreEqual(s0, w.S0, Tol);
            Assert.AreEqual(s1, w.S1, Tol);
        }

        [Test]
        public void CollapseNow_EndsAFormedTunnelButNotACollapsingOne()
        {
            var w = SolitonTunnelShapeMath.NewWindow(300f);
            SolitonTunnelShapeMath.CollapseNow(ref w);
            Assert.AreEqual(SolitonTunnelShapeMath.Phase.Collapse, w.Phase);
            w.Phase = SolitonTunnelShapeMath.Phase.Gone;
            SolitonTunnelShapeMath.CollapseNow(ref w);
            Assert.AreEqual(SolitonTunnelShapeMath.Phase.Gone, w.Phase);
        }

        static SolitonTunnelShapeMath.PlanSettings PlanShipped => new()
        {
            ReticleAhead = 30f, ShapeSeconds = 0.3f, Shape = Shipped,
            // round 24: the plan's ride speed is the slide's (the tunnel config's MaxSpeed); these goldens were made with the round-6 ride's
            // mean multiplier at 60 u/s (1.1 + 2/pi x 1.4), which the page's slideMul gives at this top speed
            SlideSpeed = 60f * (1.1f + 2f / Mathf.PI * 1.4f), TurnRateRadians = 120f * Mathf.Deg2Rad,
        };

        // the target in the hull's frame (Unity: z forward), the judge noise and its roll, turn-only -> LT, stick
        // (round 7: the plan that reaches the ring soonest, at pitchYaw 120 and the ride's mean speed; round 22: out of the
        // oval arc's way out)
        [TestCase(0f, 0f, 900f, 0f, 0.5f, false, 1f, 0f)]
        [TestCase(300f, 0f, 400f, 0f, 0.5f, false, 0.5f, 0.4775f)]
        [TestCase(0f, -200f, 250f, 0f, 0.5f, true, 0.125f, 0.4775f)]
        [TestCase(500f, 100f, 100f, 0f, 0.5f, false, 0.5f, 0.8575f)]
        [TestCase(-120f, 40f, 300f, 0.4f, 0.9f, false, 0.125f, 0.3335f)]
        public void TryPlan_MatchesThePage(float x, float y, float z, float noise, float rand, bool turnOnly, float lq, float m)
        {
            Assert.IsTrue(SolitonTunnelShapeMath.TryPlan(PlanShipped, new Vector3(x, y, z), 60f, noise, rand, turnOnly, out float gotLq, out float gotM));
            Assert.AreEqual(lq, gotLq, 1e-5f, "LT");
            Assert.AreEqual(m, gotM, 1e-4f, "stick");
        }

        [Test]
        public void TryPlan_NothingFitsATargetUnderTheNose() =>
            Assert.IsFalse(SolitonTunnelShapeMath.TryPlan(PlanShipped, new Vector3(0f, 0f, 40f), 60f, 0f, 0.5f, false, out _, out _));

        /// <summary>The page's round-3 easing: 1 - exp(-dt / tau). In the page, with the stick moved to 0.202, the reticle read
        /// 0.04 after 0.05 s, 0.119 after 0.15 s and 0.194 after 0.5 s at 60 fps (reticleSmoothSeconds 0.15) - 2, 8 and 29 easing
        /// steps, since the reticle reads the stick a frame late (the page's r.sx, the executor's _stick).</summary>
        [Test]
        public void ReticleEase_FollowsThePage()
        {
            float x = 0f;
            const float target = 0.202f;
            for (int k = 1; k <= 30; k++)
            {
                x += (target - x) * SolitonTunnelShapeMath.ReticleEase(1f / 60f, 0.15f);
                if (k == 2) Assert.AreEqual(0.040f, x, 1.5e-3f);
                if (k == 8) Assert.AreEqual(0.119f, x, 1.5e-3f);
                if (k == 29) Assert.AreEqual(0.194f, x, 1.5e-3f);
            }
            Assert.AreEqual(1f, SolitonTunnelShapeMath.ReticleEase(1f / 60f, 0f), "0 s: it follows at once");
            Assert.IsTrue(SolitonTunnelShapeMath.ReticleSettled(new Vector2(0.5f, 0f), new Vector2(0.49f, 0.01f), 0.25f, 0.24f));
            Assert.IsFalse(SolitonTunnelShapeMath.ReticleSettled(new Vector2(0.5f, 0f), new Vector2(0.45f, 0f), 0.25f, 0.25f));
        }

        [Test]
        public void Ease_AndItsInverse_RoundTrip()
        {
            foreach (float y in new[] { -0.9f, -0.3f, 0f, 0.25f, 0.8f })
                Assert.AreEqual(y, SolitonTunnelShapeMath.Ease(SolitonTunnelShapeMath.EaseInverse(y)), 1e-3f);
        }

        // ---- round 24: a single tunnel is slid as a network is (a network of one) ----

        /// <summary>The executor's SlidePath, over the same arithmetic: the conic arc, straight on past either end; the
        /// hyperboloid wall.</summary>
        sealed class TestTube : SolitonTunnelMath.ITubePath
        {
            public readonly SolitonTunnelShapeMath.Conic Path;
            public readonly float L, B, M;

            public TestTube(float L, float theta, float b, float m)
            {
                this.L = L; B = b; M = m;
                Path = new SolitonTunnelShapeMath.Conic(L, theta, SolitonTunnelShapeMath.OvalOf(Shipped, L, theta));
            }

            static SolitonTunnelMath.D3 D(Vector3 v) => new(v.x, v.y, v.z);
            static Vector3 V(SolitonTunnelMath.D3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

            public SolitonTunnelMath.D3 At(double s, out SolitonTunnelMath.D3 tangent)
            {
                float f = (float)s;
                Vector3 p, tan;
                if (f <= 0f) { tan = Vector3.forward; p = Vector3.forward * f; }
                else if (f >= L) { p = SolitonTunnelShapeMath.At(Vector3.zero, Vector3.forward, Vector3.right, Path, L, out tan); p += tan * (f - L); }
                else p = SolitonTunnelShapeMath.At(Vector3.zero, Vector3.forward, Vector3.right, Path, f, out tan);
                tangent = D(tan);
                return D(p);
            }

            public double Nearest(SolitonTunnelMath.D3 p) =>
                SolitonTunnelShapeMath.NearestAlong(Vector3.zero, Vector3.forward, Vector3.right, Path, L, V(p));

            public double Radius(double s) => SolitonTunnelShapeMath.Radius(B, M, L, 0f, L, Mathf.Clamp((float)s, 0f, L));
        }

        [Test]
        public void NearestAlong_FindsTheArcsOwnPoints_AndRunsOnPastItsEnds()
        {
            var tube = new TestTube(196f, 1.2f, 4f, 13f);
            foreach (float s in new[] { 10f, 60f, 98f, 150f, 190f })
            {
                var p = SolitonTunnelShapeMath.At(Vector3.zero, Vector3.forward, Vector3.right, tube.Path, s, out _);
                Assert.AreEqual(s, SolitonTunnelShapeMath.NearestAlong(Vector3.zero, Vector3.forward, Vector3.right, tube.Path, 196f, p), 0.05f);
            }
            Assert.AreEqual(-20f, SolitonTunnelShapeMath.NearestAlong(Vector3.zero, Vector3.forward, Vector3.right, tube.Path, 196f,
                new Vector3(0f, 0f, -20f)), 1e-3f, "before the way in");
            var e = SolitonTunnelShapeMath.At(Vector3.zero, Vector3.forward, Vector3.right, tube.Path, 196f, out var te);
            Assert.AreEqual(196f + 500f, SolitonTunnelShapeMath.NearestAlong(Vector3.zero, Vector3.forward, Vector3.right, tube.Path, 196f,
                e + te * 500f), 0.05f, "far past the way out (the page's bug: L x 48 / 48 need not be L)");
        }

        [Test]
        public void Slide_RunsTheTunnelInside_AndSlingshotsOutAlongItsVelocity()
        {
            // the slip and slide over a network of one: coasting (no RT) from 40 u before the way in at 60 u/s
            var tube = new TestTube(196f, 1.2f, 4f, 13f);
            var net = SolitonTunnelMath.Single(tube, 196f);
            var g = SolitonTunnelMath.Enter(net, 0, new SolitonTunnelMath.D3(0, 0, -40), 60, new SolitonTunnelMath.D3(0, 1, 0));
            var p = new SolitonTunnelMath.FlightSettings { Gravity = 60, Bounce = 0.35, Friction = 0.05, Thrust = 12, MinSpeed = 25, MaxSpeed = 120, Tip = 0.6, ExitMultiplier = 1.2 };
            SolitonTunnelMath.FlightResult res = SolitonTunnelMath.FlightResult.Riding;
            SolitonTunnelMath.D3 nose = default;
            int steps = 0;
            for (; steps < 60 * 30 && res == SolitonTunnelMath.FlightResult.Riding; steps++)
            {
                res = SolitonTunnelMath.Step(g, net, null, in p, 0, 1.0 / 60, out nose);
                if (g.Free && g.S > 0 && g.S < 196)
                {
                    var c = tube.At(System.Math.Min(196, System.Math.Max(0, g.S)), out _);
                    Assert.LessOrEqual(c.DistanceTo(g.Pos), tube.Radius(g.S) + 1e-3, "inside its wall");
                }
            }
            Assert.AreEqual(SolitonTunnelMath.FlightResult.OutOfTip, res, "out of the far mouth");
            // the slingshot: out along its own velocity (no nose lock to the tube's end), roughly the way the tube was going
            var v = g.Vel.Normalized;
            Assert.AreEqual(1f, (float)v.Dot(nose), 1e-6f, "out along its velocity");
            SolitonTunnelShapeMath.At(Vector3.zero, Vector3.forward, Vector3.right, tube.Path, 196f, out var last);
            Assert.Less(Vector3.Angle(new Vector3((float)nose.X, (float)nose.Y, (float)nose.Z), last), 60f, "roughly out the far mouth's way");
            // RT held all the way (the small push): out sooner
            var g2 = SolitonTunnelMath.Enter(net, 0, new SolitonTunnelMath.D3(0, 0, -40), 60, new SolitonTunnelMath.D3(0, 1, 0));
            int steps2 = 0;
            for (res = SolitonTunnelMath.FlightResult.Riding; steps2 < 60 * 30 && res == SolitonTunnelMath.FlightResult.Riding; steps2++)
                res = SolitonTunnelMath.Step(g2, net, null, in p, 1, 1.0 / 60, out _);
            Assert.Less(steps2, steps);
        }

        [Test]
        public void Ripple_IsTheEdgesOnly_AndFades()
        {
            float a = SolitonTunnelShapeMath.RippleAmplitude(0.3f, 0.2f, 100f);
            Assert.AreEqual(0.3f * 0.012f * 0.64f, a, 1e-6f);
            Assert.AreEqual(0f, SolitonTunnelShapeMath.RippleAmplitude(0.3f, 1f, 100f));
            Assert.AreEqual(a * 0.5f, SolitonTunnelShapeMath.RippleAmplitude(0.3f, 0.2f, 600f), 1e-6f, "half at 600 u");
            // the centre never moves: inside rippleEdge the mask is 0
            float front = SolitonTunnelShapeMath.RippleFront(0.45f, 0f);
            Assert.AreEqual(0.45f, front, 1e-6f);
            var centre = SolitonTunnelShapeMath.RippleDisplacement(new Vector2(0.1f, 0.05f), 1.6f, 0.01f, 0.5f, 0.45f);
            Assert.AreEqual(Vector2.zero, centre);
        }

        // The lens shader measures the wall in the tunnel's own frame: leaving the origin along +z, bending toward +x in the
        // x-z plane (round 22: the conic arc, its points (across, ahead) - where MWTube looks)
        [TestCase(0f, 600f, 60f)]
        [TestCase(1.2f, 100f, 80f)]
        [TestCase(2.617994f, 60f, 60f)]
        public void TubeWorldToLocal_PutsTheTubeWhereTheShaderLooks(float theta, float S, float R)
        {
            var a = new Vector3(12f, -40f, 7f);
            var t0 = new Vector3(0.3f, 0.2f, 0.9f).normalized;
            var side = Vector3.Cross(t0, new Vector3(0.1f, 1f, -0.2f)).normalized;
            var m = SolitonTunnelShapeMath.TubeWorldToLocal(a, t0, side);
            float L = S + R * theta;
            var path = new SolitonTunnelShapeMath.Conic(L, theta, SolitonTunnelShapeMath.OvalOf(Shipped, L, theta));
            foreach (float s in new[] { 0f, 0.5f * S, S, S + 0.37f * (L - S), L })
            {
                var local = m.MultiplyPoint3x4(SolitonTunnelShapeMath.At(a, t0, side, path, s, out var tangent));
                var localT = m.MultiplyVector(tangent);
                SolitonTunnelShapeMath.Frame(t0, side, path, s, out var normal, out var binormal);
                path.At(s, out float ahead, out float across, out float ang);
                var want = new Vector3(across, 0f, ahead);
                Assert.AreEqual(0f, Vector3.Distance(want, local), 1e-3f, $"point at s {s}");
                Assert.AreEqual(0f, Vector3.Distance(new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)), localT), 1e-4f, "tangent");
                Assert.AreEqual(0f, Vector3.Distance(new Vector3(Mathf.Cos(ang), 0f, -Mathf.Sin(ang)), m.MultiplyVector(normal)), 1e-4f, "normal");
                Assert.AreEqual(0f, Vector3.Distance(Vector3.up, m.MultiplyVector(binormal)), 1e-4f, "binormal");
            }
        }

        // ---- round 23: Garrett's surface area, the soliton's pull and its carry (goldens: the page's kitThroat and pullVel
        // in node, at the shipped numbers - throat 8, mouth 26, area 6,000 at Space L0 to 18,000 at L10)

        [TestCase(5f, 60f, 16.54691f, 53.77745f)]
        [TestCase(5f, 200f, 4.96407f, 16.13324f)]
        [TestCase(5f, 757f, 1.31151f, 4.26241f)]
        [TestCase(0f, 400f, 1.24102f, 4.03331f)]
        [TestCase(10f, 120f, 12.41018f, 40.33309f)]
        public void SurfaceAreaThroat_MatchesThePage(float spaceLevel, float L, float b, float m)
        {
            SolitonTunnelShapeMath.SurfaceAreaThroat(4f, 13f, SolitonTunnelShapeMath.ByLevel(6000f, 18000f, spaceLevel), L,
                out float gotB, out float gotM);
            Assert.AreEqual(b, gotB, Tol, "throat radius");
            Assert.AreEqual(m, gotM, Tol, "mouth radius");
        }

        [Test]
        public void SurfaceArea_ALongerTunnelIsThinner_AndShapeForUsesIt()
        {
            var p = Shipped;
            p.Area = 12000f;
            var shortOne = SolitonTunnelShapeMath.ShapeFor(p, 0f, 0f, 0f);
            var longOne = SolitonTunnelShapeMath.ShapeFor(p, 1f, 1f, 0f);
            Assert.Greater(longOne.L, shortOne.L);
            Assert.Less(longOne.B, shortOne.B, "at one area a longer tunnel is thinner (Garrett)");
            Assert.AreEqual(longOne.M / longOne.B, shortOne.M / shortOne.B, 1e-4f, "the mouth stays a fixed multiple of the throat");
            // no area: the authored fixed size, as before round 23
            var fixedOne = SolitonTunnelShapeMath.ShapeFor(Shipped, 1f, 1f, 0f);
            Assert.AreEqual(4f, fixedOne.B, Tol);
        }

        // the page's axes are three.js's (z back): these are its cases with z mirrored, and its answers with z mirrored
        [TestCase(0f, 0f, 0f, 0f, 0f, 1f, 30f, 10f, -5f, -82.11663f, -17.06474f, 13.17073f)]
        [TestCase(5f, -3f, -2f, 0.6f, 0f, 0.8f, 5f, 80f, -2f, -7.92f, -32.53012f, 5.94f)]
        [TestCase(0f, 0f, 0f, 1f, 0f, 0f, 2f, 1f, 0f, -134.16408f, -67.08204f, 0.55f)]
        public void PullVelocity_MatchesThePage(float ax, float ay, float az, float tx, float ty, float tz, float px, float py,
            float pz, float vx, float vy, float vz)
        {
            var v = SolitonTunnelShapeMath.PullVelocity(new Vector3(ax, ay, az), new Vector3(tx, ty, tz).normalized,
                new Vector3(px, py, pz), 30f, 90f, 150f);
            Assert.AreEqual(vx, v.x, Tol);
            Assert.AreEqual(vy, v.y, Tol);
            Assert.AreEqual(vz, v.z, Tol);
        }

        [Test]
        public void CarryDistortion_StretchesMostAtTheMiddle_AndKeepsTheVolume()
        {
            SolitonTunnelShapeMath.CarryDistortion(0f, 300f, 2f, 0.5f, 0, out float a0, out float c0, out float r0);
            SolitonTunnelShapeMath.CarryDistortion(150f, 300f, 2f, 0.5f, 0, out float a1, out float c1, out float r1);
            SolitonTunnelShapeMath.CarryDistortion(300f, 300f, 2f, 0.5f, 0, out float a2, out _, out float r2);
            Assert.AreEqual(1f, a0, Tol, "none at the way in");
            Assert.AreEqual(3f, a1, Tol, "1 + carryStretch at the middle");
            Assert.AreEqual(1f, a2, Tol, "none at the way out");
            Assert.AreEqual(1f, a1 * c1 * c1, Tol, "squeezed across to keep its volume");
            Assert.AreEqual(0f, r0, Tol);
            Assert.AreEqual(Mathf.PI, r2, Tol, "half a turn mouth to mouth at carryTwist 0.5");
        }

        [Test]
        public void TubeAdvance_MovesAtTheSlidesTopSpeed_AndNeverPassesTheRiderAhead()
        {
            // round 25: first in, first out - a carried prism at the slide's top speed (90 u/s), held 6 u behind a rider ahead
            Assert.AreEqual(11.5f, SolitonTunnelShapeMath.TubeAdvance(10f, 90f, 1f / 60f, false, 0f), 1e-4f, "no rider: 90 u/s");
            Assert.AreEqual(51.5f, SolitonTunnelShapeMath.TubeAdvance(50f, 90f, 1f / 60f, true, 20f), 1e-4f, "a rider behind it: on at 90 u/s");
            Assert.AreEqual(44f, SolitonTunnelShapeMath.TubeAdvance(43f, 90f, 1f / 60f, true, 50f), 1e-4f, "a rider ahead: held 6 u behind it");
            Assert.AreEqual(47f, SolitonTunnelShapeMath.TubeAdvance(47f, 90f, 1f / 60f, true, 50f), 1e-4f, "already nearer: it waits, never goes back");
            // a slow rider and a stream behind it: nobody passes anybody
            float rider = 0f, a = -10f, b = -20f;
            for (int k = 0; k < 600; k++)
            {
                rider += 45f / 60f;
                a = SolitonTunnelShapeMath.TubeAdvance(a, 90f, 1f / 60f, true, rider);
                b = SolitonTunnelShapeMath.TubeAdvance(b, 90f, 1f / 60f, true, rider);
                Assert.LessOrEqual(a, rider - SolitonTunnelShapeMath.TubeGap + 1e-3f);
                Assert.LessOrEqual(b, rider - SolitonTunnelShapeMath.TubeGap + 1e-3f);
            }
        }
    }
}

using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Meerkat's wormhole arithmetic (<see cref="MeerkatWormholeMath"/>) held to the Vessel Studio page it was ported
    /// from (<c>Docs/Studios/VesselStudio/meerkat.html</c>). Every golden number here came from running the page's OWN
    /// <c>shapeFor</c> / <c>whAt</c> / <c>whFrame</c> / <c>whRadius</c> / <c>stepTunnel</c> / <c>aiGatePlan</c> in node on
    /// the same inputs, at the page's shipped numbers (2026-10-27). A page change that moves one of these is a port to
    /// redo, not a test to loosen.
    /// </summary>
    [TestFixture]
    public class MeerkatWormholeMathTests
    {
        const float Tol = 1e-3f;

        static readonly MeerkatWormholeMath.ShapeSettings Shipped = new()
        {
            LengthStraight = 600f, LengthBent = 120f, BendMaxDegrees = 120f,
            ThroatStraight = 3f, ThroatBent = 40f, MouthFlare = 2f,
        };

        // lq, sx, sy -> L, b, M, theta, fat, dx, dy
        [TestCase(0f, 0f, 0f, 600f, 3f, 12f, 0f, 0f, 0f, 0f)]
        [TestCase(0f, 0.3f, 0f, 473.684211f, 12.736842f, 31.473684f, 0.551157f, 0.263158f, 1f, 0f)]
        [TestCase(0f, 0.7f, -0.2f, 257.426027f, 29.406744f, 64.813488f, 1.494761f, 0.713696f, 0.961524f, -0.274721f)]
        [TestCase(0.5f, 0f, 0f, 360f, 3f, 12f, 0f, 0f, 0f, 0f)]
        [TestCase(1f, 1f, 1f, 120f, 34.377468f, 54.430991f, 2.094395f, 1f, 0.707107f, 0.707107f)]
        [TestCase(0f, 0.04f, 0f, 600f, 3f, 12f, 0f, 0f, 1f, 0f)]
        public void ShapeFor_MatchesThePage(float lq, float sx, float sy, float L, float b, float m, float theta, float fat,
            float dx, float dy)
        {
            var s = MeerkatWormholeMath.ShapeFor(Shipped, lq, sx, sy);
            Assert.AreEqual(L, s.L, Tol, "L");
            Assert.AreEqual(b, s.B, Tol, "throat");
            Assert.AreEqual(m, s.M, Tol, "mouth");
            Assert.AreEqual(theta, s.Theta, 1e-5f, "theta");
            Assert.AreEqual(fat, s.Fat, 1e-5f, "fat");
            Assert.AreEqual(dx, s.Dx, 1e-5f, "dx");
            Assert.AreEqual(dy, s.Dy, 1e-5f, "dy");
        }

        // s -> point, tangent, normal, binormal (A (1,2,3), t0 +z, side +x, L 300, theta 1.2)
        [TestCase(0f, 1f, 2f, 3f, 0f, 0f, 1f, 1f, 0f, 0f)]
        [TestCase(75f, 12.165878f, 2f, 76.880052f, 0.29552f, 0f, 0.955336f, 0.955336f, 0f, -0.29552f)]
        [TestCase(150f, 44.666096f, 2f, 144.160618f, 0.564642f, 0f, 0.825336f, 0.825336f, 0f, -0.564642f)]
        [TestCase(299f, 159.479249f, 2f, 235.645551f, 0.930582f, 0f, 0.366083f, 0.366083f, 0f, -0.930582f)]
        public void AtAndFrame_MatchThePage(float s, float px, float py, float pz, float tx, float ty, float tz,
            float nx, float ny, float nz)
        {
            var p = MeerkatWormholeMath.At(new Vector3(1f, 2f, 3f), Vector3.forward, Vector3.right, 300f, 1.2f, s, out var t);
            MeerkatWormholeMath.Frame(Vector3.forward, Vector3.right, 300f, 1.2f, s, out var n, out var bn);
            Assert.AreEqual(px, p.x, Tol); Assert.AreEqual(py, p.y, Tol); Assert.AreEqual(pz, p.z, Tol);
            Assert.AreEqual(tx, t.x, 1e-5f); Assert.AreEqual(ty, t.y, 1e-5f); Assert.AreEqual(tz, t.z, 1e-5f);
            Assert.AreEqual(nx, n.x, 1e-5f); Assert.AreEqual(ny, n.y, 1e-5f); Assert.AreEqual(nz, n.z, 1e-5f);
            // the page's binormal is (0, 1, 0) at every s: t0 x side
            Assert.AreEqual(0f, bn.x, 1e-6f); Assert.AreEqual(1f, bn.y, 1e-6f); Assert.AreEqual(0f, bn.z, 1e-6f);
        }

        [TestCase(0f, 300f, 0f, 26f)]
        [TestCase(0f, 300f, 150f, 10f)]
        [TestCase(0f, 300f, 60f, 17.531686f)]
        [TestCase(100f, 200f, 150f, 3.333333f)]
        [TestCase(100f, 200f, 110f, 7.216032f)]
        public void Radius_MatchesThePage(float s0, float s1, float s, float expected) =>
            Assert.AreEqual(expected, MeerkatWormholeMath.Radius(10f, 26f, 300f, s0, s1, s), Tol);

        /// <summary>
        /// One tunnel's life at dt 0.0625 s (exact in binary, so the step a phase changes on is the page's): it forms over
        /// 0.5 s, stays open while ridden, the rider comes out on step 20, it stays 10 s, collapses at 375 u/s and is gone on
        /// step 192 at its centre.
        /// </summary>
        [Test]
        public void Step_TheTunnelsLifeMatchesThePage()
        {
            const float L = 300f, dt = 0.0625f;
            var w = MeerkatWormholeMath.NewWindow(L);
            for (int k = 1; k <= 400; k++)
            {
                if (k == 20) MeerkatWormholeMath.RiderOut(ref w);
                bool gone = MeerkatWormholeMath.Step(ref w, L, 0.5f, 10f, 0.8f, dt);
                switch (k)
                {
                    case 1: Expect(w, MeerkatWormholeMath.Phase.Form, 100.488281f, 199.511719f); break;
                    case 4: Expect(w, MeerkatWormholeMath.Phase.Form, 18.75f, 281.25f); break;
                    case 8: case 9: case 19: Expect(w, MeerkatWormholeMath.Phase.Open, 0f, 300f); break;
                    case 20: Expect(w, MeerkatWormholeMath.Phase.Stay, 0f, 300f); break;
                    case 179: Expect(w, MeerkatWormholeMath.Phase.Collapse, 0f, 300f); break;
                    case 180: Expect(w, MeerkatWormholeMath.Phase.Collapse, 11.71875f, 288.28125f); break;
                    case 181: Expect(w, MeerkatWormholeMath.Phase.Collapse, 23.4375f, 276.5625f); break;
                    case 185: Expect(w, MeerkatWormholeMath.Phase.Collapse, 70.3125f, 229.6875f); break;
                }
                if (k < 192) Assert.IsFalse(gone, $"gone early, on step {k}");
                if (k != 192) continue;
                Assert.IsTrue(gone, "the page fades it on step 192");
                Expect(w, MeerkatWormholeMath.Phase.Gone, 150f, 150f);
                return;
            }
            Assert.Fail("never collapsed");
        }

        static void Expect(MeerkatWormholeMath.Window w, MeerkatWormholeMath.Phase phase, float s0, float s1)
        {
            Assert.AreEqual(phase, w.Phase);
            Assert.AreEqual(s0, w.S0, Tol);
            Assert.AreEqual(s1, w.S1, Tol);
        }

        [Test]
        public void CollapseNow_EndsAFormedTunnelButNotACollapsingOne()
        {
            var w = MeerkatWormholeMath.NewWindow(300f);
            MeerkatWormholeMath.CollapseNow(ref w);
            Assert.AreEqual(MeerkatWormholeMath.Phase.Collapse, w.Phase);
            w.Phase = MeerkatWormholeMath.Phase.Gone;
            MeerkatWormholeMath.CollapseNow(ref w);
            Assert.AreEqual(MeerkatWormholeMath.Phase.Gone, w.Phase);
        }

        // the target in the hull's frame (Unity: z forward), the judge noise and its roll, turn-only -> LT, stick (null: none)
        [TestCase(0f, 0f, 900f, 0f, 0.5f, false, 0f, 0f)]
        [TestCase(300f, 0f, 400f, 0f, 0.5f, false, 0.75f, 0.4775f)]
        [TestCase(0f, -200f, 250f, 0f, 0.5f, true, 1f, 0.4775f)]
        [TestCase(500f, 100f, 100f, 0f, 0.5f, false, 0.875f, 0.81f)]
        [TestCase(-120f, 40f, 300f, 0.4f, 0.9f, false, 1f, 0.3886f)]
        public void TryPlan_MatchesThePage(float x, float y, float z, float noise, float rand, bool turnOnly, float lq, float m)
        {
            var p = new MeerkatWormholeMath.PlanSettings
            {
                ReticleAhead = 30f, ShapeSeconds = 0.3f, BendMaxDegrees = 120f, LengthStraight = 600f, LengthBent = 120f,
            };
            Assert.IsTrue(MeerkatWormholeMath.TryPlan(p, new Vector3(x, y, z), 60f, noise, rand, turnOnly, out float gotLq, out float gotM));
            Assert.AreEqual(lq, gotLq, 1e-5f, "LT");
            Assert.AreEqual(m, gotM, 1e-4f, "stick");
        }

        [Test]
        public void TryPlan_NothingFitsATargetUnderTheNose()
        {
            var p = new MeerkatWormholeMath.PlanSettings
            {
                ReticleAhead = 30f, ShapeSeconds = 0.3f, BendMaxDegrees = 120f, LengthStraight = 600f, LengthBent = 120f,
            };
            Assert.IsFalse(MeerkatWormholeMath.TryPlan(p, new Vector3(0f, 0f, 40f), 60f, 0f, 0.5f, false, out _, out _));
        }

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
                x += (target - x) * MeerkatWormholeMath.ReticleEase(1f / 60f, 0.15f);
                if (k == 2) Assert.AreEqual(0.040f, x, 1.5e-3f);
                if (k == 8) Assert.AreEqual(0.119f, x, 1.5e-3f);
                if (k == 29) Assert.AreEqual(0.194f, x, 1.5e-3f);
            }
            Assert.AreEqual(1f, MeerkatWormholeMath.ReticleEase(1f / 60f, 0f), "0 s: it follows at once");
            Assert.IsTrue(MeerkatWormholeMath.ReticleSettled(new Vector2(0.5f, 0f), new Vector2(0.49f, 0.01f), 0.25f, 0.24f));
            Assert.IsFalse(MeerkatWormholeMath.ReticleSettled(new Vector2(0.5f, 0f), new Vector2(0.45f, 0f), 0.25f, 0.25f));
        }

        [Test]
        public void Ease_AndItsInverse_RoundTrip()
        {
            foreach (float y in new[] { -0.9f, -0.3f, 0f, 0.25f, 0.8f })
                Assert.AreEqual(y, MeerkatWormholeMath.Ease(MeerkatWormholeMath.EaseInverse(y)), 1e-3f);
        }

        [Test]
        public void Ride_StartsAndEndsAtTheEntrySpeed_AndPeaksAtTheThroat()
        {
            // round 6: entry 1, middle 2.5, exit 1.2 x the speed before the tunnel (the page measured 60 in, ~150 at the throat,
            // 72 out of the far mouth, easing back to cruise)
            Assert.AreEqual(60f, MeerkatWormholeMath.RideSpeed(60f, 1f, 2.5f, 1.2f, -40f, 300f), 1e-4f, "the dive");
            Assert.AreEqual(60f, MeerkatWormholeMath.RideSpeed(60f, 1f, 2.5f, 1.2f, 0f, 300f), 1e-4f, "the way in");
            Assert.AreEqual(150f, MeerkatWormholeMath.RideSpeed(60f, 1f, 2.5f, 1.2f, 150f, 300f), 1e-3f, "the throat");
            Assert.AreEqual(72f, MeerkatWormholeMath.RideSpeed(60f, 1f, 2.5f, 1.2f, 300f, 300f), 1e-3f, "the way out");
            Assert.AreEqual(1f + 1.5f * Mathf.Sin(Mathf.PI * 0.25f), MeerkatWormholeMath.RideMultiplier(1f, 2.5f, 1.2f, 0.25f), 1e-5f);
            Assert.AreEqual(1.2f + 1.3f * Mathf.Sin(Mathf.PI * 0.75f), MeerkatWormholeMath.RideMultiplier(1f, 2.5f, 1.2f, 0.75f), 1e-5f);
        }

        [Test]
        public void Ride_ComesOutOnTheTunnelsLastHeading()
        {
            var q = MeerkatWormholeMath.RideRotation(Vector3.up, 1.2f, 300f, 300f, 0f, Quaternion.identity);
            MeerkatWormholeMath.At(Vector3.zero, Vector3.forward, Vector3.right, 300f, 1.2f, 300f, out var tangent);
            Assert.Less(Vector3.Angle(q * Vector3.forward, tangent), 0.01f);
        }

        [Test]
        public void Ripple_IsTheEdgesOnly_AndFades()
        {
            float a = MeerkatWormholeMath.RippleAmplitude(0.3f, 0.2f, 100f);
            Assert.AreEqual(0.3f * 0.012f * 0.64f, a, 1e-6f);
            Assert.AreEqual(0f, MeerkatWormholeMath.RippleAmplitude(0.3f, 1f, 100f));
            Assert.AreEqual(a * 0.5f, MeerkatWormholeMath.RippleAmplitude(0.3f, 0.2f, 600f), 1e-6f, "half at 600 u");
            // the centre never moves: inside rippleEdge the mask is 0
            float front = MeerkatWormholeMath.RippleFront(0.45f, 0f);
            Assert.AreEqual(0.45f, front, 1e-6f);
            var centre = MeerkatWormholeMath.RippleDisplacement(new Vector2(0.1f, 0.05f), 1.6f, 0.01f, 0.5f, 0.45f);
            Assert.AreEqual(Vector2.zero, centre);
        }
    }
}

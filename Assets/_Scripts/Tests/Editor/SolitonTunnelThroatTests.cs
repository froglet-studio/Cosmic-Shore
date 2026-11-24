using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The arithmetic behind what a camera inside a Soliton Tunnel sees (<see cref="SolitonTunnelLensPass"/>, Yash,
    /// 2026-11-22: "players should not be able to see outer world that time, it's like gliding through the inner surface of
    /// the space-time hole, and exiting to the universe again"): where the wall's rings stand ahead of the eye, how a ring
    /// and a rider land on screen. The shader finds a pixel's place on the wall between two rings, so a ring out of order
    /// or a first ring that does not hold the whole view shows as a torn or missing wall.
    /// </summary>
    [TestFixture]
    public class SolitonTunnelThroatTests
    {
        const int N = SolitonTunnelLensPass.Rings;

        [Test]
        public void Rings_RunFromJustAheadToTheTubesEnd_EachFurtherThanTheLast()
        {
            const float d0 = 2f, remaining = 180f;
            Assert.AreEqual(d0, SolitonTunnelLensPass.RingAhead(d0, remaining, 0, N), 1e-4f, "the first ring is just ahead");
            Assert.AreEqual(remaining, SolitonTunnelLensPass.RingAhead(d0, remaining, N - 1, N), 1e-2f, "the last is the tube's end");
            float prev = 0f, prevStep = 0f;
            for (int k = 0; k < N; k++)
            {
                float d = SolitonTunnelLensPass.RingAhead(d0, remaining, k, N);
                Assert.Greater(d, prev, "ring " + k + " is further than the one before");
                if (k > 1) Assert.Greater(d - prev, prevStep, "the rings spread out with distance (most of them are near the eye)");
                prevStep = d - prev;
                prev = d;
            }
        }

        [Test]
        public void Rings_AtTheWayOut_AreAllTheEnd()
        {
            for (int k = 0; k < N; k++)
            {
                Assert.AreEqual(1.5f, SolitonTunnelLensPass.RingAhead(2f, 1.5f, k, N), 1e-5f, "less than the first step left");
                Assert.AreEqual(0f, SolitonTunnelLensPass.RingAhead(2f, -3f, k, N), 1e-5f, "past the end is the end, never behind");
            }
        }

        [Test]
        public void ARing_OnScreen_ShrinksWithDepthAndSlidesWithTheBend()
        {
            const float f = 0.866f;   // a 60 degree lens: 0.5 / tan(30)
            // camera space looks down -z
            var near = SolitonTunnelLensPass.RingOnScreen(f, new Vector3(0f, 0f, -10f), 8f);
            var far = SolitonTunnelLensPass.RingOnScreen(f, new Vector3(0f, 0f, -100f), 8f);
            Assert.AreEqual(f * 0.8f, near.z, 1e-4f);
            Assert.AreEqual(f * 0.08f, far.z, 1e-4f);
            Assert.AreEqual(0f, near.x, 1e-6f);
            var bent = SolitonTunnelLensPass.RingOnScreen(f, new Vector3(20f, -5f, -100f), 8f);
            Assert.AreEqual(f * 0.2f, bent.x, 1e-4f, "a bend to the right carries the ring right");
            Assert.AreEqual(-f * 0.05f, bent.y, 1e-4f);
        }

        [Test]
        public void ARing_AtTheEyeOrBehindIt_HoldsTheWholeView()
        {
            const float f = 0.866f;
            Assert.AreEqual(SolitonTunnelLensPass.WholeView, SolitonTunnelLensPass.RingOnScreen(f, new Vector3(3f, 0f, 0f), 8f).z);
            Assert.AreEqual(SolitonTunnelLensPass.WholeView, SolitonTunnelLensPass.RingOnScreen(f, new Vector3(0f, 0f, 4f), 8f).z, "behind the eye");
            Assert.AreEqual(SolitonTunnelLensPass.WholeView, SolitonTunnelLensPass.RingOnScreen(f, new Vector3(0f, 0f, -0.2f), 8f).z,
                "a ring this close is wider than the screen: capped, not infinite");
            // the first ring of a ride (0.3 x the radius ahead, on the centreline) must cover a 16:9 screen's corner (1.02)
            Assert.Greater(SolitonTunnelLensPass.RingOnScreen(f, new Vector3(0f, 0f, -0.3f * 8f), 8f).z, 1.1f);
        }

        [Test]
        public void ARider_OnScreen_IsADiscAtItsDepth_AndNothingBehindTheEye()
        {
            const float f = 0.866f;
            var hull = SolitonTunnelLensPass.HullOnScreen(f, new Vector3(2f, -1f, -21f), 8f);
            Assert.AreEqual(f * 2f / 21f, hull.x, 1e-5f);
            Assert.AreEqual(-f / 21f, hull.y, 1e-5f);
            Assert.AreEqual(f * 8f / 21f, hull.z, 1e-5f, "the disc's radius on screen");
            Assert.AreEqual(21f, hull.w, 1e-5f, "its depth, which the shader holds the scene's depth against");
            // the shader recovers the sphere's radius as disc x depth / f
            Assert.AreEqual(8f, hull.z * hull.w / f, 1e-4f);
            Assert.AreEqual(Vector4.zero, SolitonTunnelLensPass.HullOnScreen(f, new Vector3(0f, 0f, 5f), 8f), "behind the eye: not drawn");
            Assert.AreEqual(Vector4.zero, SolitonTunnelLensPass.HullOnScreen(f, new Vector3(0f, 0f, -21f), 0f), "no radius: not drawn");
        }
    }
}

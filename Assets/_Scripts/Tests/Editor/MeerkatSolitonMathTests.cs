using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Meerkat's kit arithmetic (<see cref="MeerkatSolitonMath"/>; Garrett's design, approved by Yash 2026-10-28) held to
    /// the Vessel Studio page it was ported from (<c>Docs/Studios/VesselStudio/meerkat.html</c>, "Garrett's kit"). Every golden
    /// number came from running the page's OWN <c>lerpLv</c>, <c>perpBasis</c> and <c>kitPortal</c> arithmetic in node on
    /// the same inputs, at the page's shipped numbers (2026-10-28: zap 0.1 to 0.25 of a petal by Charge, portal 1.5 × the exit
    /// radius). A page change that moves one of these is a port to redo, not a test to loosen. The surface area's goldens are
    /// round 23's, in <c>MeerkatWormholeMathTests.SurfaceAreaThroat_MatchesThePage</c>. Runs outside Unity too:
    /// <c>Tools/Build/meerkat_kit_harness/run.sh</c>.
    /// </summary>
    [TestFixture]
    public class MeerkatSolitonMathTests
    {
        const float Tol = 1e-3f;

        // ------------------------------------------------------------------ Time: the ride

        [Test]
        public void Ride_RushAndBoostOut_AreThePages()
        {
            // rideMidMultiplier 2.5 x lerpLv(rushL0 1, rushL10 1.4) at Time 0 / 5 / 10
            Assert.AreEqual(2.5f, MeerkatSolitonMath.RideMid(2.5f, 1f), 1e-5f);
            Assert.AreEqual(3.0f, MeerkatSolitonMath.RideMid(2.5f, 1.2f), 1e-5f);
            Assert.AreEqual(3.5f, MeerkatSolitonMath.RideMid(2.5f, 1.4f), 1e-5f);
            Assert.AreEqual(1.2f, MeerkatSolitonMath.RideExit(1.2f, false, 1.5f), 1e-5f, "no L5: the plain exit");
            Assert.AreEqual(1.8f, MeerkatSolitonMath.RideExit(1.2f, true, 1.5f), 1e-5f, "L5 Boost out: 1.5 x the exit");
            Assert.AreEqual(1.2f, MeerkatSolitonMath.RideExit(1.2f, true, 0.5f), 1e-5f, "a boost out never slows the exit");
        }

        [Test]
        public void Ride_EntersOnlyThroughTheWayIn()
        {
            var a = new Vector3(0f, 0f, 100f);
            var t0 = Vector3.forward;
            Assert.IsTrue(MeerkatSolitonMath.CanEnter(new Vector3(3f, 0f, 102f), Vector3.forward, a, t0, 10f, out float along));
            Assert.AreEqual(2f, along, 1e-5f, "along");
            Assert.IsFalse(MeerkatSolitonMath.CanEnter(new Vector3(11f, 0f, 102f), Vector3.forward, a, t0, 10f, out _), "outside the mouth");
            Assert.IsFalse(MeerkatSolitonMath.CanEnter(new Vector3(0f, 0f, 115f), Vector3.forward, a, t0, 10f, out _), "past the way in");
            Assert.IsFalse(MeerkatSolitonMath.CanEnter(new Vector3(0f, 0f, 102f), Vector3.back, a, t0, 10f, out _), "flying the wrong way");
        }

        // ------------------------------------------------------------------ Charge: the sentinels

        [Test]
        public void SentinelPool_RetiresTheOldest()
        {
            var placed = new List<int>();
            var retired = new List<int>();
            for (int id = 1; id <= 3; id++) MeerkatSolitonMath.AddToPool(placed, id, 3, retired);
            Assert.AreEqual(0, retired.Count, "three fit a pool of three");
            MeerkatSolitonMath.AddToPool(placed, 4, 3, retired);
            Assert.AreEqual(1, retired.Count, "the fourth retires one");
            Assert.AreEqual(1, retired[0], "...the oldest");
            Assert.AreEqual(3, placed.Count);
            Assert.AreEqual(2, placed[0], "the next oldest is first now");
            Assert.AreEqual(4, placed[2], "the new one is last");
            // the pool shrinks back from 6 to 3 (Charge falls below L5): the three oldest go at once
            placed.Clear(); retired.Clear();
            for (int id = 1; id <= 6; id++) MeerkatSolitonMath.AddToPool(placed, id, 6, retired);
            MeerkatSolitonMath.AddToPool(placed, 7, 3, retired);
            Assert.AreEqual(4, retired.Count, "six in a pool of three: four retire for the seventh");
            Assert.AreEqual(1, retired[0]);
            Assert.AreEqual(4, retired[3]);
            Assert.AreEqual(3, MeerkatSolitonMath.PoolSize(false, 3, 6));
            Assert.AreEqual(6, MeerkatSolitonMath.PoolSize(true, 3, 6), "Charge L5: six sentinels");
        }

        [Test]
        public void Zap_PetalsPerCharge_AreThePages()
        {
            // lerpLv(zapPetalsL0 0.1, zapPetalsL10 0.25) at Charge 0 / 5 / 10 (Garrett's starting point)
            Assert.AreEqual(0.1f, MeerkatSolitonMath.ZapPetals(0.1f, 0.25f, 0f), 1e-6f);
            Assert.AreEqual(0.175f, MeerkatSolitonMath.ZapPetals(0.1f, 0.25f, 0.5f), 1e-6f);
            Assert.AreEqual(0.25f, MeerkatSolitonMath.ZapPetals(0.1f, 0.25f, 1f), 1e-6f);
            Assert.AreEqual(3.75f, MeerkatSolitonMath.GuardRadius(1.5f, 2.5f), 1e-6f, "the guard is 1.5 x the opening");
        }

        [Test]
        public void Zap_Clock_FiresFiveASecond_AndNeverBursts()
        {
            float owed = 0f;
            int zaps = 0;
            for (int f = 0; f < 600; f++)
            {
                owed = MeerkatSolitonMath.OweZaps(owed, 1f / 60f, 5f);
                while (owed >= 1f) { owed -= 1f; zaps++; }
            }
            Assert.AreEqual(50f, zaps, 1f, "five zaps a second: fifty in ten seconds at 60 fps (one may still be owed)");
            owed = MeerkatSolitonMath.OweZaps(0f, 3f, 5f);   // a three-second hitch
            Assert.Less(owed, 2f, "a hitch owes at most one zap in reserve");
        }

        // ------------------------------------------------------------------ Mass: the hexagon

        [Test]
        public void Portal_HexagonPoints_AreThePages()
        {
            AssertPortal(new Vector3(10f, 20f, 30f), Vector3.forward, 10f, new[]
            {
                new Vector3(21.25f, 26.495191f, 30f), new Vector3(10f, 32.990381f, 30f), new Vector3(-1.25f, 26.495191f, 30f),
                new Vector3(-1.25f, 13.504809f, 30f), new Vector3(10f, 7.009619f, 30f), new Vector3(21.25f, 13.504809f, 30f),
            });
            AssertPortal(Vector3.zero, Vector3.up, 8f, new[]
            {
                new Vector3(5.196152f, 0f, 9f), new Vector3(10.392305f, 0f, 0f), new Vector3(5.196152f, 0f, -9f),
                new Vector3(-5.196152f, 0f, -9f), new Vector3(-10.392305f, 0f, 0f), new Vector3(-5.196152f, 0f, 9f),
            });
            AssertPortal(new Vector3(-5f, 4f, 2f), new Vector3(1f, 2f, 3f).normalized, 12f, new[]
            {
                new Vector3(6.489759f, 10.587325f, -6.221470f), new Vector3(-7.634930f, 17.174651f, -5.904791f),
                new Vector3(-19.124690f, 10.587325f, 2.316680f), new Vector3(-16.489759f, -2.587325f, 10.221470f),
                new Vector3(-2.365070f, -9.174651f, 9.904791f), new Vector3(9.124690f, -2.587325f, 1.683320f),
            });
        }

        static void AssertPortal(Vector3 exit, Vector3 tangent, float exitRadius, Vector3[] golden)
        {
            for (int k = 0; k < MeerkatSolitonMath.PortalCount; k++)
            {
                var p = MeerkatSolitonMath.PortalPoint(exit, tangent, exitRadius, 1.5f, k, out var along, out var outward);
                Assert.AreEqual(0f, (p - golden[k]).magnitude, 2e-3f, $"prism {k}");
                Assert.AreEqual(0f, Vector3.Dot(p - exit, tangent), 1e-3f, $"prism {k} is square to the exit heading");
                Assert.AreEqual(1.5f * exitRadius * Mathf.Cos(Mathf.PI / 6f), (p - exit).magnitude, 1e-3f, $"prism {k} on the apothem");
                Assert.AreEqual(0f, Vector3.Dot(along, outward), 1e-4f, $"prism {k} lies along its edge");
                Assert.AreEqual(0f, Vector3.Dot(along, tangent), 1e-4f, $"prism {k}'s edge is square to the heading");
            }
            // the six edges close: prism k's edge ends where prism k+1's begins (a regular hexagon, side = its radius)
            float side = 1.5f * exitRadius;
            for (int k = 0; k < MeerkatSolitonMath.PortalCount; k++)
            {
                var p0 = MeerkatSolitonMath.PortalPoint(exit, tangent, exitRadius, 1.5f, k, out var a0, out _);
                var p1 = MeerkatSolitonMath.PortalPoint(exit, tangent, exitRadius, 1.5f, (k + 1) % 6, out var a1, out _);
                Assert.AreEqual(0f, ((p0 + a0 * (0.5f * side)) - (p1 - a1 * (0.5f * side))).magnitude, 2e-3f, $"edge {k} meets edge {k + 1}");
            }
        }
    }
}

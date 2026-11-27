using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The placement rules of Riposte's arena (<see cref="RiposteYard"/>, RIPOSTE.md section 4), one test a rule: a
    /// lane ends at a refuel, a rail runs beside its lane and never in it, a gate stands between the two clearings,
    /// thorns line the lane walls and ironwood fills the core.
    /// </summary>
    public class RiposteYardTests
    {
        [Test]
        public void ALane_RunsThroughItsOwnRefuelClearing()
        {
            // The ring clearings sit at Fibonacci(k, n) * distance (SpawnableJackstraw.CollectClearings).
            for (int n = 1; n <= 8; n++)
                for (int k = 0; k < n; k++)
                {
                    var clearing = SpawnableJackstraw.Fibonacci(k, n) * 330f;
                    var axis = RiposteYard.LaneAxis(k, n);
                    Assert.AreEqual(1f, axis.magnitude, 1e-4f);
                    Assert.Less(RiposteYard.PointSegmentSqr(clearing, Vector3.zero, axis * 600f), 1e-4f,
                                $"lane {k} of {n} misses its clearing");
                }
        }

        [Test]
        public void NoTwoLanes_ShareALine()
        {
            for (int n = 2; n <= 8; n++)
                for (int a = 0; a < n; a++)
                    for (int b = a + 1; b < n; b++)
                        Assert.Greater(Vector3.Angle(RiposteYard.LaneAxis(a, n), RiposteYard.LaneAxis(b, n)), 20f,
                                       $"lanes {a} and {b} of {n}");
        }

        [Test]
        public void ARail_RunsBesideItsLane_OutsideTheWall()
        {
            const float laneRadius = 40f;
            var axis = RiposteYard.LaneAxis(1, 4);
            for (int rails = 1; rails <= 4; rails++)
                for (int r = 0; r < rails; r++)
                {
                    var offset = RiposteYard.RailOffset(axis, 1, r, rails, laneRadius);
                    Assert.AreEqual(0f, Vector3.Dot(offset, axis), 1e-3f, "square to the lane");
                    Assert.AreEqual(laneRadius + RiposteYard.RailStandoff, offset.magnitude, 1e-3f);
                    Assert.Greater(offset.magnitude - SpawnableJackstraw.Section, laneRadius, "the lane stays empty");
                }
        }

        [Test]
        public void TheRailsOfALane_AreSpreadEvenlyRoundIt()
        {
            var axis = RiposteYard.LaneAxis(0, 4);
            var a = RiposteYard.RailOffset(axis, 0, 0, 4, 45f);
            var b = RiposteYard.RailOffset(axis, 0, 1, 4, 45f);
            var c = RiposteYard.RailOffset(axis, 0, 2, 4, 45f);
            Assert.AreEqual(90f, Vector3.Angle(a, b), 0.01f);
            Assert.AreEqual(180f, Vector3.Angle(a, c), 0.01f);
        }

        [Test]
        public void Pickets_StartPastTheCentreClearing_AndNeverTouch()
        {
            const float centre = 100f;
            float half = RiposteYard.PicketLength * 0.5f;
            Assert.AreEqual(centre + RiposteYard.RailStart, RiposteYard.PicketCentre(centre, 0) - half, 1e-3f);
            for (int j = 0; j < 6; j++)
            {
                float gap = RiposteYard.PicketCentre(centre, j + 1) - half - (RiposteYard.PicketCentre(centre, j) + half);
                Assert.AreEqual(RiposteYard.PicketGap, gap, 1e-3f, "a way into and out of the lane between pickets");
            }
        }

        [Test]
        public void Gates_StandBetweenTheTwoClearings_EvenlySpaced()
        {
            const float from = 100f, to = 400f;
            Assert.AreEqual(250f, RiposteYard.GateDistance(from, to, 0, 1), 1e-3f);
            Assert.AreEqual(200f, RiposteYard.GateDistance(from, to, 0, 2), 1e-3f);
            Assert.AreEqual(300f, RiposteYard.GateDistance(from, to, 1, 2), 1e-3f);
            for (int gates = 1; gates <= 4; gates++)
                for (int g = 0; g < gates; g++)
                {
                    Assert.Greater(RiposteYard.GateDistance(from, to, g, gates), from);
                    Assert.Less(RiposteYard.GateDistance(from, to, g, gates), to);
                }
        }

        [Test]
        public void GatedLanes_AreEveryNth_AndNoneAtZero()
        {
            for (int lane = 0; lane < 8; lane++) Assert.IsFalse(RiposteYard.GatedLane(lane, 0), "I1 has no gate");
            Assert.IsTrue(RiposteYard.GatedLane(0, 2));
            Assert.IsFalse(RiposteYard.GatedLane(1, 2), "every other lane stays a clean straight");
            Assert.IsTrue(RiposteYard.GatedLane(2, 2));
            for (int lane = 0; lane < 8; lane++) Assert.IsTrue(RiposteYard.GatedLane(lane, 1));
        }

        [Test]
        public void Thorns_LineTheLaneWall_AndNothingElse()
        {
            const float laneRadius = 34f, band = 70f;
            Assert.IsTrue(RiposteYard.Thorned(laneRadius + 5f, laneRadius, band), "beside the wall");
            Assert.IsFalse(RiposteYard.Thorned(laneRadius + band + 1f, laneRadius, band), "deep in the scatter");
            Assert.IsFalse(RiposteYard.Thorned(laneRadius + 5f, laneRadius, 0f), "no band, no thorns (I1, I2)");
        }

        [Test]
        public void Ironwood_FillsTheCore_AndNothingElse()
        {
            Assert.IsTrue(RiposteYard.Ironwood(new Vector3(100f, 200f, 100f), 300f));
            Assert.IsFalse(RiposteYard.Ironwood(new Vector3(300f, 200f, 100f), 300f));
            Assert.IsFalse(RiposteYard.Ironwood(Vector3.zero, 0f), "no core before I4");
        }

        [Test]
        public void Basis_IsSquareToTheAxis_ForEveryLane()
        {
            for (int k = 0; k < 8; k++)
            {
                var axis = RiposteYard.LaneAxis(k, 8);
                RiposteYard.Basis(axis, out var e1, out var e2);
                Assert.AreEqual(0f, Vector3.Dot(axis, e1), 1e-4f);
                Assert.AreEqual(0f, Vector3.Dot(axis, e2), 1e-4f);
                Assert.AreEqual(0f, Vector3.Dot(e1, e2), 1e-4f);
                Assert.AreEqual(1f, e1.magnitude, 1e-4f);
                Assert.AreEqual(1f, e2.magnitude, 1e-4f);
            }
        }
    }
}

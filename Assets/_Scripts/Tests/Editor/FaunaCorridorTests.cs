using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    public class FaunaCorridorTests
    {
        static FaunaCorridor Make(float r, bool closed = false, params Vector3[] pts)
        {
            var c = new FaunaCorridor();
            c.Set(new List<Vector3>(pts), r, closed);
            return c;
        }

        [Test]
        public void Inactive_WhenRadiusZeroOrNoPath()
        {
            Assert.IsFalse(Make(0f, false, Vector3.zero, Vector3.right * 100f).IsActive);
            Assert.IsFalse(Make(50f).IsActive);
            var c = Make(0f, false, Vector3.zero);
            Assert.AreEqual(new Vector3(1, 2, 3), c.PushOut(new Vector3(1, 2, 3)));
        }

        [Test]
        public void Contains_PointsNearAnySegment()
        {
            var c = Make(50f, false, Vector3.zero, new Vector3(1000, 0, 0), new Vector3(1000, 1000, 0));
            Assert.IsTrue(c.Contains(new Vector3(500, 40, 0)));
            Assert.IsTrue(c.Contains(new Vector3(1030, 600, 0)));
            Assert.IsFalse(c.Contains(new Vector3(500, 60, 0)));
            Assert.IsFalse(c.Contains(new Vector3(-60, 0, 0)));   // open chain: no cap past the ends beyond r
        }

        [Test]
        public void PushOut_LandsOnTheWall_AndLeavesOutsidePointsAlone()
        {
            var c = Make(50f, false, Vector3.zero, new Vector3(1000, 0, 0));
            var outside = new Vector3(300, 80, 0);
            Assert.AreEqual(outside, c.PushOut(outside));
            var p = c.PushOut(new Vector3(300, 10, 0));
            Assert.IsFalse(c.Contains(p));
            Assert.AreEqual(300f, p.x, 1e-3f);
            Assert.Greater(p.y, 49.9f);
            Assert.Less(p.y, 51f);
        }

        [Test]
        public void PushOut_WithAStandoff_ParksPastTheWall()
        {
            var c = new FaunaCorridor();
            c.Set(new List<Vector3> { Vector3.zero, new Vector3(1000, 0, 0) }, 50f, false, goalStandoff: 30f);
            Assert.IsFalse(c.Contains(new Vector3(300, 60, 0)), "the standoff moves where goals land, not the tube");
            var p = c.PushOut(new Vector3(300, 10, 0));
            Assert.AreEqual(300f, p.x, 1e-3f);
            Assert.Greater(p.y, 79.9f);
            Assert.Less(p.y, 81f);
            c.Clear();
            Assert.AreEqual(0f, c.GoalStandoff);
        }

        [Test]
        public void PushOut_OnThePath_UsesSelfSide()
        {
            var c = Make(50f, false, Vector3.zero, new Vector3(1000, 0, 0));
            var p = c.PushOut(new Vector3(400, 0, 0), new Vector3(400, 0, -300));
            Assert.IsFalse(c.Contains(p));
            Assert.Less(p.z, -49f);
        }

        [Test]
        public void PushOut_InsideACorner_ClearsBothLegs()
        {
            var c = Make(50f, false, Vector3.zero, new Vector3(1000, 0, 0), new Vector3(1000, 1000, 0));
            var p = c.PushOut(new Vector3(990, 10, 0));
            Assert.IsFalse(c.Contains(p));
        }

        [Test]
        public void Closed_AddsTheReturnLeg()
        {
            var open = Make(50f, false, Vector3.zero, new Vector3(1000, 0, 0), new Vector3(1000, 1000, 0));
            var closed = Make(50f, true, Vector3.zero, new Vector3(1000, 0, 0), new Vector3(1000, 1000, 0));
            var mid = new Vector3(500, 500, 0);
            Assert.IsFalse(open.Contains(mid));
            Assert.IsTrue(closed.Contains(mid));
        }

        [Test]
        public void CorridorRadiusFor_ClampsIntensity()
        {
            var t = new[] { 100f, 200f, 300f, 400f };
            Assert.AreEqual(100f, SwitchbackController.CorridorRadiusFor(t, 0));
            Assert.AreEqual(300f, SwitchbackController.CorridorRadiusFor(t, 3));
            Assert.AreEqual(400f, SwitchbackController.CorridorRadiusFor(t, 9));
            Assert.AreEqual(0f, SwitchbackController.CorridorRadiusFor(null, 2));
            Assert.AreEqual(0f, SwitchbackController.CorridorRadiusFor(new[] { -5f }, 1));
        }
    }
}

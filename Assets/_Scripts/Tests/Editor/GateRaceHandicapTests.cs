#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="GateRaceHandicap"/> - the lobby AI difficulty on a gate race (the Stoat's Slingshot and
    /// Warpline). It only edits what the pilot BELIEVES about its next ring, so these hold the belief itself:
    /// Hard sees the true ring, a late notice hides the ring for a while, a misjudged ring sits outside the
    /// mouth until the pilot is past it, and every leg of a lapped race is a fresh decision.
    /// </summary>
    public class GateRaceHandicapTests
    {
        static readonly Vector3 Ring = new(0f, 0f, 500f);
        static readonly Vector3 Axis = Vector3.forward;
        const float Radius = 32f;

        [Test]
        public void Hard_SeesTheTrueRing()
        {
            var h = new GateRaceHandicap(default, 1);
            var b = h.Believe(3, Ring, Axis, Radius, Vector3.zero, Vector3.forward, 10f);
            Assert.IsFalse(b.Unnoticed);
            Assert.IsFalse(b.Misjudged);
            Assert.AreEqual(Ring, b.Point);
        }

        [Test]
        public void LateNotice_HidesTheNewRing_ThenShowsIt()
        {
            var h = new GateRaceHandicap(new SkimRaceHandicapLevel(1f, 0f), 7);
            Assert.IsTrue(h.Believe(1, Ring, Axis, Radius, Vector3.zero, Vector3.forward, 0f).Unnoticed, "just threaded: not seen yet");
            // the notice comes within ReactionSpreadMax x the level's seconds
            var later = h.Believe(1, Ring, Axis, Radius, Vector3.zero, Vector3.forward, SkimRaceHandicap.ReactionSpreadMax + 0.01f);
            Assert.IsFalse(later.Unnoticed);
            Assert.AreEqual(Ring, later.Point);
        }

        [Test]
        public void TheFirstRing_IsSeenOnTheGrid()
        {
            var h = new GateRaceHandicap(new SkimRaceHandicapLevel(5f, 0f), 7);
            Assert.IsFalse(h.Believe(0, Ring, Axis, Radius, Vector3.zero, Vector3.forward, 0f).Unnoticed);
        }

        [Test]
        public void Misjudged_IsOutsideTheMouth_InTheRingsPlane_UntilPast()
        {
            var h = new GateRaceHandicap(new SkimRaceHandicapLevel(0f, 1f), 3);
            var b = h.Believe(0, Ring, Axis, Radius, Vector3.zero, Vector3.forward, 0f);
            Assert.IsTrue(b.Misjudged);
            Vector3 off = b.Point - Ring;
            Assert.Greater(off.magnitude, Radius, "the believed point is outside the mouth");
            Assert.AreEqual(0f, Vector3.Dot(off, Axis), 1e-3f, "in the ring's own plane");
            Assert.AreEqual(1, h.Mistakes);

            // fly past the believed point: now the pilot sees the real ring
            var past = h.Believe(0, Ring, Axis, Radius, b.Point + Vector3.forward * 50f, Vector3.forward, 1f);
            Assert.IsFalse(past.Misjudged);
            Assert.AreEqual(Ring, past.Point);
        }

        [Test]
        public void Misjudged_NeverOutlastsTheCap()
        {
            var h = new GateRaceHandicap(new SkimRaceHandicapLevel(0f, 1f), 3);
            Assert.IsTrue(h.Believe(0, Ring, Axis, Radius, Vector3.zero, Vector3.forward, 0f).Misjudged);
            var late = h.Believe(0, Ring, Axis, Radius, Vector3.zero, Vector3.forward, SkimRaceHandicap.MistakeMaxSeconds + 0.1f);
            Assert.IsFalse(late.Misjudged);
        }

        [Test]
        public void EveryLeg_IsANewDecision_EvenTheSameRingNextLap()
        {
            var h = new GateRaceHandicap(new SkimRaceHandicapLevel(0f, 1f), 3);
            h.Believe(0, Ring, Axis, Radius, Vector3.zero, Vector3.forward, 0f);
            h.Believe(8, Ring, Axis, Radius, Vector3.zero, Vector3.forward, 1f);   // ring 0 again, on lap 2
            Assert.AreEqual(2, h.Mistakes);
            h.Reset();
            Assert.AreEqual(0, h.Mistakes);
        }

        [Test]
        public void InPlaneOffset_IsPerpendicularToAnyAxis()
        {
            foreach (var axis in new[] { Vector3.forward, Vector3.right, Vector3.up, new Vector3(1f, 2f, 3f) })
                for (float a = 0f; a < 6.3f; a += 0.7f)
                {
                    Vector3 o = GateRaceHandicap.InPlaneOffset(axis, a, 10f);
                    Assert.AreEqual(10f, o.magnitude, 1e-3f);
                    Assert.AreEqual(0f, Vector3.Dot(o, axis.normalized), 1e-3f);
                }
        }
    }
}
#endif

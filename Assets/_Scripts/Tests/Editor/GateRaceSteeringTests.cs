#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Regatta AI studio's steering fix, ported into <see cref="GateRaceController"/>: commit no
    /// earlier than the approach point, and treat a plane crossing outside the mouth as a miss. The
    /// expected numbers are the studio's own (the deleted Regatta AI page, <c>Docs/Studios/RegattaAI/index.html</c> in git history, <c>aiInput</c>:
    /// <c>Math.min(commit, lead + 0.5 * speed / turnRate)</c>), so the page and the game cannot drift.
    /// </summary>
    public class GateRaceSteeringTests
    {
        [Test]
        public void OffCommitsAtTheShippedDistanceWhateverTheLine()
        {
            Assert.AreEqual(420f, GateRaceController.CommitDistanceFor(420f, 240f, 200f, false));
            Assert.AreEqual(420f, GateRaceController.CommitDistanceFor(420f, 10f, 0f, false));
        }

        // Studio goldens (node, the page's formula): (lead 240, R 200) -> 340; (300, 400) -> 420; (180, 0) -> 180.
        [TestCase(240f, 200f, 340f)]
        [TestCase(300f, 400f, 420f)]
        [TestCase(180f, 0f, 180f)]
        public void OnCommitsNoEarlierThanTheApproachPoint(float lead, float turnRadius, float expected)
        {
            Assert.AreEqual(expected, GateRaceController.CommitDistanceFor(420f, lead, turnRadius, true), 1e-4f);
        }

        [Test]
        public void AHullThatCannotTurnKeepsTheShippedDistance()
        {
            // MinTurnRadius is +infinity for a zero turn rate.
            Assert.AreEqual(420f, GateRaceController.CommitDistanceFor(420f, 240f, float.PositiveInfinity, true));
            Assert.AreEqual(420f, GateRaceController.CommitDistanceFor(420f, 240f, float.NaN, true));
        }

        static readonly Vector3 Centre = new(10f, 5f, -3f);
        const float Radius = 30f;

        static GatePlaneCrossing Cross(Vector3 from, Vector3 to) =>
            RaceGateRing.PlaneCrossing(Centre + from, Centre + to, Centre, Vector3.forward, Radius);

        [Test]
        public void ThroughTheMouthIsAThreadEitherWayRound()
        {
            Assert.AreEqual(GatePlaneCrossing.InsideMouth, Cross(new Vector3(5f, 0f, -4f), new Vector3(5f, 0f, 4f)));
            Assert.AreEqual(GatePlaneCrossing.InsideMouth, Cross(new Vector3(-20f, 10f, 4f), new Vector3(-20f, 10f, -4f)));
        }

        [Test]
        public void ThroughThePlaneBesideTheRingIsAMiss()
        {
            Assert.AreEqual(GatePlaneCrossing.OutsideMouth, Cross(new Vector3(31f, 0f, -4f), new Vector3(31f, 0f, 4f)));
            Assert.AreEqual(GatePlaneCrossing.OutsideMouth, Cross(new Vector3(0f, 200f, -1f), new Vector3(0f, 200f, 1f)));
        }

        [Test]
        public void StayingOnOneSideIsNoCrossing()
        {
            Assert.AreEqual(GatePlaneCrossing.None, Cross(new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, -1f)));
            Assert.AreEqual(GatePlaneCrossing.None, Cross(new Vector3(500f, 0f, 3f), new Vector3(0f, 0f, 3f)));
        }

        [Test]
        public void TheMissIsJudgedWhereTheSegmentMeetsThePlaneNotAtItsEnds()
        {
            // Both ends are outside the mouth's radius, but the crossing point is inside it: a thread.
            Assert.AreEqual(GatePlaneCrossing.InsideMouth, Cross(new Vector3(-40f, 0f, -10f), new Vector3(40f, 0f, 10f)));
        }
    }
}
#endif

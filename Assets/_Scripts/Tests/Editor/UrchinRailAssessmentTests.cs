using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Locks the pure half of the Urchin autopilot: the rail walk and the ride / reverse / leave
    /// decision <see cref="UrchinAutopilotDriver"/> flies Skein's strands by. Under Editor/ for
    /// the reason <c>UrchinChainReactionTests</c> records.
    /// </summary>
    public class UrchinRailAssessmentTests
    {
        /// <summary>A straight rail of <c>count</c> prisms along +X at <c>spacing</c>, with optional holes.</summary>
        struct LineRail : IUrchinRailPoints
        {
            public int N;
            public float Spacing;
            public int HoleFrom, HoleTo;   // inclusive; HoleTo < HoleFrom = no hole

            public int Count => N;

            public bool TryGetPoint(int index, out Vector3 point)
            {
                point = new Vector3(index * Spacing, 0f, 0f);
                return !(index >= HoleFrom && index <= HoleTo);
            }
        }

        static LineRail Line(int n = 101, float spacing = 8f) =>
            new() { N = n, Spacing = spacing, HoleFrom = 1, HoleTo = 0 };

        static UrchinRailRules Rules => new()
        {
            CaptureRadius = 20f, ClosingFraction = 0.7f, DryCrawlArc = 80f, LaunchPreferArc = 160f,
        };

        // ---------------------------------------------------------------- scan

        [Test]
        public void Scan_FindsTheClosestApproachBetweenPrisms()
        {
            var rail = Line();
            // Objective 3 u off the rail, half-way between prisms 50 and 51.
            var scan = UrchinRailAssessment.Scan(ref rail, 0, 1, new Vector3(404f, 3f, 0f), 5000f);
            Assert.IsTrue(scan.Valid);
            Assert.AreEqual(3f, scan.MinDistance, 1e-3f);
            Assert.AreEqual(404f, scan.ArcToMin, 1e-2f);
            Assert.IsTrue(scan.ReachesEnd);
        }

        [Test]
        public void Scan_StopsAtTheArcBudget()
        {
            var rail = Line();
            var scan = UrchinRailAssessment.Scan(ref rail, 0, 1, new Vector3(800f, 0f, 0f), 100f);
            Assert.IsFalse(scan.ReachesEnd);
            Assert.LessOrEqual(scan.ArcWalked, 100f);
            Assert.Greater(scan.MinDistance, 600f);
        }

        [Test]
        public void Scan_BridgesHolesLikeTheRide()
        {
            var rail = Line();
            rail.HoleFrom = 10; rail.HoleTo = 20;
            var scan = UrchinRailAssessment.Scan(ref rail, 0, 1, new Vector3(800f, 0f, 0f), 5000f);
            Assert.AreEqual(800f, scan.ArcWalked, 1e-2f);
            Assert.AreEqual(0f, scan.MinDistance, 1e-3f);
        }

        [Test]
        public void Scan_BackwardWalksTowardTheTail()
        {
            var rail = Line();
            var back = UrchinRailAssessment.Scan(ref rail, 50, -1, new Vector3(0f, 0f, 0f), 5000f);
            Assert.AreEqual(0f, back.MinDistance, 1e-3f);
            Assert.AreEqual(400f, back.ArcToMin, 1e-2f);
        }

        [Test]
        public void Scan_IsInvalidOnAnEmptyOrAllHoleRail()
        {
            var empty = Line(0);
            Assert.IsFalse(UrchinRailAssessment.Scan(ref empty, 0, 1, Vector3.zero, 100f).Valid);

            var holes = Line(10);
            holes.HoleFrom = 0; holes.HoleTo = 9;
            Assert.IsFalse(UrchinRailAssessment.Scan(ref holes, 0, 1, Vector3.zero, 100f).Valid);
        }

        // ---------------------------------------------------------------- decide

        static UrchinRailScan S(float min, float arcToMin = 100f, float walked = 1000f, bool end = false) =>
            new(true, min, arcToMin, walked, end);

        [Test]
        public void Decide_RidesARailThatThreadsTheRingAhead()
        {
            var v = UrchinRailAssessment.Decide(S(5f), S(500f), 600f, Rules, crawlingDry: false);
            Assert.AreEqual(UrchinRailVerdict.Ride, v);
        }

        [Test]
        public void Decide_ReversesOntoARingBehind()
        {
            var v = UrchinRailAssessment.Decide(S(500f), S(5f), 600f, Rules, crawlingDry: false);
            Assert.AreEqual(UrchinRailVerdict.Reverse, v);
        }

        [Test]
        public void Decide_RidesARailThatStillClosesMostOfTheWay()
        {
            var v = UrchinRailAssessment.Decide(S(300f), S(900f), 600f, Rules, crawlingDry: false);
            Assert.AreEqual(UrchinRailVerdict.Ride, v);
        }

        [Test]
        public void Decide_LeavesARailThatGoesNowhereUseful()
        {
            // Closest approach is where the pilot already is: riding on only moves away.
            var v = UrchinRailAssessment.Decide(S(600f, 0f), S(650f, 0f), 600f, Rules, crawlingDry: false);
            Assert.AreEqual(UrchinRailVerdict.Leave, v);
        }

        [Test]
        public void Decide_LaunchesOffANearEndInsteadOfSlipping()
        {
            var v = UrchinRailAssessment.Decide(S(600f, 0f, 120f, end: true), S(650f), 600f, Rules, false);
            Assert.AreEqual(UrchinRailVerdict.Ride, v);
        }

        [Test]
        public void Decide_DryHostileCrawlOnlyStaysForAShortThread()
        {
            // Threads the ring 300 u ahead, but at ~20 u/s with no ammo to convert: leave.
            Assert.AreEqual(UrchinRailVerdict.Leave,
                UrchinRailAssessment.Decide(S(5f, 300f), S(900f), 600f, Rules, crawlingDry: true));
            // Threads it 40 u ahead: worth the crawl.
            Assert.AreEqual(UrchinRailVerdict.Ride,
                UrchinRailAssessment.Decide(S(5f, 40f), S(900f), 600f, Rules, crawlingDry: true));
        }

        [Test]
        public void Decide_ReversingJustToCloseNeedsAMarkedlyBetterRail()
        {
            // Behind closes to 0.6 of range: better than ahead, but not under 0.7^2 = 0.49.
            Assert.AreEqual(UrchinRailVerdict.Leave,
                UrchinRailAssessment.Decide(S(600f, 0f), S(360f), 600f, Rules, false));
            Assert.AreEqual(UrchinRailVerdict.Reverse,
                UrchinRailAssessment.Decide(S(600f, 0f), S(200f), 600f, Rules, false));
        }

        [Test]
        public void Decide_InvalidScansLeave()
        {
            Assert.AreEqual(UrchinRailVerdict.Leave,
                UrchinRailAssessment.Decide(UrchinRailScan.Invalid, UrchinRailScan.Invalid, 600f, Rules, false));
        }
    }
}

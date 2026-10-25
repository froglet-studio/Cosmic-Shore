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

        /// <summary>A CLOSED rail: <c>N</c> points round a circle of radius <c>R</c> in the XZ plane.</summary>
        struct CircleRail : IUrchinRailPoints
        {
            public int N;
            public float R;
            public int Count => N;

            public bool TryGetPoint(int index, out Vector3 point)
            {
                float a = 2f * Mathf.PI * index / N;
                point = new Vector3(Mathf.Cos(a) * R, 0f, Mathf.Sin(a) * R);
                return true;
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

        // ---------------------------------------------------------------- closed rails

        [Test]
        public void Scan_LoopWrapsPastTheSeam()
        {
            var rail = new CircleRail { N = 100, R = 100f };
            rail.TryGetPoint(3, out var ring);   // just past the seam from index 95
            var open = UrchinRailAssessment.Scan(ref rail, 95, +1, ring, 1600f);
            var loop = UrchinRailAssessment.Scan(ref rail, 95, +1, ring, 1600f, loop: true);

            Assert.IsTrue(open.ReachesEnd, "an open walk stops at the list's end");
            Assert.Greater(open.MinDistance, 20f, "...and never sees the ring across the seam");
            Assert.Less(loop.MinDistance, 0.01f, "a loop walk wraps and threads it");
            Assert.AreEqual(8f * 2f * Mathf.PI * 100f / 100f, loop.ArcToMin, 0.5f);
            Assert.IsFalse(loop.ReachesEnd, "a loop has no end to launch off");
        }

        [Test]
        public void Scan_LoopStopsAfterOneLap()
        {
            var rail = new CircleRail { N = 100, R = 100f };
            var scan = UrchinRailAssessment.Scan(ref rail, 10, -1, new Vector3(0f, 500f, 0f), 1e6f, loop: true);
            Assert.IsTrue(scan.Valid);
            Assert.IsFalse(scan.ReachesEnd);
            // 99 segments of a 100-gon: one lap, never a second.
            Assert.Less(scan.ArcWalked, 2f * Mathf.PI * 100f);
            Assert.Greater(scan.ArcWalked, 0.97f * 2f * Mathf.PI * 100f);
        }

        // ---------------------------------------------------------------- dry crawl

        [Test]
        public void IsDryCrawl_OwnColourIsNeverACrawl()
        {
            Assert.IsFalse(UrchinRailAssessment.IsDryCrawl(hostile: false, rideSlowed: false, convertible: true, canAffordSpike: false));
            Assert.IsFalse(UrchinRailAssessment.IsDryCrawl(hostile: false, rideSlowed: false, convertible: false, canAffordSpike: false));
        }

        [Test]
        public void IsDryCrawl_ConvertibleMassIsDryOnlyWithoutAmmo()
        {
            // Skein / Hijack: plain mass - the volley converts it, so a crawl ends while the meter pays.
            Assert.IsFalse(UrchinRailAssessment.IsDryCrawl(true, true, convertible: true, canAffordSpike: true));
            Assert.IsTrue(UrchinRailAssessment.IsDryCrawl(true, true, convertible: true, canAffordSpike: false));
        }

        [Test]
        public void IsDryCrawl_SuperShieldedMassIsAlwaysDry()
        {
            // Regatta: a rival's super-shielded rail refuses every steal - a full meter changes nothing.
            Assert.IsTrue(UrchinRailAssessment.IsDryCrawl(true, true, convertible: false, canAffordSpike: true));
            Assert.IsTrue(UrchinRailAssessment.IsDryCrawl(true, true, convertible: false, canAffordSpike: false));
        }

        [Test]
        public void IsDryCrawl_SlipstreamRidesHostileMassAtFullPace()
        {
            // Time-5 Slipstream: the ride is not slowed, so it is not a crawl at all.
            Assert.IsFalse(UrchinRailAssessment.IsDryCrawl(true, rideSlowed: false, convertible: false, canAffordSpike: false));
        }

        [Test]
        public void Regatta_RivalLaneIsLeftUnlessTheRingIsAShortCrawl()
        {
            // The braid threads every ring on every lane, so "threads ahead" alone would RIDE a rival's
            // lane at crawl speed. As a dry crawl it is left unless the ring is within DryCrawlArc.
            bool dry = UrchinRailAssessment.IsDryCrawl(true, true, convertible: false, canAffordSpike: true);
            Assert.AreEqual(UrchinRailVerdict.Leave,
                UrchinRailAssessment.Decide(S(5f, 600f), S(900f, 4000f), 700f, Rules, dry));
            Assert.AreEqual(UrchinRailVerdict.Ride,
                UrchinRailAssessment.Decide(S(5f, 600f), S(900f, 4000f), 700f, Rules, crawlingDry: false));
        }
    }
}

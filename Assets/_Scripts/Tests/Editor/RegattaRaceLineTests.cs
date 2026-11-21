using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Every hull races its own line (<see cref="RegattaRaceLine"/>, <see cref="RaceLineStyleSO"/>): the gap between two
    /// rails, clear of all three, at the hull's distance from the braid. A synthetic Regatta braid: a closed circular spine,
    /// three lanes 22 u off it at 120 degrees with one twist per lap, as <c>RegattaCourse.BuildRails</c> lays them.
    /// </summary>
    public class RegattaRaceLineTests
    {
        const int N = 720;
        const float SpineRadius = 2000f, LaneOffset = 22f;

        static Vector3 Spine(float u) => new(SpineRadius * Mathf.Cos(u), 0f, SpineRadius * Mathf.Sin(u));

        static List<Vector3> Lane(int lane)
        {
            var points = new List<Vector3>(N);
            for (int i = 0; i < N; i++)
            {
                float u = 2f * Mathf.PI * i / N;
                Vector3 outward = new(Mathf.Cos(u), 0f, Mathf.Sin(u));
                float theta = lane * 2f * Mathf.PI / 3f + u;   // one twist per lap
                points.Add(Spine(u) + (outward * Mathf.Cos(theta) + Vector3.up * Mathf.Sin(theta)) * LaneOffset);
            }
            return points;
        }

        [Test]
        public void GapLine_StaysClearOfEveryRail_AtItsRadius()
        {
            var a = Lane(0); var b = Lane(1); var c = Lane(2);
            var line = RegattaRaceLine.BuildGapLine(a, b, c, 60f);
            Assert.IsNotNull(line);
            // On the gap's bisector at radius r the nearest rail is sqrt(r² + 22² - 22 r) away: 51.6 at r = 60.
            float expected = Mathf.Sqrt(60f * 60f + LaneOffset * LaneOffset - LaneOffset * 60f);
            for (int i = 0; i < line.Count; i += 9)
            {
                float d = Mathf.Min(RegattaRaceLine.DistanceTo(a, line[i]),
                                    Mathf.Min(RegattaRaceLine.DistanceTo(b, line[i]), RegattaRaceLine.DistanceTo(c, line[i])));
                Assert.Greater(d, expected - 3f, $"point {i} comes within {d:F1} u of a rail");
            }
        }

        [Test]
        public void GapLine_SitsItsRadiusFromTheSpine_SoItThreadsEveryMouthWiderThanThat()
        {
            var line = RegattaRaceLine.BuildGapLine(Lane(0), Lane(1), Lane(2), 40f);
            for (int i = 0; i < line.Count; i += 7)
            {
                float u = 2f * Mathf.PI * i / N;
                Assert.AreEqual(40f, (line[i] - Spine(u)).magnitude, 1.5f, $"point {i}");
            }
        }

        [Test]
        public void TheCentreLine_IsTheOneTwentyTwoUnitsFromTheRails()
        {
            // Negative control: radius 0 is the braid's middle, where every rail is 22 u away - what the AI flew before.
            var a = Lane(0);
            var line = RegattaRaceLine.BuildGapLine(a, Lane(1), Lane(2), 0f);
            Assert.AreEqual(LaneOffset, RegattaRaceLine.DistanceTo(a, line[100]), 1.5f);
        }

        [Test]
        public void Nearest_FollowsTheHint_AndPointAhead_WrapsTheLoop()
        {
            var line = RegattaRaceLine.BuildGapLine(Lane(0), Lane(1), Lane(2), 50f);
            int i = line.Nearest(line[300] + Vector3.up * 3f, 290);
            Assert.AreEqual(300, i, 1);
            Assert.AreEqual(300, line.Nearest(line[300], -1), 1, "a global search finds it too");
            float step = (line[1] - line[0]).magnitude;
            Vector3 ahead = line.PointAhead(N - 2, 5f * step);
            Assert.Less((ahead - line[3]).magnitude, 2f * step, "ahead of the last points is the first ones: a closed loop");
        }

        // AimThroughRing (2026-10-25): a pilot steering at a point a look-ahead ahead cuts every bend, so at a ring on a bend
        // the chord to that point can cross the ring's plane outside a small mouth. The test ring is centred ON the line
        // (to isolate the cut from the line's own radius) at point K, with the pilot ON the line Back points before it.
        const int K = 300, Back = 10;
        const float Look = 700f;   // ~40 line points on this braid: a fast hull's look-ahead

        static float ChordOffset(RegattaRaceLine line, Vector3 self, Vector3 aim, Vector3 centre, Vector3 axis)
        {
            float d0 = Vector3.Dot(self - centre, axis), d1 = Vector3.Dot(aim - centre, axis);
            Vector3 hit = Vector3.Lerp(self, aim, d0 / (d0 - d1));
            return (hit - centre).magnitude;
        }

        static (RegattaRaceLine line, Vector3 self, Vector3 centre, Vector3 axis, float cut) RingOnABend()
        {
            var line = RegattaRaceLine.BuildGapLine(Lane(0), Lane(1), Lane(2), 50f);
            Vector3 centre = line[K];
            Vector3 axis = (line[K + 1] - line[K - 1]).normalized;
            Vector3 self = line[K - Back];
            float cut = ChordOffset(line, self, line.PointAhead(K - Back, Look), centre, axis);
            return (line, self, centre, axis, cut);
        }

        [Test]
        public void AimThroughRing_ChordWouldMissTheMouth_AimsThroughTheCrossingPoint()
        {
            var (line, self, centre, axis, cut) = RingOnABend();
            Assert.Greater(cut, 8f, "the bend's chord must cut the ring for this test to mean anything");
            float mouth = cut - 5f;
            Vector3 aim = line.AimThroughRing(K - Back, Look, self, K, centre, axis, mouth);
            Assert.AreEqual(GatePlaneCrossing.InsideMouth, RaceGateRing.PlaneCrossing(self, aim, centre, axis, mouth),
                "the pilot would fly its chord outside the mouth: a miss and a swing round (the 2026-10-25 recordings)");
            Assert.Less(Vector3.Cross((centre - self).normalized, (aim - self).normalized).magnitude, 1e-3f,
                "it aims straight on through the crossing point");
            Assert.AreEqual(Look, (aim - self).magnitude, 0.05f * Look, "still about a look-ahead away, so it steers as gently");
        }

        [Test]
        public void AimThroughRing_ChordThreadsTheMouth_KeepsTheLookahead()
        {
            // Negative control: where the free chord already threads the mouth, the line is not bent.
            var (line, self, centre, axis, cut) = RingOnABend();
            Vector3 aim = line.AimThroughRing(K - Back, Look, self, K, centre, axis, cut + 5f);
            Assert.AreEqual(line.PointAhead(K - Back, Look), aim);
        }

        [Test]
        public void AimThroughRing_RingBeyondTheLookahead_KeepsTheLookahead()
        {
            var line = RegattaRaceLine.BuildGapLine(Lane(0), Lane(1), Lane(2), 50f);
            Vector3 centre = line[K + 200];
            Vector3 axis = (line[K + 201] - line[K + 199]).normalized;
            Vector3 aim = line.AimThroughRing(K, 300f, line[K], K + 200, centre, axis, 1f);
            Assert.AreEqual(line.PointAhead(K, 300f), aim, "a ring far down the line does not bend the aim");
        }

        [Test]
        public void DistanceAlong_WrapsTheLoop_AndStopsAtTheLimit()
        {
            var line = RegattaRaceLine.BuildGapLine(Lane(0), Lane(1), Lane(2), 50f);
            float step = (line[1] - line[0]).magnitude;
            Assert.AreEqual(4f * step, line.DistanceAlong(N - 2, 2), 0.1f * step, "forward past the end is the start");
            Assert.LessOrEqual(line.DistanceAlong(0, N / 2, 10f * step), 11.5f * step, "counting stops past the limit");
            Assert.AreEqual(0f, line.DistanceAlong(7, 7));
        }

        [Test]
        public void Style_LaneRadius_StaysInsideTheMouthLessTheRoom_AndOutsideTheRails()
        {
            var style = ScriptableObject.CreateInstance<RaceLineStyleSO>();
            try
            {
                style.LaneShare = 1f; style.HullClearance = 18f;
                float r = style.LaneRadius(54f, LaneOffset, 0.4f);
                Assert.LessOrEqual(r, 54f - style.ClearanceAt(0.4f) + 1e-3f, "the hull's room is kept from the rim");
                style.LaneShare = 0f;
                Assert.AreEqual(LaneOffset, style.LaneRadius(110f, LaneOffset, 0.5f), 1e-3f);
                style.LaneShare = 0.8f; style.HullClearance = 60f;
                Assert.AreEqual(LaneOffset, style.LaneRadius(54f, LaneOffset, 0f), 1e-3f, "no room: the rails' radius, never inside");
            }
            finally { Object.DestroyImmediate(style); }
        }

        [Test]
        public void Style_Risk_IsSeededPerSeat_AndBoldLooksFurther()
        {
            var style = ScriptableObject.CreateInstance<RaceLineStyleSO>();
            try
            {
                style.Risk = 0.5f; style.RiskSpread = 0.15f;
                Assert.AreEqual(style.RiskFor(42), style.RiskFor(42), "the same seat draws the same risk");
                for (int seat = 0; seat < 50; seat++)
                    Assert.AreEqual(0.5f, style.RiskFor(seat), 0.15f + 1e-4f);
                Assert.Greater(style.Lookahead(600f, 1f), style.Lookahead(600f, 0f), "a bold pilot cuts more of each bend");
                Assert.Greater(style.ClearanceAt(0f), style.ClearanceAt(1f), "a careful pilot keeps more room");
                Assert.AreEqual(style.MinLookahead, style.Lookahead(0f, 0.5f), 1e-3f);
            }
            finally { Object.DestroyImmediate(style); }
        }

        [Test]
        public void EveryRegattaHullButTheSquirrelAndUrchin_HasAStyle()
        {
            foreach (var hull in new[] { VesselClassType.Rhino, VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Stoat })
                Assert.IsNotNull(RaceLineStyleSO.Load(hull, GameModes.Regatta), $"{hull} races its own line");
            Assert.IsNull(RaceLineStyleSO.Load(VesselClassType.Squirrel, GameModes.Regatta), "the Squirrel flies its own pilot");
            Assert.IsNull(RaceLineStyleSO.Load(VesselClassType.Urchin, GameModes.Regatta), "the Urchin rides its own lane");
        }
    }
}

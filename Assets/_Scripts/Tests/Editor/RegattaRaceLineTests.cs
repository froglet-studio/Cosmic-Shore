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

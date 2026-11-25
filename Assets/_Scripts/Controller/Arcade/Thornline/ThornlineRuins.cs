using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thornline's ruins (THORNLINE.md §2a): the garden ruins Spearline lays round its racing line, laid round
    /// Thornline's. The ruins themselves are <see cref="SpearlineRuins"/>' - this class only tells that generator where
    /// Thornline is flown, as the list of gates it reads a line from.
    ///
    /// <para><b>The line.</b> A Shrike does not fly a curve between rings: it slings straight from thorn to thorn. So
    /// the lane list is the thorn chain, every stretch between two gates that share the leg's direction (a straight
    /// line to the generator), with a gate at each thorn and at each leg's ring. The three snipe lines (the thorn before
    /// a snipe bend straight to the thorn after it) are walked too, out along the snipe and back along the two legs, so
    /// the list never leaves the real lines.</para>
    ///
    /// <para><b>Where a ruin stands.</b> The generator lays a ruin on every stretch of at least
    /// <see cref="SpearlineRuins.MinLegLength"/>; every stretch that is to stay bare is cut shorter than that. A ruin
    /// leg is the SECOND half of a thorn leg (its ring to its far thorn). Up to seven legs carry one, the longest
    /// first, never the first leg, never a snipe's two legs, and never a half that passes the cone the pilots start on
    /// (<see cref="SeatMargin"/>). The generator draws a leg's kind from its index, so the bare stretch before each
    /// ruin is cut finer until the index draws a kind the course has not had: no kind twice in the first six, and no
    /// wheel on a half too short to turn one (<see cref="WheelFits"/>).</para>
    ///
    /// <para>Pure: a function of the thorns (themselves a function of the broadcast rings) and the rung, so every peer
    /// lays the same ruins and <c>ThornlineRuinsTests</c> holds it. Decoration: nothing here reads or writes a vessel.</para>
    /// </summary>
    public static class ThornlineRuins
    {
        /// <summary>A stretch that is to stay bare is cut into pieces no longer than this (under the generator's 380).</summary>
        public const float MaxPiece = 360f;

        /// <summary>No ruin half comes this near a line from a start seat to the start thorn: the first sling is
        /// everyone's, at any seat count (the widest ruin, the wheel, plus the lane and some air).</summary>
        public const float SeatMargin = 260f;

        /// <summary>Lines sampled round the start cone, and points along a candidate half, for <see cref="SeatMargin"/>.</summary>
        public const int SeatLines = 72, HalfSamples = 9;

        /// <summary>
        /// The lanes, the ruin legs and the ruins of a thorn chain on a rung. <paramref name="lanes"/> receives the gate
        /// list the generator read (the tests measure clearance against it); <paramref name="ruinLegs"/> the lane index
        /// of each ruin leg in course order (a part's <see cref="SpearlineRuinPart.Leg"/> is one of them).
        /// </summary>
        public static void Lay(IReadOnlyList<Vector3> thorns, int intensity, List<RaceGate> lanes, List<int> ruinLegs,
                               List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels)
        {
            lanes.Clear();
            ruinLegs.Clear();
            parts.Clear();
            wheels?.Clear();
            var ruins = SpearlineRuins.ForIntensity(intensity);
            int legs = thorns.Count - 1;
            if (ruins.PerLap <= 0 || legs < 2) return;
            var course = ThornlineCourse.ForIntensity(intensity);
            course.RingCount = legs;

            var chosen = Choose(thorns, course, ruins.PerLap);
            float thornRadius = ThornlineCourse.HalfDiagonal(course.ThornSize);
            float startRadius = thornRadius * ThornlineCourse.StartThornScale;
            var drawn = new bool[SpearlineRuins.KindCount];
            int drawnCount = 0;
            for (int k = 0; k < legs; k++)
            {
                Vector3 a = thorns[k], b = thorns[k + 1];
                float ra = k == 0 ? startRadius : thornRadius;
                if (k + 2 <= legs && ThornlineCourse.IsSkipBend(k + 1, legs, course.SkipBends))
                {
                    // The snipe from thorn k to thorn k+2, then back along both legs to where the chain goes on.
                    Vector3 far = thorns[k + 2];
                    Bare(lanes, a, far, ra, thornRadius);
                    Bare(lanes, far, b, thornRadius, thornRadius);
                    Bare(lanes, b, a, thornRadius, ra);
                }

                Vector3 dir = RaceCourseGeometry.SafeNormalize(b - a, Vector3.forward);
                Vector3 ring = (a + b) * 0.5f;
                float half = (b - a).magnitude * 0.5f;
                int pieces = Mathf.Max(1, Mathf.CeilToInt(half / MaxPiece));
                lanes.Add(new RaceGate(a, dir, ra));
                if (!chosen[k])
                {
                    Cut(lanes, a, ring, dir, pieces, course.RingRadius);
                    Cut(lanes, ring, b, dir, pieces, thornRadius);
                    continue;
                }
                // The ring's gate opens the ruin leg, and the generator draws a leg's kind from its index: cut the bare
                // half before it one piece finer until that index draws a kind this course has not had (and never a
                // wheel on a half too short to turn one). Seven indices in a row draw all seven kinds.
                bool wheelFits = WheelFits(half, Mathf.Max(course.RingRadius, thornRadius));
                if (drawnCount >= (wheelFits ? SpearlineRuins.KindCount : SpearlineRuins.KindCount - 1))
                {
                    System.Array.Clear(drawn, 0, drawn.Length);
                    drawnCount = 0;
                }
                int ringIndex = lanes.Count - 1 + pieces;
                int kind = SpearlineRuins.KindOf(ringIndex, lanes[0].Position);
                while (drawn[kind] || (kind == SpearlineRuins.Wheel && !wheelFits))
                {
                    pieces++;
                    kind = SpearlineRuins.KindOf(++ringIndex, lanes[0].Position);
                }
                drawn[kind] = true;
                drawnCount++;
                Cut(lanes, a, ring, dir, pieces, course.RingRadius);
                ruinLegs.Add(lanes.Count - 1);
                lanes.Add(new RaceGate(b, dir, thornRadius));
            }
            SpearlineRuins.Lay(lanes, ruins, parts, wheels);
        }

        /// <summary>Is a ruin half of this length long enough to turn a wheel clear of its ring and its thorn? The wheel
        /// sits within 6% of the half's middle and wants its radius and the ring margin on both sides. On Thornline's
        /// legs (1,150 u at most) that is rare: the wheel is the one kind most courses do not have.</summary>
        public static bool WheelFits(float half, float gateRadius) =>
            half * 0.44f - gateRadius >= SpearlineRuins.RingMargin + SpearlineRuins.WheelRadius;

        /// <summary>Which ruin of the course (0, 1, 2... in course order) a part's leg is: what its colour is drawn by.</summary>
        public static int Ordinal(List<int> ruinLegs, int leg) => Mathf.Max(0, ruinLegs.IndexOf(leg));

        /// <summary>May leg <paramref name="k"/> (thorn k to thorn k+1) carry a ruin on its second half?</summary>
        public static bool Eligible(IReadOnlyList<Vector3> thorns, in ThornlineSettings course, int k)
        {
            int legs = thorns.Count - 1;
            if (k <= 0 || k >= legs) return false;
            if (ThornlineCourse.IsSkipBend(k, legs, course.SkipBends) || ThornlineCourse.IsSkipBend(k + 1, legs, course.SkipBends)) return false;
            Vector3 a = thorns[k], b = thorns[k + 1];
            if ((b - a).magnitude * 0.5f < SpearlineRuins.MinLegLength + 1f) return false;
            Vector3 ring = (a + b) * 0.5f;
            for (int i = 0; i < HalfSamples; i++)
                if (SeatDistance(thorns[0], Vector3.Lerp(ring, b, i / (HalfSamples - 1f))) < SeatMargin) return false;
            return true;
        }

        /// <summary>How near <paramref name="p"/> is to the cone of seat lines under the start thorn (sampled).</summary>
        public static float SeatDistance(Vector3 startThorn, Vector3 p)
        {
            float dep = ThornlineCourse.StartSeatDepression * Mathf.Deg2Rad;
            float radial = ThornlineCourse.StartSeatDistance * Mathf.Cos(dep), drop = ThornlineCourse.StartSeatDistance * Mathf.Sin(dep);
            float best = float.MaxValue;
            for (int i = 0; i < SeatLines; i++)
            {
                float az = 2f * Mathf.PI * i / SeatLines;
                Vector3 seat = startThorn + new Vector3(Mathf.Cos(az) * radial, -drop, Mathf.Sin(az) * radial);
                best = Mathf.Min(best, ThornlineCourse.DistanceToSegment(p, startThorn, seat));
            }
            return best;
        }

        /// <summary>The legs that carry a ruin: the eligible ones, the longest first, <paramref name="most"/> at most.</summary>
        static bool[] Choose(IReadOnlyList<Vector3> thorns, in ThornlineSettings course, int most)
        {
            int legs = thorns.Count - 1;
            var chosen = new bool[legs];
            for (int n = 0; n < most; n++)
            {
                int best = -1;
                float bestLength = 0f;
                for (int k = 0; k < legs; k++)
                {
                    if (chosen[k] || !Eligible(thorns, course, k)) continue;
                    float length = (thorns[k + 1] - thorns[k]).magnitude;
                    if (length > bestLength) { bestLength = length; best = k; }
                }
                if (best < 0) break;
                chosen[best] = true;
            }
            return chosen;
        }

        /// <summary>A whole bare stretch in a direction of its own: its opening gate, then <see cref="Cut"/>.</summary>
        static void Bare(List<RaceGate> lanes, Vector3 from, Vector3 to, float fromRadius, float toRadius)
        {
            Vector3 dir = RaceCourseGeometry.SafeNormalize(to - from, Vector3.forward);
            lanes.Add(new RaceGate(from, dir, fromRadius));
            Cut(lanes, from, to, dir, Mathf.Max(1, Mathf.CeilToInt((to - from).magnitude / MaxPiece)), toRadius);
        }

        /// <summary>From the gate already at <paramref name="from"/> to one at <paramref name="to"/>, in
        /// <paramref name="pieces"/> equal pieces: waypoints of no radius, then the end gate.</summary>
        static void Cut(List<RaceGate> lanes, Vector3 from, Vector3 to, Vector3 dir, int pieces, float endRadius)
        {
            for (int i = 1; i < pieces; i++)
                lanes.Add(new RaceGate(Vector3.Lerp(from, to, i / (float)pieces), dir, 0f));
            lanes.Add(new RaceGate(to, dir, endRadius));
        }
    }
}

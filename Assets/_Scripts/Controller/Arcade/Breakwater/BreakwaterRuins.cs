using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Breakwater's garden ruins (BREAKWATER.md "The ruins"): the SAME ruins Spearline lays round its racing line at I4
    /// (<see cref="SpearlineRuins"/>: a clear lane down the flown line, nothing at a ring), laid from Breakwater's
    /// circuit. This file adds no ruin of its own. It states Breakwater's clearances and decides which of those prisms
    /// Breakwater leaves out, because this course carries three things Spearline's does not.
    ///
    /// <list type="bullet">
    /// <item><b>A station at every ring.</b> The dish reaches well past the port, so the margin round a ring is the
    /// station's own reach (<see cref="BreakwaterCourse.StationReach"/>) and a little more.</item>
    /// <item><b>A start gate off the circuit.</b> The generator is handed the circuit alone, so its closing leg is the
    /// real one; the leg from the start gate into the circuit, and the run from every spawn pad up to the start gate,
    /// are kept clear here.</item>
    /// <item><b>Short legs.</b> 300 to 380 u at I4, with a station at each end, so a ruin stands on every OTHER leg and
    /// the course keeps open water between them. Seven legs of the thirteen, which is each of the seven kinds once.</item>
    /// </list>
    ///
    /// <para>Decoration only (Yash, 2026-11-24). Pure: a function of the course and the rung, so every peer lays the
    /// same ruins from the broadcast course (<c>BreakwaterRuinsTests</c>). Nothing here touches a vessel or a station.</para>
    /// </summary>
    public static class BreakwaterRuins
    {
        /// <summary>The clear lane's radius (u): wider than the I4 port (42), so the lane a station allows stays open.</summary>
        public const float ClearRadius = 60f;

        /// <summary>Breakwater's I4 legs are 300 to 380 u (<see cref="BreakwaterCourseSettings.ForIntensity"/>).</summary>
        public const float MinLegLength = 300f;

        /// <summary>u a ruin prism keeps off a station's reach, so the dish reads as its own thing.</summary>
        public const float StationKeep = 10f;

        /// <summary>u a ruin prism keeps off a spawn pad and off the straight run from it up to the start gate.</summary>
        public const float PadKeep = 80f;

        /// <summary>Every leg long enough is offered to the generator; the odd legs then leave theirs out.</summary>
        public const int LegBudget = 32;

        /// <summary>No ruin prism stands within this distance (u) of a port's rim: the station's reach past its port,
        /// and <see cref="StationKeep"/>.</summary>
        public static float RingMargin(int intensity)
        {
            float port = BreakwaterCourseSettings.PortRadiusForIntensity(intensity);
            return BreakwaterCourse.StationReach(port) - port + StationKeep;
        }

        /// <summary>Spearline's rung with Breakwater's clearances. No ruins below I4.</summary>
        public static SpearlineRuinSettings Settings(int intensity)
        {
            var s = SpearlineRuins.ForIntensity(intensity).With(ClearRadius, MinLegLength, RingMargin(intensity));
            if (s.PerLap > 0) s.PerLap = LegBudget;
            return s;
        }

        /// <summary>
        /// Every ruin prism of <paramref name="course"/> on this rung into <paramref name="parts"/>, the turning ones
        /// named in <paramref name="wheels"/>, and for each part which ruin it belongs to (<paramref name="ruin"/>, the
        /// same length as <paramref name="parts"/>): 0, 1, 2... in the order the ruins stand round the circuit, or -1
        /// where Breakwater leaves the part out. <paramref name="lap"/> receives the circuit the ruins were laid on
        /// (the course without its first <paramref name="leadIn"/> gates); a part's <c>Leg</c> indexes it. The lists
        /// are cleared first. A part that is left out keeps its place, so a wheel's <c>First</c> and <c>Count</c> stay
        /// true.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> course, int leadIn, int intensity, IReadOnlyList<Vector3> pads,
                               List<RaceGate> lap, List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels,
                               List<int> ruin)
        {
            lap.Clear();
            parts.Clear();
            wheels.Clear();
            ruin.Clear();
            if (course == null) return;
            leadIn = Mathf.Clamp(leadIn, 0, course.Count);
            for (int i = leadIn; i < course.Count; i++) lap.Add(course[i]);
            if (lap.Count < 3) return;
            var settings = Settings(intensity);
            if (settings.PerLap <= 0) return;
            SpearlineRuins.Lay(lap, settings, parts, wheels);

            for (int i = 0; i < parts.Count; i++)
                ruin.Add(OnBareLeg(parts[i].Leg) || ReachesTheStart(parts[i], course, leadIn, pads, settings, 0f) ? -1 : 0);

            // A wheel turns whole or not at all, and never through the way in from the pads.
            for (int w = 0; w < wheels.Count; w++)
            {
                var wheel = wheels[w];
                int end = Mathf.Min(wheel.First + wheel.Count, parts.Count);
                bool turnsFree = true;
                for (int i = wheel.First; i < end && turnsFree; i++)
                    turnsFree = ruin[i] >= 0 &&
                                !ReachesTheStart(parts[i], course, leadIn, pads, settings, SpearlineRuins.WheelRadius);
                if (turnsFree) continue;
                for (int i = wheel.First; i < end; i++) ruin[i] = -1;
            }

            // Number the ruins that stand, one to a leg, in course order.
            int ordinal = -1, leg = -1;
            for (int i = 0; i < parts.Count; i++)
            {
                if (ruin[i] < 0) continue;
                if (parts[i].Leg != leg)
                {
                    leg = parts[i].Leg;
                    ordinal++;
                }
                ruin[i] = ordinal;
            }
        }

        /// <summary>The odd legs of the circuit carry no ruin: open water between one ruin and the next.</summary>
        public static bool OnBareLeg(int leg) => (leg & 1) == 1;

        /// <summary>Does <paramref name="part"/> (and <paramref name="sweep"/>, a turning part's reach) come into the
        /// way in: the lane of a lead-in leg, the margin of a lead-in gate, or within <see cref="PadKeep"/> of a spawn
        /// pad or of the straight run from a pad up to the first gate?</summary>
        public static bool ReachesTheStart(in SpearlineRuinPart part, IReadOnlyList<RaceGate> course, int leadIn,
                                           IReadOnlyList<Vector3> pads, in SpearlineRuinSettings s, float sweep)
        {
            if (course == null || course.Count == 0) return false;
            float half = 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
            int samples = 1 + Mathf.CeilToInt(part.Scale.z / 10f);
            for (int n = 0; n < samples; n++)
            {
                float along = samples == 1 ? 0f : (n / (float)(samples - 1) - 0.5f) * part.Scale.z;
                Vector3 p = part.Position + part.Forward * along;
                for (int g = 0; g < leadIn && g < course.Count; g++)
                {
                    if ((p - course[g].Position).magnitude - course[g].Radius < s.RingMargin + half + sweep) return true;
                    if (g + 1 < course.Count && LegDistance(course[g], course[g + 1], p) < s.ClearRadius + half + sweep)
                        return true;
                }
                if (pads == null) continue;
                Vector3 first = course[0].Position;
                for (int k = 0; k < pads.Count; k++)
                    if (SegmentDistance(p, pads[k], first) < PadKeep + half + sweep) return true;
            }
            return false;
        }

        /// <summary>How many prisms of <paramref name="ruin"/> (as <see cref="Lay"/> numbered them) each colour slot
        /// gets when the ruins take the <paramref name="slots"/> colours in the fairest order: each ruin, in course
        /// order, goes to the slot holding the fewest prisms so far (the earliest on a tie). Breakwater's own structure
        /// is one colour so that every pilot's rounds earn the same ammunition on it; the ruins are in the pilots'
        /// colours, so their prisms are shared out as evenly as whole ruins allow. <paramref name="slotOf"/> receives
        /// the slot of ruin 0, 1, 2...</summary>
        public static void ShareOut(IReadOnlyList<int> ruin, int slots, List<int> slotOf, List<int> prismsInSlot)
        {
            slotOf.Clear();
            prismsInSlot.Clear();
            if (slots <= 0) return;
            for (int k = 0; k < slots; k++) prismsInSlot.Add(0);
            int i = 0;
            while (i < ruin.Count)
            {
                if (ruin[i] < 0) { i++; continue; }
                int ordinal = ruin[i], count = 0;
                for (int j = i; j < ruin.Count; j++) if (ruin[j] == ordinal) count++;
                int best = 0;
                for (int k = 1; k < slots; k++) if (prismsInSlot[k] < prismsInSlot[best]) best = k;
                while (slotOf.Count <= ordinal) slotOf.Add(0);
                slotOf[ordinal] = best;
                prismsInSlot[best] += count;
                while (i < ruin.Count && (ruin[i] == ordinal || ruin[i] < 0)) i++;
            }
        }

        /// <summary>How far <paramref name="p"/> is from the flown line from <paramref name="a"/> to
        /// <paramref name="b"/> (the generator's own line, <see cref="SpearlineRuins.LinePoint"/>).</summary>
        static float LegDistance(in RaceGate a, in RaceGate b, Vector3 p)
        {
            float best = float.MaxValue;
            Vector3 prev = a.Position;
            for (int k = 1; k <= SpearlineRuins.LineSamples; k++)
            {
                Vector3 cur = SpearlineRuins.LinePoint(a, b, k / (float)SpearlineRuins.LineSamples);
                best = Mathf.Min(best, SegmentDistance(p, prev, cur));
                prev = cur;
            }
            return best;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}

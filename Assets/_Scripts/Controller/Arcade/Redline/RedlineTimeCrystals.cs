using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where Redline's Time crystals sit on a lap: a pure function of the BROADCAST course, so every peer lays the
    /// same field (<see cref="RedlineController"/>) and the offline model races the same one
    /// (<c>Tools/Build/manta_redline_sim.py</c>, held to this file by <c>Tools/Build/redline_course_harness</c>).
    ///
    /// <para><b>Time only</b> (Yash, 2026-11-04: "no other elemental abilities needed, only time"). A Time crystal is
    /// one Time level, and on the Manta a Time level is Soar's top speed (+3% a level, x1.3 at level 10: the
    /// ability map's <c>BoostSpeedMultiplier</c>). So the crystals are the Time ability's own risk and reward: more
    /// speed down the next leg, and a corner that now needs more lift to make.</para>
    ///
    /// <code>
    ///   I1  none                  the lesson is Soar itself: bury both triggers
    ///   I2  1, on the line        halfway down the leg INTO the lap's costing corner: free speed, and the corner
    ///                             after it is a little harder
    ///   I3  2, on the line        the same for its two costing corners
    ///   I4  3, inside the apexes  a quarter of the way out of each costing corner, inset toward the inside of
    ///                             the turn: taking one is a tighter exit, so less Soar - greed against the line
    /// </code>
    ///
    /// <para>A lap's costing corners are its tightest (<see cref="RaceCourseGeometry.CornerRadius"/>), the ones
    /// <see cref="RedlineCourse.ForIntensity"/> cuts inside the full-Soar circle; ties keep the lower index.</para>
    /// </summary>
    public static class RedlineTimeCrystals
    {
        /// <summary>Time crystals a lap at intensity 1-4.</summary>
        public static readonly int[] PerLapByIntensity = { 0, 1, 2, 3 };

        /// <summary>I2-I3: how far down the leg into the corner a crystal sits (0 = the gate before, 1 = the corner).</summary>
        public const float OnLineFraction = 0.5f;

        /// <summary>I4: how far down the leg OUT of the corner a crystal sits.</summary>
        public const float ApexAlongExit = 0.25f;

        /// <summary>I4: how far toward the inside of the turn a crystal is set off that leg (u).</summary>
        public const float ApexInset = 70f;

        public static int PerLap(int intensity) =>
            PerLapByIntensity[Mathf.Clamp(intensity, 1, PerLapByIntensity.Length) - 1];

        /// <summary>The Time crystals of one lap of <paramref name="course"/> at <paramref name="intensity"/>, in the
        /// course's own space, tightest corner first. Clears <paramref name="into"/>.</summary>
        public static void Place(IReadOnlyList<RaceGate> course, int intensity, List<Vector3> into)
        {
            into.Clear();
            int n = course?.Count ?? 0;
            if (n < 3) return;
            int count = Mathf.Min(PerLap(intensity), n);
            if (count <= 0) return;

            var order = new List<int>(n);
            var radius = new float[n];
            for (int i = 0; i < n; i++)
            {
                order.Add(i);
                radius[i] = RaceCourseGeometry.CornerRadius(course[(i - 1 + n) % n].Position, course[i].Position,
                                                            course[(i + 1) % n].Position);
            }
            order.Sort((a, b) => radius[a] != radius[b] ? radius[a].CompareTo(radius[b]) : a.CompareTo(b));

            for (int k = 0; k < count; k++)
            {
                int c = order[k];
                Vector3 prev = course[(c - 1 + n) % n].Position, at = course[c].Position, next = course[(c + 1) % n].Position;
                into.Add(intensity >= 4 ? Apex(prev, at, next) : Vector3.Lerp(prev, at, OnLineFraction));
            }
        }

        /// <summary>A quarter of the way out of the corner at <paramref name="at"/>, set <see cref="ApexInset"/> toward
        /// the inside of the turn (the side the gates before and after lie on).</summary>
        public static Vector3 Apex(Vector3 prev, Vector3 at, Vector3 next)
        {
            Vector3 exit = next - at;
            Vector3 along = at + exit * ApexAlongExit;
            Vector3 inside = 0.5f * (prev + next) - at;
            Vector3 dir = exit.sqrMagnitude > 1e-6f ? exit.normalized : Vector3.forward;
            inside -= dir * Vector3.Dot(inside, dir);
            return inside.sqrMagnitude > 1e-6f ? along + inside.normalized * ApexInset : along;
        }
    }
}

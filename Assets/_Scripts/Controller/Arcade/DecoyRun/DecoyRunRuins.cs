using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Decoy Run's garden ruins (DECOY_RUN.md §2a): Spearline's ruins (<see cref="SpearlineRuins"/>, called, never
    /// copied) standing round the Jacks' racing line on the hardest rung. Decoration (Yash, 2026-11-24).
    ///
    /// <para><b>The racing line here is straight.</b> Spearline's pilot flies a curve from ring to ring; a Jacks hull
    /// ZIPS in straight lines (the travel to a decoy) and its jacks run straight on the board. So this hands the
    /// generator the line as pairs of gates that share one axis, which its curve draws as a straight leg:
    /// every leg from ring to ring, and for a hairpin the second zip's own line, which leaves from the first decoy
    /// (<see cref="DecoyRunCourse.Overshoot"/> past the skewer), not from the ring.</para>
    ///
    /// <para>Pure: a function of the broadcast rings and the rung, so every peer lays the same ruins. Nothing here
    /// touches the vessel.</para>
    /// </summary>
    public static class DecoyRunRuins
    {
        /// <summary>The rung's ruins: Spearline's own ladder (the hardest rung only) and its clear lane.</summary>
        public static SpearlineRuinSettings ForIntensity(int intensity) => SpearlineRuins.ForIntensity(intensity);

        /// <summary>Leg <paramref name="leg"/> (ring <paramref name="leg"/> to the next) is a HAIRPIN: the second zip of
        /// a group, flown from the first decoy and not from the ring.</summary>
        public static bool IsHairpin(int leg, in DecoyRunSettings s) =>
            s.Group >= 3 && !DecoyRunCourse.StartsGroup(leg, s) && !DecoyRunCourse.StartsGroup(leg + 1, s);

        /// <summary>Where the flown line of leg <paramref name="leg"/> starts: its ring, or the first decoy for a hairpin.</summary>
        public static Vector3 LegStart(IReadOnlyList<RaceGate> rings, int leg, in DecoyRunSettings s) =>
            IsHairpin(leg, s) ? rings[leg].Position + rings[leg].Axis * DecoyRunCourse.Overshoot : rings[leg].Position;

        /// <summary>
        /// The racing line as the gate list the generator reads: two gates a leg, both facing down it, so gates
        /// 2k and 2k+1 are the straight flown line of the leg from ring k to ring k+1. The gates between two legs
        /// (2k+1 to 2k+2) are a ring apart at most and carry nothing.
        /// </summary>
        public static void RacingLine(IReadOnlyList<RaceGate> rings, in DecoyRunSettings s, List<RaceGate> line)
        {
            line.Clear();
            if (rings == null) return;
            for (int leg = 0; leg + 1 < rings.Count; leg++)
            {
                Vector3 from = LegStart(rings, leg, s), to = rings[leg + 1].Position;
                Vector3 dir = RaceCourseGeometry.SafeNormalize(to - from, rings[leg + 1].Axis);
                line.Add(new RaceGate(from, dir, rings[leg].Radius));
                line.Add(new RaceGate(to, dir, rings[leg + 1].Radius));
            }
        }

        /// <summary>The course leg (ring to ring) a ruin prism stands on.</summary>
        public static int CourseLeg(in SpearlineRuinPart part) => part.Leg / 2;

        /// <summary>
        /// Every ruin prism of the course on this rung, appended to <paramref name="into"/>; <paramref name="line"/>
        /// is scratch (it holds the racing line after). A turning ruin is left out whole: Decoy Run's legs are too
        /// short for one to clear its rings, and the mode poses nothing every frame.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> rings, in DecoyRunSettings s, int intensity,
                               List<RaceGate> line, List<SpearlineRuinPart> into)
        {
            RacingLine(rings, s, line);
            var wheels = new List<SpearlineRuinWheel>();
            int first = into.Count;
            SpearlineRuins.Lay(line, ForIntensity(intensity), into, wheels);
            for (int w = wheels.Count - 1; w >= 0; w--)
                into.RemoveRange(first + wheels[w].First, wheels[w].Count);
        }
    }
}

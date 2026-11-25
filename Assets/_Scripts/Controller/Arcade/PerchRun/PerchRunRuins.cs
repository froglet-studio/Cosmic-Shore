using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Perch Run's garden ruins at I4 (PERCH_RUN.md §3a): Spearline's ruins (<see cref="SpearlineRuins"/>, called, not
    /// copied) laid round the lines the GOBY flies. Decoration: plain prisms with the game's own rules.
    ///
    /// <para><b>Why a lap of its own.</b> Spearline keeps its lane clear round a curve that leaves and arrives along
    /// the rings' axes. The Goby does not fly a curve: it DARTS, straight from ring to ring, and a dart stops on the
    /// first block on its line. So the ruins are laid round <see cref="DartLap"/>: every leg of the course as a
    /// straight line (each ring stands in it twice, once ending its leg in and once starting its leg out, both on the
    /// leg's own direction), with the run from the start line to ring 0 ahead of them. Lap leg 0 is the start
    /// run, the even legs from 2 are the course's legs, and the odd legs have no length (two gates on one ring).</para>
    ///
    /// <para>What that gives at I4: ruins only on the legs of 380 u or more, which are the hairpins' legs out, the
    /// blocked legs and the first leg; never on a double or a hairpin's short leg in. The 60 u clear lane is wider
    /// than the I4 mouth (54), so a centre dart and a rim shot both pass the six standing kinds. The wheel, the one
    /// kind that sweeps the line, never fits: it needs 270 u from its hub to every ring, a leg of about 650 u, and the
    /// longest here is 560, so its turn goes to a pergola and no dart ever meets a ruin on its line.</para>
    ///
    /// <para>Pure: a function of the broadcast rings and the rung, so every peer lays the same ruins, and
    /// <c>PerchRunRuinsTests</c> holds it offline (Tools/Build/perch_run_harness/run.sh).</para>
    /// </summary>
    public static class PerchRunRuins
    {
        /// <summary>The lap leg that is course leg <paramref name="courseLeg"/> (ring k to ring k + 1).</summary>
        public static int LapLeg(int courseLeg) => 2 * courseLeg + 2;

        /// <summary>The course leg (ring k to ring k + 1) a ruin part's <see cref="SpearlineRuinPart.Leg"/> names.</summary>
        public static int CourseLeg(int lapLeg) => (lapLeg - 2) / 2;

        /// <summary>
        /// The straight lines the Goby darts, as the gates <see cref="SpearlineRuins.Lay"/> reads: the start line, ring
        /// 0 on the start run's direction, then for every leg its two rings on the leg's own direction.
        /// </summary>
        public static void DartLap(IReadOnlyList<RaceGate> rings, List<RaceGate> lap)
        {
            lap.Clear();
            if (rings == null || rings.Count < 2) return;
            var first = rings[0];
            lap.Add(new RaceGate(first.Position - first.Axis * PerchRunCourse.StartStandoff, first.Axis, first.Radius));
            lap.Add(first);
            for (int k = 0; k + 1 < rings.Count; k++)
            {
                var a = rings[k];
                var b = rings[k + 1];
                Vector3 line = RaceCourseGeometry.SafeNormalize(b.Position - a.Position, b.Axis);
                lap.Add(new RaceGate(a.Position, line, a.Radius));
                lap.Add(new RaceGate(b.Position, line, b.Radius));
            }
        }

        /// <summary>
        /// Every ruin prism of the course on this rung (none below I4), into <paramref name="parts"/>.
        /// <paramref name="lap"/> is filled with the dart lap they were laid round. <paramref name="wheels"/> (may be
        /// null) names any turning ones: none on this course, which the tests hold.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> rings, int intensity, List<RaceGate> lap,
                               List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels)
        {
            parts.Clear();
            wheels?.Clear();
            DartLap(rings, lap);
            if (lap.Count == 0) return;
            SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(intensity), parts, wheels);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Breach's garden ruins (BREACH.md §3a): the SAME ruins Spearline lays round its racing line at I4
    /// (<see cref="SpearlineRuins"/>: a clear lane down the flown line, nothing at a ring), laid from Breach's rings.
    /// This file adds no ruin of its own. It states Breach's clearances (its legs are shorter than Spearline's) and
    /// decides which of those prisms Breach leaves out, because this course carries something Spearline's does not:
    /// the WALLS.
    ///
    /// <para><b>A leg into a walled ring is left bare.</b> The hull does not fly that leg's line: it swings out round
    /// the wall's edge (<see cref="BreachCourse.RoundPoint"/>), past the ruins' clear lane, so a ruin there would stand
    /// in the dog-leg. And no ruin prism of any leg stands within <see cref="WallKeep"/> of a wall. A hard-turn leg is
    /// shorter than <see cref="MinLegLength"/>, so the generator gives it none.</para>
    ///
    /// <para>Decoration only (Yash, 2026-11-24). Pure: a function of the rings and the rung, so every peer lays the
    /// same ruins from the broadcast course (<c>BreachRuinsTests</c>). Nothing here touches a vessel or a wall.</para>
    /// </summary>
    public static class BreachRuins
    {
        /// <summary>The clear lane's radius (u): wider than the I4 ring (54) and than Spearline's lane (60), for a
        /// heavy hull whose burning turn runs wide.</summary>
        public const float ClearRadius = 70f;

        /// <summary>Breach's ordinary legs are 300 to 380 u; its hard-turn legs (160 to 220 u) stay bare.</summary>
        public const float MinLegLength = 300f;

        /// <summary>No ruin prism stands within this distance (u) of a ring's mouth.</summary>
        public const float RingMargin = 100f;

        /// <summary>u a ruin prism keeps off a wall's disc, so the wall reads as its own thing.</summary>
        public const float WallKeep = 20f;

        /// <summary>Every leg long enough may carry a ruin; the walled legs then leave theirs out.</summary>
        public const int LegBudget = 16;

        /// <summary>Spearline's rung with Breach's clearances. No ruins below I4.</summary>
        public static SpearlineRuinSettings Settings(int intensity)
        {
            var s = SpearlineRuins.ForIntensity(intensity).With(ClearRadius, MinLegLength, RingMargin);
            if (s.PerLap > 0) s.PerLap = LegBudget;
            return s;
        }

        /// <summary>
        /// Every ruin prism of <paramref name="rings"/> on this rung into <paramref name="parts"/>, the turning ones
        /// named in <paramref name="wheels"/>, and for each part which ruin it belongs to (<paramref name="ruin"/>,
        /// the same length as <paramref name="parts"/>): 0, 1, 2... in the order the ruins stand round the course, or
        /// -1 where Breach leaves the part out. The lists are cleared first. A part that is left out keeps its place,
        /// so a wheel's <c>First</c> and <c>Count</c> stay true.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> rings, int intensity, List<SpearlineRuinPart> parts,
                               List<SpearlineRuinWheel> wheels, List<int> ruin)
        {
            parts.Clear();
            wheels.Clear();
            ruin.Clear();
            if (rings == null || rings.Count < 3) return;
            var settings = Settings(intensity);
            if (settings.PerLap <= 0) return;
            SpearlineRuins.Lay(rings, settings, parts, wheels);

            var course = BreachCourse.ForIntensity(intensity);
            for (int i = 0; i < parts.Count; i++)
                ruin.Add(IntoWall(parts[i].Leg, course) || ReachesWall(parts[i], rings, course, 0f) ? -1 : 0);

            // A wheel turns whole or not at all, and never through a wall.
            for (int w = 0; w < wheels.Count; w++)
            {
                var wheel = wheels[w];
                int end = Mathf.Min(wheel.First + wheel.Count, parts.Count);
                bool turnsFree = true;
                for (int i = wheel.First; i < end && turnsFree; i++)
                    turnsFree = ruin[i] >= 0 && !ReachesWall(parts[i], rings, course, SpearlineRuins.WheelRadius);
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

        /// <summary>Leg <paramref name="leg"/> (ring <paramref name="leg"/> to the next) runs into a walled ring.</summary>
        public static bool IntoWall(int leg, in BreachSettings course) => BreachCourse.IsWalled(leg + 1, course);

        /// <summary>Does <paramref name="part"/> come within <see cref="WallKeep"/> (and <paramref name="sweep"/>, a
        /// turning part's reach) of any wall's disc, anywhere along its length? The disc is taken as a ball.</summary>
        public static bool ReachesWall(in SpearlineRuinPart part, IReadOnlyList<RaceGate> rings, in BreachSettings course,
                                       float sweep)
        {
            float half = 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
            int samples = 1 + Mathf.CeilToInt(part.Scale.z / 10f);
            for (int k = 2; k < rings.Count; k++)
            {
                if (!BreachCourse.IsWalled(k, course)) continue;
                Vector3 centre = BreachCourse.WallCentre(rings, k, course);
                float reach = BreachCourse.WallRadius(rings[k], course) + WallKeep + half + sweep;
                for (int n = 0; n < samples; n++)
                {
                    float along = (n / (float)(samples - 1) - 0.5f) * part.Scale.z;
                    if ((part.Position + part.Forward * along - centre).magnitude < reach) return true;
                }
            }
            return false;
        }
    }
}

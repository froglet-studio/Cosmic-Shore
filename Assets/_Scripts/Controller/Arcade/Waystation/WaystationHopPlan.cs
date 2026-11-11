using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How a Drop Tower hop landed, against its lanterns (Lantern Towers, Docs/Studios/TIGER_MOTH_TIME.md §6).</summary>
    public enum HopGrade
    {
        /// <summary>Short of the window: on or in front of the floor plate.</summary>
        Short = 0,

        /// <summary>Inside the lanterns, above the deep band: safe, slower.</summary>
        Clean = 1,

        /// <summary>In the deep band at the lanterns' foot, or just past it within the reaction margin: the fast landing.</summary>
        Deep = 2,

        /// <summary>Past the shelf by more than the reaction and the mouth allow: the next ring is out of reach.</summary>
        Past = 3,
    }

    /// <summary>
    /// The one plan the local pilot's grading, the autopilot and the tests share: where a hop should land, how a landing
    /// grades, and the streak a run of deep landings builds. Pure (no <c>Time</c>, no scene, no random), so every number
    /// it gives is the same on every peer and in <c>Tools/Build/waystation_tower_course.py</c>, which mirrors it.
    /// </summary>
    public static class WaystationHopPlan
    {
        /// <summary>Streak lengths at which the whole tower lights for the local pilot.</summary>
        public const int TierOne = 3, TierTwo = 6, TierThree = 10;

        /// <summary>How far short of the crown gem's centre (u) the autopilot lands on a plunge, so it flies through it.</summary>
        public const float AiGemShort = 10f;

        /// <summary>The autopilot's target never lies nearer the plate or the hard far edge than this (u): a 30 fps
        /// frame of fall released on the nearest frame (15), the carry a slowed or quickened hull lands with (8), and 2
        /// spare. <c>waystation_tower_course.py</c>'s `aibudget` check holds it on every hop of the sweep.</summary>
        public const float AiSafety = 25f;

        /// <summary>Grade a landing <paramref name="depth"/> u under its press point, on a hop whose shelf is
        /// <paramref name="shelf"/> (<see cref="WaystationTower.ShelfDepth"/>) and whose hard far edge is
        /// <paramref name="farEdge"/> (<see cref="WaystationTower.FarEdge"/>). Deep runs from the deep band's top to the
        /// far edge: a landing past the shelf that still threads spent the reaction margin, and that is the fast line.</summary>
        public static HopGrade Grade(float depth, float shelf, float farEdge, in WaystationTowerSettings s)
        {
            if (depth < s.WindowLow) return HopGrade.Short;
            if (depth > farEdge) return HopGrade.Past;
            float lanternEnd = shelf - WaystationTower.LanternBack;
            return depth >= lanternEnd - s.DeepBand ? HopGrade.Deep : HopGrade.Clean;
        }

        /// <summary>Grade a landing on tower ring <paramref name="i"/> of the broadcast course. A plunge grades against its
        /// own deepest landing (the window's far edge past the plunge) and is never Past.</summary>
        public static HopGrade GradeOn(IReadOnlyList<RaceGate> course, int i, float depth, in WaystationTowerSettings s)
        {
            if (WaystationTower.IsPlunge(course, i, s))
            {
                float plunge = WaystationTower.PlungeOf(course, i, s);
                return Grade(depth - plunge, s.WindowHigh, float.PositiveInfinity, s);
            }
            return Grade(depth, WaystationTower.ShelfDepth(course, i, s), WaystationTower.FarEdge(course, i, s), s);
        }

        /// <summary>The autopilot's landing error (u) for seat <paramref name="seat"/> on ring <paramref name="gate"/>:
        /// hashed, so the same on every peer and every run, and different seat to seat.</summary>
        public static float AiReleaseError(int seat, int gate, in WaystationTowerSettings s) =>
            (WaystationTower.Hash01(seat + 1, gate, 4243) * 2f - 1f) * s.AiErrorU;

        /// <summary>
        /// Where the autopilot aims to land on tower ring <paramref name="i"/> (u under its press point): the middle of
        /// the deep band plus its seat's error on a hop with lanterns; on a plunge, just short of the crown gem when
        /// there is one, else the window's centre past the plunge (as before the lanterns).
        /// </summary>
        public static float AiTargetDepth(IReadOnlyList<RaceGate> course, int i, in WaystationTowerSettings s, int seat)
        {
            if (WaystationTower.IsPlunge(course, i, s))
                return s.CrownGem > 0f
                    ? WaystationTower.GemDepth(course, i, s) - AiGemShort
                    : s.WindowCentre + WaystationTower.PlungeOf(course, i, s);
            float aim = WaystationTower.LanternEnd(course, i, s) - s.DeepBand * 0.5f + AiReleaseError(seat, i, s);
            return Mathf.Clamp(aim, s.WindowLow + AiSafety, WaystationTower.FarEdge(course, i, s) - AiSafety);
        }

        /// <summary>A run of deep landings, and the tier of light it has earned.</summary>
        public readonly struct Streak
        {
            public readonly int Count;
            public readonly int Tier;

            public Streak(int count, int tier)
            {
                Count = count;
                Tier = tier;
            }
        }

        /// <summary>0 below <see cref="TierOne"/>, then 1, 2, 3.</summary>
        public static int TierFor(int count) =>
            count >= TierThree ? 3 : count >= TierTwo ? 2 : count >= TierOne ? 1 : 0;

        /// <summary>A deep landing adds one, a clean one holds, and a short or past landing - or a missed ring - resets.</summary>
        public static Streak Step(Streak streak, HopGrade grade, bool missedRing)
        {
            if (missedRing || grade == HopGrade.Short || grade == HopGrade.Past) return new Streak(0, 0);
            if (grade == HopGrade.Clean) return streak;
            int count = streak.Count + 1;
            return new Streak(count, TierFor(count));
        }
    }
}

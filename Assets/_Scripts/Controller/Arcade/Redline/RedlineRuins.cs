using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Redline's garden ruins (REDLINE.md section 4b): Spearline's seven ruin kinds (<see cref="SpearlineRuins"/>, called,
    /// never copied) laid round Redline's own lap at I4, one to a leg between gates. Decoration only: plain prisms with
    /// the game's own rules, nothing here reads or writes a vessel.
    ///
    /// <para>What Redline adds is <see cref="Hide"/>: a ruin prism is left out where it would stand on one of the
    /// lap's Time crystals (I4 sets them 70 u inside the apexes, just outside the ruins' 60 u lane) or inside the
    /// nucleus, and a wheel whose sweep reaches a crystal is left out whole. The parts list itself is never edited, so
    /// a wheel's part indices stay true.</para>
    ///
    /// <para>Pure: a function of the lap and the rung, so every peer lays the same ruins from the broadcast course and
    /// <c>RedlineRuinsTests</c> holds it (headless: <c>Tools/Build/redline_course_harness</c>).</para>
    /// </summary>
    public static class RedlineRuins
    {
        /// <summary>No ruin prism stands nearer than this (u) to a Time crystal's place: the crystal stays in the open
        /// and can be taken without touching a ruin.</summary>
        public const float CrystalClearance = 40f;

        /// <summary>No ruin prism stands nearer than this (u) to the cell's centre: the nucleus body is 392 u
        /// (Nucleus.prefab at scale 400), and a leg's chord may dip inside the course's 480 u keep-out.</summary>
        public const float NucleusClearance = 410f;

        /// <summary>Every ruin prism of <paramref name="lap"/> at <paramref name="intensity"/> (none below I4), the
        /// turning ones named in <paramref name="wheels"/>, and for each part whether Redline leaves it out
        /// (<paramref name="hidden"/>, one entry a part). Clears all three lists first.</summary>
        public static void Lay(IReadOnlyList<RaceGate> lap, int intensity, IReadOnlyList<Vector3> crystals,
                               List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels, List<bool> hidden)
        {
            parts.Clear();
            wheels.Clear();
            hidden.Clear();
            if (lap == null || lap.Count < 3) return;
            SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(intensity), parts, wheels);
            Hide(parts, wheels, crystals, hidden);
        }

        /// <summary>Which of <paramref name="parts"/> are left out: one flag a part, appended to <paramref name="hidden"/>.</summary>
        public static void Hide(IReadOnlyList<SpearlineRuinPart> parts, IReadOnlyList<SpearlineRuinWheel> wheels,
                                IReadOnlyList<Vector3> crystals, List<bool> hidden)
        {
            for (int i = 0; i < parts.Count; i++)
                hidden.Add(parts[i].Position.magnitude < NucleusClearance + Reach(parts[i])
                           || CrystalDistance(crystals, parts[i].Position) < CrystalClearance + Reach(parts[i]));

            // A wheel turns, so its prisms are judged by the ball it sweeps, and it stands whole or not at all.
            for (int w = 0; w < wheels.Count; w++)
            {
                var wheel = wheels[w];
                bool hide = wheel.Centre.magnitude < NucleusClearance + SpearlineRuins.WheelRadius
                            || CrystalDistance(crystals, wheel.Centre) < CrystalClearance + SpearlineRuins.WheelRadius;
                for (int i = wheel.First; i < wheel.First + wheel.Count && i < hidden.Count; i++) hidden[i] = hide;
            }
        }

        /// <summary>How far a part reaches from its centre: half its longest side.</summary>
        public static float Reach(in SpearlineRuinPart part) =>
            0.5f * Mathf.Max(part.Scale.x, Mathf.Max(part.Scale.y, part.Scale.z));

        /// <summary>How far <paramref name="p"/> is from the nearest Time crystal's place (no crystals: very far).</summary>
        public static float CrystalDistance(IReadOnlyList<Vector3> crystals, Vector3 p)
        {
            float best = float.MaxValue;
            if (crystals == null) return best;
            for (int i = 0; i < crystals.Count; i++)
                best = Mathf.Min(best, (p - crystals[i]).magnitude);
            return best;
        }
    }
}

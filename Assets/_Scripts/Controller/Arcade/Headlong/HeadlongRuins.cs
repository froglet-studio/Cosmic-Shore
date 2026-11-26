using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Headlong's garden ruins at I4 (HEADLONG.md §0a; Yash, 2026-11-24): Spearline's ruins
    /// (<see cref="SpearlineRuins"/>, called here, never copied) laid round one lap of the circuit, less whatever would
    /// stand in a hedge or on a Time crystal. Headlong already stands a hedge across nearly every leg at I4, about where
    /// a ruin stands, so a ruin prism inside a hedge's disc is left out and the hedge shows through the ruin; the wheel
    /// is laid whole or not at all, and not where its sweep would turn through a hedge or a crystal.
    ///
    /// <para>Pure: a function of the lap, the rung, the hedges and the crystals, so every peer lays the same ruins
    /// (<c>HeadlongRuinsTests</c>). Decoration: plain prisms with the game's own rules. Nothing here touches a vessel.</para>
    /// </summary>
    public static class HeadlongRuins
    {
        /// <summary>No ruin prism reaches nearer than this (u) to a hedge's face or its rim: the slabs are 4 u thick
        /// and stand back up in place, so the ruin leaves them room.</summary>
        public const float HedgeGap = 14f;

        /// <summary>No ruin prism reaches nearer than this (u) to a Time crystal (the stampede's own clearance).</summary>
        public const float CrystalGap = HeadlongHedges.CrystalHedgeClearance;

        /// <summary>Half the thickness (u) of a wheel's sweep along its axis: its spoke stones are 30 u through.</summary>
        public const float WheelHalfDepth = 15f;

        const int WheelRings = 5, WheelSpokesSampled = 24;

        /// <summary>
        /// Every ruin prism of <paramref name="lap"/> at <paramref name="intensity"/> (none below I4). Clears the three
        /// lists. <paramref name="laid"/> has one entry for each part: false where the part is left out, so a wheel
        /// still names its parts by index. A wheel left out is taken off <paramref name="wheels"/>.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> lap, int intensity, IReadOnlyList<HeadlongHedges.Hedge> hedges,
                               IReadOnlyList<Vector3> crystals, List<SpearlineRuinPart> parts,
                               List<SpearlineRuinWheel> wheels, List<bool> laid)
        {
            parts.Clear();
            wheels.Clear();
            laid.Clear();
            if (lap == null) return;
            SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(intensity), parts, wheels);
            for (int i = 0; i < parts.Count; i++) laid.Add(true);

            for (int w = wheels.Count - 1; w >= 0; w--)
            {
                var wheel = wheels[w];
                if (!WheelMeets(wheel, hedges, crystals)) continue;
                for (int i = wheel.First; i < wheel.First + wheel.Count; i++) laid[i] = false;
                wheels.RemoveAt(w);
            }

            for (int i = 0; i < parts.Count; i++)
            {
                if (!laid[i] || Turns(wheels, i)) continue;
                if (Clearance(parts[i], hedges, crystals) < 0f) laid[i] = false;
            }
        }

        static bool Turns(List<SpearlineRuinWheel> wheels, int part)
        {
            for (int w = 0; w < wheels.Count; w++)
                if (part >= wheels[w].First && part < wheels[w].First + wheels[w].Count) return true;
            return false;
        }

        /// <summary>
        /// How far (u) <paramref name="part"/> stands clear of every hedge's kept volume and every crystal's: negative
        /// when it reaches into one. The prism is sampled along its length and taken as round, half its wider side.
        /// </summary>
        public static float Clearance(in SpearlineRuinPart part, IReadOnlyList<HeadlongHedges.Hedge> hedges,
                                      IReadOnlyList<Vector3> crystals)
        {
            float halfLength = 0.5f * part.Scale.z;
            int samples = part.Scale.z <= 16f ? 1 : 1 + Mathf.CeilToInt(part.Scale.z / 10f);
            // A short prism is one ball that holds its ends; a long one is balls along its length.
            float half = 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
            if (samples == 1) half = Mathf.Max(half, halfLength);
            float best = float.MaxValue;
            for (int k = 0; k < samples; k++)
            {
                float t = samples == 1 ? 0f : Mathf.Lerp(-halfLength, halfLength, k / (float)(samples - 1));
                best = Mathf.Min(best, PointClearance(part.Position + part.Forward * t, half, hedges, crystals));
            }
            return best;
        }

        /// <summary>The same for a ball of radius <paramref name="half"/> at <paramref name="p"/>.</summary>
        public static float PointClearance(Vector3 p, float half, IReadOnlyList<HeadlongHedges.Hedge> hedges,
                                           IReadOnlyList<Vector3> crystals)
        {
            float best = float.MaxValue;
            for (int h = 0; hedges != null && h < hedges.Count; h++)
            {
                var hedge = hedges[h];
                Vector3 d = p - hedge.Centre;
                float along = Vector3.Dot(d, hedge.Normal);
                float axial = Mathf.Abs(along) - half - HedgeGap;
                float radial = (d - hedge.Normal * along).magnitude - half - hedge.Radius - HedgeGap;
                // Outside the kept volume if it is clear of the faces OR clear of the rim.
                best = Mathf.Min(best, Mathf.Max(axial, radial));
            }
            for (int c = 0; crystals != null && c < crystals.Count; c++)
                best = Mathf.Min(best, (p - crystals[c]).magnitude - half - CrystalGap);
            return best;
        }

        /// <summary>Would the wheel's sweep (the whole disc from hub to rim, at every angle) reach a hedge or a crystal?</summary>
        public static bool WheelMeets(in SpearlineRuinWheel wheel, IReadOnlyList<HeadlongHedges.Hedge> hedges,
                                      IReadOnlyList<Vector3> crystals)
        {
            HeadlongHedges.Frame(wheel.Axis, out var right, out var up);
            for (int r = 0; r < WheelRings; r++)
            {
                float radius = Mathf.Lerp(SpearlineRuins.HubRadius, SpearlineRuins.WheelRadius, r / (float)(WheelRings - 1));
                for (int a = 0; a < WheelSpokesSampled; a++)
                {
                    float phi = a * 2f * Mathf.PI / WheelSpokesSampled;
                    Vector3 p = wheel.Centre + (right * Mathf.Cos(phi) + up * Mathf.Sin(phi)) * radius;
                    if (PointClearance(p, WheelHalfDepth, hedges, crystals) < 0f) return true;
                }
            }
            return false;
        }
    }
}

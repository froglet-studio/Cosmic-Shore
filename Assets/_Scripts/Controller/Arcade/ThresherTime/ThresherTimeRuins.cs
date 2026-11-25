using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thresher Time's garden ruins (THRESHERTIME.md §0b): Spearline's seven kinds (<see cref="SpearlineRuins"/>, called
    /// here and never copied), laid round the legs of the Belfry's hardest rung with the Belfry's own clearances:
    /// <list type="bullet">
    /// <item>the clear lane is as wide as the rings swing, plus a mouth, so the line between ANY two poses of a leg's
    /// rings stays open;</item>
    /// <item>the margin round a ring is wider by <see cref="SwingAllowance"/>, so a swung ring never reaches a ruin;</item>
    /// <item>a prism that stands where a mace swings is left out, and a wheel that would turn through one is left out
    /// whole, so a mace is never hidden in a ruin.</item>
    /// </list>
    /// Pure: a function of the lap, the rung and the Belfry's layout, so every peer lays the same ruins and
    /// <c>ThresherTimeRuinsTests</c> holds it. Decoration: nothing here reads or writes a vessel.
    /// </summary>
    public static class ThresherTimeRuins
    {
        /// <summary>Added to Spearline's ring margin (u): a ring swings sideways out of its rest pose, toward a ruin
        /// that stands off the leg's line.</summary>
        public const float SwingAllowance = 20f;

        /// <summary>Clear space (u) kept between a mace's reach and a ruin prism.</summary>
        public const float MaceMargin = 12f;

        /// <summary>Poses of a full turn a wheel is tested at against the maces.</summary>
        public const int WheelPoses = 24;

        /// <summary>How far (u) a ring of this rung swings from its rest pose: the chord of its pendulum's arc.</summary>
        public static float RingSwingReach(int intensity)
        {
            var spec = ThresherBelfry.SpecFor(intensity);
            return 2f * spec.RingLength * Mathf.Sin(spec.RingSwingDeg * Mathf.Deg2Rad * 0.5f);
        }

        /// <summary>Spearline's rung with this course's clearances. <paramref name="mouthRadius"/> is the rung's ring
        /// radius; <paramref name="belfry"/> off (the rings stand still) leaves Spearline's own lane and margin.</summary>
        public static SpearlineRuinSettings ForIntensity(int intensity, float mouthRadius, bool belfry)
        {
            var s = SpearlineRuins.ForIntensity(intensity);
            if (!belfry) return s;
            return s.With(Mathf.Max(SpearlineRuins.LaneRadius, RingSwingReach(intensity) + mouthRadius),
                          SpearlineRuins.MinLegLength, SpearlineRuins.RingMargin + SwingAllowance);
        }

        /// <summary>
        /// Every ruin prism of <paramref name="lap"/> on this rung, appended to <paramref name="parts"/> (the turning
        /// ones named in <paramref name="wheels"/>, as <see cref="SpearlineRuins.Lay"/> gives them), and for each part
        /// whether it is laid (<paramref name="stands"/>): false where a mace of <paramref name="belfry"/> swings.
        /// A null or empty <paramref name="belfry"/> is a course with no clockwork.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> lap, int intensity, ThresherBelfry.Layout belfry,
                               List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels, List<bool> stands)
        {
            if (lap == null || lap.Count == 0) return;
            bool clockwork = belfry != null && belfry.Rings.Count > 0;
            int first = parts.Count, firstWheel = wheels.Count;
            SpearlineRuins.Lay(lap, ForIntensity(intensity, lap[0].Radius, clockwork), parts, wheels);
            for (int i = first; i < parts.Count; i++)
                stands.Add(!clockwork || !InMaceSweep(belfry, parts[i]));

            if (!clockwork) return;
            for (int w = firstWheel; w < wheels.Count; w++)
            {
                var wheel = wheels[w];
                if (!WheelMeetsMace(belfry, parts, wheel)) continue;
                for (int i = wheel.First; i < wheel.First + wheel.Count && i < parts.Count; i++) stands[i - first] = false;
            }
        }

        /// <summary>Does <paramref name="part"/>, where it stands, reach into the space any mace swings through?</summary>
        public static bool InMaceSweep(ThresherBelfry.Layout belfry, in SpearlineRuinPart part) =>
            InMaceSweep(belfry, part.Position, part.Forward, part.Scale);

        static bool InMaceSweep(ThresherBelfry.Layout belfry, Vector3 position, Vector3 forward, Vector3 scale)
        {
            float pad = Mathf.Max(scale.x, scale.y) * 0.5f + MaceMargin;
            int samples = scale.z <= 16f ? 1 : 1 + Mathf.CeilToInt(scale.z / 10f);
            for (int k = 0; k < samples; k++)
            {
                Vector3 p = samples == 1 ? position : position + forward * ((k / (float)(samples - 1) - 0.5f) * scale.z);
                for (int m = 0; m < belfry.Maces.Count; m++)
                    if (InSweep(belfry.Maces[m], p, pad)) return true;
            }
            return false;
        }

        /// <summary>Is <paramref name="p"/> within <paramref name="pad"/> of where <paramref name="mace"/>'s chain and
        /// head pass: the sector its pendulum swings through, as thick as its head.</summary>
        public static bool InSweep(in ThresherBelfry.Pendulum mace, Vector3 p, float pad)
        {
            Vector3 rel = p - mace.Pivot;
            if (Mathf.Abs(Vector3.Dot(rel, mace.Forward)) > ThresherBelfry.HeadReach + pad) return false;
            float side = Vector3.Dot(rel, mace.Side), down = -Vector3.Dot(rel, mace.Up);
            float r = Mathf.Sqrt(side * side + down * down);
            float reach = ThresherBelfry.HeadReach + pad;
            if (r > mace.Length + reach) return false;
            if (r <= reach) return true;
            return Mathf.Abs(Mathf.Atan2(side, down)) <= mace.Swing + Mathf.Asin(Mathf.Min(1f, reach / r));
        }

        static bool WheelMeetsMace(ThresherBelfry.Layout belfry, List<SpearlineRuinPart> parts, in SpearlineRuinWheel wheel)
        {
            float turn = 360f / Mathf.Max(1e-3f, Mathf.Abs(wheel.DegreesPerSecond));
            for (int i = wheel.First; i < wheel.First + wheel.Count && i < parts.Count; i++)
            {
                for (int k = 0; k < WheelPoses; k++)
                {
                    SpearlineRuins.Pose(parts[i], wheel, turn * k / WheelPoses, out var position, out var forward, out _);
                    if (InMaceSweep(belfry, position, forward, parts[i].Scale)) return true;
                }
            }
            return false;
        }
    }
}

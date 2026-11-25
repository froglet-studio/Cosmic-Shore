using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Time's ruins at I4 (GRIZZLYTIME.md §0.2; Yash, 2026-11-24): Spearline's garden ruins
    /// (<see cref="SpearlineRuins"/>, which lays them and is not copied here) standing on the legs between the rings,
    /// then cut back for the way a Grizzly really flies this course. Spearline keeps a lane clear down the ring-to-ring
    /// line; a Grizzly mostly is not on it. It flies keg to keg, and a bank shot throws it from a keg at the next ring.
    /// So on top of Spearline's lane this drops every standing ruin prism that reaches into
    /// <list type="bullet">
    /// <item>the <b>throw lane</b>: from each keg to the next ring (where a blast in the keg sends the hull),</item>
    /// <item>the <b>keg line</b>: from each keg to the next keg, and from each ring to the next keg (the two approaches),</item>
    /// <item>the <b>keg's own space</b>: <see cref="KegClear"/> round each keg, which also holds its chalk's first dots.</item>
    /// </list>
    /// A turning wheel is kept whole or dropped whole: dropped only if its sweep would reach a keg's space. It still
    /// sweeps the lanes, as it does in Spearline.
    ///
    /// <para>Pure: a function of the lap and the rung, so every peer lays the same ruins from the broadcast rings and
    /// <c>GrizzlyTimeRuinsTests</c> holds it headless. Decoration only: nothing here reads or writes a vessel.</para>
    /// </summary>
    public static class GrizzlyTimeRuins
    {
        /// <summary>The clear radius (u) of the throw lane and the keg lines: Spearline's lane.</summary>
        public const float LaneRadius = SpearlineRuins.LaneRadius;

        /// <summary>No ruin prism stands this near (u) a keg's centre: the space a keg needs empty of hulls to regrow.</summary>
        public const float KegClear = GrizzlyTimeKegs.HullClear;

        /// <summary>Every ruin prism of <paramref name="lap"/> on this rung, appended to <paramref name="into"/>; the
        /// turning ones are named in <paramref name="wheels"/> (their ranges index <paramref name="into"/>).</summary>
        public static void Lay(IReadOnlyList<RaceGate> lap, int intensity, List<SpearlineRuinPart> into,
                               List<SpearlineRuinWheel> wheels)
        {
            var raw = new List<SpearlineRuinPart>();
            var rawWheels = new List<SpearlineRuinWheel>();
            SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(intensity), raw, rawWheels);
            if (raw.Count == 0) return;

            var kegs = new List<GrizzlyTimeKegs.Keg>();
            GrizzlyTimeKegs.KegsFor(lap, kegs);

            int i = 0;
            while (i < raw.Count)
            {
                int w = WheelAt(rawWheels, i);
                if (w >= 0)
                {
                    var wheel = rawWheels[w];
                    if (WheelStands(kegs, wheel))
                    {
                        wheels?.Add(new SpearlineRuinWheel(wheel.Centre, wheel.Axis, wheel.DegreesPerSecond, into.Count, wheel.Count));
                        for (int k = 0; k < wheel.Count; k++) into.Add(raw[wheel.First + k]);
                    }
                    i = wheel.First + wheel.Count;
                    continue;
                }
                if (Stands(lap, kegs, raw[i])) into.Add(raw[i]);
                i++;
            }
        }

        static int WheelAt(List<SpearlineRuinWheel> wheels, int part)
        {
            for (int w = 0; w < wheels.Count; w++)
                if (wheels[w].Count > 0 && part == wheels[w].First) return w;
            return -1;
        }

        /// <summary>May a wheel turn here: its whole sweep outside every keg's space?</summary>
        public static bool WheelStands(List<GrizzlyTimeKegs.Keg> kegs, in SpearlineRuinWheel wheel)
        {
            float keep = SpearlineRuins.WheelRadius + KegClear;
            for (int k = 0; k < kegs.Count; k++)
                if ((kegs[k].Centre - wheel.Centre).sqrMagnitude < keep * keep) return false;
            return true;
        }

        /// <summary>May a standing ruin prism stay: its whole length outside every keg lane and every keg's space?</summary>
        public static bool Stands(IReadOnlyList<RaceGate> lap, List<GrizzlyTimeKegs.Keg> kegs, in SpearlineRuinPart part) =>
            PartKegLaneDistance(lap, kegs, part) >= LaneRadius && PartKegDistance(kegs, part) >= KegClear;

        /// <summary>How near <paramref name="part"/> comes to any throw lane or keg line, along its whole length.</summary>
        public static float PartKegLaneDistance(IReadOnlyList<RaceGate> lap, List<GrizzlyTimeKegs.Keg> kegs, in SpearlineRuinPart part)
        {
            float best = float.MaxValue;
            int samples = LengthSamples(part);
            for (int k = 0; k < samples; k++)
                best = Mathf.Min(best, KegLaneDistance(lap, kegs, LengthPoint(part, k, samples)));
            return best;
        }

        /// <summary>How near <paramref name="part"/> comes to any keg's centre, along its whole length.</summary>
        public static float PartKegDistance(List<GrizzlyTimeKegs.Keg> kegs, in SpearlineRuinPart part)
        {
            float best = float.MaxValue;
            int samples = LengthSamples(part);
            for (int k = 0; k < samples; k++)
            {
                Vector3 p = LengthPoint(part, k, samples);
                for (int g = 0; g < kegs.Count; g++)
                    best = Mathf.Min(best, (p - kegs[g].Centre).magnitude);
            }
            return best;
        }

        /// <summary>How far <paramref name="p"/> is from the nearest throw lane or keg line (every leg, the closing one too).</summary>
        public static float KegLaneDistance(IReadOnlyList<RaceGate> lap, List<GrizzlyTimeKegs.Keg> kegs, Vector3 p)
        {
            float best = float.MaxValue;
            int n = kegs.Count;
            for (int k = 0; k < n; k++)
            {
                int next = (k + 1) % n;
                Vector3 keg = kegs[k].Centre, nextKeg = kegs[next].Centre;
                best = Mathf.Min(best, SegmentDistanceSqr(p, keg, lap[next].Position));
                best = Mathf.Min(best, SegmentDistanceSqr(p, keg, nextKeg));
                best = Mathf.Min(best, SegmentDistanceSqr(p, lap[k].Position, nextKeg));
            }
            return Mathf.Sqrt(best);
        }

        static int LengthSamples(in SpearlineRuinPart part) => part.Scale.z <= 16f ? 1 : 1 + Mathf.CeilToInt(part.Scale.z / 10f);

        static Vector3 LengthPoint(in SpearlineRuinPart part, int k, int samples) =>
            samples == 1 ? part.Position : part.Position + part.Forward * ((k / (float)(samples - 1) - 0.5f) * part.Scale.z);

        static float SegmentDistanceSqr(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
            return (p - (a + ab * t)).sqrMagnitude;
        }
    }
}

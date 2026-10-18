using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Skim Race MAP FINGERPRINT for one intensity: eight hex digits that change when the race
    /// itself changes - the track's path points, its curve setting, the number of laps, or where the
    /// crystals sit - and stay the same for every other edit to the scene (colours, prism looks, spawn
    /// jitter). A tuned AI policy is only as good as the map it was tuned on, so each per-intensity
    /// policy records the fingerprint of that map (<see cref="SkimRaceAIConfigSO.TrackFingerprint"/>)
    /// and <see cref="SkimRaceAIDeployment"/> flies the general policy instead when the live map's
    /// fingerprint (<see cref="SkimRaceCourseSource.TryFingerprintFromScene"/>) no longer matches.
    ///
    /// <para>Coordinates count in WHOLE units (half to even, <see cref="Math.Round(double)"/>'s
    /// default): a waypoint nudged by a fraction of a unit is not a different map. The value is 32-bit
    /// FNV-1a over the little-endian int32 sequence
    /// <c>[Version, #points, x, y, z ..., spline, laps, #crystals, x, y, z ...]</c>.</para>
    ///
    /// <para>Pure C# on purpose: <c>Tools/Build/skimrace_track_fingerprint.py</c> computes the same
    /// value straight from the scene file (for the <c>--check</c> that says "retune needed" before
    /// anyone presses Play), and the offline simulator compiles this class to confirm the two agree on
    /// the real track. Both sides assert one shared golden value
    /// (<c>SkimRaceTrackFingerprintTests</c> and the script's <c>--self-test</c>).</para>
    /// </summary>
    public static class SkimRaceTrackFingerprint
    {
        /// <summary>Bump here AND in the Python script when what goes into the fingerprint changes:
        /// every recorded fingerprint then reads as a changed map, which is what such a change is.</summary>
        public const int Version = 1;

        const uint OffsetBasis = 2166136261u;
        const uint Prime = 16777619u;

        /// <summary>The fingerprint of one intensity's race: its path <paramref name="waypoints"/>,
        /// whether the path is a <paramref name="spline"/>, its <paramref name="laps"/>, and the crystal
        /// <paramref name="anchors"/>. A null list counts as an empty one.</summary>
        public static string Compute(IReadOnlyList<Vector3> waypoints, bool spline, int laps, IReadOnlyList<Vector3> anchors)
        {
            uint h = OffsetBasis;
            Mix(ref h, Version);
            MixPoints(ref h, waypoints);
            Mix(ref h, spline ? 1 : 0);
            Mix(ref h, laps);
            MixPoints(ref h, anchors);
            return h.ToString("x8");
        }

        static void MixPoints(ref uint h, IReadOnlyList<Vector3> points)
        {
            int count = points?.Count ?? 0;
            Mix(ref h, count);
            for (int i = 0; i < count; i++)
            {
                var p = points[i];
                Mix(ref h, Units(p.x));
                Mix(ref h, Units(p.y));
                Mix(ref h, Units(p.z));
            }
        }

        static int Units(float v) => (int)Math.Round(v);

        static void Mix(ref uint h, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    h ^= (uint)(value >> shift) & 0xFFu;
                    h *= Prime;
                }
            }
        }
    }
}

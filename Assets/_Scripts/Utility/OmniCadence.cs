using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>Which square turn a cadence is in, if any.</summary>
    public enum SquareTurn
    {
        None = 0,
        Pentagon = 1,
        Triangle = 2,
    }

    /// <summary>
    /// The omni crystal's BEATS (Omni Motion Lab round 1, "interleaved beats"): one cycle is
    /// <c>[pentagon turn] gap [Time wave] gap [triangle turn] gap [Time wave] gap</c>, so each stop of the Space squares
    /// holds exactly one Time wave and the squares and the rhombi never move at once (the lab measured that they were
    /// both moving 87% of the time on their separate clocks). Pure maths, a function of time: every client and every
    /// frame agree, and nothing is stepped.
    ///
    /// JS twin: <c>beatsOf()</c> / <c>activeBeats()</c> / <c>squaresAt()</c> / <c>wavesAt()</c> in
    /// <c>Docs/Studios/OmniMotionLab.html</c>. Change one, change both.
    /// </summary>
    public readonly struct OmniCadence
    {
        public readonly float PentagonSeconds, WaveSeconds, TriangleSeconds, GapSeconds;

        public OmniCadence(float pentagonSeconds, float waveSeconds, float triangleSeconds, float gapSeconds)
        {
            PentagonSeconds = Mathf.Max(0.05f, pentagonSeconds);
            WaveSeconds = Mathf.Max(0.05f, waveSeconds);
            TriangleSeconds = Mathf.Max(0.05f, triangleSeconds);
            GapSeconds = Mathf.Max(0f, gapSeconds);   // a negative gap would overlap the beats - the thing this exists to stop
        }

        /// <summary>One cycle: two square turns, two waves, four gaps.</summary>
        public float CycleSeconds => PentagonSeconds + TriangleSeconds + 2f * WaveSeconds + 4f * GapSeconds;

        float WaveStart(int k) => k == 0 ? PentagonSeconds + GapSeconds : PentagonSeconds + WaveSeconds + TriangleSeconds + 3f * GapSeconds;
        float TriangleStart => PentagonSeconds + WaveSeconds + 2f * GapSeconds;

        /// <summary>The square turn under way at <paramref name="seconds"/>, and how far through it (0..1).</summary>
        public SquareTurn SquaresAt(float seconds, out float progress)
        {
            float local = Local(seconds, out _);
            if (local < PentagonSeconds) { progress = local / PentagonSeconds; return SquareTurn.Pentagon; }
            float s = local - TriangleStart;
            if (s >= 0f && s < TriangleSeconds) { progress = s / TriangleSeconds; return SquareTurn.Triangle; }
            progress = 0f;
            return SquareTurn.None;
        }

        /// <summary>
        /// The Time wave under way at <paramref name="seconds"/>: seconds into it, and its index (two per cycle, counting
        /// from the clock's zero), so a driver can pick a new start vertex for each.
        /// </summary>
        public bool WaveAt(float seconds, out float secondsIn, out long index)
        {
            float local = Local(seconds, out long cycle);
            for (int k = 0; k < 2; k++)
            {
                float s = local - WaveStart(k);
                if (s >= 0f && s < WaveSeconds)
                {
                    secondsIn = s;
                    index = 2 * cycle + k;
                    return true;
                }
            }
            secondsIn = 0f;
            index = -1;
            return false;
        }

        float Local(float seconds, out long cycle)
        {
            float c = CycleSeconds;
            cycle = (long)System.Math.Floor(seconds / c);
            return seconds - cycle * c;
        }

        /// <summary>A quintic ease (smootherstep): no jolt as a square turn starts or stops (lab "Ease" 0).</summary>
        public static float Smoother(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * u * (u * (u * 6f - 15f) + 10f);
        }
    }
}

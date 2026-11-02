using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Main-thread cost of the Switchback AI hooks (aim, drift look, emergency steer, lane), summed per frame for all AIs.
    /// The hit counter's race summary reports the mean and worst frame; the Profiler shows the marker "Switchback.AI".
    /// </summary>
    public static class SwitchbackAIClock
    {
        public static readonly ProfilerMarker Marker = new("Switchback.AI");
        static long s_frameTicks, s_totalTicks, s_maxTicks;
        static int s_frame = -1, s_frames;

        public static long Begin() { Marker.Begin(); return Stopwatch.GetTimestamp(); }

        public static void End(long start)
        {
            long dt = Stopwatch.GetTimestamp() - start;
            Marker.End();
            int f = Time.frameCount;
            if (f != s_frame) { Close(); s_frame = f; }
            s_frameTicks += dt;
        }

        static void Close()
        {
            if (s_frame < 0) return;
            s_totalTicks += s_frameTicks; s_frames++;
            if (s_frameTicks > s_maxTicks) s_maxTicks = s_frameTicks;
            s_frameTicks = 0;
        }

        public static void Reset() { s_frameTicks = s_totalTicks = s_maxTicks = 0; s_frame = -1; s_frames = 0; }

        /// <summary>"aiMs/frame=mean aiMsMax=worst frames=n" since the last <see cref="Reset"/>.</summary>
        public static string Report()
        {
            Close(); s_frame = -1;
            double ms = 1000.0 / Stopwatch.Frequency;
            double mean = s_frames > 0 ? s_totalTicks * ms / s_frames : 0;
            return $"aiMs/frame={mean:F3} aiMsMax={s_maxTicks * ms:F2} aiFrames={s_frames}";
        }
    }
}

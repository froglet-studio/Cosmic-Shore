using System;
using System.Diagnostics;

namespace CosmicShore.Engine
{
    /// <summary>One frame's CPU/GPU timings (UnityEngine.FrameTiming).</summary>
    public struct FrameTiming
    {
        public ulong cpuTimePresentCalled, cpuTimeFrameComplete, frameStartTimestamp, firstSubmitTimestamp;
        public double cpuFrameTime, cpuMainThreadFrameTime, cpuMainThreadPresentWaitTime, cpuRenderThreadFrameTime, gpuFrameTime;
        public float heightScale, widthScale;
        public uint syncInterval;
    }

    /// <summary>
    /// Frame-timing capture (UnityEngine.FrameTimingManager): what each frame really cost on the wall clock,
    /// measured with <see cref="Stopwatch"/>. It never reads the loop's step. A scripted run ticks the game
    /// at a fixed 1/60 s, so <see cref="Time.unscaledDeltaTime"/> says 16.7 ms however long the frame took,
    /// and a frame timer built on it says the same. This one reports the real frame.
    ///
    /// The engine runs one thread, so its frame maps onto Unity's fields like this:
    /// <list type="bullet">
    /// <item><c>cpuFrameTime</c> and <c>cpuMainThreadFrameTime</c>: wall time from one <see cref="GameLoop.Tick"/>
    /// to the next. That is the whole frame: simulation, rendering, present, and any frame limiter.</item>
    /// <item><c>cpuMainThreadPresentWaitTime</c>: the part after the frame's work finished (the host's
    /// <see cref="ReportRender"/>, or the end of the tick when nothing rendered). That is swap, vsync and
    /// the frame limiter.</item>
    /// <item><c>cpuRenderThreadFrameTime</c>: the CPU time the host spent issuing the frame's draws.</item>
    /// <item><c>gpuFrameTime</c>: the host's GPU timer queries, or 0 when it has none. Unity also reports 0 on
    /// a platform it cannot time.</item>
    /// </list>
    /// As in Unity, a frame's timing becomes available once the next frame starts, and
    /// <see cref="GetLatestTimings"/> reads the snapshot taken by the last <see cref="CaptureFrameTimings"/>.
    /// </summary>
    public static class FrameTimingManager
    {
        const int History = 8;
        static readonly FrameTiming[] s_completed = new FrameTiming[History];   // newest at (s_count - 1) % History
        static int s_count;
        static readonly FrameTiming[] s_captured = new FrameTiming[History];    // newest first
        static int s_capturedCount;

        // The frame in flight.
        static long s_frameStart, s_workEnd;
        static double s_renderMs, s_gpuMs;

        static readonly double s_msPerTick = 1000.0 / Stopwatch.Frequency;

        public static bool IsFeatureEnabled() => true;

        /// <summary>Snapshot the completed frames for <see cref="GetLatestTimings"/> (Unity's contract).</summary>
        public static void CaptureFrameTimings()
        {
            s_capturedCount = Math.Min(s_count, History);
            for (int i = 0; i < s_capturedCount; i++)
                s_captured[i] = s_completed[(s_count - 1 - i) % History];
        }

        /// <summary>Copies up to <paramref name="numFrames"/> captured frames into <paramref name="timings"/>, newest first.</summary>
        public static uint GetLatestTimings(uint numFrames, FrameTiming[] timings)
        {
            if (timings == null) return 0;
            int n = (int)Math.Min(Math.Min(numFrames, (uint)timings.Length), (uint)s_capturedCount);
            Array.Copy(s_captured, timings, n);
            return (uint)n;
        }

        public static ulong GetCpuTimerFrequency() => (ulong)Stopwatch.Frequency;
        public static ulong GetGpuTimerFrequency() => 0;
        public static ulong GetVSyncsPerSecond() => 60;

        /// <summary>
        /// Engine addition: the last completed frame, without a capture. For engine-side readers such as
        /// ProfilerRecorder's frame-time stats. False until a frame has completed.
        /// </summary>
        public static bool TryGetLastFrame(out FrameTiming timing)
        {
            timing = s_count > 0 ? s_completed[(s_count - 1) % History] : default;
            return s_count > 0;
        }

        /// <summary>
        /// Engine addition, for the host that draws the frame: the CPU milliseconds it spent issuing the frame's
        /// draws, and the GPU milliseconds when it has timer queries. Call it once the frame's draws are issued;
        /// whatever happens after that, up to the next tick, counts as present wait.
        /// </summary>
        public static void ReportRender(double renderCpuMs, double? gpuMs = null)
        {
            s_renderMs = renderCpuMs;
            if (gpuMs is { } g) s_gpuMs = g;
            s_workEnd = Stopwatch.GetTimestamp();
        }

        /// <summary>GameLoop.Tick's first act: closes the previous frame and starts this one.</summary>
        internal static void BeginFrame() => BeginFrame(Stopwatch.GetTimestamp());

        internal static void BeginFrame(long now)
        {
            if (s_frameStart != 0)
            {
                double frameMs = (now - s_frameStart) * s_msPerTick;
                double waitMs = Math.Max(0.0, (now - Math.Max(s_workEnd, s_frameStart)) * s_msPerTick);
                s_completed[s_count % History] = new FrameTiming
                {
                    frameStartTimestamp = (ulong)s_frameStart,
                    firstSubmitTimestamp = (ulong)s_frameStart,
                    cpuTimePresentCalled = (ulong)Math.Max(s_workEnd, s_frameStart),
                    cpuTimeFrameComplete = (ulong)now,
                    cpuFrameTime = frameMs,
                    cpuMainThreadFrameTime = frameMs,
                    cpuMainThreadPresentWaitTime = waitMs,
                    cpuRenderThreadFrameTime = s_renderMs,
                    gpuFrameTime = s_gpuMs,
                    heightScale = 1f,
                    widthScale = 1f,
                    syncInterval = 0,
                };
                s_count++;
            }
            s_frameStart = now;
            s_workEnd = 0;
            s_renderMs = 0;
            // GPU time stays at the last reading: timer queries answer a frame or more late, and a frame
            // without one is not a frame that cost the GPU nothing.
        }

        /// <summary>GameLoop.Tick's last act: the simulation's work is done (the host may still render).</summary>
        internal static void EndSimulation() => s_workEnd = Stopwatch.GetTimestamp();

        /// <summary>A fresh world: no frames, nothing in flight (test isolation, as <see cref="Time.Reset"/>).</summary>
        public static void Reset()
        {
            s_count = 0;
            s_capturedCount = 0;
            s_frameStart = 0;
            s_workEnd = 0;
            s_renderMs = 0;
            s_gpuMs = 0;
        }
    }
}

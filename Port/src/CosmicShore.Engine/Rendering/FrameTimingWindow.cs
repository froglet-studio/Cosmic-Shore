using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Engine addition: frame-time figures over a window of recent frames, read from <see cref="FrameTimingManager"/>.
    /// Amoebius's F3 overlay (Player/PlayerDiagnostics) draws its FRAME line from one. That is the same
    /// measured frame the game's DiagnosticsHUD (F7), PerformanceBenchmarkRunner and ProfilerRecorder read, so
    /// the two panels cannot drift apart (F3 kept its own Stopwatch until 2026-10-14).
    /// <list type="bullet">
    /// <item>Frame time average, 1% worst and max, and fps: over the last <see cref="Window"/> completed frames.</item>
    /// <item>Simulation, render and GPU milliseconds: the mean of the frames completed since the previous
    /// <see cref="Read"/> (F3 reads twice a second), and of at least the last <see cref="SplitFrames"/>. A slow
    /// frame rate gives a read only one or two new frames, and the text should show a steady number rather than one frame's.</item>
    /// </list>
    /// It holds nothing but its buffers: every number comes from <see cref="FrameTimingManager"/>.
    /// </summary>
    public sealed class FrameTimingWindow
    {
        /// <summary>Frames the frame-time figures cover (4 s at 60 fps).</summary>
        public const int Window = 240;

        /// <summary>The fewest frames the simulation/render/GPU split averages.</summary>
        public const int SplitFrames = 10;

        readonly FrameTiming[] _timings = new FrameTiming[Window];
        readonly double[] _simMs = new double[Window];
        readonly double[] _sorted = new double[Window];
        int _completedAtRead;

        /// <summary>Completed frames in the window (fewer than <see cref="Window"/> just after start).</summary>
        public int Frames { get; private set; }
        /// <summary>Frames completed since the previous <see cref="Read"/>, however many (the window holds at most <see cref="Window"/>).</summary>
        public int NewFrames { get; private set; }
        public double AverageMs { get; private set; }
        public double Worst1PercentMs { get; private set; }
        public double MaxMs { get; private set; }
        public double Fps => AverageMs > 0 ? 1000 / AverageMs : 0;
        public double SimulationMs { get; private set; }
        public double RenderMs { get; private set; }
        /// <summary>Null until the host's GPU timer queries have answered (a GL ES context has none).</summary>
        public double? GpuMs { get; private set; }

        /// <summary>Captures <see cref="FrameTimingManager"/> and recomputes every figure.</summary>
        public void Read()
        {
            FrameTimingManager.CaptureFrameTimings();
            int n = (int)FrameTimingManager.GetLatestTimings(Window, _timings, _simMs);
            Frames = n;

            int completed = FrameTimingManager.CompletedFrames;
            NewFrames = completed >= _completedAtRead ? completed - _completedAtRead : completed;   // a new world restarts the count
            _completedAtRead = completed;
            int split = Math.Min(Math.Max(NewFrames, SplitFrames), n);

            if (n > 0)
            {
                double sum = 0;
                for (int i = 0; i < n; i++) sum += _sorted[i] = _timings[i].cpuFrameTime;
                Array.Sort(_sorted, 0, n);
                AverageMs = sum / n;
                Worst1PercentMs = _sorted[(int)Math.Min(n - 1, Math.Floor(n * 0.99))];
                MaxMs = _sorted[n - 1];
            }

            if (NewFrames == 0 || split == 0) return;   // nothing new: keep the last split
            double sim = 0, render = 0, gpu = 0;
            int timed = 0;
            for (int i = 0; i < split; i++)
            {
                sim += _simMs[i];
                render += _timings[i].cpuRenderThreadFrameTime;
                if (_timings[i].gpuFrameTime > 0) { gpu += _timings[i].gpuFrameTime; timed++; }
            }
            SimulationMs = sim / split;
            RenderMs = render / split;
            if (timed > 0) GpuMs = gpu / timed;
        }
    }
}

using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Single source of truth for the CPU-vs-GPU "bound" classification, shared by the
    /// benchmark analysis (<see cref="BenchmarkAnalysis"/>) and the live overlays
    /// (<see cref="DiagnosticsHUD"/>, <see cref="BenchmarkHUDOverlay"/>).
    ///
    /// FrameTimingManager semantics that shape this API:
    /// • <c>cpuFrameTime</c> spans the whole CPU frame INCLUDING the wait for present -
    ///   under vsync or <c>Application.targetFrameRate</c> it converges on the frame budget
    ///   and would always read "CPU-bound". The live overlays therefore classify on
    ///   BUSY CPU time: max(main thread − present wait, render thread). See
    ///   <see cref="BusyCpuMs"/> and <see cref="IsAtCap"/>.
    /// • <c>gpuFrameTime</c> arrives a few frames late and is 0 on platforms / graphics
    ///   APIs without GPU timing support - then the verdict is <see cref="Unknown"/>.
    /// • It can also come back as GARBAGE rather than 0 - a raw counter, an uninitialised
    ///   value, NaN. A reading of 77,028,560,000 ms (2.4 years) was observed live on a
    ///   frame whose GPU was idle. Every such value exceeds any CPU time, so an unguarded
    ///   <see cref="Classify"/> returns <see cref="GpuBound"/> with total confidence, and
    ///   the one row the capture recipes say to read FIRST then points at the wrong
    ///   processor. Run every <c>gpuFrameTime</c> read through <see cref="SanitizeGpuMs"/>.
    /// • FrameTimingManager is always active in the Editor and Development builds; release
    ///   builds only report timings when "Frame Timing Stats" is enabled in Player Settings.
    /// </summary>
    public static class FrameBoundness
    {
        public const string Unknown = "Unknown";
        public const string CpuBound = "CPU-bound";
        public const string GpuBound = "GPU-bound";
        public const string Balanced = "Balanced";

        // One side must exceed the other by 10% before we call a winner - below that the
        // frame is genuinely shared and "Balanced" is the honest answer.
        const float DominanceRatio = 1.1f;

        /// <summary>
        /// The largest GPU frame time still worth believing, in milliseconds. Deliberately
        /// far above any frame anyone would profile (10 s): the job here is to reject values
        /// that are not timings at all, never to quietly hide a real - even catastrophic -
        /// frame. A tighter ceiling would start lying about the thing it exists to report.
        /// </summary>
        public const float MaxPlausibleGpuMs = 10_000f;

        /// <summary>
        /// Passes a <c>gpuFrameTime</c> reading through, or returns 0 - the value this whole
        /// API already reads as "GPU timing unavailable" - when it is not finite or not a
        /// plausible frame time. Returning the EXISTING sentinel rather than a new state is
        /// what makes it safe to drop in: every consumer's unsupported-platform handling
        /// applies unchanged.
        ///
        /// Apply it at the READ, before any smoothing: one garbage sample fed to an
        /// exponential average poisons that average for many frames afterwards, so a filter
        /// downstream of the smoothing does not help.
        /// </summary>
        public static float SanitizeGpuMs(float gpuMs)
        {
            if (float.IsNaN(gpuMs) || float.IsInfinity(gpuMs)) return 0f;
            if (gpuMs < 0f || gpuMs > MaxPlausibleGpuMs) return 0f;
            return gpuMs;
        }

        /// <summary>Classifies which processor limits the frame. Inputs in milliseconds.</summary>
        public static string Classify(float cpuMs, float gpuMs)
        {
            // Defence in depth: the read sites sanitize, but recorded or averaged data can
            // reach this from a run that predates the filter.
            gpuMs = SanitizeGpuMs(gpuMs);
            if (cpuMs <= 0.001f && gpuMs <= 0.001f) return Unknown;
            if (gpuMs > cpuMs * DominanceRatio) return GpuBound;
            if (cpuMs > gpuMs * DominanceRatio) return CpuBound;
            return Balanced;
        }

        /// <summary>
        /// Actual CPU work in the frame with the main thread's wait-for-present removed -
        /// the number to compare against GPU time while a frame-rate cap is active. Falls
        /// back to the total when the per-thread timings are unavailable (all zero).
        /// </summary>
        public static float BusyCpuMs(float cpuTotalMs, float mainThreadMs, float presentWaitMs, float renderThreadMs)
        {
            float busy = Mathf.Max(mainThreadMs - presentWaitMs, renderThreadMs);
            return busy > 0.001f ? busy : cpuTotalMs;
        }

        /// <summary>
        /// The fps the application is capped at (vsync wins over
        /// <c>Application.targetFrameRate</c>, matching Unity's behavior), or -1 when uncapped.
        /// </summary>
        public static float TargetFpsCap()
        {
            if (QualitySettings.vSyncCount > 0)
            {
                double hz = Screen.currentResolution.refreshRateRatio.value;
                if (hz > 1.0) return (float)(hz / QualitySettings.vSyncCount);
            }
            return Application.targetFrameRate > 0 ? Application.targetFrameRate : -1f;
        }

        /// <summary>
        /// True when the measured fps sits at the active cap - the limiter is the cap
        /// itself, so neither processor verdict applies.
        /// </summary>
        public static bool IsAtCap(float measuredFps, out float cap)
        {
            cap = TargetFpsCap();
            return cap > 0f && measuredFps >= cap * 0.95f;
        }
    }
}

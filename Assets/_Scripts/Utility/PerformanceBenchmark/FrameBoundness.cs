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

        /// <summary>Classifies which processor limits the frame. Inputs in milliseconds.</summary>
        public static string Classify(float cpuMs, float gpuMs)
        {
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

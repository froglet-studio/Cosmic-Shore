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
        /// A frame must exceed the work in it by at least this FRACTION, and by at least
        /// <see cref="PresentIdleFloorMs"/>, before it counts as limited by something other
        /// than work. Both bars together, so ordinary jitter and measurement skew between the
        /// frame clock and FrameTimingManager cannot trip it.
        /// </summary>
        const float PresentIdleRatio = 0.25f;
        const float PresentIdleFloorMs = 2f;

        /// <summary>
        /// True when the frame is limited by something OTHER than the work in it — vsync, a
        /// target frame rate, or any other present wait — with <paramref name="idleMs"/> set
        /// to how much of the frame was spent waiting.
        ///
        /// WHY THIS EXISTS RATHER THAN <see cref="IsAtCap"/>. IsAtCap asks the platform what
        /// the cap IS, via <see cref="TargetFpsCap"/> → <c>Screen.currentResolution
        /// .refreshRateRatio</c>, which in the EDITOR does not report a real refresh rate. So
        /// a vsync-locked editor frame reads "no cap", falls through to
        /// <see cref="Classify"/>, and is confidently named CPU-bound. Measured live: 8.4 ms
        /// frames at 120 FPS containing 3.2 ms CPU and 0.4 ms GPU — 57% of every frame idle —
        /// reported as "CPU-bound".
        ///
        /// This asks a different question, of two numbers that are always available: is the
        /// frame much longer than the work in it? That needs no refresh rate, holds when
        /// vsync is applied by something the game never told (a compositor, a driver
        /// override, the editor itself), and degrades to "not capped" rather than to a wrong
        /// processor when the inputs are missing.
        ///
        /// It matters because a capped frame cannot MEASURE: with 4.8 ms of idle per frame,
        /// any change costing less than that moves neither FPS nor frame time, and an A/B run
        /// under a cap reports "no difference" from a test that could not have shown one.
        /// </summary>
        public static bool IsLimitedByPresent(float frameMs, float busyCpuMs, float gpuMs, out float idleMs)
        {
            idleMs = 0f;
            if (frameMs <= 0.001f) return false;

            float work = Mathf.Max(busyCpuMs, SanitizeGpuMs(gpuMs));
            if (work <= 0.001f) return false;   // no timing data — say nothing rather than guess

            idleMs = frameMs - work;
            return idleMs >= PresentIdleFloorMs && idleMs >= frameMs * PresentIdleRatio;
        }

        /// <summary>
        /// True when a frame-rate cap is CONFIGURED at all — distinct from
        /// <see cref="TargetFpsCap"/>, which additionally has to NAME the rate and cannot in
        /// the editor. Pure overload so the decision is testable without a live QualitySettings.
        ///
        /// It exists because <see cref="IsLimitedByPresent"/> answers "is the frame longer
        /// than the work in it", which a cap causes but does not exclusively cause: in the
        /// editor <c>Time.unscaledDeltaTime</c> (the frame clock) also carries editor-only
        /// work that FrameTimingManager's <c>cpuMainThreadFrameTime</c> never attributes, so
        /// an UNCAPPED editor frame still measures a couple of ms of slack. Reporting that as
        /// "Capped" names a cause that is not there — measured live at
        /// <c>vsync off · target uncapped</c> still reading "Capped — 2.8 ms idle".
        /// </summary>
        public static bool IsFrameCapConfigured(int vSyncCount, int targetFrameRate) =>
            vSyncCount > 0 || targetFrameRate > 0;

        /// <summary>Live reading of <see cref="IsFrameCapConfigured(int,int)"/>.</summary>
        public static bool IsFrameCapConfigured() =>
            IsFrameCapConfigured(QualitySettings.vSyncCount, Application.targetFrameRate);

        /// <summary>
        /// How much longer than the cap's own budget a frame may run and still be called
        /// "capped". A cap holds a frame AT its budget; it cannot make a frame arbitrarily
        /// longer. Generous (2x) because the editor's frame clock carries work
        /// FrameTimingManager never attributes, and because a cap that is being MISSED still
        /// reads as the cap for a frame or two.
        /// </summary>
        const float CapBudgetTolerance = 2f;

        /// <summary>
        /// True when the idle in a frame is CONSISTENT with the configured cap — i.e. the frame
        /// lands at or near the cap's own budget rather than wildly past it.
        ///
        /// WHY THIS IS SEPARATE FROM <see cref="IsFrameCapConfigured"/>. A configured cap is
        /// evidence that a cap COULD be binding, not that it IS. Measured live: a 90.9 ms frame
        /// containing 4.8 ms of main-thread work and 2.2 ms of GPU, with the cap reading
        /// <c>vsync 1 · target 120</c>. Vsync-1 on a 120 Hz display is an 8.3 ms FLOOR — it
        /// cannot produce a 90.9 ms frame — so 86.1 ms was idle for some other reason entirely
        /// (an unfocused editor Game view throttling, in that case). Labelling it "Capped" names
        /// a cause that is not there, and an operator who believes it stops looking.
        ///
        /// <paramref name="capFps"/> is the cap's rate when it can be named
        /// (<see cref="TargetFpsCap"/>), or &lt;= 0 when a cap is configured but unnameable — the
        /// editor case, where <c>Screen.currentResolution.refreshRateRatio</c> reads ~0. With no
        /// nameable rate there is no budget to compare against, so the honest answer is that the
        /// cap is merely PLAUSIBLE, which this reports as true: a configured-but-unnameable cap
        /// keeps the old behaviour rather than silently losing the label it earned in §1.5.
        /// </summary>
        public static bool IsIdleConsistentWithCap(float frameMs, float capFps)
        {
            if (capFps <= 0f) return true;              // cap configured but unnameable — see above
            float budgetMs = 1000f / capFps;
            return frameMs <= budgetMs * CapBudgetTolerance;
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

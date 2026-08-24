#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Automated A/B envelope benchmark for the prism-grid explosion rig
    /// (<see cref="PrismGridExplosionHarness"/>). Runs N repetitions of:
    /// rebuild the lattice fresh → settle → record a pre-roll baseline →
    /// detonate → record every frame's unscaled delta time across the FULL
    /// explosion interval → persist the run as JSON. Results land OUTSIDE
    /// Assets (project-root <c>BenchmarkResults/PrismExplosion/</c>) so they
    /// survive branch switches — run the series once on the legacy-CPU
    /// baseline branch and once on the gpu-clock branch, then generate the
    /// comparison with FrogletTools ▸ Performance ▸ Prism Grid Benchmark▸
    /// Generate Comparison Report.
    ///
    /// BRANCH-PORTABLE: compiles against both worlds. The variant label is
    /// detected by reflection (PrismScaleManager exists only on legacy
    /// branches), never by referencing branch-specific APIs.
    ///
    /// Start it from the harness panel's <b>Bench</b> button, the console
    /// command <c>bench [runs]</c> (DiagnosticsHUD), or the component context
    /// menu. <c>bench stop</c> cancels after the current run.
    /// </summary>
    [RequireComponent(typeof(PrismGridExplosionHarness))]
    public class PrismExplosionBenchmark : MonoBehaviour
    {
        const string StatsSection = "Bench";
        const string CommandName = "bench";

        [Header("Series")]
        [Tooltip("Repetitions per series. Each run rebuilds the lattice from scratch so every detonation hits an identical, fully-materialized grid.")]
        [SerializeField, Min(1)] int runs = 5;

        [Header("Recording window (seconds, unscaled)")]
        [Tooltip("Baseline recorded BEFORE detonation — the resting-lattice envelope the explosion cost is judged against.")]
        [SerializeField, Min(0f)] float preRollSeconds = 1f;
        [Tooltip("Recorded AFTER detonation. Must cover the FULL explosion interval: the visual " +
                 "wavefront, the per-prism debris/fade effects (5s at full length), and — only when " +
                 "the safety throttles are NOT lifted — the damage-backlog drain (48 destructions/" +
                 "frame, ~19s for the inscribed 47³ kill at 60fps; the frame-locked drain makes " +
                 "slower variants show LONGER tails, which is signal). With the rig's default " +
                 "throttle lifts every contained prism dies the frame the wavefront reaches it, so " +
                 "the whole interval ends at wavefront + 5s effect tail.")]
        [SerializeField, Min(1f)] float windowSeconds = 20f;
        [Tooltip("Quiet time between the lattice reporting Ready and recording starting, so spawn-tail hitching can't pollute the baseline.")]
        [SerializeField, Min(0f)] float settleSeconds = 1.5f;
        [Tooltip("Give up waiting for a lattice rebuild after this long (materialization is ~6 prisms/frame, so large grids take minutes).")]
        [SerializeField, Min(10f)] float readyTimeoutSeconds = 900f;

        PrismGridExplosionHarness _harness;
        bool _running;
        bool _cancelRequested;

        // Per-frame capture. Update() only appends — all bookkeeping lives in the driver.
        bool _recording;
        readonly List<float> _dts = new(8192);

        /// <summary>Project-root results folder — OUTSIDE Assets so runs survive branch switches
        /// and Unity never imports them.</summary>
        public static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults", "PrismExplosion"));

        [Serializable]
        public class RunResult
        {
            public string variant;        // "legacy-cpu" | "gpu-clock" (reflection-detected)
            public string branch;         // git HEAD at run time (editor only, best effort)
            public string unity;          // Application.unityVersion
            public string series;         // one id per StartSeries invocation
            public int runIndex;          // 1-based within the series
            public string utc;            // run start, ISO-8601
            public int prismTotal;        // live lattice prisms at detonation
            public int countsX, countsY, countsZ;
            public float gapX, gapY, gapZ;
            public float blastRadius;
            public float explosionDuration;   // wavefront full-expansion time (s)
            public bool throttlesLifted;      // safety throttles lifted for this run
            public float preRollSeconds;
            public float windowSeconds;
            public int preRollFrames;     // deltaTimes[0..preRollFrames) are pre-detonation
            public float[] deltaTimes;    // unscaled, one per frame, in capture order
        }

        void Awake() => _harness = GetComponent<PrismGridExplosionHarness>();

        void Start() => DiagnosticsHUD.RegisterCommand(CommandName, HandleCommand);

        void OnDestroy()
        {
            _cancelRequested = true;
            DiagnosticsHUD.ClearStats(StatsSection);
            DiagnosticsHUD.UnregisterCommand(CommandName);
        }

        void Update()
        {
            if (_recording) _dts.Add(Time.unscaledDeltaTime);
        }

        string HandleCommand(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                if (!_running) return "bench: not running";
                _cancelRequested = true;
                return "bench: stopping after the current run";
            }

            int n = runs;
            if (args.Length > 0 && int.TryParse(args[0], out int parsed)) n = Mathf.Max(1, parsed);
            if (_running) return "bench: already running (bench stop to cancel)";
            StartSeries(n);
            return $"bench: {n} runs → {OutputDirectory}";
        }

        [ContextMenu("Run Benchmark Series")]
        public void StartSeries() => StartSeries(runs);

        public void StartSeries(int n)
        {
            if (_running || _harness == null) return;
            RunSeriesAsync(Mathf.Max(1, n)).Forget();
        }

        async UniTaskVoid RunSeriesAsync(int n)
        {
            _running = true;
            _cancelRequested = false;

            string variant = DetectVariant();
            string branch = DetectBranch();
            string series = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(OutputDirectory);

            Debug.Log($"[PrismExplosionBenchmark] Series {series} starting: {n} runs, variant '{variant}' " +
                      $"(branch {branch}), window -{preRollSeconds:F1}s..+{windowSeconds:F1}s → {OutputDirectory}");

            int completed = 0;
            try
            {
                for (int run = 1; run <= n && !_cancelRequested; run++)
                {
                    Publish($"run {run}/{n}", "rebuilding lattice");

                    // Identical initial conditions per run: always rebuild from scratch.
                    if (!_harness.IsIdle)
                    {
                        _harness.Clear();
                        float clearWait = (_harness.Config != null ? _harness.Config.ClearSeconds : 0.5f) + 0.5f;
                        await UniTask.Delay(TimeSpan.FromSeconds(clearWait), DelayType.UnscaledDeltaTime,
                            cancellationToken: this.GetCancellationTokenOnDestroy());
                    }
                    _harness.Spawn();

                    float waitStart = Time.unscaledTime;
                    while (!_harness.IsReady && !_cancelRequested)
                    {
                        if (Time.unscaledTime - waitStart > readyTimeoutSeconds)
                        {
                            Debug.LogError("[PrismExplosionBenchmark] Lattice never reached Ready — aborting series. " +
                                           "Shrink the grid or raise readyTimeoutSeconds.");
                            _cancelRequested = true;
                            break;
                        }
                        Publish($"run {run}/{n}", "materializing…");
                        await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
                    }
                    if (_cancelRequested) break;

                    // An empty lattice "runs" fine and writes plausible-looking numbers —
                    // refuse it: a benchmark of nothing is worse than no benchmark.
                    if (_harness.LivePrismCount == 0)
                    {
                        Debug.LogError("[PrismExplosionBenchmark] Lattice is Ready but EMPTY (0 live prisms) — " +
                                       "the lay failed (see the harness readout / console). Aborting series.");
                        break;
                    }

                    Publish($"run {run}/{n}", "settling");
                    await UniTask.Delay(TimeSpan.FromSeconds(settleSeconds), DelayType.UnscaledDeltaTime,
                        cancellationToken: this.GetCancellationTokenOnDestroy());

                    // ── Record: pre-roll baseline → detonate → full explosion window ──
                    var result = new RunResult
                    {
                        variant = variant,
                        branch = branch,
                        unity = Application.unityVersion,
                        series = series,
                        runIndex = run,
                        utc = DateTime.UtcNow.ToString("o"),
                        prismTotal = _harness.LivePrismCount,
                        countsX = _harness.Counts.x, countsY = _harness.Counts.y, countsZ = _harness.Counts.z,
                        gapX = _harness.Gaps.x, gapY = _harness.Gaps.y, gapZ = _harness.Gaps.z,
                        blastRadius = _harness.EffectiveBlastRadius,
                        explosionDuration = _harness.EffectiveExplosionDuration,
                        throttlesLifted = _harness.ThrottlesLifted,
                        preRollSeconds = preRollSeconds,
                        windowSeconds = windowSeconds,
                    };

                    _dts.Clear();
                    _recording = true;
                    Publish($"run {run}/{n}", "recording (pre-roll)");
                    await UniTask.Delay(TimeSpan.FromSeconds(preRollSeconds), DelayType.UnscaledDeltaTime,
                        cancellationToken: this.GetCancellationTokenOnDestroy());

                    result.preRollFrames = _dts.Count;
                    _harness.Explode();
                    Publish($"run {run}/{n}", "recording (explosion)");
                    await UniTask.Delay(TimeSpan.FromSeconds(windowSeconds), DelayType.UnscaledDeltaTime,
                        cancellationToken: this.GetCancellationTokenOnDestroy());
                    _recording = false;

                    result.deltaTimes = _dts.ToArray();
                    string path = Path.Combine(OutputDirectory,
                        $"{variant}_{series}_run{run:D2}.json");
                    File.WriteAllText(path, JsonUtility.ToJson(result));
                    completed++;

                    int frames = result.deltaTimes.Length - result.preRollFrames;
                    float sum = 0f;
                    for (int i = result.preRollFrames; i < result.deltaTimes.Length; i++) sum += result.deltaTimes[i];
                    float meanFps = frames > 0 && sum > 0f ? frames / sum : 0f;
                    Debug.Log($"[PrismExplosionBenchmark] run {run}/{n}: {frames} frames over {sum:F2}s " +
                              $"→ mean {meanFps:F1} fps ({Path.GetFileName(path)})");
                    Publish($"run {run}/{n}", $"done — mean {meanFps:F0} fps");
                }
            }
            finally
            {
                _recording = false;
                _running = false;
                Publish("idle", $"{completed} runs saved");
                Debug.Log($"[PrismExplosionBenchmark] Series {series} finished: {completed} runs in {OutputDirectory}. " +
                          "Run the OTHER branch's series into the same folder, then " +
                          "FrogletTools > Performance > Prism Grid Benchmark> Generate Comparison Report.");
            }
        }

        void Publish(string state, string detail)
        {
            DiagnosticsHUD.SetStat(StatsSection, "state", state);
            DiagnosticsHUD.SetStat(StatsSection, "detail", detail);
        }

        /// <summary>Reflection-based so this file compiles on every branch: the legacy
        /// CPU animation manager type only exists on pre-clock branches.</summary>
        static string DetectVariant() =>
            Type.GetType("CosmicShore.Gameplay.PrismScaleManager, Assembly-CSharp") != null
                ? "legacy-cpu"
                : "gpu-clock";

        static string DetectBranch()
        {
            try
            {
                string head = File.ReadAllText(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".git", "HEAD"))).Trim();
                const string prefix = "ref: refs/heads/";
                return head.StartsWith(prefix) ? head.Substring(prefix.Length) : head;
            }
            catch { return "unknown"; }
        }
    }
}
#endif

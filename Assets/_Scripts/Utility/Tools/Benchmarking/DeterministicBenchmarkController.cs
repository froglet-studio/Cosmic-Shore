using UnityEngine;

namespace CosmicShore.Utility.Tools.Benchmarking
{
    /// <summary>
    /// Forces deterministic scene conditions for repeatable benchmark runs.
    /// Attach via BenchmarkWindow when deterministic mode is enabled.
    ///
    /// What it does:
    /// - Seeds UnityEngine.Random with a fixed value
    /// - Locks physics timestep to a fixed value
    /// - Waits a fixed number of rendered frames (not wall-clock time) before
    ///   signalling that the scene is stable and sampling can begin.
    /// </summary>
    public class DeterministicBenchmarkController : MonoBehaviour
    {
        [Header("Deterministic Settings")]
        [SerializeField, Tooltip("Fixed seed for UnityEngine.Random. Same seed = same random sequence.")]
        int _seed = 42;

        [SerializeField, Tooltip("Fixed physics timestep to eliminate physics timing variance.")]
        float _fixedDeltaTime = 0.02f;

        [SerializeField, Tooltip("Number of rendered frames to wait before the scene is considered stable.")]
        int _warmupFrames = 120;

        // ── State ──────────────────────────────────────────────────────────
        int _framesElapsed;
        bool _ready;
        float _originalFixedDeltaTime;
        int _originalVSyncCount;
        int _originalTargetFrameRate;

        // ── Public API ─────────────────────────────────────────────────────

        public bool IsReady => _ready;
        public int FramesRemaining => Mathf.Max(0, _warmupFrames - _framesElapsed);
        public int Seed => _seed;

        public void Configure(int seed, int warmupFrames, float fixedDeltaTime)
        {
            _seed = seed;
            _warmupFrames = warmupFrames;
            _fixedDeltaTime = fixedDeltaTime;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        void Awake()
        {
            ApplyDeterministicSettings();
        }

        void Update()
        {
            if (_ready) return;

            _framesElapsed++;
            if (_framesElapsed >= _warmupFrames)
            {
                _ready = true;
                // Re-seed right before sampling starts so the random sequence
                // during the measured window is identical every run.
                Random.InitState(_seed);
            }
        }

        void OnDestroy()
        {
            RestoreSettings();
        }

        // ── Internals ──────────────────────────────────────────────────────

        void ApplyDeterministicSettings()
        {
            // Seed the random number generator
            Random.InitState(_seed);

            // Lock physics timestep
            _originalFixedDeltaTime = Time.fixedDeltaTime;
            Time.fixedDeltaTime = _fixedDeltaTime;

            // Disable VSync to remove frame-pacing variance from measurements
            _originalVSyncCount = QualitySettings.vSyncCount;
            _originalTargetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1; // uncapped

            // Disable physics auto-simulation jitter (noise from variable sub-steps)
            Physics.simulationMode = SimulationMode.FixedUpdate;

            CSDebug.Log($"[Benchmark] Deterministic mode ON — seed={_seed}, fixedDt={_fixedDeltaTime}, warmupFrames={_warmupFrames}");
        }

        void RestoreSettings()
        {
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            QualitySettings.vSyncCount = _originalVSyncCount;
            Application.targetFrameRate = _originalTargetFrameRate;

            CSDebug.Log("[Benchmark] Deterministic mode OFF — settings restored.");
        }
    }
}

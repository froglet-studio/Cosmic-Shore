using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using Obvious.Soap;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Behind-script for the in-scene benchmark overlay. The UI author builds the visuals (canvas,
    /// images, labels, buttons); this drives them: a live FPS / frame-time / 1%-low readout (the
    /// "see which is best for me" signal), quick graphics toggles that change settings on the fly via
    /// <see cref="DisplayGraphicsSettings"/>, and a Run button that drives the author's
    /// <see cref="PerformanceBenchmarkRunner"/> to save a full report for the editor Benchmark window.
    ///
    /// All UI references are serialized and null-tolerant - a minimal HUD (just the FPS label) works.
    /// Hook the quick-setting buttons to the public Cycle*/Toggle*/AutoDetect methods.
    /// </summary>
    public class BenchmarkSceneHud : MonoBehaviour
    {
        [Header("Live readout")]
        [SerializeField] TMP_Text fpsText;
        [SerializeField] TMP_Text frameTimeText;
        [SerializeField] TMP_Text onePercentLowText;
        [SerializeField] TMP_Text statusText;

        [Header("Current-settings labels")]
        [SerializeField] TMP_Text qualityLabel;
        [SerializeField] TMP_Text antiAliasingLabel;
        [SerializeField] TMP_Text vsyncLabel;
        [SerializeField] TMP_Text frameCapLabel;
        [SerializeField] TMP_Text ecosystemLabel;

        [Header("Formal benchmark run")]
        [SerializeField, Tooltip("Assign the existing BenchmarkConfig.asset. If empty, the Run button is hidden.")]
        BenchmarkConfigSO benchmarkConfig;
        [SerializeField] Button runBenchmarkButton;

        [Header("Exit")]
        [SerializeField] Button exitButton;
        [SerializeField, Tooltip("Same OnClickToMainMenuButton event asset SceneLoader listens to.")]
        ScriptableEventNoParam onClickToMainMenuButton;

        [Inject] GameDataSO gameData;

        const int SampleWindow = 240;
        readonly float[] _frameMs = new float[SampleWindow];
        int _frameCursor;
        int _frameFilled;
        float _smoothFps;
        float _refreshTimer;

        PerformanceBenchmarkRunner _runner;

        void OnEnable() => DisplayGraphicsSettings.OnAnySettingChanged += HandleSettingsChanged;
        void OnDisable() => DisplayGraphicsSettings.OnAnySettingChanged -= HandleSettingsChanged;

        void Start()
        {
            if (runBenchmarkButton)
            {
                runBenchmarkButton.gameObject.SetActive(benchmarkConfig != null);
                runBenchmarkButton.onClick.AddListener(RunFormalBenchmark);
            }
            if (exitButton) exitButton.onClick.AddListener(ExitToMenu);

            _smoothFps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            RefreshSettingLabels();
            SetStatus("Free flight - fly around, then Run Benchmark.");
        }

        void Update()
        {
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);

            // Smoothed live FPS + ring buffer of frame times for the 1% low.
            _smoothFps = Mathf.Lerp(_smoothFps, 1f / dt, 0.1f);
            _frameMs[_frameCursor] = dt * 1000f;
            _frameCursor = (_frameCursor + 1) % SampleWindow;
            _frameFilled = Mathf.Min(_frameFilled + 1, SampleWindow);

            _refreshTimer += dt;
            if (_refreshTimer < 0.2f) return;
            _refreshTimer = 0f;

            if (fpsText) fpsText.text = $"{Mathf.RoundToInt(_smoothFps)} FPS";
            if (frameTimeText) frameTimeText.text = $"{1000f / Mathf.Max(1f, _smoothFps):0.0} ms";
            if (onePercentLowText) onePercentLowText.text = $"1% low: {Mathf.RoundToInt(OnePercentLowFps())} FPS";

            if (_runner != null && statusText)
            {
                if (_runner.IsRunning)
                    SetStatus($"Benchmarking… {_runner.Progress * 100f:0}%");
                else
                {
                    SetStatus(string.IsNullOrEmpty(_runner.LastReportPath)
                        ? "Benchmark finished."
                        : $"Benchmark saved → {_runner.LastReportPath}");
                    _runner = null;
                }
            }
        }

        float OnePercentLowFps()
        {
            int n = _frameFilled;
            if (n < 10) return _smoothFps;

            var copy = new float[n];
            Array.Copy(_frameMs, copy, n);
            Array.Sort(copy);                 // ascending ms
            int idx = Mathf.Clamp(Mathf.FloorToInt(n * 0.99f), 0, n - 1);
            float worstMs = copy[idx];
            return worstMs > 0f ? 1000f / worstMs : _smoothFps;
        }

        // ── Quick settings (wire buttons to these) ──

        public void CycleQuality()
        {
            var s = DisplayGraphicsSettings.Instance;
            if (s == null) return;
            int next = (int)s.Current.QualityPreset;
            next = next < (int)QualityPresetSetting.Ultra ? Mathf.Max(0, next) + 1 : 0;
            s.SetQualityPreset((QualityPresetSetting)Mathf.Clamp(next, 0, (int)QualityPresetSetting.Ultra));
        }

        public void CycleAntiAliasing()
        {
            var s = DisplayGraphicsSettings.Instance;
            if (s == null) return;
            int next = ((int)s.Current.AntiAliasing + 1) % ((int)AntiAliasingSetting.TAA + 1);
            s.SetAntiAliasing((AntiAliasingSetting)next);
        }

        public void ToggleVSync()
        {
            var s = DisplayGraphicsSettings.Instance;
            if (s == null) return;
            s.SetVSync(s.Current.VSync == VSyncSetting.Off ? VSyncSetting.On : VSyncSetting.Off);
        }

        public void CycleFrameCap()
        {
            var s = DisplayGraphicsSettings.Instance;
            if (s == null) return;
            int[] caps = { 30, 60, 120, 144, 240, -1 };
            int cur = s.Current.TargetFrameRate;
            int i = Array.IndexOf(caps, cur);
            s.SetTargetFrameRate(caps[(i + 1 + caps.Length) % caps.Length]);
        }

        public void CycleEcosystemDensity()
        {
            var s = DisplayGraphicsSettings.Instance;
            if (s == null) return;
            int next = ((int)s.Current.EcosystemDensity + 1) % ((int)EcosystemDensitySetting.Lush + 1);
            s.SetEcosystemDensity((EcosystemDensitySetting)next);
        }

        public void AutoDetect() => DisplayGraphicsSettings.Instance?.ApplyAutoDetect();

        void HandleSettingsChanged(GraphicsSettingsData _) => RefreshSettingLabels();

        void RefreshSettingLabels()
        {
            var s = DisplayGraphicsSettings.Instance;
            if (s == null) return;
            var d = s.Current;
            if (qualityLabel) qualityLabel.text = $"Quality: {d.QualityPreset}";
            if (antiAliasingLabel) antiAliasingLabel.text = $"AA: {d.AntiAliasing}";
            if (vsyncLabel) vsyncLabel.text = $"VSync: {d.VSync}";
            if (frameCapLabel) frameCapLabel.text = $"Cap: {(d.TargetFrameRate <= 0 ? "Uncapped" : d.TargetFrameRate + " FPS")}";
            if (ecosystemLabel) ecosystemLabel.text = $"Ecosystem: {d.EcosystemDensity}";
        }

        // ── Formal run + exit ──

        void RunFormalBenchmark()
        {
            if (benchmarkConfig == null || (_runner != null && _runner.IsRunning)) return;

            var go = new GameObject("[BenchmarkRunner]");
            go.transform.SetParent(transform, false);
            _runner = go.AddComponent<PerformanceBenchmarkRunner>();
            _runner.Configure(benchmarkConfig, gameDataContainer: gameData);
            _runner.StartBenchmark(); // warmup → sample → auto-save
            SetStatus("Benchmark starting…");
        }

        void ExitToMenu()
        {
            if (onClickToMainMenuButton) onClickToMainMenuButton.Raise();
        }

        void SetStatus(string msg)
        {
            if (statusText) statusText.text = msg;
        }
    }
}

using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Behind-script for the reworked 4-tab options panel (GENERAL = accessibility + legal, DISPLAY,
    /// PERFORMANCE = graphics + perf, OTHER = controls + audio). One component the whole canvas wires
    /// to. The UI author designs the visuals; this routes control events onto the backend stores
    /// (<see cref="DisplayGraphicsSettings"/>, <see cref="GameSetting"/>, <see cref="AccessibilitySettings"/>,
    /// <see cref="AnalyticsServiceFacade"/>) and exposes Current* getters so each row can show its
    /// saved value on open.
    ///
    /// Toggles are ON/OFF (segmented), so the setters take an explicit bool — wire ON → Set*(true),
    /// OFF → Set*(false). Requires a Reflex ContainerScope in the scene (Menu_Main + in-game both have one).
    /// </summary>
    public class GameSettingsPanelController : MonoBehaviour
    {
        [Inject] GameSetting gameSetting;
        [Inject] AnalyticsServiceFacade analytics;

        [Header("Display — resolution dropdown (auto-populated; assign it)")]
        [SerializeField] TMP_Dropdown resolutionDropdown;

        [Header("General — version label")]
        [SerializeField] TMP_Text versionText;

        [Header("Performance — Benchmark button target")]
        [SerializeField] BenchmarkSceneLauncher benchmarkLauncher;

        [Header("General tab — links")]
        [SerializeField] string privacyPolicyUrl = "https://cosmicshore.com/privacy";
        [SerializeField] string termsUrl = "https://cosmicshore.com/terms";
        [SerializeField, Tooltip("Web/email form for data-deletion requests (right-to-erasure).")]
        string deleteDataUrl = "https://cosmicshore.com/delete-my-data";
        [SerializeField, Tooltip("Support / bug-report URL (Discord, issue tracker, or feedback form).")]
        string bugReportUrl = "https://cosmicshore.com/support";

        static readonly int[] FrameCaps = { 30, 60, 120, 144, -1 };      // -1 = Uncapped (last option)
        static readonly float[] SubtitleScales = { 0.85f, 1.0f, 1.25f }; // Small / Medium / Large
        readonly List<Resolution> _resolutions = new();

        DisplayGraphicsSettings S => DisplayGraphicsSettings.Instance;

        void Start()
        {
            PopulateResolutionDropdown();
            if (versionText) versionText.text = $"Version {Application.version}";
        }

        // ════════════════════════ GENERAL (accessibility + legal) ════════════════════════

        public void SetColorblindModeIndex(int index) => AccessibilitySettings.ColorblindMode = (ColorblindModeSetting)index;
        public void SetSubtitles(bool on) => AccessibilitySettings.Subtitles = on;
        public void SetSubtitleScaleIndex(int index) => AccessibilitySettings.SubtitleScale = SubtitleScales[Mathf.Clamp(index, 0, SubtitleScales.Length - 1)];
        public void SetAnalyticsConsent(bool granted) => analytics?.SetConsent(granted);

        public void OpenBugReport() => OpenUrl(bugReportUrl);
        public void OpenPrivacyPolicy() => OpenUrl(privacyPolicyUrl);
        public void OpenTerms() => OpenUrl(termsUrl);
        public void OpenDeleteDataForm() => OpenUrl(deleteDataUrl);

        public int ColorblindIndex => (int)AccessibilitySettings.ColorblindMode;
        public bool SubtitlesOn => AccessibilitySettings.Subtitles;
        public int SubtitleScaleIndex => NearestScaleIndex(AccessibilitySettings.SubtitleScale);
        public bool ConsentOn => analytics != null && analytics.ConsentGranted;

        // ════════════════════════ DISPLAY ════════════════════════

        public void SetDisplayModeIndex(int index) => S?.SetDisplayMode((DisplayModeSetting)index);

        public void SetResolutionIndex(int index)
        {
            if (S == null || index < 0 || index >= _resolutions.Count) return;
            var r = _resolutions[index];
            S.SetResolution(r.width, r.height);
        }

        /// <summary>Dropdown order: 30 / 60 / 120 / 144 / Uncapped.</summary>
        public void SetFrameCapIndex(int index)
        {
            if (S == null || index < 0 || index >= FrameCaps.Length) return;
            S.SetTargetFrameRate(FrameCaps[index]);
        }

        public void SetVSync(bool on) => S?.SetVSync(on ? VSyncSetting.On : VSyncSetting.Off);
        public void SetFieldOfView(float fov) => S?.SetFieldOfView(fov);

        public int DisplayModeIndex => S != null ? (int)S.Current.DisplayMode : 0;
        public int ResolutionIndex => CurrentResolutionIndex();
        public int FrameCapIndex => FrameCapToIndex(S != null ? S.Current.TargetFrameRate : 60);
        public bool VSyncOn => S != null && S.Current.VSync != VSyncSetting.Off;
        public float FieldOfView => S != null ? S.Current.FieldOfView : 60f;

        // ════════════════════════ PERFORMANCE (graphics + perf) ════════════════════════

        public void AutoDetect() => S?.ApplyAutoDetect();
        public void RunBenchmark() => benchmarkLauncher?.LaunchBenchmark();

        /// <summary>Dropdown order maps 0..5 to Very Low..Ultra.</summary>
        public void SetQualityPresetIndex(int index) => S?.SetQualityPreset((QualityPresetSetting)index);
        public void SetAntiAliasingIndex(int index) => S?.SetAntiAliasing((AntiAliasingSetting)index);
        public void SetTextureQualityIndex(int index) => S?.SetTextureQuality(index);
        public void SetUpscalingIndex(int index) => S?.SetUpscaling((UpscalingSetting)index);
        public void SetAdaptivePerformanceIndex(int index) => S?.SetAdaptivePerformance((AdaptivePerformanceSetting)index);
        public void SetPhysicsDetailIndex(int index) => S?.SetPhysicsDetail((PhysicsDetailSetting)index);

        /// <summary>Current quality tier index, or -1 when an individual override put it on "Custom".</summary>
        public int QualityIndex => S != null ? (int)S.Current.QualityPreset : 0;
        public int AntiAliasingIndex => S != null ? (int)S.Current.AntiAliasing : 0;
        public int TextureQualityIndex => S != null ? S.Current.TextureQuality : 0;
        public int UpscalingIndex => S != null ? (int)S.Current.Upscaling : 0;
        public int AdaptivePerformanceIndex => S != null ? (int)S.Current.AdaptivePerformance : 0;
        public int PhysicsDetailIndex => S != null ? (int)S.Current.PhysicsDetail : 0;

        // ════════════════════════ OTHER (controls + audio) ════════════════════════

        public void SetInvertY(bool on) { if (gameSetting != null && gameSetting.InvertYEnabled != on) gameSetting.ChangeInvertYEnabledStatus(); }
        public void SetInvertThrottle(bool on) { if (gameSetting != null && gameSetting.InvertThrottleEnabled != on) gameSetting.ChangeInvertThrottleEnabledStatus(); }
        public void SetMusic(bool on) { if (gameSetting != null && gameSetting.MusicEnabled != on) gameSetting.ChangeMusicEnabledSetting(); }
        public void SetSFX(bool on) { if (gameSetting != null && gameSetting.SFXEnabled != on) gameSetting.ChangeSFXEnabledSetting(); }
        public void SetHaptics(bool on) { if (gameSetting != null && gameSetting.HapticsEnabled != on) gameSetting.ChangeHapticsEnabledSetting(); }

        public void SetMusicLevel(float level) => gameSetting?.SetMusicLevel(level);
        public void SetSFXLevel(float level) => gameSetting?.SetSFXLevel(level);
        public void SetHapticsLevel(float level) => gameSetting?.SetHapticsLevel(level);

        public bool InvertYOn => gameSetting != null && gameSetting.InvertYEnabled;
        public bool InvertThrottleOn => gameSetting != null && gameSetting.InvertThrottleEnabled;
        public bool MusicOn => gameSetting != null && gameSetting.MusicEnabled;
        public bool SFXOn => gameSetting != null && gameSetting.SFXEnabled;
        public bool HapticsOn => gameSetting != null && gameSetting.HapticsEnabled;
        public float MusicLevel => gameSetting != null ? gameSetting.MusicLevel : 1f;
        public float SFXLevel => gameSetting != null ? gameSetting.SFXLevel : 1f;
        public float HapticsLevel => gameSetting != null ? gameSetting.HapticsLevel : 1f;

        // ════════════════════════ helpers ════════════════════════

        void PopulateResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            _resolutions.Clear();
            _resolutions.Add(new Resolution { width = 0, height = 0 }); // index 0 = Native

            var seen = new HashSet<(int, int)>();
            foreach (var r in Screen.resolutions)
                if (seen.Add((r.width, r.height)))
                    _resolutions.Add(r);

            resolutionDropdown.ClearOptions();
            var labels = new List<string> { "Native" };
            for (int i = 1; i < _resolutions.Count; i++)
                labels.Add($"{_resolutions[i].width} × {_resolutions[i].height}");
            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.SetValueWithoutNotify(CurrentResolutionIndex());
            resolutionDropdown.onValueChanged.RemoveListener(SetResolutionIndex);
            resolutionDropdown.onValueChanged.AddListener(SetResolutionIndex);
        }

        int CurrentResolutionIndex()
        {
            if (S == null) return 0;
            int w = S.Current.ResolutionWidth, h = S.Current.ResolutionHeight;
            if (w <= 0 || h <= 0) return 0; // Native
            for (int i = 1; i < _resolutions.Count; i++)
                if (_resolutions[i].width == w && _resolutions[i].height == h) return i;
            return 0;
        }

        static int FrameCapToIndex(int fps)
        {
            if (fps <= 0) return FrameCaps.Length - 1; // Uncapped
            int idx = System.Array.IndexOf(FrameCaps, fps);
            return idx >= 0 ? idx : 1; // default 60
        }

        static int NearestScaleIndex(float scale)
        {
            int best = 1; float bestDiff = float.MaxValue;
            for (int i = 0; i < SubtitleScales.Length; i++)
            {
                float d = Mathf.Abs(SubtitleScales[i] - scale);
                if (d < bestDiff) { bestDiff = d; best = i; }
            }
            return best;
        }

        static void OpenUrl(string url)
        {
            if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }
    }
}

using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Self-wiring controller for the 4-tab options panel. Drag each UI control into its serialized
    /// slot and you're done — on <c>Start</c> the controller populates every dropdown's options (from
    /// the enums, so order can never drift), sets each control to its saved value, and attaches the
    /// change listener. Reopening the panel refreshes displayed values. No per-control UnityEvent
    /// wiring, no option authoring, no init code.
    ///
    /// ON/OFF rows: assign a Unity <see cref="Toggle"/> if that's what your row uses. If your segmented
    /// ON/OFF is a custom two-button widget, leave the Toggle slot empty and wire your buttons to the
    /// public <c>Set*(bool)</c> methods (read the <c>*On</c> getter for the highlight).
    ///
    /// Routes to: <see cref="DisplayGraphicsSettings"/> (display/graphics/perf), <see cref="GameSetting"/>
    /// (audio/controls), <see cref="AccessibilitySettings"/>, <see cref="AnalyticsServiceFacade"/>
    /// (consent), <see cref="Application.OpenURL"/> (links). Needs a Reflex ContainerScope in the scene.
    /// </summary>
    public class GameSettingsPanelController : MonoBehaviour
    {
        [Inject] GameSetting gameSetting;
        [Inject] AnalyticsServiceFacade analytics;

        [Header("GENERAL — accessibility + legal")]
        [SerializeField] TMP_Dropdown colorblindDropdown;
        [SerializeField] Toggle subtitlesToggle;
        [SerializeField] TMP_Dropdown subtitleScaleDropdown;
        [SerializeField] Toggle analyticsConsentToggle;
        [SerializeField] Button bugReportButton;
        [SerializeField] Button privacyPolicyButton;
        [SerializeField] Button deleteDataButton;
        [SerializeField] TMP_Text versionText;

        [Header("DISPLAY")]
        [SerializeField] TMP_Dropdown displayModeDropdown;
        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] TMP_Dropdown frameCapDropdown;
        [SerializeField] Toggle vsyncToggle;
        [SerializeField] Slider fovSlider;
        [SerializeField] float fovMin = 60f, fovMax = 90f;

        [Header("PERFORMANCE — graphics + perf")]
        [SerializeField] TMP_Dropdown qualityDropdown;
        [SerializeField] TMP_Dropdown antiAliasingDropdown;
        [SerializeField] TMP_Dropdown textureQualityDropdown;
        [SerializeField] TMP_Dropdown upscalingDropdown;
        [SerializeField] TMP_Dropdown adaptivePerformanceDropdown;
        [SerializeField] TMP_Dropdown physicsDetailDropdown;
        [SerializeField] Button autoDetectButton;
        [SerializeField] Button benchmarkButton;
        [SerializeField] BenchmarkSceneLauncher benchmarkLauncher;

        [Header("OTHER — controls + audio")]
        [SerializeField] Toggle invertYToggle;
        [SerializeField] Toggle invertThrottleToggle;
        [SerializeField] Toggle musicToggle;
        [SerializeField] Slider musicSlider;
        [SerializeField] Toggle sfxToggle;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Toggle hapticsToggle;
        [SerializeField] Slider hapticsSlider;

        [Header("General tab — links")]
        [SerializeField] string privacyPolicyUrl = "https://cosmicshore.com/privacy";
        [SerializeField] string deleteDataUrl = "https://cosmicshore.com/delete-my-data";
        [SerializeField] string bugReportUrl = "https://cosmicshore.com/support";

        // Option labels (index == enum value — never author these in the dropdown, the controller fills them)
        static readonly string[] ColorblindOpts = { "Off", "Protanopia", "Deuteranopia", "Tritanopia" };
        static readonly string[] SubtitleScaleOpts = { "Small", "Medium", "Large" };
        static readonly string[] DisplayModeOpts = { "Fullscreen", "Borderless", "Windowed" };
        static readonly string[] FrameCapOpts = { "30", "60", "120", "144", "Uncapped" };
        static readonly string[] QualityOpts = { "Very Low", "Low", "Medium", "High", "Very High", "Ultra" };
        static readonly string[] AntiAliasingOpts = { "Off", "FXAA", "SMAA", "MSAA 2x", "MSAA 4x", "MSAA 8x", "TAA" };
        static readonly string[] TextureOpts = { "Full", "Half", "Quarter", "Eighth" };
        static readonly string[] UpscalingOpts = { "Auto", "Linear", "FSR", "STP" };
        static readonly string[] AdaptiveOpts = { "Off", "Balanced", "Aggressive" };
        static readonly string[] PhysicsOpts = { "Low", "High" };

        static readonly int[] FrameCaps = { 30, 60, 120, 144, -1 };
        static readonly float[] SubtitleScales = { 0.85f, 1.0f, 1.25f };
        readonly List<Resolution> _resolutions = new();

        bool _bound;
        DisplayGraphicsSettings S => DisplayGraphicsSettings.Instance;

        void Start()
        {
            BindAll();
            _bound = true;
        }

        void OnEnable()
        {
            // Re-pull current values whenever the panel is shown again (settings may have changed in
            // the benchmark scene or via auto-detect). Skip before the first Start, when injected
            // services aren't ready yet.
            if (_bound) RefreshValues();
        }

        // ───────────────────────── self-wiring ─────────────────────────

        void BindAll()
        {
            if (versionText) versionText.text = $"Version {Application.version}";

            // GENERAL
            BindDropdown(colorblindDropdown, ColorblindOpts, SetColorblindModeIndex);
            BindDropdown(subtitleScaleDropdown, SubtitleScaleOpts, SetSubtitleScaleIndex);
            BindToggle(subtitlesToggle, SetSubtitles);
            BindToggle(analyticsConsentToggle, SetAnalyticsConsent);
            BindButton(bugReportButton, OpenBugReport);
            BindButton(privacyPolicyButton, OpenPrivacyPolicy);
            BindButton(deleteDataButton, OpenDeleteDataForm);

            // DISPLAY
            BindDropdown(displayModeDropdown, DisplayModeOpts, SetDisplayModeIndex);
            PopulateResolutionDropdown();
            BindDropdown(frameCapDropdown, FrameCapOpts, SetFrameCapIndex);
            BindToggle(vsyncToggle, SetVSync);
            BindSlider(fovSlider, fovMin, fovMax, true, SetFieldOfView);

            // PERFORMANCE
            BindDropdown(qualityDropdown, QualityOpts, SetQualityPresetIndex);
            BindDropdown(antiAliasingDropdown, AntiAliasingOpts, SetAntiAliasingIndex);
            BindDropdown(textureQualityDropdown, TextureOpts, SetTextureQualityIndex);
            BindDropdown(upscalingDropdown, UpscalingOpts, SetUpscalingIndex);
            BindDropdown(adaptivePerformanceDropdown, AdaptiveOpts, SetAdaptivePerformanceIndex);
            BindDropdown(physicsDetailDropdown, PhysicsOpts, SetPhysicsDetailIndex);
            BindButton(autoDetectButton, AutoDetect);
            BindButton(benchmarkButton, RunBenchmark);

            // OTHER
            BindToggle(invertYToggle, SetInvertY);
            BindToggle(invertThrottleToggle, SetInvertThrottle);
            BindToggle(musicToggle, SetMusic);
            BindSlider(musicSlider, 0f, 1f, false, SetMusicLevel);
            BindToggle(sfxToggle, SetSFX);
            BindSlider(sfxSlider, 0f, 1f, false, SetSFXLevel);
            BindToggle(hapticsToggle, SetHaptics);
            BindSlider(hapticsSlider, 0f, 1f, false, SetHapticsLevel);

            RefreshValues();
        }

        /// <summary>Pushes the current saved values into every assigned control (no listener changes).</summary>
        void RefreshValues()
        {
            SetDropdown(colorblindDropdown, ColorblindIndex);
            SetDropdown(subtitleScaleDropdown, SubtitleScaleIndex);
            SetToggle(subtitlesToggle, SubtitlesOn);
            SetToggle(analyticsConsentToggle, ConsentOn);

            SetDropdown(displayModeDropdown, DisplayModeIndex);
            SetDropdown(resolutionDropdown, ResolutionIndex);
            SetDropdown(frameCapDropdown, FrameCapIndex);
            SetToggle(vsyncToggle, VSyncOn);
            SetSliderValue(fovSlider, FieldOfView);

            SetDropdown(qualityDropdown, QualityIndex);
            SetDropdown(antiAliasingDropdown, AntiAliasingIndex);
            SetDropdown(textureQualityDropdown, TextureQualityIndex);
            SetDropdown(upscalingDropdown, UpscalingIndex);
            SetDropdown(adaptivePerformanceDropdown, AdaptivePerformanceIndex);
            SetDropdown(physicsDetailDropdown, PhysicsDetailIndex);

            SetToggle(invertYToggle, InvertYOn);
            SetToggle(invertThrottleToggle, InvertThrottleOn);
            SetToggle(musicToggle, MusicOn);
            SetSliderValue(musicSlider, MusicLevel);
            SetToggle(sfxToggle, SFXOn);
            SetSliderValue(sfxSlider, SFXLevel);
            SetToggle(hapticsToggle, HapticsOn);
            SetSliderValue(hapticsSlider, HapticsLevel);
        }

        static void BindDropdown(TMP_Dropdown dd, string[] options, UnityAction<int> onChange)
        {
            if (dd == null) return;
            dd.ClearOptions();
            dd.AddOptions(new List<string>(options));
            dd.onValueChanged.RemoveListener(onChange);
            dd.onValueChanged.AddListener(onChange);
        }

        static void BindSlider(Slider s, float min, float max, bool wholeNumbers, UnityAction<float> onChange)
        {
            if (s == null) return;
            s.minValue = min;
            s.maxValue = max;
            s.wholeNumbers = wholeNumbers;
            s.onValueChanged.RemoveListener(onChange);
            s.onValueChanged.AddListener(onChange);
        }

        static void BindToggle(Toggle t, UnityAction<bool> onChange)
        {
            if (t == null) return;
            t.onValueChanged.RemoveListener(onChange);
            t.onValueChanged.AddListener(onChange);
        }

        static void BindButton(Button b, UnityAction onClick)
        {
            if (b == null) return;
            b.onClick.RemoveListener(onClick);
            b.onClick.AddListener(onClick);
        }

        static void SetDropdown(TMP_Dropdown dd, int index)
        {
            if (dd != null && index >= 0 && index < dd.options.Count) dd.SetValueWithoutNotify(index);
        }

        static void SetToggle(Toggle t, bool on) { if (t != null) t.SetIsOnWithoutNotify(on); }
        static void SetSliderValue(Slider s, float v) { if (s != null) s.SetValueWithoutNotify(v); }

        // ───────────────────────── setters (listeners call these) ─────────────────────────

        public void SetColorblindModeIndex(int index) => AccessibilitySettings.ColorblindMode = (ColorblindModeSetting)index;
        public void SetSubtitles(bool on) => AccessibilitySettings.Subtitles = on;
        public void SetSubtitleScaleIndex(int index) => AccessibilitySettings.SubtitleScale = SubtitleScales[Mathf.Clamp(index, 0, SubtitleScales.Length - 1)];
        public void SetAnalyticsConsent(bool granted) => analytics?.SetConsent(granted);
        public void OpenBugReport() => OpenUrl(bugReportUrl);
        public void OpenPrivacyPolicy() => OpenUrl(privacyPolicyUrl);
        public void OpenDeleteDataForm() => OpenUrl(deleteDataUrl);

        public void SetDisplayModeIndex(int index) => S?.SetDisplayMode((DisplayModeSetting)index);
        public void SetResolutionIndex(int index)
        {
            if (S == null || index < 0 || index >= _resolutions.Count) return;
            var r = _resolutions[index];
            S.SetResolution(r.width, r.height);
        }
        public void SetFrameCapIndex(int index)
        {
            if (S == null || index < 0 || index >= FrameCaps.Length) return;
            S.SetTargetFrameRate(FrameCaps[index]);
        }
        public void SetVSync(bool on) => S?.SetVSync(on ? VSyncSetting.On : VSyncSetting.Off);
        public void SetFieldOfView(float fov) => S?.SetFieldOfView(fov);

        public void AutoDetect() { S?.ApplyAutoDetect(); RefreshValues(); }
        public void RunBenchmark() => benchmarkLauncher?.LaunchBenchmark();
        public void SetQualityPresetIndex(int index) => S?.SetQualityPreset((QualityPresetSetting)index);
        public void SetAntiAliasingIndex(int index) => S?.SetAntiAliasing((AntiAliasingSetting)index);
        public void SetTextureQualityIndex(int index) => S?.SetTextureQuality(index);
        public void SetUpscalingIndex(int index) => S?.SetUpscaling((UpscalingSetting)index);
        public void SetAdaptivePerformanceIndex(int index) => S?.SetAdaptivePerformance((AdaptivePerformanceSetting)index);
        public void SetPhysicsDetailIndex(int index) => S?.SetPhysicsDetail((PhysicsDetailSetting)index);

        public void SetInvertY(bool on) { if (gameSetting != null && gameSetting.InvertYEnabled != on) gameSetting.ChangeInvertYEnabledStatus(); }
        public void SetInvertThrottle(bool on) { if (gameSetting != null && gameSetting.InvertThrottleEnabled != on) gameSetting.ChangeInvertThrottleEnabledStatus(); }
        public void SetMusic(bool on) { if (gameSetting != null && gameSetting.MusicEnabled != on) gameSetting.ChangeMusicEnabledSetting(); }
        public void SetSFX(bool on) { if (gameSetting != null && gameSetting.SFXEnabled != on) gameSetting.ChangeSFXEnabledSetting(); }
        public void SetHaptics(bool on) { if (gameSetting != null && gameSetting.HapticsEnabled != on) gameSetting.ChangeHapticsEnabledSetting(); }
        public void SetMusicLevel(float level) => gameSetting?.SetMusicLevel(level);
        public void SetSFXLevel(float level) => gameSetting?.SetSFXLevel(level);
        public void SetHapticsLevel(float level) => gameSetting?.SetHapticsLevel(level);

        // ───────────────────────── getters (for custom widgets / external readers) ─────────────────────────

        public int ColorblindIndex => (int)AccessibilitySettings.ColorblindMode;
        public bool SubtitlesOn => AccessibilitySettings.Subtitles;
        public int SubtitleScaleIndex => NearestScaleIndex(AccessibilitySettings.SubtitleScale);
        public bool ConsentOn => analytics != null && analytics.ConsentGranted;
        public int DisplayModeIndex => S != null ? (int)S.Current.DisplayMode : 0;
        public int ResolutionIndex => CurrentResolutionIndex();
        public int FrameCapIndex => FrameCapToIndex(S != null ? S.Current.TargetFrameRate : 60);
        public bool VSyncOn => S != null && S.Current.VSync != VSyncSetting.Off;
        public float FieldOfView => S != null ? S.Current.FieldOfView : 60f;
        public int QualityIndex => S != null ? (int)S.Current.QualityPreset : 0;
        public int AntiAliasingIndex => S != null ? (int)S.Current.AntiAliasing : 0;
        public int TextureQualityIndex => S != null ? S.Current.TextureQuality : 0;
        public int UpscalingIndex => S != null ? (int)S.Current.Upscaling : 0;
        public int AdaptivePerformanceIndex => S != null ? (int)S.Current.AdaptivePerformance : 0;
        public int PhysicsDetailIndex => S != null ? (int)S.Current.PhysicsDetail : 0;
        public bool InvertYOn => gameSetting != null && gameSetting.InvertYEnabled;
        public bool InvertThrottleOn => gameSetting != null && gameSetting.InvertThrottleEnabled;
        public bool MusicOn => gameSetting != null && gameSetting.MusicEnabled;
        public bool SFXOn => gameSetting != null && gameSetting.SFXEnabled;
        public bool HapticsOn => gameSetting != null && gameSetting.HapticsEnabled;
        public float MusicLevel => gameSetting != null ? gameSetting.MusicLevel : 1f;
        public float SFXLevel => gameSetting != null ? gameSetting.SFXLevel : 1f;
        public float HapticsLevel => gameSetting != null ? gameSetting.HapticsLevel : 1f;

        // ───────────────────────── helpers ─────────────────────────

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
            resolutionDropdown.onValueChanged.RemoveListener(SetResolutionIndex);
            resolutionDropdown.onValueChanged.AddListener(SetResolutionIndex);
        }

        int CurrentResolutionIndex()
        {
            if (S == null) return 0;
            int w = S.Current.ResolutionWidth, h = S.Current.ResolutionHeight;
            if (w <= 0 || h <= 0) return 0;
            for (int i = 1; i < _resolutions.Count; i++)
                if (_resolutions[i].width == w && _resolutions[i].height == h) return i;
            return 0;
        }

        static int FrameCapToIndex(int fps)
        {
            if (fps <= 0) return FrameCaps.Length - 1;
            int idx = System.Array.IndexOf(FrameCaps, fps);
            return idx >= 0 ? idx : 1;
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

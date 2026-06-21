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
    /// Behind-script for the PC/Steam settings window (8 tabs: General, Display, Graphics,
    /// Performance, Audio, Controls, Accessibility, Advanced). The UI author designs the canvas /
    /// images / layout and wires each control's event to the matching public method here (the
    /// project's existing SettingsModal pattern). This controller is a thin router — it adapts the
    /// UI event types (int from dropdowns, float from sliders, bool from toggles) onto the typed
    /// backend stores:
    ///   • <see cref="DisplayGraphicsSettings"/> — Display / Graphics / Performance (device-local)
    ///   • <see cref="GameSetting"/>             — Audio / Controls (cloud-roaming, existing)
    ///   • <see cref="AccessibilitySettings"/>   — Accessibility
    ///   • <see cref="AnalyticsServiceFacade"/>  — analytics consent
    ///   • <see cref="Application.OpenURL"/>      — General-tab legal/support links
    ///
    /// All serialized references are optional/null-tolerant, so a partial panel still works while
    /// you build it out. The Benchmark button should hook directly to BenchmarkSceneLauncher.
    /// </summary>
    public class GameSettingsPanelController : MonoBehaviour
    {
        [Inject] GameSetting gameSetting;
        [Inject] AnalyticsServiceFacade analytics;

        [Header("Display — resolution dropdown (auto-populated)")]
        [SerializeField] TMP_Dropdown resolutionDropdown;

        [Header("Optional value labels / initial-state refs")]
        [SerializeField] TMP_Text versionText;
        [SerializeField] Toggle analyticsConsentToggle;

        [Header("General tab — links")]
        [SerializeField] string privacyPolicyUrl = "https://cosmicshore.com/privacy";
        [SerializeField] string termsUrl = "https://cosmicshore.com/terms";
        [SerializeField, Tooltip("Web/email form for data-deletion requests (right-to-erasure).")]
        string deleteDataUrl = "https://cosmicshore.com/delete-my-data";
        [SerializeField, Tooltip("Support / bug-report URL (Discord, issue tracker, or feedback form).")]
        string bugReportUrl = "https://cosmicshore.com/support";

        static readonly int[] FrameCaps = { 30, 60, 120, 144, -1 };
        readonly List<Resolution> _resolutions = new();

        DisplayGraphicsSettings S => DisplayGraphicsSettings.Instance;

        void Start()
        {
            PopulateResolutionDropdown();

            if (versionText) versionText.text = $"v{Application.version}";
            if (analyticsConsentToggle && analytics != null)
                analyticsConsentToggle.SetIsOnWithoutNotify(analytics.ConsentGranted);
        }

        // ───────────────────────── Display ─────────────────────────

        public void SetDisplayModeIndex(int index) => S?.SetDisplayMode((DisplayModeSetting)index);

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

        public void SetResolutionIndex(int index)
        {
            if (S == null || index < 0 || index >= _resolutions.Count) return;
            var r = _resolutions[index];
            S.SetResolution(r.width, r.height);
        }

        public void SetVSyncIndex(int index) => S?.SetVSync((VSyncSetting)index);

        /// <summary>Dropdown order: 30 / 60 / 120 / 144 / Uncapped.</summary>
        public void SetFrameCapIndex(int index)
        {
            if (S == null || index < 0 || index >= FrameCaps.Length) return;
            S.SetTargetFrameRate(FrameCaps[index]);
        }

        public void SetFieldOfView(float fov) => S?.SetFieldOfView(fov);

        // ───────────────────────── Graphics ─────────────────────────

        public void AutoDetect() => S?.ApplyAutoDetect();

        /// <summary>Dropdown order maps 0..5 to Very Low..Ultra. (Custom is a state, not a choice.)</summary>
        public void SetQualityPresetIndex(int index) => S?.SetQualityPreset((QualityPresetSetting)index);

        public void SetAntiAliasingIndex(int index) => S?.SetAntiAliasing((AntiAliasingSetting)index);
        public void SetRenderScale(float percent) => S?.SetRenderScalePercent(Mathf.RoundToInt(percent));
        public void SetUpscalingIndex(int index) => S?.SetUpscaling((UpscalingSetting)index);
        public void SetTextureQualityIndex(int index) => S?.SetTextureQuality(index);

        // ─────────────────────── Performance (CPU) ───────────────────────

        public void SetAdaptivePerformanceIndex(int index) => S?.SetAdaptivePerformance((AdaptivePerformanceSetting)index);
        public void SetEcosystemDensityIndex(int index) => S?.SetEcosystemDensity((EcosystemDensitySetting)index);
        public void SetPhysicsDetailIndex(int index) => S?.SetPhysicsDetail((PhysicsDetailSetting)index);
        public void SetAiCrowdSize(float count) => S?.SetAiCrowdSize(Mathf.RoundToInt(count));
        public void SetVfxDensity(float percent) => S?.SetVfxDensityPercent(Mathf.RoundToInt(percent));

        // ───────────────────────── Audio (existing GameSetting) ─────────────────────────

        public void ToggleMusic() => gameSetting?.ChangeMusicEnabledSetting();
        public void SetMusicLevel(float level) => gameSetting?.SetMusicLevel(level);
        public void ToggleSFX() => gameSetting?.ChangeSFXEnabledSetting();
        public void SetSFXLevel(float level) => gameSetting?.SetSFXLevel(level);
        public void ToggleHaptics() => gameSetting?.ChangeHapticsEnabledSetting();
        public void SetHapticsLevel(float level) => gameSetting?.SetHapticsLevel(level);

        // ───────────────────────── Controls (existing GameSetting) ─────────────────────────

        public void ToggleInvertY() => gameSetting?.ChangeInvertYEnabledStatus();
        public void ToggleInvertThrottle() => gameSetting?.ChangeInvertThrottleEnabledStatus();
        public void ToggleJoystickVisuals() => gameSetting?.ChangeJoystickVisualsStatus();

        // ───────────────────────── Accessibility ─────────────────────────

        public void SetColorblindModeIndex(int index) => AccessibilitySettings.ColorblindMode = (ColorblindModeSetting)index;
        public void ToggleSubtitles(bool on) => AccessibilitySettings.Subtitles = on;
        public void SetSubtitleScale(float scale) => AccessibilitySettings.SubtitleScale = scale;
        public void ToggleReduceFlashing(bool on) => AccessibilitySettings.ReduceFlashing = on;
        public void SetCameraShake(float intensity) => AccessibilitySettings.CameraShakeIntensity = intensity;

        // ───────────────────────── General (consent + links) ─────────────────────────

        public void SetAnalyticsConsent(bool granted) => analytics?.SetConsent(granted);
        public void OpenPrivacyPolicy() => OpenUrl(privacyPolicyUrl);
        public void OpenTerms() => OpenUrl(termsUrl);
        public void OpenDeleteDataForm() => OpenUrl(deleteDataUrl);
        public void OpenBugReport() => OpenUrl(bugReportUrl);

        // ───────────────────────── Advanced ─────────────────────────

        public void ResetToDefaults() => S?.ResetToDefaults();

        static void OpenUrl(string url)
        {
            if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }
    }
}

using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Self-wiring controller for the 4-tab options panel. Drag each UI control into its serialized
    /// slot - the controller populates dropdown options (from the enums, so order can't drift), sets
    /// saved values, attaches listeners, and drives the ON/OFF highlight. No per-control UnityEvent
    /// wiring, no option authoring, no init step. Reopening the panel refreshes displayed values.
    ///
    /// ON/OFF rows are a separate ON button + OFF button (see <see cref="OnOffControl"/>): the controller
    /// wires ON→Set(true), OFF→Set(false) and tints each button's label selected/dimmed.
    ///
    /// Routes to <see cref="DisplayGraphicsSettings"/> (display/graphics/perf), <see cref="GameSetting"/>
    /// (audio/controls), <see cref="AccessibilitySettings"/>, <see cref="AnalyticsServiceFacade"/>
    /// (consent), <see cref="Application.OpenURL"/> (links). Needs a Reflex ContainerScope in the scene.
    /// </summary>
    public class GameSettingsPanelController : MonoBehaviour
    {
        /// <summary>A two-button ON/OFF row, each with an optional active-state underline. All fields null-tolerant.</summary>
        [Serializable]
        public class OnOffControl
        {
            public Button onButton;
            public GameObject onUnderline;
            public Button offButton;
            public GameObject offUnderline;
        }

        [Inject] GameSetting gameSetting;
        [Inject] AnalyticsServiceFacade analytics;
        [Inject] ApplicationStateDataVariable appState;

        [Header("Panel")]
        [SerializeField, Tooltip("OptionsMenuContent root - enabled by Open(), disabled by Close(). Wire the settings modal's open event to Open() and its close event to Close().")]
        GameObject optionsMenuContent;
        [SerializeField, Tooltip("Shown while the panel is open IN-GAME: Performance settings + Auto-Detect + Benchmark, plus the GENERAL tab's Bug Report / Privacy Policy / Delete Data / Quit Game buttons, are locked outside the main menu (big graphics changes and anything that leaves the game happen in the menu only).")]
        GameObject menuOnlyHint;
        [SerializeField, Tooltip("Shown when a renderer-level change (Quality / AA / Texture / Upscaling) is made - 'some changes apply after a restart'. Hidden on open.")]
        GameObject restartRequiredNotice;

        [Header("ON/OFF Highlight")]
        [SerializeField] Color selectedColor = Color.white;
        [SerializeField] Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField, Tooltip("Scale of the selected ON/OFF button.")] float selectedScale = 1.1f;
        [SerializeField, Tooltip("Scale of the unselected ON/OFF button.")] float unselectedScale = 0.95f;

        [Header("General")]
        [SerializeField] TMP_Dropdown colorblindDropdown;
        [SerializeField] OnOffControl subtitles;
        [SerializeField] TMP_Dropdown subtitleScaleDropdown;
        [SerializeField] OnOffControl analyticsConsent;
        [SerializeField, Tooltip("Opens the support page in a browser. Main menu only - see ApplyContextLock.")]
        Button bugReportButton;
        [SerializeField, Tooltip("Opens the privacy policy in a browser. Main menu only - see ApplyContextLock.")]
        Button privacyPolicyButton;
        [SerializeField, Tooltip("Opens the delete-my-data form in a browser. Main menu only - see ApplyContextLock.")]
        Button deleteDataButton;
        [SerializeField, Tooltip("PC/Steam only: closes the game. Hidden automatically on mobile, where " +
             "the OS owns app exit and a quit button is a store-review flag. Main menu only - see ApplyContextLock.")]
        Button quitGameButton;
        [SerializeField] TMP_Text versionText;

        [Header("Display")]
        [SerializeField] TMP_Dropdown displayModeDropdown;
        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] TMP_Dropdown frameCapDropdown;
        [SerializeField] OnOffControl vsync;
        [SerializeField] Slider fovSlider;
        [SerializeField] float fovMin = 60f, fovMax = 90f;

        [Header("Performance")]
        [SerializeField] TMP_Dropdown qualityDropdown;
        [SerializeField] TMP_Dropdown antiAliasingDropdown;
        [SerializeField] TMP_Dropdown textureQualityDropdown;
        [SerializeField] TMP_Dropdown upscalingDropdown;
        [SerializeField] TMP_Dropdown adaptivePerformanceDropdown;
        [SerializeField] TMP_Dropdown physicsDetailDropdown;
        [SerializeField] Button autoDetectButton;
        [SerializeField] Button benchmarkButton;
        [SerializeField] BenchmarkSceneLauncher benchmarkLauncher;

        [Header("Other")]
        [SerializeField] OnOffControl invertY;
        [SerializeField] OnOffControl invertThrottle;
        [SerializeField] OnOffControl music;
        [SerializeField] Slider musicSlider;
        [SerializeField] OnOffControl sfx;
        [SerializeField] Slider sfxSlider;
        [SerializeField] OnOffControl haptics;
        [SerializeField] Slider hapticsSlider;

        [Header("Links")]
        [SerializeField] string privacyPolicyUrl = "https://cosmicshore.com/privacy";
        [SerializeField] string deleteDataUrl = "https://cosmicshore.com/delete-my-data";
        [SerializeField] string bugReportUrl = "https://cosmicshore.com/support";

        // Option labels (index == enum value - the controller fills these, never author them in the dropdown)
        static readonly string[] ColorblindOpts = { "Off", "Protanopia", "Deuteranopia", "Tritanopia" };
        static readonly string[] SubtitleScaleOpts = { "Small", "Medium", "Large" };
        static readonly string[] DisplayModeOpts = { "Fullscreen", "Borderless", "Windowed" };
        static readonly string[] FrameCapOpts = { "30", "60", "120", "144", "240", "Uncapped" };
        static readonly string[] QualityOpts = { "Very Low", "Low", "Medium", "High", "Very High", "Ultra" };
        static readonly string[] AntiAliasingOpts = { "Off", "FXAA", "SMAA", "MSAA 2x", "MSAA 4x", "MSAA 8x", "TAA" };
        static readonly string[] TextureOpts = { "Full", "Half", "Quarter", "Eighth" };
        static readonly string[] UpscalingOpts = { "Auto", "Linear", "FSR", "STP" };
        static readonly string[] AdaptiveOpts = { "Off", "Balanced", "Aggressive" };
        static readonly string[] PhysicsOpts = { "Low", "High" };

        static readonly int[] FrameCaps = { 30, 60, 120, 144, 240, -1 };
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
            // Refresh shown values when the panel is reopened (settings may have changed in the
            // benchmark scene or via auto-detect). Skip before the first Start (injection not ready).
            if (_bound) RefreshValues();
        }

        /// <summary>
        /// Shows the options panel by enabling OptionsMenuContent. Wire the settings modal's open
        /// event here. Safe to call while this component's GameObject is inactive (it activates the
        /// referenced content, which triggers binding on first show).
        /// </summary>
        public void Open()
        {
            if (optionsMenuContent != null) optionsMenuContent.SetActive(true);
            if (restartRequiredNotice != null) restartRequiredNotice.SetActive(false);
            if (_bound) RefreshValues();
        }

        /// <summary>Hides the options panel by disabling OptionsMenuContent. Wire the modal's close event here.</summary>
        public void Close()
        {
            if (optionsMenuContent != null) optionsMenuContent.SetActive(false);
        }

        // ───────────────────────── context lock (menu vs in-game) ─────────────────────────

        /// <summary>True only in the main menu - see <see cref="ApplyContextLock"/> for what is locked elsewhere.</summary>
        bool InMainMenu => appState == null || appState.Value == null || appState.Value.State == ApplicationState.MainMenu;

        /// <summary>
        /// Locks to the main menu everything that shouldn't be reachable mid-session:
        /// <list type="bullet">
        /// <item>Performance settings + Auto-Detect + Benchmark - real-game pattern, big graphics
        /// changes don't happen mid-session.</item>
        /// <item>The GENERAL tab's four exit actions - Bug Report, Privacy Policy, Delete Data and
        /// Quit Game. Each one either leaves the game (an external browser tab, or closing outright)
        /// or acts on the account, and doing that from a live session drops the player out of a
        /// running match - and, in multiplayer, out of a session other people are still in.</item>
        /// </list>
        /// Live-safe settings (audio, controls, FOV, VSync, frame cap) stay editable in-game.
        /// Shows <see cref="menuOnlyHint"/> while locked.
        /// </summary>
        void ApplyContextLock()
        {
            bool menu = InMainMenu;
            SetInteractable(qualityDropdown, menu);
            SetInteractable(antiAliasingDropdown, menu);
            SetInteractable(textureQualityDropdown, menu);
            SetInteractable(upscalingDropdown, menu);
            SetInteractable(adaptivePerformanceDropdown, menu);
            SetInteractable(physicsDetailDropdown, menu);
            SetInteractable(autoDetectButton, menu);
            SetInteractable(benchmarkButton, menu);

            // GENERAL tab exit actions - main menu only. quitGameButton may be hidden entirely here
            // (mobile, see BindQuitButton); SetInteractable is safe either way.
            SetInteractable(bugReportButton, menu);
            SetInteractable(privacyPolicyButton, menu);
            SetInteractable(deleteDataButton, menu);
            SetInteractable(quitGameButton, menu);

            UnlockLiveSafeControls();

            if (menuOnlyHint != null) menuOnlyHint.SetActive(!menu);
        }

        /// <summary>
        /// Asserts the OTHER side of <see cref="ApplyContextLock"/>: every control that is editable
        /// everywhere is positively re-enabled each time the panel refreshes.
        ///
        /// Without this the lock is one-directional - it only ever writes <c>false</c>, to the
        /// menu-only controls - so a live-safe control that arrives disabled for ANY other reason
        /// (authored that way, a prefab-instance override, a future caller) stays disabled for the
        /// life of the session, and the panel has no way back. That reads to a player as "this
        /// setting is locked in-game", which is exactly what the context lock is NOT supposed to say
        /// about the frame cap, VSync, FOV, audio or controls.
        /// </summary>
        void UnlockLiveSafeControls()
        {
            // DISPLAY - live everywhere (the frame cap in particular: a player capping FPS mid-match
            // is a normal thing to want, and it costs nothing to apply).
            SetInteractable(displayModeDropdown, true);
            SetInteractable(resolutionDropdown, true);
            SetInteractable(frameCapDropdown, true);
            SetOnOffInteractable(vsync, true);
            SetInteractable(fovSlider, true);

            // GENERAL - accessibility + consent.
            SetInteractable(colorblindDropdown, true);
            SetInteractable(subtitleScaleDropdown, true);
            SetOnOffInteractable(subtitles, true);
            SetOnOffInteractable(analyticsConsent, true);

            // OTHER - controls + audio.
            SetOnOffInteractable(invertY, true);
            SetOnOffInteractable(invertThrottle, true);
            SetOnOffInteractable(music, true);
            SetInteractable(musicSlider, true);
            SetOnOffInteractable(sfx, true);
            SetInteractable(sfxSlider, true);
            SetOnOffInteractable(haptics, true);
            SetInteractable(hapticsSlider, true);
        }

        static void SetInteractable(Selectable s, bool on) { if (s != null) s.interactable = on; }

        static void SetOnOffInteractable(OnOffControl c, bool on)
        {
            if (c == null) return;
            SetInteractable(c.onButton, on);
            SetInteractable(c.offButton, on);
        }

        /// <summary>Shows the "some changes apply after a restart" notice for renderer-level changes.</summary>
        void FlagRestartNeeded()
        {
            if (restartRequiredNotice != null) restartRequiredNotice.SetActive(true);
        }

        // ───────────────────────── self-wiring ─────────────────────────

        void BindAll()
        {
            if (versionText) versionText.text = $"Version {Application.version}";

            // GENERAL
            BindDropdown(colorblindDropdown, ColorblindOpts, SetColorblindModeIndex);
            BindDropdown(subtitleScaleDropdown, SubtitleScaleOpts, SetSubtitleScaleIndex);
            BindOnOff(subtitles, SetSubtitles, () => SubtitlesOn);
            BindOnOff(analyticsConsent, SetAnalyticsConsent, () => ConsentOn);
            BindButton(bugReportButton, OpenBugReport);
            BindButton(privacyPolicyButton, OpenPrivacyPolicy);
            BindButton(deleteDataButton, OpenDeleteDataForm);
            BindQuitButton();

            // DISPLAY
            BindDropdown(displayModeDropdown, DisplayModeOpts, SetDisplayModeIndex);
            PopulateResolutionDropdown();
            BindDropdown(frameCapDropdown, FrameCapOpts, SetFrameCapIndex);
            BindOnOff(vsync, SetVSync, () => VSyncOn);
            BindSlider(fovSlider, fovMin, fovMax, true, SetFieldOfView, FieldOfView);

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
            BindOnOff(invertY, SetInvertY, () => InvertYOn);
            BindOnOff(invertThrottle, SetInvertThrottle, () => InvertThrottleOn);
            BindOnOff(music, SetMusic, () => MusicOn);
            BindSlider(musicSlider, 0f, 1f, false, SetMusicLevel, MusicLevel);
            BindOnOff(sfx, SetSFX, () => SFXOn);
            BindSlider(sfxSlider, 0f, 1f, false, SetSFXLevel, SFXLevel);
            BindOnOff(haptics, SetHaptics, () => HapticsOn);
            BindSlider(hapticsSlider, 0f, 1f, false, SetHapticsLevel, HapticsLevel);

            RefreshValues();
        }

        /// <summary>Pushes current saved values into every assigned control (no listener changes).</summary>
        void RefreshValues()
        {
            SetDropdown(colorblindDropdown, ColorblindIndex);
            SetDropdown(subtitleScaleDropdown, SubtitleScaleIndex);
            UpdateOnOff(subtitles, SubtitlesOn);
            UpdateOnOff(analyticsConsent, ConsentOn);

            SetDropdown(displayModeDropdown, DisplayModeIndex);
            SetDropdown(resolutionDropdown, ResolutionIndex);
            SetDropdown(frameCapDropdown, FrameCapIndex);
            UpdateOnOff(vsync, VSyncOn);
            SetSliderValue(fovSlider, FieldOfView);

            SetDropdown(qualityDropdown, QualityIndex);
            SetDropdown(antiAliasingDropdown, AntiAliasingIndex);
            SetDropdown(textureQualityDropdown, TextureQualityIndex);
            SetDropdown(upscalingDropdown, UpscalingIndex);
            SetDropdown(adaptivePerformanceDropdown, AdaptivePerformanceIndex);
            SetDropdown(physicsDetailDropdown, PhysicsDetailIndex);

            UpdateOnOff(invertY, InvertYOn);
            UpdateOnOff(invertThrottle, InvertThrottleOn);
            UpdateOnOff(music, MusicOn);
            SetSliderValue(musicSlider, MusicLevel);
            UpdateOnOff(sfx, SFXOn);
            SetSliderValue(sfxSlider, SFXLevel);
            UpdateOnOff(haptics, HapticsOn);
            SetSliderValue(hapticsSlider, HapticsLevel);

            ApplyContextLock();
        }

        // ───────────────────────── ON/OFF button pairs ─────────────────────────

        void BindOnOff(OnOffControl c, Action<bool> setter, Func<bool> getter)
        {
            if (c == null) return;
            if (c.onButton) c.onButton.onClick.AddListener(() => { setter(true); UpdateOnOff(c, true); });
            if (c.offButton) c.offButton.onClick.AddListener(() => { setter(false); UpdateOnOff(c, false); });
            UpdateOnOff(c, getter());
        }

        void UpdateOnOff(OnOffControl c, bool isOn)
        {
            if (c == null) return;
            SetButtonVisual(c.onButton, c.onUnderline, isOn);
            SetButtonVisual(c.offButton, c.offUnderline, !isOn);
        }

        /// <summary>Tints the label, toggles the underline, and scales the button for the selected/unselected state.</summary>
        void SetButtonVisual(Button b, GameObject underline, bool selected)
        {
            SetLabelColor(b, selected ? selectedColor : unselectedColor);
            if (underline != null) underline.SetActive(selected);
            if (b != null) b.transform.localScale = Vector3.one * (selected ? selectedScale : unselectedScale);
        }

        static void SetLabelColor(Button b, Color col)
        {
            if (b == null) return;
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label) label.color = col;
        }

        // ───────────────────────── dropdown / slider / button binding ─────────────────────────

        static void BindDropdown(TMP_Dropdown dd, string[] options, UnityAction<int> onChange)
        {
            if (dd == null) return;
            dd.ClearOptions();
            dd.AddOptions(new List<string>(options));
            dd.onValueChanged.RemoveListener(onChange);
            dd.onValueChanged.AddListener(onChange);
        }

        /// <summary>
        /// Binds a slider to its setter, seating it on <paramref name="value"/> (the saved setting).
        /// The range goes on through <see cref="SliderRange"/> rather than by direct assignment,
        /// because assigning a narrower window CLAMPS whatever the prefab authored and broadcasts
        /// the clamped result to the slider's PERSISTENT inspector listeners - which, on the audio
        /// rows, are the listeners that persist the setting. Binding the panel therefore used to
        /// overwrite the player's saved level before it was ever displayed.
        /// </summary>
        static void BindSlider(Slider s, float min, float max, bool wholeNumbers, UnityAction<float> onChange, float value)
        {
            if (s == null) return;
            SliderRange.ApplyWithoutNotify(s, min, max, wholeNumbers, value);
            s.onValueChanged.RemoveListener(onChange);
            s.onValueChanged.AddListener(onChange);
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

        static void SetSliderValue(Slider s, float v) { if (s != null) s.SetValueWithoutNotify(v); }

        // ───────────────────────── setters (listeners call these) ─────────────────────────

        public void SetColorblindModeIndex(int index) => AccessibilitySettings.ColorblindMode = (ColorblindModeSetting)index;
        public void SetSubtitles(bool on) => AccessibilitySettings.Subtitles = on;
        public void SetSubtitleScaleIndex(int index) => AccessibilitySettings.SubtitleScale = SubtitleScales[Mathf.Clamp(index, 0, SubtitleScales.Length - 1)];
        public void SetAnalyticsConsent(bool granted) => analytics?.SetConsent(granted);
        public void OpenBugReport() => OpenUrl(bugReportUrl);
        public void OpenPrivacyPolicy() => OpenUrl(privacyPolicyUrl);
        public void OpenDeleteDataForm() => OpenUrl(deleteDataUrl);

        /// <summary>Closes the game. Desktop only - see <see cref="BindQuitButton"/>.</summary>
        public void QuitGame() => DesktopPlatformServices.Quit();

        /// <summary>
        /// Shows the quit button on desktop and hides it everywhere else. A PC player expects to be
        /// able to close the game from its own UI; a mobile player does not, and iOS review treats a
        /// self-quit control as a defect.
        /// </summary>
        void BindQuitButton()
        {
            if (quitGameButton == null) return;

            bool desktop = DesktopPlatformServices.IsDesktop;
            quitGameButton.gameObject.SetActive(desktop);
            if (desktop) BindButton(quitGameButton, QuitGame);
        }

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

        public void AutoDetect()
        {
            if (!InMainMenu) { CSDebug.Log("[Settings] Auto-Detect is available only in the main menu."); return; }
            S?.ApplyAutoDetect();
            RefreshValues();
            FlagRestartNeeded();
            CSDebug.Log($"[Settings] Auto-Detect applied - Quality preset index {QualityIndex}, AA index {AntiAliasingIndex}.");
        }

        public void RunBenchmark()
        {
            if (!InMainMenu) { CSDebug.Log("[Settings] Benchmark is available only in the main menu."); return; }
            benchmarkLauncher?.LaunchBenchmark();
        }

        public void SetQualityPresetIndex(int index) { S?.SetQualityPreset((QualityPresetSetting)index); FlagRestartNeeded(); }
        public void SetAntiAliasingIndex(int index) { S?.SetAntiAliasing((AntiAliasingSetting)index); FlagRestartNeeded(); }
        public void SetTextureQualityIndex(int index) { S?.SetTextureQuality(index); FlagRestartNeeded(); }
        public void SetUpscalingIndex(int index) { S?.SetUpscaling((UpscalingSetting)index); FlagRestartNeeded(); }
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

        // ───────────────────────── getters ─────────────────────────

        public int ColorblindIndex => (int)AccessibilitySettings.ColorblindMode;
        public bool SubtitlesOn => AccessibilitySettings.Subtitles;
        public int SubtitleScaleIndex => NearestScaleIndex(AccessibilitySettings.SubtitleScale);
        public bool ConsentOn => analytics != null && analytics.ConsentGranted;
        public int DisplayModeIndex => S != null ? (int)S.Current.DisplayMode : 0;
        public int ResolutionIndex => CurrentResolutionIndex();
        public int FrameCapIndex => FrameCapToIndex(S != null ? S.Current.TargetFrameRate : 60);
        public bool VSyncOn => S != null && S.Current.VSync != VSyncSetting.Off;
        public float FieldOfView => S != null ? S.Current.FieldOfView : GraphicsSettingsData.DefaultFieldOfView;
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

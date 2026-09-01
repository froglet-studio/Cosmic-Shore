using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Device-local settings store for Display, Graphics, and the CPU/simulation "Performance" tab.
    /// Sibling to <see cref="GameSetting"/> (which owns the cloud-roaming audio/input prefs); this
    /// one is PlayerPrefs-only by design - resolution / quality / framerate are per-device and must
    /// not roam. Auto-instantiating <see cref="SingletonPersistent{T}"/>, so it needs no DI wiring.
    ///
    /// On first run it seeds from <see cref="SettingsAutoDetector"/>; thereafter it loads the saved
    /// snapshot and applies it via <see cref="GraphicsSettingsApplier"/>. The settings UI reads
    /// <see cref="Current"/> and calls the typed setters, which persist + apply + raise the change
    /// event for the matching consumer.
    /// </summary>
    public class DisplayGraphicsSettings : SingletonPersistent<DisplayGraphicsSettings>
    {
        const string P = "gfx.";

        /// <summary>
        /// Schema version of the persisted snapshot. Bump it whenever a saved value needs a
        /// one-time fix-up, and handle that bump in <see cref="Migrate"/>.
        /// </summary>
        const int SettingsVersion = 1;

        GraphicsSettingsData _data = new();

        /// <summary>Live snapshot. Treat as read-only; mutate through the setters so changes persist + apply.</summary>
        public GraphicsSettingsData Current => _data;

        /// <summary>
        /// Zero-wire bootstrap: ensure the settings manager exists and applies saved prefs at startup
        /// even if no one placed it in a scene (SingletonPersistent does not auto-instantiate). Guards
        /// on a live <see cref="Instance"/> so an inspector-placed one wins.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Instance == null)
                new GameObject("[DisplayGraphicsSettings]").AddComponent<DisplayGraphicsSettings>();
        }

        // ── Change events (so consumers react without polling) ──
        public static event Action<GraphicsSettingsData> OnAnySettingChanged;
        public static event Action<EcosystemDensitySetting> OnEcosystemDensityChanged;
        public static event Action<PhysicsDetailSetting> OnPhysicsDetailChanged;
        public static event Action<AdaptivePerformanceSetting> OnAdaptivePerformanceChanged;
        public static event Action<int> OnAiCrowdSizeChanged;
        public static event Action<int> OnVfxDensityChanged;
        public static event Action<float> OnFieldOfViewChanged;

        public override void Awake()
        {
            base.Awake();
            if (Instance != this) return; // duplicate destroyed by base

            if (PlayerPrefs.GetInt(P + "Initialized", 0) == 1)
            {
                Load();
                Migrate();
            }
            else
            {
                _data = SettingsAutoDetector.RecommendSettings();
            }

            GraphicsSettingsApplier.ApplyAll(_data);
            Save(); // persists the first-run auto-detect snapshot too
        }

        #region Apply / persistence

        void Save()
        {
            PlayerPrefs.SetInt(P + "DisplayMode", (int)_data.DisplayMode);
            PlayerPrefs.SetInt(P + "ResW", _data.ResolutionWidth);
            PlayerPrefs.SetInt(P + "ResH", _data.ResolutionHeight);
            PlayerPrefs.SetInt(P + "Refresh", _data.RefreshRateHz);
            PlayerPrefs.SetInt(P + "VSync", (int)_data.VSync);
            PlayerPrefs.SetInt(P + "Fps", _data.TargetFrameRate);
            PlayerPrefs.SetFloat(P + "Fov", _data.FieldOfView);

            PlayerPrefs.SetInt(P + "Preset", (int)_data.QualityPreset);
            PlayerPrefs.SetInt(P + "AA", (int)_data.AntiAliasing);
            PlayerPrefs.SetInt(P + "RenderScale", _data.RenderScalePercent);
            PlayerPrefs.SetInt(P + "Upscale", (int)_data.Upscaling);
            PlayerPrefs.SetInt(P + "Texture", _data.TextureQuality);

            PlayerPrefs.SetInt(P + "Adaptive", (int)_data.AdaptivePerformance);
            PlayerPrefs.SetInt(P + "Ecosystem", (int)_data.EcosystemDensity);
            PlayerPrefs.SetInt(P + "Physics", (int)_data.PhysicsDetail);
            PlayerPrefs.SetInt(P + "AiCrowd", _data.AiCrowdSize);
            PlayerPrefs.SetInt(P + "Vfx", _data.VfxDensityPercent);

            PlayerPrefs.SetInt(P + "Initialized", 1);
            PlayerPrefs.SetInt(P + "Version", SettingsVersion);
            PlayerPrefs.Save();
        }

        void Load()
        {
            var d = new GraphicsSettingsData();
            d.DisplayMode = (DisplayModeSetting)PlayerPrefs.GetInt(P + "DisplayMode", (int)d.DisplayMode);
            d.ResolutionWidth = PlayerPrefs.GetInt(P + "ResW", d.ResolutionWidth);
            d.ResolutionHeight = PlayerPrefs.GetInt(P + "ResH", d.ResolutionHeight);
            d.RefreshRateHz = PlayerPrefs.GetInt(P + "Refresh", d.RefreshRateHz);
            d.VSync = (VSyncSetting)PlayerPrefs.GetInt(P + "VSync", (int)d.VSync);
            d.TargetFrameRate = PlayerPrefs.GetInt(P + "Fps", d.TargetFrameRate);
            d.FieldOfView = PlayerPrefs.GetFloat(P + "Fov", d.FieldOfView);

            d.QualityPreset = (QualityPresetSetting)PlayerPrefs.GetInt(P + "Preset", (int)d.QualityPreset);
            d.AntiAliasing = (AntiAliasingSetting)PlayerPrefs.GetInt(P + "AA", (int)d.AntiAliasing);
            d.RenderScalePercent = PlayerPrefs.GetInt(P + "RenderScale", d.RenderScalePercent);
            d.Upscaling = (UpscalingSetting)PlayerPrefs.GetInt(P + "Upscale", (int)d.Upscaling);
            d.TextureQuality = PlayerPrefs.GetInt(P + "Texture", d.TextureQuality);

            d.AdaptivePerformance = (AdaptivePerformanceSetting)PlayerPrefs.GetInt(P + "Adaptive", (int)d.AdaptivePerformance);
            d.EcosystemDensity = (EcosystemDensitySetting)PlayerPrefs.GetInt(P + "Ecosystem", (int)d.EcosystemDensity);
            d.PhysicsDetail = (PhysicsDetailSetting)PlayerPrefs.GetInt(P + "Physics", (int)d.PhysicsDetail);
            d.AiCrowdSize = PlayerPrefs.GetInt(P + "AiCrowd", d.AiCrowdSize);
            d.VfxDensityPercent = PlayerPrefs.GetInt(P + "Vfx", d.VfxDensityPercent);

            _data = d;
        }

        /// <summary>
        /// One-time fix-ups for a snapshot written by an older build. Every install persists the
        /// WHOLE snapshot on first run (see <see cref="Awake"/>), so once a shipped default is on
        /// disk it is indistinguishable from a deliberate pick — a changed default therefore has
        /// to be carried forward explicitly or existing players keep the old value forever.
        /// Mutates <see cref="_data"/> only; the version stamp is written by <see cref="Save"/>,
        /// which <see cref="Awake"/> always calls afterwards.
        /// </summary>
        void Migrate()
        {
            int version = PlayerPrefs.GetInt(P + "Version", 0);
            if (version >= SettingsVersion) return;

            // v1: the default field of view moved 60° → 90°. Only a saved value still sitting on
            // the OLD default is moved — anything else is a lens the player picked, so it stands.
            if (version < 1 &&
                Mathf.Approximately(_data.FieldOfView, GraphicsSettingsData.LegacyDefaultFieldOfView))
            {
                _data.FieldOfView = GraphicsSettingsData.DefaultFieldOfView;
            }
        }

        void Commit(bool reapplyDisplay = false, bool reapplyQuality = false, bool reapplyFrameRate = false)
        {
            if (reapplyDisplay) GraphicsSettingsApplier.ApplyDisplay(_data);
            if (reapplyQuality) GraphicsSettingsApplier.ApplyQuality(_data);
            if (reapplyFrameRate) GraphicsSettingsApplier.ApplyFrameRate(_data);
            Save();
            OnAnySettingChanged?.Invoke(_data);
        }

        /// <summary>Any individual graphics override flips the preset label to Custom.</summary>
        void MarkCustomPreset() => _data.QualityPreset = QualityPresetSetting.Custom;

        #endregion

        #region Display setters

        public void SetDisplayMode(DisplayModeSetting mode) { _data.DisplayMode = mode; Commit(reapplyDisplay: true); }

        public void SetResolution(int width, int height, int refreshHz = 0)
        {
            _data.ResolutionWidth = width;
            _data.ResolutionHeight = height;
            _data.RefreshRateHz = refreshHz;
            Commit(reapplyDisplay: true);
        }

        public void SetVSync(VSyncSetting v) { _data.VSync = v; Commit(reapplyFrameRate: true); }

        public void SetTargetFrameRate(int fps) { _data.TargetFrameRate = fps; Commit(reapplyFrameRate: true); }

        public void SetFieldOfView(float fov)
        {
            _data.FieldOfView = fov;
            Save();
            OnFieldOfViewChanged?.Invoke(fov);
            OnAnySettingChanged?.Invoke(_data);
        }

        #endregion

        #region Graphics setters

        public void SetQualityPreset(QualityPresetSetting preset)
        {
            _data.QualityPreset = preset;
            Commit(reapplyQuality: true);
        }

        public void SetAntiAliasing(AntiAliasingSetting aa) { _data.AntiAliasing = aa; MarkCustomPreset(); Commit(reapplyQuality: true); }
        public void SetRenderScalePercent(int pct) { _data.RenderScalePercent = Mathf.Clamp(pct, 25, 200); MarkCustomPreset(); Commit(reapplyQuality: true); }
        public void SetUpscaling(UpscalingSetting u) { _data.Upscaling = u; MarkCustomPreset(); Commit(reapplyQuality: true); }
        public void SetTextureQuality(int mipLimit) { _data.TextureQuality = Mathf.Clamp(mipLimit, 0, 3); MarkCustomPreset(); Commit(reapplyQuality: true); }

        #endregion

        #region Performance (CPU) setters

        public void SetAdaptivePerformance(AdaptivePerformanceSetting a)
        {
            _data.AdaptivePerformance = a; Save();
            OnAdaptivePerformanceChanged?.Invoke(a);
            OnAnySettingChanged?.Invoke(_data);
        }

        public void SetEcosystemDensity(EcosystemDensitySetting e)
        {
            _data.EcosystemDensity = e; Save();
            OnEcosystemDensityChanged?.Invoke(e);
            OnAnySettingChanged?.Invoke(_data);
        }

        public void SetPhysicsDetail(PhysicsDetailSetting p)
        {
            _data.PhysicsDetail = p; Save();
            OnPhysicsDetailChanged?.Invoke(p);
            OnAnySettingChanged?.Invoke(_data);
        }

        public void SetAiCrowdSize(int count)
        {
            _data.AiCrowdSize = Mathf.Max(0, count); Save();
            OnAiCrowdSizeChanged?.Invoke(_data.AiCrowdSize);
            OnAnySettingChanged?.Invoke(_data);
        }

        public void SetVfxDensityPercent(int pct)
        {
            _data.VfxDensityPercent = Mathf.Clamp(pct, 25, 100); Save();
            OnVfxDensityChanged?.Invoke(_data.VfxDensityPercent);
            OnAnySettingChanged?.Invoke(_data);
        }

        #endregion

        #region Bulk operations

        /// <summary>Apply the instant SystemInfo heuristic and persist it.</summary>
        public void ApplyAutoDetect()
        {
            _data = SettingsAutoDetector.RecommendSettings();
            GraphicsSettingsApplier.ApplyAll(_data);
            Save();
            OnAnySettingChanged?.Invoke(_data);
            CSDebug.Log($"[Settings] Auto-detect → {_data.QualityPreset} (capability score {SettingsAutoDetector.CapabilityScore()}/7).");
        }

        /// <summary>Adopt a whole snapshot (e.g. the preset the benchmark sweep settled on).</summary>
        public void ApplySnapshot(GraphicsSettingsData data)
        {
            if (data == null) return;
            _data = data.Clone();
            GraphicsSettingsApplier.ApplyAll(_data);
            Save();
            OnAnySettingChanged?.Invoke(_data);
        }

        public void ResetToDefaults()
        {
            _data = new GraphicsSettingsData();
            GraphicsSettingsApplier.ApplyAll(_data);
            Save();
            OnAnySettingChanged?.Invoke(_data);
        }

        #endregion
    }
}

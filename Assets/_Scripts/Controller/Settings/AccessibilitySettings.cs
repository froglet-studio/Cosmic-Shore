using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Lightweight static store for accessibility preferences (colorblind, subtitles, photosensitivity,
    /// camera shake). PlayerPrefs-backed with change events so the eventual consumers - a colorblind
    /// screen-space correction, the subtitle renderer (Dialogue System), the flash-limiter, and the
    /// camera shake driver - can subscribe and react. The values persist today; wiring each consumer
    /// is a follow-up feature, tracked in the settings handoff doc.
    /// </summary>
    public static class AccessibilitySettings
    {
        const string P = "a11y.";

        public static event Action<ColorblindModeSetting> OnColorblindModeChanged;
        public static event Action<bool> OnSubtitlesChanged;
        public static event Action<float> OnSubtitleScaleChanged;
        public static event Action<bool> OnReduceFlashingChanged;
        public static event Action<float> OnCameraShakeChanged;

        public static ColorblindModeSetting ColorblindMode
        {
            get => (ColorblindModeSetting)PlayerPrefs.GetInt(P + "Colorblind", 0);
            set { PlayerPrefs.SetInt(P + "Colorblind", (int)value); PlayerPrefs.Save(); OnColorblindModeChanged?.Invoke(value); }
        }

        public static bool Subtitles
        {
            get => PlayerPrefs.GetInt(P + "Subtitles", 0) == 1;
            set { PlayerPrefs.SetInt(P + "Subtitles", value ? 1 : 0); PlayerPrefs.Save(); OnSubtitlesChanged?.Invoke(value); }
        }

        /// <summary>Subtitle text scale multiplier (1 = default).</summary>
        public static float SubtitleScale
        {
            get => PlayerPrefs.GetFloat(P + "SubtitleScale", 1f);
            set { PlayerPrefs.SetFloat(P + "SubtitleScale", value); PlayerPrefs.Save(); OnSubtitleScaleChanged?.Invoke(value); }
        }

        /// <summary>Photosensitivity safe mode - dampen full-screen flashes/strobes (neon-heavy game).</summary>
        public static bool ReduceFlashing
        {
            get => PlayerPrefs.GetInt(P + "ReduceFlashing", 0) == 1;
            set { PlayerPrefs.SetInt(P + "ReduceFlashing", value ? 1 : 0); PlayerPrefs.Save(); OnReduceFlashingChanged?.Invoke(value); }
        }

        /// <summary>Camera shake / motion intensity, 0 (off) .. 1 (full).</summary>
        public static float CameraShakeIntensity
        {
            get => PlayerPrefs.GetFloat(P + "CameraShake", 1f);
            set { PlayerPrefs.SetFloat(P + "CameraShake", Mathf.Clamp01(value)); PlayerPrefs.Save(); OnCameraShakeChanged?.Invoke(Mathf.Clamp01(value)); }
        }
    }
}

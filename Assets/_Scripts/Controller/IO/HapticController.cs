using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Lofelt.NiceVibrations;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Haptic Type
    /// Abstract Haptic Patterns to Haptic Types in the game.
    /// </summary>
    public enum HapticType
    {
        None = 0,
        ButtonPress = 1,
        PrismCollision = 2,
        ShipCollision = 3,
        CrystalCollision = 4,
        MineCollision = 5,
    }

    public class HapticController : MonoBehaviour
    {
        [Inject] GameSetting injectedGameSetting;
        static GameSetting s_gameSetting;

        // NOTE: injection lands AFTER Awake (project DI rule), so this assignment was always null
        // when the component was scene-placed — and in practice the component was never placed in
        // any scene at all, which made every haptic in the game a silent no-op. Settings therefore
        // resolves lazily through the GameSetting persistent singleton (set in its Awake in
        // Bootstrap), with the injected reference as a bonus when a scene instance exists.
        void Start() => s_gameSetting = injectedGameSetting ? injectedGameSetting : s_gameSetting;

        static GameSetting Settings
        {
            get
            {
                if (s_gameSetting == null) s_gameSetting = GameSetting.Instance;
                return s_gameSetting;
            }
        }

        // Per-type real-time rate limit. Skim/crystal effects fire per collider
        // contact (many per frame while skimming), so without this a hand-held
        // buzz would machine-gun. Unscaled so it's independent of timescale.
        const float MinIntervalSeconds = 0.08f;
        static readonly float[] s_lastPlayByType = new float[16];

        static bool RateLimitPass(HapticType type)
        {
            int i = (int)type;
            if (i < 0 || i >= s_lastPlayByType.Length) return true;
            float now = Time.unscaledTime;
            if (now - s_lastPlayByType[i] < MinIntervalSeconds) return false;
            s_lastPlayByType[i] = now;
            return true;
        }

        /// <summary>
        /// Play Haptic
        /// Play haptic pattern presets when haptics are enabled.
        /// </summary>
        /// <param name="type">Haptic type</param>
        public static void PlayHaptic(HapticType type)
        {
            var settings = Settings;
            if (settings == null || !settings.HapticsEnabled || settings.HapticsLevel == 0)
                return;
            if (type == HapticType.None)
                return;
            if (!RateLimitPass(type))
                return;

#if UNITY_ANDROID && !UNITY_EDITOR
            // On-device Android: go straight to the OS Vibrator. Reliable across
            // devices where Lofelt's advanced-haptics gate silently no-ops.
            GetPulse(type, out long ms, out int baseAmplitude);
            int amp = Mathf.Clamp(Mathf.RoundToInt(baseAmplitude * Mathf.Clamp01(settings.HapticsLevel)), 1, 255);
            AndroidHaptics.Pulse(ms, amp);
#else
            // Editor / iOS / other: keep the NiceVibrations preset path (gamepad
            // rumble in-editor, Taptic Engine on iOS).
            Lofelt.NiceVibrations.HapticController.outputLevel = settings.HapticsLevel;
            HapticPatterns.PlayPreset(GetPatternForHapticType(type));
#endif
        }

        /// <summary>
        /// Maps a HapticType to a native Android one-shot (pulse duration in ms +
        /// nominal amplitude 1..255 before the strength slider is applied).
        /// </summary>
        static void GetPulse(HapticType type, out long durationMs, out int amplitude)
        {
            switch (type)
            {
                case HapticType.ButtonPress:      durationMs = 12; amplitude = 130; break;
                case HapticType.PrismCollision:   durationMs = 14; amplitude = 165; break;
                case HapticType.CrystalCollision: durationMs = 22; amplitude = 205; break;
                case HapticType.ShipCollision:    durationMs = 45; amplitude = 255; break;
                case HapticType.MineCollision:    durationMs = 45; amplitude = 255; break;
                default:                          durationMs = 15; amplitude = 160; break;
            }
        }

        static float s_lastConstant = float.NegativeInfinity;

        public static void PlayConstant(float amplitude, float frequency, float duration)
        {
            var settings = Settings;
            if (settings == null || !settings.HapticsEnabled || settings.HapticsLevel == 0)
                return;

#if UNITY_ANDROID && !UNITY_EDITOR
            // Some callers drive this per-frame (distance-scaled skim). Rate-limit
            // so the native Vibrator isn't machine-gunned into a solid buzz.
            float now = Time.unscaledTime;
            if (now - s_lastConstant < MinIntervalSeconds) return;
            s_lastConstant = now;

            long ms = Mathf.Clamp(Mathf.RoundToInt(duration * 1000f), 8, 200);
            int amp = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(amplitude) * 255f * Mathf.Clamp01(settings.HapticsLevel)), 1, 255);
            AndroidHaptics.Pulse(ms, amp);
#else
            // Honor the strength slider for continuous rumbles too (PlayPreset already does via
            // outputLevel above).
            Lofelt.NiceVibrations.HapticController.outputLevel = settings.HapticsLevel;
            HapticPatterns.PlayConstant(amplitude, frequency, duration);
#endif
        }

        /// <summary>
        /// Get Pattern For Haptic Type
        /// Returns mapped Haptic Patterns
        /// </summary>
        /// <param name="type">Haptic Type</param>
        /// <returns></returns>
        private static HapticPatterns.PresetType GetPatternForHapticType(HapticType type)
        {
            switch (type)
            {
                case HapticType.ButtonPress:
                    return HapticPatterns.PresetType.LightImpact;
                case HapticType.PrismCollision:
                    return HapticPatterns.PresetType.Success;
                case HapticType.ShipCollision:
                    return HapticPatterns.PresetType.HeavyImpact;
                case HapticType.CrystalCollision:
                    return HapticPatterns.PresetType.MediumImpact;
                case HapticType.MineCollision:
                    return HapticPatterns.PresetType.HeavyImpact;
                case HapticType.None:
                    return HapticPatterns.PresetType.None;
                default:
                    CSDebug.LogErrorFormat("{0} - {1} - Unsupported haptic types.", nameof(HapticController), nameof(GetPatternForHapticType));
                    return HapticPatterns.PresetType.None; // Return Failure for unsupported haptic types
            }
        }
    }
}
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
        static bool s_lofeltInitialized;
        static bool s_diagnosticsLogged;

        void Awake() => s_gameSetting = injectedGameSetting;

        static GameSetting ResolveGameSetting()
        {
            if (s_gameSetting != null) return s_gameSetting;
            s_gameSetting = FindFirstObjectByType<GameSetting>();
            return s_gameSetting;
        }

        /// <summary>
        /// Initializes Lofelt's HapticController and dumps a one-shot diagnostic block
        /// the first time we cross this path. Useful for diagnosing "I feel nothing"
        /// reports — the log identifies platform, version support, advanced-requirement
        /// status, and game-side settings in one place.
        /// </summary>
        static bool EnsureLofeltInitialized()
        {
            if (s_lofeltInitialized) return true;
            s_lofeltInitialized = true;

            bool meetsAdvanced = Lofelt.NiceVibrations.HapticController.Init();
            LogDiagnostics(meetsAdvanced);
            return meetsAdvanced;
        }

        /// <summary>
        /// Dumps the full state of the haptic chain. Safe to call any time — useful for
        /// the Tools > Cosmic Shore > Test Haptic menu and for ad-hoc debugging.
        /// </summary>
        public static void LogDiagnostics(bool? meetsAdvancedOverride = null)
        {
            if (s_diagnosticsLogged && meetsAdvancedOverride == null) return;
            s_diagnosticsLogged = true;

            var settings = ResolveGameSetting();
            bool meetsAdvanced = meetsAdvancedOverride ?? Lofelt.NiceVibrations.HapticController.Init();

            string settingsStr = settings == null
                ? "null (GameSetting not in scene)"
                : $"HapticsEnabled={settings.HapticsEnabled} HapticsLevel={settings.HapticsLevel}";

            Debug.Log(
                "[HapticController] Diagnostic dump:\n" +
                $"  Application.platform = {Application.platform}\n" +
                $"  Application.isEditor = {Application.isEditor}\n" +
                $"  GameSetting:           {settingsStr}\n" +
                $"  Lofelt.hapticsEnabled = {Lofelt.NiceVibrations.HapticController.hapticsEnabled}\n" +
                $"  Lofelt.outputLevel    = {Lofelt.NiceVibrations.HapticController.outputLevel}\n" +
                $"  DeviceCapabilities.platform           = {DeviceCapabilities.platform}\n" +
                $"  DeviceCapabilities.platformVersion    = {DeviceCapabilities.platformVersion}\n" +
                $"  DeviceCapabilities.isVersionSupported = {DeviceCapabilities.isVersionSupported}\n" +
                $"  meetsAdvancedRequirements             = {meetsAdvanced}\n" +
                $"  Note: Lofelt only fires the device vibrator on iOS/Android device builds. " +
                "In Editor or on PC, haptics only emit if a gamepad with rumble is connected and " +
                "the new Input System is installed."
            );
        }

        /// <summary>
        /// Manual smoke test: force-init Lofelt, dump diagnostics, then fire a HeavyImpact preset.
        /// Use from a play-mode menu item to validate the chain end-to-end.
        /// </summary>
        public static void ForceTestPlay()
        {
            s_diagnosticsLogged = false;
            EnsureLofeltInitialized();

            var settings = ResolveGameSetting();
            if (settings != null) Lofelt.NiceVibrations.HapticController.outputLevel = Mathf.Max(0.5f, settings.HapticsLevel);
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.HeavyImpact);
            Debug.Log("[HapticController] ForceTestPlay() invoked — fired HeavyImpact preset. " +
                      "If you felt nothing, see the diagnostic block above.");
        }

        /// <summary>
        /// Play Haptic
        /// Play haptic pattern presets when haptics are enabled.
        /// </summary>
        /// <param name="type">Haptic type</param>
        public static void PlayHaptic(HapticType type)
        {
            if (type == HapticType.None) return;

            var settings = ResolveGameSetting();
            if (settings == null) { LogFirstFailure("GameSetting not resolved (no instance in any loaded scene)."); return; }
            if (!settings.HapticsEnabled) { LogFirstFailure("settings.HapticsEnabled is false (PlayerPref / cloud setting)."); return; }
            if (settings.HapticsLevel == 0) { LogFirstFailure("settings.HapticsLevel is 0 — turn it up in the settings menu."); return; }

            EnsureLofeltInitialized();

            Lofelt.NiceVibrations.HapticController.outputLevel = settings.HapticsLevel;
            HapticPatterns.PlayPreset(GetPatternForHapticType(type));
        }

        public static void PlayConstant(float amplitude, float frequency, float duration)
        {
            var settings = ResolveGameSetting();
            if (settings == null || !settings.HapticsEnabled) return;
            EnsureLofeltInitialized();
            HapticPatterns.PlayConstant(amplitude, frequency, duration);
        }

        /// <summary>
        /// Play a baked .haptic clip authored by HapticClipBaker (or Lofelt Studio).
        /// Falls through silently when disabled or when the clip is empty.
        /// </summary>
        public static void PlayClip(HapticClip clip)
        {
            if (clip == null || clip.json == null || clip.json.Length == 0) return;

            var settings = ResolveGameSetting();
            if (settings == null || !settings.HapticsEnabled || settings.HapticsLevel == 0) return;

            EnsureLofeltInitialized();

            Lofelt.NiceVibrations.HapticController.outputLevel = settings.HapticsLevel;
            Lofelt.NiceVibrations.HapticController.Play(clip);
        }

        static bool s_failureLogged;
        static void LogFirstFailure(string reason)
        {
            if (s_failureLogged) return;
            s_failureLogged = true;
            Debug.LogWarning($"[HapticController] First haptic call short-circuited: {reason}");
            LogDiagnostics();
        }

        /// <summary>
        /// Get Pattern For Haptic Type
        /// Returns mapped Haptic Patterns
        /// </summary>
        /// <param name="type">Haptic Type</param>
        private static HapticPatterns.PresetType GetPatternForHapticType(HapticType type)
        {
            switch (type)
            {
                case HapticType.ButtonPress: return HapticPatterns.PresetType.LightImpact;
                case HapticType.PrismCollision: return HapticPatterns.PresetType.Success;
                case HapticType.ShipCollision: return HapticPatterns.PresetType.HeavyImpact;
                case HapticType.CrystalCollision: return HapticPatterns.PresetType.MediumImpact;
                case HapticType.MineCollision: return HapticPatterns.PresetType.HeavyImpact;
                case HapticType.None: return HapticPatterns.PresetType.None;
                default:
                    CSDebug.LogErrorFormat("{0} - {1} - Unsupported haptic types.", nameof(HapticController), nameof(GetPatternForHapticType));
                    return HapticPatterns.PresetType.None;
            }
        }
    }
}

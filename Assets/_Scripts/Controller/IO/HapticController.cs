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

        void Awake() => s_gameSetting = injectedGameSetting;

        /// <summary>
        /// Play Haptic
        /// Play haptic pattern presets when haptics are enabled.
        /// </summary>
        /// <param name="type">Haptic type</param>
        public static void PlayHaptic(HapticType type)
        {
            if (s_gameSetting == null || !s_gameSetting.HapticsEnabled || s_gameSetting.HapticsLevel == 0)
                return;

            Lofelt.NiceVibrations.HapticController.outputLevel = s_gameSetting.HapticsLevel;

            var pattern = GetPatternForHapticType(type);

            HapticPatterns.PlayPreset(pattern);
        }

        public static void PlayConstant(float amplitude, float frequency, float duration)
        {
            if (s_gameSetting == null || !s_gameSetting.HapticsEnabled)
                return;
            HapticPatterns.PlayConstant(amplitude, frequency, duration);
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
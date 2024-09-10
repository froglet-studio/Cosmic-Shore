using CosmicShore.Core;
using Lofelt.NiceVibrations;
using UnityEngine;

namespace CosmicShore.Game.IO
{
    /// <summary>
    /// Haptic Type
    /// Abstract Haptic Patterns to Haptic Types in the game.
    /// </summary>
    public enum HapticType
    {
        None = 0,
        ButtonPress = 1,
        BlockCollision = 2,
        ShipCollision = 3,
        CrystalCollision = 4,
        FakeCrystalCollision = 5,
    }

    public class HapticController : MonoBehaviour
    {
        /// <summary>
        /// Play Haptic
        /// Play haptic pattern presets when haptics are enabled.
        /// </summary>
        /// <param name="type">Haptic type</param>
        public static void PlayHaptic(HapticType type)
        {
            //Debug.Log($"PlayHaptic - HapticType:{type}");
            if (!GameSetting.Instance.HapticsEnabled || GameSetting.Instance.HapticsLevel == 0)
                return;

            // TODO: would be better to have a haptics manager register for the event in the gamesetting instance to adjust the haptics level
            Lofelt.NiceVibrations.HapticController.outputLevel = GameSetting.Instance.HapticsLevel;

            var pattern = GetPatternForHapticType(type);
            
            HapticPatterns.PlayPreset(pattern);
            /*
            haptic preset notes:

            0, 1, 4, 8  = would good for UI use - feedback for correct input on tutorial
            2, 5, 7 - might be good for running through stuff (positive)
            3 - Not in use (negative) crash? odd pattern - I wouldn't use it unless its going to match an animation cause it might seem out of place otherwise

            5 - Crystal
            4 - UI
            6 - crash into blocks - intense (negative feedback)
            */
            
        }

        public static void PlayConstant(float amplitude, float frequency, float duration)
        {
            if (!GameSetting.Instance.HapticsEnabled)
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
                case HapticType.BlockCollision:
                    return HapticPatterns.PresetType.Success;
                case HapticType.ShipCollision:
                    return HapticPatterns.PresetType.HeavyImpact;
                case HapticType.CrystalCollision:
                    return HapticPatterns.PresetType.MediumImpact;
                case HapticType.FakeCrystalCollision:
                    return HapticPatterns.PresetType.HeavyImpact;
                case HapticType.None:
                    return HapticPatterns.PresetType.None;
                default:
                    Debug.LogErrorFormat("{0} - {1} - Unsupported haptic types.", nameof(HapticController), nameof(GetPatternForHapticType));
                    return HapticPatterns.PresetType.None; // Return Failure for unsupported haptic types
            }
        }
    }
}
using Lofelt.NiceVibrations;
using StarWriter.Core;
using UnityEngine;

namespace _Scripts._Core.Input
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
        DecoyCollision = 5,
    }

    public class HapticController : MonoBehaviour
    {
        /// <summary>
        /// Play Haptic
        /// Play haptic pattern presets when haptics are enabled.
        /// </summary>
        /// <param name="type"></param>
        public static void PlayHaptic(HapticType type)
        {
            if (!GameSetting.Instance.HapticsEnabled)
                return;

            var pattern = GetPatternForHapticType(type);
            if (pattern >= 0)
            {
                HapticPatterns.PlayPreset((HapticPatterns.PresetType)pattern);
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
        }

        /// <summary>
        /// Get Pattern For Haptic Type
        /// Returns mapped Haptic Patterns
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static int GetPatternForHapticType(HapticType type)
        {
            switch (type)
            {
                case HapticType.ButtonPress:
                    return (int)HapticPatterns.PresetType.LightImpact;
                case HapticType.BlockCollision:
                    return (int)HapticPatterns.PresetType.Success;
                case HapticType.ShipCollision:
                    return (int)HapticPatterns.PresetType.HeavyImpact;
                case HapticType.CrystalCollision:
                    return (int)HapticPatterns.PresetType.MediumImpact;
                case HapticType.DecoyCollision:
                    return (int)HapticPatterns.PresetType.HeavyImpact;
                case HapticType.None:
                    Debug.LogWarningFormat("{0} - {1} - haptic type: none.", nameof(HapticController), nameof(GetPatternForHapticType));
                    return 0;
                default:
                    Debug.LogErrorFormat("{0} - {1} - Unsupported haptic types.", nameof(HapticController), nameof(GetPatternForHapticType));
                    return -1; // Return -1 for unsupported haptic types
            }
        }
    }
}
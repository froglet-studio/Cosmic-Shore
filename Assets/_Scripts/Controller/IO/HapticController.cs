using CosmicShore.Core;
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

    /// <summary>
    /// Static gameplay-facing haptics facade. Calls route through
    /// <see cref="AudioHapticsOrchestrator"/>, which plays envelopes measured
    /// from the corresponding sound's actual waveform and arbitrates the single
    /// haptic channel (see Docs/HapticsSystem/ARCHITECTURE.md). When no
    /// orchestrator exists (tests, stripped scenes), calls degrade to the old
    /// direct preset behavior so haptics never silently die.
    /// </summary>
    public class HapticController : MonoBehaviour
    {
        [Inject] GameSetting injectedGameSetting;
        static GameSetting s_gameSetting;

        void Awake() => s_gameSetting = injectedGameSetting;

        /// <summary>
        /// Plays the audio-matched haptic for a gameplay haptic type at full
        /// strength. Prefer the world-position overload for events that happen
        /// somewhere in the world rather than "on" the player.
        /// </summary>
        public static void PlayHaptic(HapticType type)
        {
            if (type == HapticType.None) return;

            var orchestrator = AudioHapticsOrchestrator.Instance;
            if (orchestrator != null)
            {
                RouteThroughOrchestrator(orchestrator, type, null);
                return;
            }

            PlayFallbackPreset(type);
        }

        /// <summary>
        /// Plays the audio-matched haptic for an event at
        /// <paramref name="worldPosition"/>, attenuated by listener distance the
        /// same way the sound is — a far-off detonation is a murmur, not a slam.
        /// </summary>
        public static void PlayHaptic(HapticType type, Vector3 worldPosition)
        {
            if (type == HapticType.None) return;

            var orchestrator = AudioHapticsOrchestrator.Instance;
            if (orchestrator != null)
            {
                RouteThroughOrchestrator(orchestrator, type, worldPosition);
                return;
            }

            PlayFallbackPreset(type);
        }

        /// <summary>
        /// Continuous haptic drive (skim proximity scaling, elemental debuff
        /// buzz). Mixed into the orchestrator's continuous bed rather than
        /// reloading a clip per call, so repeated/per-frame callers no longer
        /// evict one-shot haptics from the channel.
        /// </summary>
        public static void PlayConstant(float amplitude, float frequency, float duration)
        {
            var orchestrator = AudioHapticsOrchestrator.Instance;
            if (orchestrator != null)
            {
                orchestrator.PulseContinuous(amplitude, frequency, duration);
                return;
            }

            if (s_gameSetting == null || !s_gameSetting.HapticsEnabled || s_gameSetting.HapticsLevel == 0)
                return;
            HapticPatterns.PlayConstant(amplitude, frequency, duration);
        }

        /// <summary>
        /// The legacy 5-type vocabulary maps onto the audio categories whose
        /// baked envelopes carry the matching sound's real shape.
        /// </summary>
        static void RouteThroughOrchestrator(AudioHapticsOrchestrator orchestrator, HapticType type, Vector3? worldPosition)
        {
            switch (type)
            {
                case HapticType.ButtonPress:
                    orchestrator.PlayMenuTransient(MenuAudioCategory.OptionClick);
                    return;
                case HapticType.PrismCollision:
                    PlayGameplay(orchestrator, GameplaySFXCategory.CrystalSkim, worldPosition);
                    return;
                case HapticType.ShipCollision:
                    PlayGameplay(orchestrator, GameplaySFXCategory.VesselImpact, worldPosition);
                    return;
                case HapticType.CrystalCollision:
                    PlayGameplay(orchestrator, GameplaySFXCategory.CrystalCollect, worldPosition);
                    return;
                case HapticType.MineCollision:
                    PlayGameplay(orchestrator, GameplaySFXCategory.MineExplode, worldPosition);
                    return;
                default:
                    CSDebug.LogErrorFormat("{0} - {1} - Unsupported haptic type.", nameof(HapticController), nameof(RouteThroughOrchestrator));
                    return;
            }
        }

        static void PlayGameplay(AudioHapticsOrchestrator orchestrator, GameplaySFXCategory category, Vector3? worldPosition)
        {
            if (worldPosition.HasValue) orchestrator.PlayGameplayTransient(category, worldPosition.Value);
            else orchestrator.PlayGameplayTransient(category);
        }

        /// <summary>Pre-orchestrator behavior, kept as the no-orchestrator fallback.</summary>
        static void PlayFallbackPreset(HapticType type)
        {
            if (s_gameSetting == null || !s_gameSetting.HapticsEnabled || s_gameSetting.HapticsLevel == 0)
                return;

            Lofelt.NiceVibrations.HapticController.outputLevel = s_gameSetting.HapticsLevel;
            HapticPatterns.PlayPreset(GetPatternForHapticType(type));
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
                    return HapticPatterns.PresetType.None;
            }
        }
    }
}

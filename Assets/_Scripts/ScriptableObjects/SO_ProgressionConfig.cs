using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Designer-tunable knobs for the game-mode quest progression system.
    ///
    /// Feature unlocks are driven entirely by the unlock chain (<see cref="SO_UnlockList"/> +
    /// <see cref="SO_UnlockData"/>) — quest completion is the only progression currency
    /// (there is no XP). This asset centralizes the rules that used to be hardcoded inside
    /// <c>GameModeProgressionService</c> so they can be changed without touching code:
    ///
    ///   • which modes are always unlocked (e.g. Tournament),
    ///   • whether the first quest in the chain is free,
    ///   • the intensity floor a mode starts at and the absolute intensity cap,
    ///   • which modes ignore intensity gating,
    ///   • the DisplayName that marks the Vessel Hangar feature-unlock quest.
    ///
    /// When no asset is wired the service falls back to a default instance whose field
    /// values reproduce the previous hardcoded behavior exactly, so wiring this asset is
    /// optional and purely additive.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProgressionConfig",
        menuName = "ScriptableObjects/Progression/ProgressionConfig")]
    public class SO_ProgressionConfig : ScriptableObject
    {
        [Header("Default Unlocks")]
        [Tooltip("Game modes that are ALWAYS unlocked regardless of quest progress " +
                 "(e.g. Tournament, a session-level meta that is not part of the chain).")]
        // Maelstrom (Tournament) is now part of the quest chain — claimed after Joust —
        // so nothing is always-unlocked by default anymore.
        public List<GameModes> alwaysUnlockedModes = new();

        [Tooltip("If true, the first quest in the quest list is always unlocked " +
                 "('the first game is free'). This is independent of the always-unlocked list.")]
        public bool firstQuestAlwaysUnlocked = true;

        [Header("Intensity")]
        [Tooltip("Highest intensity available the moment a mode is unlocked (intensity 1..N " +
                 "playable). Default 3 => intensities 1, 2 and 3 are open immediately and only " +
                 "intensity 4 is gated: completing the intensity-3 goal unlocks 4 and finishes " +
                 "the quest.")]
        [Min(1)] public int defaultMaxIntensity = 3;

        [Tooltip("Absolute maximum intensity tier any mode can reach.")]
        [Min(1)] public int maxIntensity = 4;

        [Tooltip("Game modes whose FULL intensity range is always available (not gated by " +
                 "play counts), e.g. Tournament (one intensity is picked in the lobby).")]
        public List<GameModes> fullIntensityModes = new() { GameModes.Tournament };

        [Header("Feature Unlocks")]
        [Tooltip("DisplayName of the quest that gates the Vessel Hangar feature. The hangar " +
                 "unlocks once every quest before this one in the chain is completed.")]
        public string vesselHangarQuestDisplayName = "VESSEL HANGAR";

        /// <summary>True if the mode is in the always-unlocked list.</summary>
        public bool IsAlwaysUnlocked(GameModes mode) =>
            alwaysUnlockedModes != null && alwaysUnlockedModes.Contains(mode);

        /// <summary>True if the mode ignores intensity gating (full range always available).</summary>
        public bool HasFullIntensity(GameModes mode) =>
            fullIntensityModes != null && fullIntensityModes.Contains(mode);
    }
}

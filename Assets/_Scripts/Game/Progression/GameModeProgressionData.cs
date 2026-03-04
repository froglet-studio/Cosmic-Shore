using System;
using System.Collections.Generic;

namespace CosmicShore.Game.Progression
{
    /// <summary>
    /// Cloud-saved progression data for the game-mode quest chain.
    /// Persisted under UGSKeys.GameModeProgression in Cloud Save.
    /// Follows the same pattern as VesselStatsCloudData.
    /// </summary>
    [Serializable]
    public class GameModeProgressionData
    {
        /// <summary>
        /// Set of game mode names that the player has unlocked.
        /// Uses string keys (GameModes enum name) for JSON serialization compatibility.
        /// The first quest's game mode is always considered unlocked regardless.
        /// </summary>
        public List<string> UnlockedModes = new();

        /// <summary>
        /// Set of game mode names where the quest has been completed
        /// (target met) but the player hasn't yet claimed/acknowledged the unlock.
        /// When the player taps the unlock button on the quest screen,
        /// the mode moves from CompletedQuests → UnlockedModes for the next mode.
        /// </summary>
        public List<string> CompletedQuests = new();

        /// <summary>
        /// Per-mode best stats for quest evaluation.
        /// Key = GameModes enum name, Value = best achieved value for that mode's target.
        /// </summary>
        public Dictionary<string, float> BestStats = new();

        public bool IsUnlocked(string modeName)
        {
            return UnlockedModes.Contains(modeName);
        }

        public bool IsQuestCompleted(string modeName)
        {
            return CompletedQuests.Contains(modeName);
        }

        public void MarkUnlocked(string modeName)
        {
            if (!UnlockedModes.Contains(modeName))
                UnlockedModes.Add(modeName);
        }

        public void MarkQuestCompleted(string modeName)
        {
            if (!CompletedQuests.Contains(modeName))
                CompletedQuests.Add(modeName);
        }

        public float GetBestStat(string modeName)
        {
            return BestStats.TryGetValue(modeName, out var val) ? val : 0f;
        }

        public bool TryUpdateBestStat(string modeName, float value)
        {
            if (BestStats.TryGetValue(modeName, out var current) && value <= current)
                return false;

            BestStats[modeName] = value;
            return true;
        }
    }
}

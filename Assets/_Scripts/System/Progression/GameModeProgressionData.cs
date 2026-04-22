using System;
using System.Collections.Generic;

namespace CosmicShore.Core
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
        /// Set of feature names that the player has unlocked via the quest chain.
        /// Feature quests (e.g. Vessel Hangar) use DisplayName as their unique key
        /// instead of a GameMode enum name.
        /// </summary>
        public List<string> UnlockedFeatures = new();

        /// <summary>
        /// Per-mode best stats for quest evaluation.
        /// Key = GameModes enum name, Value = best achieved value for that mode's target.
        /// </summary>
        public Dictionary<string, float> BestStats = new();

        /// <summary>
        /// Per-mode highest unlocked intensity level.
        /// Key = GameModes enum name, Value = max intensity the player can play (2, 3, or 4).
        /// Defaults to 2 when a mode is first unlocked (intensity 1 and 2 available).
        /// </summary>
        public Dictionary<string, int> MaxUnlockedIntensity = new();

        /// <summary>
        /// Play counts per mode and intensity level for intensity unlock progression.
        /// Key = "{ModeName}:{intensity}", Value = number of games completed at that intensity.
        /// </summary>
        public Dictionary<string, int> IntensityPlayCounts = new();

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

        public bool IsFeatureUnlocked(string featureName)
        {
            return UnlockedFeatures.Contains(featureName);
        }

        public void MarkFeatureUnlocked(string featureName)
        {
            if (!UnlockedFeatures.Contains(featureName))
                UnlockedFeatures.Add(featureName);
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

        // ── Intensity Progression ────────────────────────────────────────────

        static string PlayCountKey(string modeName, int intensity) => $"{modeName}:{intensity}";

        /// <summary>
        /// Returns the highest intensity level the player can play for this mode.
        /// Defaults to 2 (intensity 1 and 2 available when a mode is first unlocked).
        /// </summary>
        public int GetMaxUnlockedIntensity(string modeName)
        {
            return MaxUnlockedIntensity.TryGetValue(modeName, out var val) ? val : 2;
        }

        public void SetMaxUnlockedIntensity(string modeName, int value)
        {
            MaxUnlockedIntensity[modeName] = value;
        }

        /// <summary>
        /// Returns how many games the player has completed at the given intensity for this mode.
        /// </summary>
        public int GetIntensityPlayCount(string modeName, int intensity)
        {
            return IntensityPlayCounts.TryGetValue(PlayCountKey(modeName, intensity), out var val) ? val : 0;
        }

        /// <summary>
        /// Increments the play count for the given mode and intensity. Returns the new count.
        /// </summary>
        public int IncrementIntensityPlayCount(string modeName, int intensity)
        {
            var key = PlayCountKey(modeName, intensity);
            IntensityPlayCounts.TryGetValue(key, out var count);
            IntensityPlayCounts[key] = count + 1;
            return count + 1;
        }

        /// <summary>
        /// Ensures the intensity tracking is initialized for a mode (sets default max to 2 if missing).
        /// </summary>
        public void EnsureIntensityInitialized(string modeName)
        {
            if (!MaxUnlockedIntensity.ContainsKey(modeName))
                MaxUnlockedIntensity[modeName] = 2;
        }

        /// <summary>
        /// Merges data from another instance into this one, keeping the higher/newer values.
        /// Used when local ProgressionData diverges from the repo's data object
        /// (e.g., game ended before cloud data finished loading).
        /// </summary>
        public void MergeFrom(GameModeProgressionData other)
        {
            if (other == null) return;

            foreach (var mode in other.UnlockedModes)
                MarkUnlocked(mode);

            foreach (var quest in other.CompletedQuests)
                MarkQuestCompleted(quest);

            foreach (var feature in other.UnlockedFeatures)
                MarkFeatureUnlocked(feature);

            foreach (var kvp in other.BestStats)
            {
                if (!BestStats.TryGetValue(kvp.Key, out var existing) || kvp.Value > existing)
                    BestStats[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in other.MaxUnlockedIntensity)
            {
                if (!MaxUnlockedIntensity.TryGetValue(kvp.Key, out var existing) || kvp.Value > existing)
                    MaxUnlockedIntensity[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in other.IntensityPlayCounts)
            {
                if (!IntensityPlayCounts.TryGetValue(kvp.Key, out var existing) || kvp.Value > existing)
                    IntensityPlayCounts[kvp.Key] = kvp.Value;
            }
        }
    }
}

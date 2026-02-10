using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game.Analytics
{
    [CreateAssetMenu(fileName = "LeaderboardConfig", menuName = "CosmicShore/Analytics/LeaderboardConfig")]
    public class LeaderboardConfigSO : ScriptableObject
    {
        [Serializable]
        public struct LeaderboardMapping
        {
            public GameModes GameMode;
            [Range(1, 4)] public int Intensity;
            public string LeaderboardId;
        }

        [SerializeField] List<LeaderboardMapping> leaderboardMappings = new List<LeaderboardMapping>();
        [SerializeField] List<GameModes> activeGameModes = new List<GameModes>();

        private Dictionary<(GameModes, int), string> _cache;

        /// <summary>
        /// Gets the leaderboard ID for the specified game mode and intensity.
        /// </summary>
        public string GetLeaderboardId(GameModes mode, int intensity)
        {
            // Use cache for better performance
            if (_cache == null)
                BuildCache();

            if (_cache.TryGetValue((mode, intensity), out string id))
                return id;

            Debug.LogWarning($"No leaderboard ID found for {mode} with intensity {intensity}");
            return null;
        }

        /// <summary>
        /// Checks if a leaderboard mapping exists for the given mode and intensity.
        /// </summary>
        public bool HasMapping(GameModes mode, int intensity)
        {
            if (_cache == null)
                BuildCache();

            return _cache.ContainsKey((mode, intensity));
        }

        /// <summary>
        /// Gets all intensity levels available for a specific game mode.
        /// </summary>
        public int[] GetAvailableIntensities(GameModes mode)
        {
            return leaderboardMappings
                .Where(x => x.GameMode == mode)
                .Select(x => x.Intensity)
                .OrderBy(x => x)
                .ToArray();
        }

        /// <summary>
        /// Gets all leaderboard IDs for a specific game mode.
        /// </summary>
        public string[] GetLeaderboardIds(GameModes mode)
        {
            return leaderboardMappings
                .Where(x => x.GameMode == mode)
                .Select(x => x.LeaderboardId)
                .ToArray();
        }

        /// <summary>
        /// Checks if a game mode is marked as active.
        /// </summary>
        public bool IsGameModeActive(GameModes mode)
        {
            // If no active modes are defined, consider all modes active
            if (activeGameModes == null || activeGameModes.Count == 0)
                return true;
            
            return activeGameModes.Contains(mode);
        }

        /// <summary>
        /// Gets all active game modes.
        /// </summary>
        public GameModes[] GetActiveGameModes()
        {
            // If no active modes defined, return all enum values
            if (activeGameModes == null || activeGameModes.Count == 0)
            {
                return System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>().ToArray();
            }
            
            return activeGameModes.ToArray();
        }

        /// <summary>
        /// Rebuilds the internal cache. Called automatically when needed.
        /// </summary>
        private void BuildCache()
        {
            _cache = new Dictionary<(GameModes, int), string>();
            
            foreach (var mapping in leaderboardMappings)
            {
                var key = (mapping.GameMode, mapping.Intensity);
                if (!_cache.ContainsKey(key))
                {
                    _cache[key] = mapping.LeaderboardId;
                }
                else
                {
                    Debug.LogWarning($"Duplicate mapping found for {mapping.GameMode} - Intensity {mapping.Intensity}. Using first occurrence.");
                }
            }
        }

        /// <summary>
        /// Clears the cache. Useful when mappings are modified at runtime (editor only).
        /// </summary>
        public void ClearCache()
        {
            _cache = null;
        }

        private void OnEnable()
        {
            // Clear cache when asset is loaded to ensure fresh data
            ClearCache();
        }

        private void OnValidate()
        {
            // Clear cache when values change in editor
            ClearCache();
            
            // Validate intensity values
            for (int i = 0; i < leaderboardMappings.Count; i++)
            {
                var mapping = leaderboardMappings[i];
                if (mapping.Intensity < 1 || mapping.Intensity > 4)
                {
                    Debug.LogError($"Invalid intensity value {mapping.Intensity} for {mapping.GameMode}. Must be between 1-4.");
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor utility: Get all mappings (for inspector use).
        /// </summary>
        public List<LeaderboardMapping> GetAllMappings() => leaderboardMappings;
#endif
    }
}
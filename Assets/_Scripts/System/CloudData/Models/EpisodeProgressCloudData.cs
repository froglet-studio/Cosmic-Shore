using System;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persists episode unlock/completion state to UGS Cloud Save.
    ///
    /// JSON example:
    /// {
    ///   "UnlockedEpisodes": ["episode_01", "episode_02"],
    ///   "CompletedEpisodes": ["episode_01"],
    ///   "EpisodeProgress": {
    ///     "episode_01": {
    ///       "MissionsCompleted": 5,
    ///       "TotalMissions": 5,
    ///       "BestScore": 12500,
    ///       "StarsEarned": 3
    ///     },
    ///     "episode_02": {
    ///       "MissionsCompleted": 2,
    ///       "TotalMissions": 8,
    ///       "BestScore": 3200,
    ///       "StarsEarned": 1
    ///     }
    ///   }
    /// }
    /// </summary>
    [Serializable]
    public class EpisodeProgressCloudData
    {
        public List<string> UnlockedEpisodes = new();
        public List<string> CompletedEpisodes = new();
        public Dictionary<string, EpisodeState> EpisodeProgress = new();

        public bool IsUnlocked(string episodeId)
        {
            return UnlockedEpisodes.Contains(episodeId);
        }

        public bool IsCompleted(string episodeId)
        {
            return CompletedEpisodes.Contains(episodeId);
        }

        public void UnlockEpisode(string episodeId)
        {
            if (!UnlockedEpisodes.Contains(episodeId))
                UnlockedEpisodes.Add(episodeId);
        }

        public void CompleteEpisode(string episodeId)
        {
            if (!CompletedEpisodes.Contains(episodeId))
                CompletedEpisodes.Add(episodeId);
        }

        public EpisodeState GetOrCreate(string episodeId)
        {
            if (!EpisodeProgress.TryGetValue(episodeId, out var state))
            {
                state = new EpisodeState();
                EpisodeProgress[episodeId] = state;
            }
            return state;
        }

        public void ReportMissionCompleted(string episodeId, int totalMissions, int score)
        {
            var state = GetOrCreate(episodeId);
            state.MissionsCompleted = Math.Min(state.MissionsCompleted + 1, totalMissions);
            state.TotalMissions = totalMissions;
            if (score > state.BestScore)
                state.BestScore = score;

            if (state.MissionsCompleted >= totalMissions)
                CompleteEpisode(episodeId);
        }
    }

    [Serializable]
    public class EpisodeState
    {
        public int MissionsCompleted;
        public int TotalMissions;
        public int BestScore;
        public int StarsEarned;
    }
}

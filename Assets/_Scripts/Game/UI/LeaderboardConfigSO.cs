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

        [SerializeField] List<LeaderboardMapping> leaderboardMappings;

        public string GetLeaderboardId(GameModes mode, int intensity)
        {
            var mapping = leaderboardMappings.FirstOrDefault(x => x.GameMode == mode && x.Intensity == intensity);
            return mapping.LeaderboardId;
        }
    }
}
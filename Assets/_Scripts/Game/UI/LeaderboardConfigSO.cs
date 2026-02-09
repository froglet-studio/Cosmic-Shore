using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game.Analytics
{
    [CreateAssetMenu(fileName = "LeaderboardConfig", menuName = "ScriptableObjects/Analytics/LeaderboardConfig")]
    public class LeaderboardConfigSO : ScriptableObject
    {
        [Serializable]
        public struct LeaderboardEntry
        {
            public string EntryName;
            public GameModes GameMode;
            public bool IsMultiplayer;
            [Range(1, 4)] public int Intensity;
            public string LeaderboardId;
        }

        [SerializeField] private List<LeaderboardEntry> leaderboardEntries = new();

        public string GetLeaderboardId(GameModes mode, bool isMultiplayer, int intensity)
        {
            var entry = leaderboardEntries.FirstOrDefault(x => 
                x.GameMode == mode && 
                x.IsMultiplayer == isMultiplayer && 
                x.Intensity == intensity
            );

            if (!string.IsNullOrEmpty(entry.LeaderboardId))
            {
                return entry.LeaderboardId;
            }

            Debug.LogWarning($"[LeaderboardConfig] No ID found for Mode: {mode} | MP: {isMultiplayer} | Intensity: {intensity}");
            return null;
        }
    }
}
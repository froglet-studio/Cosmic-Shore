using System;
using System.Collections.Generic;

namespace CosmicShore.App.Systems.CloudData.Models
{
    /// <summary>
    /// Persists training game tier progress to UGS Cloud Save.
    /// Replaces the local-file-based TrainingGameProgressSystem (training_progress.data).
    ///
    /// JSON example:
    /// {
    ///   "Games": {
    ///     "WildlifeBlitz": {
    ///       "CurrentIntensity": 3,
    ///       "Tiers": [
    ///         { "Satisfied": true,  "Claimed": true  },
    ///         { "Satisfied": true,  "Claimed": true  },
    ///         { "Satisfied": true,  "Claimed": false },
    ///         { "Satisfied": false, "Claimed": false }
    ///       ]
    ///     },
    ///     "HexRace": {
    ///       "CurrentIntensity": 1,
    ///       "Tiers": [
    ///         { "Satisfied": false, "Claimed": false },
    ///         { "Satisfied": false, "Claimed": false },
    ///         { "Satisfied": false, "Claimed": false },
    ///         { "Satisfied": false, "Claimed": false }
    ///       ]
    ///     }
    ///   }
    /// }
    /// </summary>
    [Serializable]
    public class TrainingProgressCloudData
    {
        public Dictionary<string, TrainingGameState> Games = new();

        public TrainingGameState GetOrCreate(string gameModeName)
        {
            if (!Games.TryGetValue(gameModeName, out var state))
            {
                state = TrainingGameState.CreateDefault();
                Games[gameModeName] = state;
            }
            return state;
        }
    }

    [Serializable]
    public class TrainingGameState
    {
        public int CurrentIntensity = 1;
        public List<TrainingTierState> Tiers = new();

        public static TrainingGameState CreateDefault()
        {
            return new TrainingGameState
            {
                CurrentIntensity = 1,
                Tiers = new List<TrainingTierState>
                {
                    new(), new(), new(), new()
                }
            };
        }

        public void SatisfyTier(int tier)
        {
            if (tier < 1 || tier > Tiers.Count) return;
            Tiers[tier - 1].Satisfied = true;
            if (tier > CurrentIntensity)
                CurrentIntensity = Math.Min(tier + 1, Tiers.Count);
        }

        public void ClaimTier(int tier)
        {
            if (tier < 1 || tier > Tiers.Count) return;
            Tiers[tier - 1].Claimed = true;
        }

        public bool IsTierSatisfied(int tier)
        {
            return tier >= 1 && tier <= Tiers.Count && Tiers[tier - 1].Satisfied;
        }

        public bool IsTierClaimed(int tier)
        {
            return tier >= 1 && tier <= Tiers.Count && Tiers[tier - 1].Claimed;
        }
    }

    [Serializable]
    public class TrainingTierState
    {
        public bool Satisfied;
        public bool Claimed;
    }
}

using System;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persists daily challenge state to UGS Cloud Save.
    /// Replaces the PlayerPrefs-based DailyChallengeSystem storage.
    ///
    /// JSON example:
    /// {
    ///   "ChallengeDate": "2026-03-03",
    ///   "LastTicketIssuedDate": "2026-03-03",
    ///   "TicketBalance": 2,
    ///   "GameMode": "WildlifeBlitz",
    ///   "Intensity": 3,
    ///   "HighScore": 1500,
    ///   "RewardTiers": [
    ///     { "Satisfied": true,  "Claimed": true  },
    ///     { "Satisfied": true,  "Claimed": false },
    ///     { "Satisfied": false, "Claimed": false }
    ///   ]
    /// }
    /// </summary>
    [Serializable]
    public class DailyChallengeCloudData
    {
        public string ChallengeDate = "";
        public string LastTicketIssuedDate = "";
        public int TicketBalance;
        public string GameMode = "";
        public int Intensity;
        public int HighScore;
        public List<RewardTierState> RewardTiers = new()
        {
            new(), new(), new()
        };

        public bool IsNewDay(DateTime utcNow)
        {
            if (string.IsNullOrEmpty(ChallengeDate)) return true;
            if (DateTime.TryParse(ChallengeDate, out var date))
                return utcNow.Date > date.Date;
            return true;
        }

        public bool NeedsTicketRefill(DateTime utcNow)
        {
            if (string.IsNullOrEmpty(LastTicketIssuedDate)) return true;
            if (DateTime.TryParse(LastTicketIssuedDate, out var date))
                return utcNow.Date > date.Date;
            return true;
        }

        public void ResetForNewDay(string gameMode, int intensity, int dailyAttempts)
        {
            ChallengeDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            GameMode = gameMode;
            Intensity = intensity;
            HighScore = 0;
            RewardTiers = new List<RewardTierState> { new(), new(), new() };

            if (NeedsTicketRefill(DateTime.UtcNow))
            {
                TicketBalance = Math.Max(TicketBalance, dailyAttempts);
                LastTicketIssuedDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            }
        }

        public bool TryReportScore(int score)
        {
            if (score <= HighScore) return false;
            HighScore = score;
            return true;
        }

        public bool SatisfyTier(int tier)
        {
            if (tier < 1 || tier > RewardTiers.Count) return false;
            if (RewardTiers[tier - 1].Satisfied) return false;
            RewardTiers[tier - 1].Satisfied = true;
            return true;
        }

        public bool ClaimTier(int tier)
        {
            if (tier < 1 || tier > RewardTiers.Count) return false;
            var t = RewardTiers[tier - 1];
            if (!t.Satisfied || t.Claimed) return false;
            RewardTiers[tier - 1].Claimed = true;
            return true;
        }
    }

    [Serializable]
    public class RewardTierState
    {
        public bool Satisfied;
        public bool Claimed;
    }
}

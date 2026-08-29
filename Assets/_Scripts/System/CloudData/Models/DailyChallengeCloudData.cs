using System;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// The player's PROGRESS against the daily challenge, persisted to UGS Cloud Save under
    /// <c>DAILY_CHALLENGE</c>.
    ///
    /// <para>It deliberately does NOT define the challenge. The definition is a pure function of
    /// the UTC date (<c>DailyChallengeCatalogSO.ForDate</c>), so a cold or offline launch can
    /// still draw the card; the mode/intensity/target/metric mirrored here are a RECORD of what
    /// the player was working against, used to detect that the day rolled over and to keep the
    /// card honest if the catalog is ever re-authored mid-day.</para>
    ///
    /// <para>JSON example:</para>
    /// <code>
    /// {
    ///   "SchemaVersion": 2,
    ///   "ChallengeDate": "2026-08-29",
    ///   "GameMode": "MultiplayerCrystalCapture",
    ///   "Intensity": 1,
    ///   "Metric": "Crystals",
    ///   "TargetValue": 30,
    ///   "BestValue": 30,
    ///   "Completed": true,
    ///   "CompletedAtUnixMs": 1756468800000,
    ///   "Attempts": 2,
    ///   "LastTicketIssuedDate": "2026-08-29",
    ///   "TicketBalance": 1
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public class DailyChallengeCloudData
    {
        public int SchemaVersion = 2;

        /// <summary>UTC "yyyy-MM-dd" the rest of this record belongs to.</summary>
        public string ChallengeDate = "";

        // ── Record of the challenge these numbers were earned against ──
        public string GameMode = "";
        public int Intensity;
        public string Metric = "";
        public int TargetValue;

        // ── Progress ──
        /// <summary>Best value of the challenge metric the player has reached today.</summary>
        public int BestValue;
        public bool Completed;
        public long CompletedAtUnixMs;
        public int Attempts;

        // ── Attempt tickets (optional throttle; 0 tickets configured = unlimited) ──
        public string LastTicketIssuedDate = "";
        public int TicketBalance;

        // ── Legacy reward ladder (kept so an existing cloud record round-trips intact) ──
        public int HighScore;
        public List<RewardTierState> RewardTiers = new() { new(), new(), new() };

        /// <summary>True when this record is for an earlier UTC day than <paramref name="dateKey"/>.</summary>
        public bool IsStale(string dateKey) =>
            string.IsNullOrEmpty(ChallengeDate) || ChallengeDate != dateKey;

        /// <summary>
        /// Wipes the day's progress and stamps the new challenge. Tickets are topped up to
        /// <paramref name="dailyAttempts"/> rather than overwritten, so a player who banked
        /// attempts does not lose them at midnight.
        /// </summary>
        public void ResetForNewDay(string dateKey, string gameMode, int intensity,
                                   string metric, int targetValue, int dailyAttempts)
        {
            ChallengeDate = dateKey;
            GameMode = gameMode;
            Intensity = intensity;
            Metric = metric;
            TargetValue = targetValue;

            BestValue = 0;
            Completed = false;
            CompletedAtUnixMs = 0;
            Attempts = 0;
            HighScore = 0;
            RewardTiers = new List<RewardTierState> { new(), new(), new() };

            if (LastTicketIssuedDate != dateKey)
            {
                TicketBalance = Math.Max(TicketBalance, dailyAttempts);
                LastTicketIssuedDate = dateKey;
            }
        }

        /// <summary>
        /// Folds one attempt's result in. Returns true when anything changed (so the caller only
        /// marks the repository dirty on a real change).
        /// </summary>
        public bool RecordAttempt(int achievedValue, int targetValue, DateTime utcNow)
        {
            bool changed = false;

            Attempts++;
            changed = true;

            if (achievedValue > BestValue)
                BestValue = achievedValue;

            if (!Completed && targetValue > 0 && BestValue >= targetValue)
            {
                Completed = true;
                CompletedAtUnixMs = new DateTimeOffset(utcNow.ToUniversalTime()).ToUnixTimeMilliseconds();
            }

            return changed;
        }
    }

    [Serializable]
    public class RewardTierState
    {
        public bool Satisfied;
        public bool Claimed;
    }
}

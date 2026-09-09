using System;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// The player's PROGRESS against the weekly challenge, persisted to UGS Cloud Save under
    /// <c>WEEKLY_CHALLENGE</c>.
    ///
    /// <para>It deliberately does NOT define the challenge. The definition is a pure function of
    /// the UTC date (<c>WeeklyChallengeCatalogSO.ForDate</c>), so a cold or offline launch can
    /// still draw the card; the mode/intensity/target/metric mirrored here are a RECORD of what
    /// the player was working against, used to detect that the day rolled over and to keep the
    /// card honest if the catalog is ever re-authored mid-day.</para>
    ///
    /// <para>JSON example:</para>
    /// <code>
    /// {
    ///   "SchemaVersion": 4,
    ///   "ChallengeWeek": "2026-08-29",
    ///   "GameMode": "Scurry",
    ///   "Intensity": 1,
    ///   "Metric": "Crystals",
    ///   "TargetValue": 30,
    ///   "BestValue": 30,
    ///   "Completed": true,
    ///   "CompletedAtUnixMs": 1756468800000,
    ///   "BestTimeMs": 47300,
    ///   "AttemptDay": "2026-09-03",
    ///   "Attempts": 1,
    ///   "LastTicketIssuedDate": "2026-08-29",
    ///   "TicketBalance": 1
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public class WeeklyChallengeCloudData
    {
        public int SchemaVersion = 4;

        /// <summary>UTC "yyyy-MM-dd" the rest of this record belongs to.</summary>
        public string ChallengeWeek = "";

        // ── Record of the challenge these numbers were earned against ──
        public string GameMode = "";
        public int Intensity;
        public string Metric = "";
        public int TargetValue;

        // ── Progress ──
        /// <summary>Best value of the challenge metric the player has reached this week.</summary>
        public int BestValue;
        public bool Completed;
        public long CompletedAtUnixMs;

        /// <summary>Fastest completion this week, in milliseconds. 0 = never completed.</summary>
        public long BestTimeMs;

        /// <summary>
        /// The UTC day <see cref="Attempts"/> counts. The challenge is WEEKLY but the attempt is
        /// DAILY - a player comes back every day to beat their time - so the counter is filed
        /// under a day and reads as zero on any other one (<see cref="AttemptsOn"/>).
        /// </summary>
        public string AttemptDay = "";

        /// <summary>Attempts started on <see cref="AttemptDay"/>.</summary>
        public int Attempts;

        // ── Legacy fields (kept so an existing cloud record round-trips intact) ──
        public string LastTicketIssuedDate = "";
        public int TicketBalance;
        public int HighScore;
        public List<RewardTierState> RewardTiers = new() { new(), new(), new() };

        /// <summary>True when this record is for an earlier UTC day than <paramref name="periodKey"/>.</summary>
        public bool IsStale(string periodKey) =>
            string.IsNullOrEmpty(ChallengeWeek) || ChallengeWeek != periodKey;

        /// <summary>
        /// Wipes the period's progress and stamps the new challenge. The attempt counter resets
        /// with it - attempts do not bank, because "one a day" is a rhythm rather than a currency.
        /// </summary>
        public void ResetForNewDay(string periodKey, string gameMode, int intensity,
                                   string metric, int targetValue)
        {
            ChallengeWeek = periodKey;
            GameMode = gameMode;
            Intensity = intensity;
            Metric = metric;
            TargetValue = targetValue;

            BestValue = 0;
            Completed = false;
            CompletedAtUnixMs = 0;
            BestTimeMs = 0;
            AttemptDay = "";
            Attempts = 0;
            HighScore = 0;
            RewardTiers = new List<RewardTierState> { new(), new(), new() };
        }

        /// <summary>Attempts started on <paramref name="dayKey"/> - zero for any other day, which is
        /// how the daily attempt refreshes: a KEY comparison at read time, never a timer.</summary>
        public int AttemptsOn(string dayKey) =>
            !string.IsNullOrEmpty(dayKey) && AttemptDay == dayKey ? Attempts : 0;

        /// <summary>Spends one attempt on <paramref name="dayKey"/>, rolling the counter over when
        /// the day has moved on. Attempts do not bank across days.</summary>
        public void SpendAttempt(string dayKey)
        {
            if (AttemptDay != dayKey)
            {
                AttemptDay = dayKey ?? "";
                Attempts = 0;
            }
            Attempts++;
        }

        /// <summary>
        /// Records a completion TIME. Returns true when it is the week's fastest so far - the one
        /// case worth submitting, since the board ranks a player by their best and a slower later
        /// run has nothing to add to it. The first completion of the week always qualifies.
        /// </summary>
        public bool RecordCompletionTime(long milliseconds)
        {
            if (milliseconds <= 0) return false;
            if (BestTimeMs > 0 && milliseconds >= BestTimeMs) return false;
            BestTimeMs = milliseconds;
            return true;
        }

        /// <summary>
        /// Folds one attempt's RESULT in. It deliberately does not touch <see cref="Attempts"/>:
        /// an attempt is counted when it STARTS (<c>WeeklyChallengeService.SpendAttempt</c>), so
        /// that quitting mid-run still spends it. Returns true when anything changed.
        /// </summary>
        public bool RecordResult(int achievedValue, int targetValue, DateTime utcNow) =>
            RecordResult(achievedValue, targetValue > 0 && achievedValue >= targetValue, utcNow);

        /// <summary>
        /// The same fold with completion DECIDED BY THE CALLER - for a challenge whose target is
        /// the mode's own end condition, where "done" is the match's verdict (the player's domain
        /// won) rather than a number this record could compare against.
        /// </summary>
        public bool RecordResult(int achievedValue, bool completed, DateTime utcNow)
        {
            bool changed = false;

            if (achievedValue > BestValue)
            {
                BestValue = achievedValue;
                changed = true;
            }

            if (!Completed && completed)
            {
                Completed = true;
                CompletedAtUnixMs = new DateTimeOffset(utcNow.ToUniversalTime()).ToUnixTimeMilliseconds();
                changed = true;
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

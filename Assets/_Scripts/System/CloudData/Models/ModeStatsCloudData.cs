using System;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Per-game-mode, per-intensity play record. Cloud key: <c>MODE_STATS</c>.
    ///
    /// Replaces <c>PLAYER_STATS_PROFILE</c> and its four bespoke sub-models
    /// (WildlifeBlitz / HexRace / Joust / CrystalCapture), which held the same idea under
    /// four different field names - <c>HighScores</c>, <c>BestMultiplayerRaceTimes</c>,
    /// <c>BestRaceTimes</c>, <c>HighScores</c> - two of them int and two float.
    ///
    /// Adding a mode is now DATA, not code: previously it meant a fifth class, a fifth root
    /// field, a fifth null-coalesce in the repository, and a fifth branch in
    /// <c>UGSStatsManager.GetEvaluatedHighScore</c>.
    /// See Docs/Analytics/DATA_ARCHITECTURE.md §3.3.
    ///
    /// Sole writer: <c>UGSStatsManager.Report*Stats</c> at game end.
    /// </summary>
    [Serializable]
    public class ModeStatsCloudData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>Keyed by <see cref="MakeKey"/> - "{GameMode}:{Intensity}".</summary>
        public Dictionary<string, ModeRecord> Modes = new();

        /// <summary>
        /// The one composite-key format for the whole project. <c>GAME_MODE_PROGRESSION</c>
        /// already used ':'; the old stats profile used '_'. One convention now.
        /// </summary>
        public static string MakeKey(object mode, int intensity) => $"{mode}:{intensity}";

        public ModeRecord GetOrCreate(object mode, int intensity)
        {
            var key = MakeKey(mode, intensity);
            if (!Modes.TryGetValue(key, out var record))
            {
                record = new ModeRecord();
                Modes[key] = record;
            }
            return record;
        }

        public bool TryGet(object mode, int intensity, out ModeRecord record) =>
            Modes.TryGetValue(MakeKey(mode, intensity), out record);
    }

    /// <summary>
    /// One mode at one intensity. Uniform across every mode.
    ///
    /// Golf-vs-high-score direction is deliberately NOT stored here: <c>LeaderboardConfigSO</c>
    /// already owns it, and duplicating it per record would let the two drift.
    /// </summary>
    [Serializable]
    public class ModeRecord
    {
        public int GamesPlayed;

        /// <summary>Wins. With <see cref="GamesPlayed"/> this gives per-mode win rate, and it is
        /// the denominator the rematch-rate-by-mode analysis needs.</summary>
        public int GamesWon;

        /// <summary>Best result. Float for every mode - the old models mixed int and float
        /// for the same concept. Interpretation (lower/higher is better) comes from config.</summary>
        public float BestScore;

        /// <summary>True once <see cref="BestScore"/> holds a real result, so "no score yet"
        /// is distinguishable from a legitimate 0.</summary>
        public bool HasScore;

        public long LastPlayedUtcMs;

        /// <summary>Time at the stick in this mode, pause and background excluded.</summary>
        public float FlightTimeSeconds;

        /// <summary>
        /// Records a result. <paramref name="lowerIsBetter"/> comes from the mode's leaderboard
        /// config. Returns true when this run set a new best.
        /// </summary>
        public bool TryUpdateBest(float score, bool lowerIsBetter)
        {
            if (!HasScore)
            {
                BestScore = score;
                HasScore = true;
                return true;
            }

            bool improved = lowerIsBetter ? score < BestScore : score > BestScore;
            if (!improved)
                return false;

            BestScore = score;
            return true;
        }
    }
}

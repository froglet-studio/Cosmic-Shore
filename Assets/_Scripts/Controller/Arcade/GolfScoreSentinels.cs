using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Single source of truth for the "loser / did-not-finish" score sentinels used by the
    /// time-based golf modes (SkimRace, Joust). Golf rules: lower is better, so a non-winner
    /// gets a large sentinel that always sorts behind any real finish time (elapsed seconds).
    /// Controllers/trackers ENCODE these on game end; scoreboards, end-game controllers and
    /// stats/progression services DECODE them. Centralizing the values + helpers here means
    /// the write and read sides cannot drift (see Docs/ScoringSystem/REFACTOR.md R4).
    /// </summary>
    public static class GolfScoreSentinels
    {
        /// <summary>
        /// Scores at or above this are loser / DNF sentinels, not real finish times. Real
        /// finish times are elapsed seconds and always sit well below this, so this also
        /// serves as the "is this a real finish time?" ceiling for every time-based golf
        /// mode (it is &lt;= the smallest loser sentinel of each such mode).
        /// </summary>
        public const float DnfThreshold = 10000f;

        /// <summary>
        /// SkimRace losers encode the team's remaining crystals as
        /// <see cref="DnfThreshold"/> + crystalsLeft, so the scoreboard can show the deficit.
        /// </summary>
        public const float SkimRaceLoserBase = DnfThreshold;

        /// <summary>Joust losers get a flat sentinel (no payload to decode).</summary>
        public const float JoustLoserScore = 99999f;

        /// <summary>Encode a SkimRace loser score from the team's remaining crystals.</summary>
        public static float EncodeSkimRaceLoserScore(int crystalsLeft) =>
            SkimRaceLoserBase + Mathf.Max(0, crystalsLeft);

        /// <summary>Decode a SkimRace loser score back into the team's remaining crystals.</summary>
        public static int DecodeSkimRaceCrystalsLeft(float score) =>
            Mathf.Max(0, (int)(score - SkimRaceLoserBase));

        /// <summary>True for a SkimRace loser/DNF score (not a real finish time).</summary>
        public static bool IsSkimRaceLoserScore(float score) => score >= SkimRaceLoserBase;

        /// <summary>True for a Joust loser/DNF score (not a real finish time).</summary>
        public static bool IsJoustLoserScore(float score) => score >= JoustLoserScore;

        /// <summary>
        /// True if the score is a real finish time (a win) rather than a loser/DNF sentinel,
        /// for any time-based golf mode. Equivalent to "not a loser score" for SkimRace and Joust.
        /// </summary>
        public static bool IsFinishTime(float score) => score < DnfThreshold;
    }
}

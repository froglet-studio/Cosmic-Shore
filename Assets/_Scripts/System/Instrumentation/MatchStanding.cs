using System;
using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Where the local player finished, for <c>game_completed</c>'s <c>score</c>, <c>placement</c>,
    /// <c>players_in_match</c> and <c>behind_by</c>, and <c>match_abandoned</c>'s <c>placement_at_quit</c>
    /// (Shombith 2026-11-07, the Balance page). Read from the ranked rows the mode itself produced
    /// (<c>GameDataSO.Results</c>, best first), so it is the same answer the scoreboard shows.
    ///
    /// <para>Every pilot is a row, AI included, so placement is among everyone in the match. In a team
    /// mode it is the player's own row's rank (the scoreboard's), not the team's.</para>
    ///
    /// Pure arithmetic on the rows it is handed; no Unity calls, so the edit-mode tests drive it.
    /// </summary>
    public readonly struct MatchStanding
    {
        /// <summary>The local player's own final score (the mode's score: golf modes, lower is better).</summary>
        public readonly float Score;

        /// <summary>1 = best, among every pilot in the match (AI included).</summary>
        public readonly int Placement;

        /// <summary>Rows in the match: humans and AI.</summary>
        public readonly int PlayersInMatch;

        /// <summary>Gap between the best score in the match and the player's own; 0 when the player's side won.</summary>
        public readonly float BehindBy;

        public MatchStanding(float score, int placement, int playersInMatch, float behindBy)
        {
            Score = score;
            Placement = placement;
            PlayersInMatch = playersInMatch;
            BehindBy = behindBy;
        }

        /// <summary>
        /// Finds <paramref name="localIndex"/>'s row in <paramref name="scoresBestFirst"/> (the mode's own
        /// order, best first). False when the local player is not among the rows: then nothing is sent
        /// rather than a guess. <paramref name="localWon"/> null means the result was not resolved; the gap
        /// is then the plain distance to the top score.
        /// </summary>
        public static bool TryResolve(IReadOnlyList<float> scoresBestFirst, int localIndex, bool? localWon,
            out MatchStanding standing)
        {
            standing = default;
            if (scoresBestFirst == null || localIndex < 0 || localIndex >= scoresBestFirst.Count)
                return false;

            float mine = scoresBestFirst[localIndex];
            // The rows are best first, so the top score is the winner's whatever the mode's direction
            // (golf or not); the distance to it is the gap either way.
            float behind = localWon == true ? 0f : Math.Abs(scoresBestFirst[0] - mine);
            standing = new MatchStanding(mine, localIndex + 1, scoresBestFirst.Count, behind);
            return true;
        }

        /// <summary>
        /// A mid-match rank for <c>placement_at_quit</c> in a higher-is-better mode: 1 + the number of
        /// pilots strictly ahead of <paramref name="mine"/> (ties share a rank). Null while nobody has
        /// scored yet, when an order would be invented from a row of zeros.
        /// </summary>
        public static int? PlacementAmong(IReadOnlyList<float> scores, float mine)
        {
            if (scores == null || scores.Count == 0)
                return null;

            bool anyScored = false;
            int ahead = 0;
            foreach (float s in scores)
            {
                if (s > 0f) anyScored = true;
                if (s > mine) ahead++;
            }

            return anyScored ? ahead + 1 : (int?)null;
        }

        /// <summary>Writes the four <c>game_completed</c> fields.</summary>
        public void AddTo(IDictionary<string, object> parameters)
        {
            parameters[UGSKeys.ParamScore] = Score;
            parameters[UGSKeys.ParamPlacement] = Placement;
            parameters[UGSKeys.ParamPlayersInMatch] = PlayersInMatch;
            parameters[UGSKeys.ParamBehindBy] = BehindBy;
        }

        /// <summary>
        /// <c>ai_difficulty</c>: the level the opponent AI flew, as the AIDifficulty enum names it
        /// (Easy, Medium, Hard, Legendary), <see cref="MixedDifficulty"/> when the opponents flew
        /// different levels (per-seat picks), or null when no AI opposed the player.
        /// </summary>
        public static string AiDifficultyLabel(IEnumerable<AIDifficulty> opponentLevels)
        {
            if (opponentLevels == null)
                return null;

            AIDifficulty? first = null;
            foreach (var level in opponentLevels)
            {
                if (first == null)
                    first = level;
                else if (first.Value != level)
                    return MixedDifficulty;
            }

            return first?.ToString();
        }

        /// <summary>What <c>ai_difficulty</c> says when the opponent AI flew more than one level.</summary>
        public const string MixedDifficulty = "Mixed";
    }

    /// <summary>
    /// Why a running match was left before its end, for <c>match_abandoned</c>'s <c>reason</c>.
    /// </summary>
    public static class MatchAbandonReason
    {
        public const string QuitToMenu = "quit_to_menu";
        public const string AppQuit = "app_quit";
        public const string Disconnect = "disconnect";

        /// <summary>
        /// The reason for a match left by a scene change: a dropped link (the guest's bounce marked the
        /// match dropped, or the network went away during the match) is <see cref="Disconnect"/>; any
        /// other scene change out of the match is <see cref="QuitToMenu"/>.
        /// </summary>
        public static string ForSceneExit(bool linkDropped, bool networkLostDuringMatch) =>
            linkDropped || networkLostDuringMatch ? Disconnect : QuitToMenu;
    }
}

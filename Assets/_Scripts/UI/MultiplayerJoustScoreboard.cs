// MultiplayerJoustScoreboard.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    public class MultiplayerJoustScoreboard : Scoreboard
    {
        [Header("Joust")]
        [SerializeField] private JoustCollisionTurnMonitor joustTurnMonitor;

        /// <summary>
        /// Golf rules — ascending sort puts winning team's elapsed-time score first,
        /// then losers ordered by Score (penalty 99999f; all equal, so tiebreaker decides).
        /// Tiebreaker: JoustCollisions desc — in team games the finisher and their
        /// teammate share the same Score (elapsed time), so the finisher (more jousts)
        /// ranks above the teammate.
        /// </summary>
        protected override List<IRoundStats> SortPlayers(List<IRoundStats> stats)
        {
            if (stats == null) return new List<IRoundStats>();
            var sorted = stats.ToList();
            sorted.Sort((a, b) =>
            {
                int byScore = a.Score.CompareTo(b.Score);
                if (byScore != 0) return byScore;
                return b.JoustCollisions.CompareTo(a.JoustCollisions);
            });
            return sorted;
        }

        /// <summary>
        /// Winning team members (score &lt; 99999) show MM:SS:CS finish time.
        /// Losers (score = 99999) show "{N} Jousts Left".
        /// </summary>
        protected override string FormatPlayerScore(IRoundStats stats)
        {
            float score = stats.Score;

            if (score < 99999f)
            {
                TimeSpan t = TimeSpan.FromSeconds(score);
                return $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
            }

            int needed = joustTurnMonitor ? joustTurnMonitor.CollisionsNeeded : 0;
            int joustsLeft = Mathf.Max(0, needed - stats.JoustCollisions);
            string plural = joustsLeft == 1 ? "" : "s";
            return $"{joustsLeft} Joust{plural} Left";
        }

        protected override string FormatSecondaryStat(IRoundStats stats)
        {
            return $"{stats.JoustCollisions} Jousts";
        }
    }
}

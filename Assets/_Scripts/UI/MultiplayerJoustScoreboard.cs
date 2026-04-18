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
        /// Golf rules — ascending sort so that players who finished (score = raceTime)
        /// appear before those who did not (score = penaltyScoreBase + jousts left).
        /// </summary>
        protected override List<IRoundStats> SortPlayers(List<IRoundStats> stats)
        {
            if (stats == null) return new List<IRoundStats>();
            var sorted = stats.ToList();
            sorted.Sort((a, b) => a.Score.CompareTo(b.Score));
            return sorted;
        }

        protected override string FormatPlayerScore(IRoundStats stats)
        {
            int needed = joustTurnMonitor ? joustTurnMonitor.CollisionsNeeded : 0;
            int joustsLeft = Mathf.Max(0, needed - stats.JoustCollisions);
            bool thisPlayerWon = needed > 0 && joustsLeft == 0;

            if (thisPlayerWon)
            {
                TimeSpan t = TimeSpan.FromSeconds(stats.Score);
                return $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
            }

            string plural = joustsLeft == 1 ? "" : "s";
            return $"{joustsLeft} Joust{plural} Left";
        }

        protected override string FormatSecondaryStat(IRoundStats stats)
        {
            return $"{stats.JoustCollisions} Jousts";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    public class HexRaceScoreboard : Scoreboard
    {
        /// <summary>
        /// Golf rules — ascending sort puts fastest finish time first,
        /// then losers ordered by crystals left (fewer left = higher placement).
        /// </summary>
        protected override List<IRoundStats> SortPlayers(List<IRoundStats> stats)
        {
            if (stats == null) return new List<IRoundStats>();
            var sorted = stats.ToList();
            sorted.Sort((a, b) => a.Score.CompareTo(b.Score));
            return sorted;
        }

        /// <summary>
        /// Winners (score &lt; 10000) show MM:SS:CS finish time.
        /// Losers (score = 10000 + crystalsLeft) show "{N} Crystals Left".
        /// </summary>
        protected override string FormatPlayerScore(IRoundStats stats)
        {
            float score = stats.Score;

            if (score < 10000f)
            {
                TimeSpan t = TimeSpan.FromSeconds(score);
                return $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
            }

            int crystalsLeft = Mathf.Max(0, (int)(score - 10000f));
            return $"{crystalsLeft} Crystals Left";
        }

        /// <summary>
        /// Secondary stat: omni crystals collected.
        /// </summary>
        protected override string FormatSecondaryStat(IRoundStats stats)
        {
            return $"{stats.OmniCrystalsCollected} Crystals";
        }
    }
}

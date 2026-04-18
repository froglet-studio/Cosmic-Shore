using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    public class MultiplayerCrystalCaptureScoreboard : Scoreboard
    {
        /// <summary>
        /// Descending sort — most crystals wins. NOT golf rules.
        /// </summary>
        protected override List<IRoundStats> SortPlayers(List<IRoundStats> stats)
        {
            if (stats == null) return new List<IRoundStats>();
            var sorted = stats.ToList();
            sorted.Sort((a, b) => b.Score.CompareTo(a.Score));
            return sorted;
        }

        protected override string FormatPlayerScore(IRoundStats stats)
        {
            return $"{(int)stats.Score} Crystals";
        }
    }
}

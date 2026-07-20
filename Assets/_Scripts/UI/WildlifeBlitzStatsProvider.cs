using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;
namespace CosmicShore.UI
{
    public class WildlifeBlitzStatsProvider : ScoreboardStatsProvider
    {
        [Header("Dependencies")]
        [SerializeField] WildlifeBlitzScoreKeeper scoreTracker;

        [Header("Icons")]
        [SerializeField] Sprite lifeFormIcon;
        [SerializeField] Sprite crystalIcon;

        public override List<StatData> GetStats()
        {
            var list = new List<StatData>();

            if (!scoreTracker) return list;

            list.Add(new StatData
            {
                Label = "Life Forms",
                Value = scoreTracker.TotalLifeFormsKilled.ToString(),
                Icon = lifeFormIcon
            });

            list.Add(new StatData
            {
                Label = "Crystals",
                Value = scoreTracker.TotalCrystalsCollected.ToString(),
                Icon = crystalIcon
            });

            return list;
        }
    }
}

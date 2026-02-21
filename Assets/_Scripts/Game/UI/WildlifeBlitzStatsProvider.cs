using System.Collections.Generic;
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Arcade.Scoring;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class WildlifeBlitzStatsProvider : ScoreboardStatsProvider
    {
        [Header("Dependencies")]
        [SerializeField] WildlifeBlitzScoreTracker scoreTracker;

        [Header("Icons")]
        [SerializeField] Sprite lifeFormIcon;
        [SerializeField] Sprite crystalIcon;

        public override List<StatData> GetStats()
        {
            var list = new List<StatData>();

            if (!scoreTracker) return list;

            var lifeFormScoring = scoreTracker.GetScoring<LifeFormsKilledScoring>();
            if (lifeFormScoring != null)
            {
                list.Add(new StatData 
                { 
                    Label = "Life Forms", 
                    Value = lifeFormScoring.GetTotalLifeFormsKilled().ToString(),
                    Icon = lifeFormIcon // Passing the reference
                });
            }

            var crystalScoring = scoreTracker.GetScoring<ElementalCrystalsCollectedBlitzScoring>();
            if (crystalScoring != null)
            {
                list.Add(new StatData 
                { 
                    Label = "Crystals", 
                    Value = crystalScoring.GetTotalCrystalsCollected().ToString(),
                    Icon = crystalIcon 
                });
            }

            return list;
        }
    }
}
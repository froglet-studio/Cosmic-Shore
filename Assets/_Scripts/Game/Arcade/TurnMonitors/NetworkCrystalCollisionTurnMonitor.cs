using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        private List<IRoundStats> _subscribedStats = new List<IRoundStats>();

        public override bool CheckForEndOfTurn() =>
            gameData.RoundStatsList.Any(stats => stats.OmniCrystalsCollected >= CrystalCollisions);

        protected override void InitializeStatsListener()
        {
            _subscribedStats.Clear();

            foreach (var stats in gameData.RoundStatsList)
            {
                stats.OnOmniCrystalsCollectedChanged += OnAnyPlayerOmniCrystalsChanged;
                _subscribedStats.Add(stats);
            }
        }

        public override void StopMonitor()
        {
            foreach (var stats in _subscribedStats.Where(stats => stats != null))
            {
                stats.OnOmniCrystalsCollectedChanged -= OnAnyPlayerOmniCrystalsChanged;
            }

            _subscribedStats.Clear();
        }
        
        private void OnAnyPlayerOmniCrystalsChanged(IRoundStats stats)
        {
            UpdateCrystalsRemainingUI();
        }
        
        protected override string GetRemainingCrystalsCountToCollect()
        {
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0)
                return "-";
            
            int maxCollected = gameData.RoundStatsList.Max(s => s.OmniCrystalsCollected);
            int remaining = Mathf.Max(0, CrystalCollisions - maxCollected);
            return remaining.ToString();
        }
    }
}
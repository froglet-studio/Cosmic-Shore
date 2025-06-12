using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class VolumeDestroyedTurnMonitor : TurnMonitor
    {
        private string currentPlayerName;

        private void Awake()
        {
            eliminatesPlayer = true; // This monitor eliminates players when they destroy volume
        }

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            // Check if any volume was destroyed
            if (StatsManager.Instance.PlayerStats.ContainsKey(currentPlayerName) && 
                StatsManager.Instance.PlayerStats[currentPlayerName].VolumeDestroyed > 0)
            {
                return true;
            }

            return false;
        }

        public override void NewTurn(string playerName)
        {
            currentPlayerName = playerName;
            StatsManager.Instance.ResetStats();
        }
    }
}

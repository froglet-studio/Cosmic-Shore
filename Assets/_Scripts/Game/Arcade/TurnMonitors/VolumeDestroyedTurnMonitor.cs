using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class VolumeDestroyedTurnMonitor : TurnMonitor
    {
        private void Awake()
        {
            // eliminatesPlayer = true; // This monitor eliminates players when they destroy volume
        }

        public override bool CheckForEndOfTurn()
        {
            // TODO - Check if any volume was destroyed -> from miniGameData ,
            // but not sure how much of volume need to be destroyed.

            if (!miniGameData.TryGetActivePlayerStats(out IPlayer _, out IRoundStats roundStats))
                return false;

            return roundStats.VolumeDestroyed > 0;
            
            /*if (StatsManager.Instance.PlayerStats.ContainsKey(currentPlayerName) && 
                StatsManager.Instance.PlayerStats[currentPlayerName].VolumeDestroyed > 0)
            {
                return true;
            }

            return false;*/
        }

        /*public override void NewTurn(string playerName)
        {
            currentPlayerName = playerName;
            StatsManager.Instance.ResetStats();
        }*/
        
        protected override void StartTurn()
        {
            // Get PlayerName from MiniGameData
            // currentPlayerName = playerName;
            // StatsManager.Instance.ResetStats();
        }
    }
}

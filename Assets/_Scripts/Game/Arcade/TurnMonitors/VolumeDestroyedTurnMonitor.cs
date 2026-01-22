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

            if (!gameData.TryGetLocalPlayerStats(out IPlayer _, out IRoundStats roundStats))
                return false;

            return roundStats.TotalVolumeDestroyed > 0;
            
            /*if (StatsManager.Instance.PlayerStats.ContainsKey(currentPlayerName) && 
                StatsManager.Instance.PlayerStats[currentPlayerName].TotalVolumeDestroyed > 0)
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
        
        /*public override void StartMonitor()
        {
            // Get PlayerName from MiniGameData
            // currentPlayerName = playerName;
            // StatsManager.Instance.ResetStats();
        }*/
    }
}

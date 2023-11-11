using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class VolumeCreatedTurnMonitor : TurnMonitor
    {
        [SerializeField] float Amount;
        [SerializeField] MiniGame Game;
        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            return Amount >= StatsManager.Instance.playerStats[Game.ActivePlayer.PlayerName].volumeCreated;
        }

        public override void NewTurn(string playerName)
        {
            StatsManager.Instance.ResetStats();
        }
    }
}
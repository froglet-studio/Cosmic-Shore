using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class ResourceAccumulationTurnMonitor : TurnMonitor
    {
        [SerializeField] float percent;
        [SerializeField] MiniGame Game;

        [SerializeField] int resourceIndex = 0;

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            return Game.ActivePlayer.Ship.ResourceSystem.Resources[resourceIndex].CurrentAmount / Game.ActivePlayer.Ship.ResourceSystem.Resources[resourceIndex].MaxAmount >= percent / 100;
        }

        public override void NewTurn(string playerName)
        {
        }
    }
}
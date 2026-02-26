using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class ResourceAccumulationTurnMonitor : TurnMonitor
    {
        [SerializeField] float percent;
        
        // Get PlayerData from MiniGameData
        // [SerializeField] MiniGame Game;

        [SerializeField] int resourceIndex = 0;

        public override bool CheckForEndOfTurn()
        {
            // return Game.LocalPlayer.Vessel.VesselStatus.ResourceSystem.Resources[resourceIndex].CurrentAmount / Game.LocalPlayer.Vessel.VesselStatus.ResourceSystem.Resources[resourceIndex].MaxAmount >= percent / 100;
            return true;    // TEMP
        }
    }
}
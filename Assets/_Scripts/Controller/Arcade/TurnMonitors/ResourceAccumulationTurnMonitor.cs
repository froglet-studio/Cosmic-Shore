using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class ResourceAccumulationTurnMonitor : TurnMonitor
    {
        [SerializeField] float percent;

        public override bool CheckForEndOfTurn()
        {
            // TODO: Re-enable when ResourceSystem is wired up
            // return Game.LocalPlayer.Vessel.VesselStatus.ResourceSystem.Resources[resourceIndex].CurrentAmount / Game.LocalPlayer.Vessel.VesselStatus.ResourceSystem.Resources[resourceIndex].MaxAmount >= percent / 100;
            return true;
        }
    }
}
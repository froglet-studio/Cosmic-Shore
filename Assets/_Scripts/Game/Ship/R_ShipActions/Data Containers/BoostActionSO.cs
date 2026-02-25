using CosmicShore.Core;
using CosmicShore.Game.Ship;
using UnityEngine;
using CosmicShore.Game.Ship.R_ShipActions.Executors;
using CosmicShore.Game.Ship.ShipActions;
namespace CosmicShore.Game.Ship.R_ShipActions.DataContainers
{
    [CreateAssetMenu(fileName = "BoostAction", menuName = "ScriptableObjects/Vessel Actions/Boost")]
    public class BoostActionSO : ShipActionSO
    {
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (vesselStatus == null) return;
            vesselStatus.IsBoosting = true;
            vesselStatus.IsStationary = false;
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (vesselStatus == null) return;
            vesselStatus.IsBoosting = false;
        }
    }
}

using CosmicShore.Gameplay;
using UnityEngine;
namespace CosmicShore.Gameplay
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

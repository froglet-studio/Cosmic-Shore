using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "FalconTrailSO", menuName = "ScriptableObjects/Vessel Actions/Falcon Trail SO")]
    public class FalconTrailSO : ShipActionSO
    {
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
           => execs?.Get<FalconsTrailExecutor>()?.Begin(this);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FalconsTrailExecutor>()?.End();
    }
}

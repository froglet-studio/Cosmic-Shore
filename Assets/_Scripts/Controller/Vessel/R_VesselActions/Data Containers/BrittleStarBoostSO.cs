using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "BrittleStarBoostSO",
        menuName = "ScriptableObjects/Vessel Actions/BrittleStar Boost Action")]
    public class BrittleStarBoostSO : ShipActionSO
    {
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<BrittleStarBoostActionExecutor>()?.Begin(this);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<BrittleStarBoostActionExecutor>()?.End();
    }
}
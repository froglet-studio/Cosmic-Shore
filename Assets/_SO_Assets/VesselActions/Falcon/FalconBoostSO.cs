using CosmicShore.Game;
using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "FalconBoostSO", menuName = "ScriptableObjects/Vessel Actions/Falcon Boost Action")]
    public class FalconBoostSO : ShipActionSO
    {

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
                => execs?.Get<FalconBoostActionExecutor>()?.Begin(this);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
         => execs?.Get<FalconBoostActionExecutor>()?.End();
    }




}

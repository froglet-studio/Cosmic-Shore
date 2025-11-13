using CosmicShore.Game;
using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "FalconBoostAction", menuName = "Scriptable Objects/Vessel Actions/FalconBoost")]
    public class Falcon : ShipActionSO
    {
        public override void Initialize(IVessel ship)
        {
            base.Initialize(ship);
        }

        public override void StartAction(ActionExecutorRegistry execs)
        {
            if (ShipStatus == null) return;
            ShipStatus.Boosting = true;
            ShipStatus.IsStationary = false;
        }

        public override void StopAction(ActionExecutorRegistry execs)
        {
            if (ShipStatus == null) return;
            ShipStatus.Boosting = false;
        }




    }
}

using CosmicShore.Game;
using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "FalconBoostAction", menuName = "ScriptableObjects/Vessel Actions/FalconBoost")]
    public class FalconBoostAction : ShipActionExecutorBase
    {
        void Initialize(IVessel ship)
        {
            //base.Initialize(ship);
        }

     void StartAction(ActionExecutorRegistry execs)
        {
            //if (ShipStatus == null) return;
            //ShipStatus.Boosting = true;
            //ShipStatus.IsStationary = false;
        }

        void StopAction(ActionExecutorRegistry execs)
        {
            //if (ShipStatus == null) return;
            //ShipStatus.Boosting = false;
        }




    }
}

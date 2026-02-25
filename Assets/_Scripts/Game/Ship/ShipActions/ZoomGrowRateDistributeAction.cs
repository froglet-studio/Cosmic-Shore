using UnityEngine;
using CosmicShore.Game.Ship;
using CosmicShore.Utility;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Game.Ship.ShipActions
{
    public class ZoomGrowRateDistributeAction : ShipAction
    {
        [SerializeField] private ElementalFloat sharedRate;

        void Awake()
        {
            CSDebug.Log("Storing values");
            // var grow = GetComponent<GrowActionBase>();
            // if (grow != null)
            //     grow.SetShrinkRate(sharedRate);
            
            // var zoom = GetComponent<ZoomOutAction>();
            // if (zoom != null)
            //     zoom.SetZoomInRate(sharedRate);
        }

        public override void StartAction()
        {
            //
        }

        public override void StopAction()
        {
            //
        }
    }
}

using UnityEngine;

namespace CosmicShore
{
    public class ZoomGrowRateDistributeAction : ShipAction
    {
        [SerializeField] private ElementalFloat sharedRate;

        void Awake()
        {
            Debug.Log("Storing values");
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
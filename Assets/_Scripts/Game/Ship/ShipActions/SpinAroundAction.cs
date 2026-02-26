using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Ship
{
    public class SpinAroundAction : ShipAction
    {
    
        public override void StartAction()
        {
            Vessel.VesselStatus.VesselTransformer.FlatSpinShip(180);
        }

        public override void StopAction()
        {

        }

    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpinAroundAction : ShipAction
{
    
    public override void StartAction()
    {
        Ship.ShipStatus.ShipTransformer.FlatSpinShip(180);
    }

    public override void StopAction()
    {

    }

}

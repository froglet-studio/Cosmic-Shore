using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpinAroundAction : ShipActionAbstractBase
{
    
    public override void StartAction()
    {
        ship.ShipController.FlatSpinShip(180);
    }

    public override void StopAction()
    {

    }

}

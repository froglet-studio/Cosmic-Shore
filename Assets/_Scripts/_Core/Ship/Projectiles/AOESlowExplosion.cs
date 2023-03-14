using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AOESlowExplosion : AOEExplosion
{

    protected override void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<ShipGeometry>(out var shipGeometry))
        {
            if (shipGeometry.Ship.Team == Team)
            {
                return;
            }
            shipGeometry.Ship.ModifySpeed(.1f,10);
        }
    }

}

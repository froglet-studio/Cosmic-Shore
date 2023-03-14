using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AOESlowExplosion : AOEExplosion
{

    protected override void OnTriggerEnter(Collider other)
    {
        Debug.Log("AOE Slow Explosion Collision");
        if (other.TryGetComponent<ShipGeometry>(out var shipGeometry))
        {
            if (shipGeometry.Ship.Team == Team)
            {
                Debug.Log("tried to slow yourself");
                return;
            }

            Debug.Log("tried to slow foe");
            shipGeometry.Ship.ModifySpeed(.1f,10);
        }
    }

}

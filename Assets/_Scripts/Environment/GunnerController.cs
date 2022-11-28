using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunnerController : MonoBehaviour
{

    TrailSpawner trailSpawner;
    float blocksFromShip;
    float gunnerSpeed;
    bool direction;

    // Start is called before the first frame update
    void Start()
    {
        trailSpawner = transform.parent.GetComponent<TrailSpawner>();
        gunnerSpeed = .1f;
        blocksFromShip = 1;
        direction = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (direction)
        {
            blocksFromShip += gunnerSpeed;
        }
        if (blocksFromShip > trailSpawner.trailList.Count - 3)
        {
            direction = false;
        }
        if (!direction)
        {
            blocksFromShip -= gunnerSpeed;
        }
        if (blocksFromShip < 3)
        {
            direction = true;
        }

        transform.position = trailSpawner.trailList[trailSpawner.trailList.Count - (int)blocksFromShip].transform.position;
    }
}

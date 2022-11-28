using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunnerController : MonoBehaviour
{

    TrailSpawner trailSpawner;
    float blockIndex;
    float gunnerSpeed;
    bool direction;
    int gap = 3;

    // Start is called before the first frame update
    void Start()
    {
        trailSpawner = transform.parent.GetComponent<TrailSpawner>();
        gunnerSpeed = .1f;
        blockIndex = 0;
        direction = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (direction)
        {
            blockIndex += gunnerSpeed;
        }
        if (blockIndex > trailSpawner.trailList.Count - gap - gunnerSpeed)
        {
            direction = false;
        }
        if (!direction)
        {
            blockIndex -= gunnerSpeed;
        }
        if (blockIndex < gap)
        {
            direction = true;
        }
        Debug.Log($"block index: {blockIndex}");
        transform.position = trailSpawner.trailList[(int)blockIndex].transform.position;
        if (trailSpawner.trailList[(int)blockIndex].destroyed) trailSpawner.trailList[(int)blockIndex].restore();
    }
}

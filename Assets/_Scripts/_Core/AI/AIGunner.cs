using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIGunner : MonoBehaviour
{

    TrailSpawner trailSpawner;
    int nextBlockIndex;
    int previousblockIndex;
    float gunnerSpeed;
    float lerpAmount;
    bool direction;
    int gap = 3;


    // Start is called before the first frame update
    void Start()
    {
        trailSpawner = transform.parent.GetComponent<TrailSpawner>();
        gunnerSpeed = 5f;
        nextBlockIndex = 1;
        direction = true;
    }

    // Update is called once per frame
    void Update()
    {
        lerpAmount += gunnerSpeed * Time.deltaTime;
        if (direction)
        {
            if (lerpAmount > 1)
            {
                previousblockIndex = nextBlockIndex;
                nextBlockIndex++;
                lerpAmount -= 1f;
            }
        }
        if (nextBlockIndex > trailSpawner.trailList.Count - gap)
        {
            direction = false;
        }
        if (!direction)
        {
            if (lerpAmount > 1)
            {
                previousblockIndex = nextBlockIndex;
                nextBlockIndex--;
                lerpAmount -= 1f;
            }
        }
        if (nextBlockIndex < gap)
        {
            direction = true;
        }
        Debug.Log($"block index: {nextBlockIndex}");
        
        transform.position = Vector3.Lerp(trailSpawner.trailList[previousblockIndex].transform.position,
                                          trailSpawner.trailList[nextBlockIndex].transform.position,
                                          lerpAmount);
        if (trailSpawner.trailList[(int)nextBlockIndex].destroyed) trailSpawner.trailList[(int)nextBlockIndex].restore();
    }
}

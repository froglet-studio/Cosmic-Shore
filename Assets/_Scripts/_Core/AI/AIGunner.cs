using Firebase;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIGunner : MonoBehaviour
{

    //public delegate void Fire();
    //public static event Fire OnFire;

    TrailSpawner trailSpawner;
    int nextBlockIndex = 1;
    int previousBlockIndex;
    float gunnerSpeed = 5;
    float lerpAmount;
    bool direction = true;
    int gap = 3;
    float rotationSpeed = 40;
    Gun gun;

    [SerializeField] GameObject gun;
    [SerializeField] GameObject gunMount;


    // Start is called before the first frame update
    void Start()
    {
        trailSpawner = transform.parent.GetComponent<TrailSpawner>();
        fire = gun.transform.GetComponent<Gun>();
    }

    // Update is called once per frame
    void Update()
    {
        if (trailSpawner.trailList[(int)nextBlockIndex].destroyed) lerpAmount += gunnerSpeed/4f * Time.deltaTime;
        else lerpAmount += gunnerSpeed * Time.deltaTime;
        if (direction && lerpAmount > 1)
        {
            previousBlockIndex = nextBlockIndex;
            nextBlockIndex++;
            lerpAmount -= 1f;
        }
        if (nextBlockIndex > trailSpawner.trailList.Count - gap)
        {
            direction = false;
        }
        if (!direction && lerpAmount > 1)
        {
            previousBlockIndex = nextBlockIndex;
            nextBlockIndex--;
            lerpAmount -= 1f;
        }
        if (nextBlockIndex < gap)
        {
            direction = true;
        }
        Debug.Log($"block index: {nextBlockIndex}");
        
        transform.position = Vector3.Lerp(trailSpawner.trailList[previousBlockIndex].transform.position,
                                          trailSpawner.trailList[nextBlockIndex].transform.position,
                                          lerpAmount);

        transform.rotation = Quaternion.Lerp(trailSpawner.trailList[previousBlockIndex].transform.rotation,
                                             trailSpawner.trailList[nextBlockIndex].transform.rotation,
                                             lerpAmount);
        transform.Rotate(90, 0, 0);

        if (trailSpawner.trailList[(int)previousBlockIndex].destroyed) trailSpawner.trailList[(int)nextBlockIndex].restore();

        //gun.transform.localRotation = Quaternion.Lerp(gun.transform.localRotation, Quaternion.Euler(new Vector3(0, 0, 0)), .05f);
        gunMount.transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        Gun.FireGun();
        //OnFire?.Invoke();
    }
}

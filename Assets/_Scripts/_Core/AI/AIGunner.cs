using UnityEngine;

public class AIGunner : MonoBehaviour
{   
    int nextBlockIndex = 1;
    int previousBlockIndex;
    float gunnerSpeed = 5;
    float lerpAmount;
    bool direction = true;
    int gap = 3;
    float rotationSpeed = 40;
    TrailSpawner trailSpawner;
    public Teams Team;

    [SerializeField] Gun gun;
    [SerializeField] GameObject gunMount;
    [SerializeField] Player player;

    private void Start()
    {
        trailSpawner = player.Ship.TrailSpawner;
        Team = player.Team;
        gun.Team = Team;
    }

    void Update()
    {
        // Give the ships a small head start so some blocks exist
        if (trailSpawner.trailList.Count < gap+1)
            return;

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
        gun.FireGun(player.transform);
    }
}
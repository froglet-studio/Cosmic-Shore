using CosmicShore.Core;
//using System.Collections;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Numerics;
using UnityEngine;
//using static UnityEngine.ParticleSystem;

public class SpawnableDriftCourse : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    static int ObjectsSpawned = 0;
    [SerializeField] Vector3 blockScale = new Vector3(1,3,5);
    [SerializeField] float spawnDistance = 5f;
    [SerializeField] Vector3 Orgin;
    [SerializeField] int blocksPerSegment = 10;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "DriftCourse" + ObjectsSpawned++;

        var trail = new Trail();

        int blockCount = 2000;

        var position = new Vector3(Orgin.x, Orgin.y, Orgin.z);
        Quaternion rotation = Quaternion.identity;

        for (int block = 0; block < blockCount; block++)
        {          
            if (block % blocksPerSegment == 0)
            {
                ChangeDirection(position, out rotation);
            }

            CreateBlock(position, rotation * Vector3.forward, container.name + "::BLOCK::" + block, trail, blockScale, trailBlock, container);

            var dir = rotation * Vector3.forward;
            position += spawnDistance * dir;
            
        }

        trails.Add(trail);
        return container;
    }

    private Vector3 ChangeDirection(Vector3 direction, out Quaternion rotation)
    {
        float altitude = Random.Range(70, 90);
        float azimuth = Random.Range(0,360);

        rotation = Quaternion.Euler(0f, 0f, azimuth) * Quaternion.Euler(0f, altitude, 0f);
        Vector3 newDirection = rotation * direction;
        return newDirection;
    }

}

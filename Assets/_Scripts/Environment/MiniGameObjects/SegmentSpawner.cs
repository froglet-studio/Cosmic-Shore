using System.Collections.Generic;
using System.Security.Cryptography;
using StarWriter.Core;
using UnityEngine;

public class SegmentSpawner : MonoBehaviour
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] int numberOfSegments = 1;
    [SerializeField] List<SpawnableAbstractBase> spawnableSegments;
    [SerializeField] List<float> spawnSegmentWeights;
    List<Trail> trails = new List<Trail>();


    [SerializeField] float aa;
    [SerializeField] float bb;

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        // Clear out last run
        foreach (Trail trail in trails) { 
            foreach (var block in trail.TrailList)
            {
                Destroy(block);
            }    
        }
        trails.Clear();

        float totalWeight = 0;
        foreach (var weight in spawnSegmentWeights)
        {
            totalWeight+= weight;
        }
        for (int i=0; i<spawnSegmentWeights.Count; i++)
        {
            spawnSegmentWeights[i] = spawnSegmentWeights[i] * (1 / totalWeight);
        }

        var spawned = SpawnRandom();
        spawned.transform.position = new Vector3(90, 90, 0);
        
        spawned = SpawnRandom();
        spawned.transform.position = new Vector3(-90, 90, 0);

        spawned = SpawnRandom();
        spawned.transform.position = new Vector3(-90, -90, 0);

        spawned = SpawnRandom();
        spawned.transform.position = new Vector3(90, -90, 0);

        /*
        for (int i = 0; i < numberOfSegments; i++)
        {
            spawned = spawnableSegments[0].Spawn();
            trails.AddRange(spawnableSegments[0].GetTrails());
        }

        var heart = spawnableSegments[1].Spawn(); ;
        trails.AddRange(spawnableSegments[1].GetTrails());
        heart.transform.position = new Vector3(90, 90, 0);
        */
    }

    GameObject SpawnRandom()
    {
        var random = new System.Random();

        var spawnWeight = random.NextDouble();
        var spawnIndex = 0;
        var totalWeight = 0f;
        for (int i = 0; i < spawnSegmentWeights.Count && totalWeight < spawnWeight; i++)
        {
            spawnIndex = i;
            totalWeight += spawnSegmentWeights[i];
        }

        return spawnableSegments[spawnIndex].Spawn();
    }

    void CreateSpiralSegment()
    {
        GameObject container = new GameObject();
        container.name = "spiral";

        //x(t) = aa · exp(bb · t) · cos(t) y(t) = aa · exp(bb · t) · sin(t).
        var trail = new Trail();

        float blockCount = Mathf.PI * 8;
        for (float block = .1f; block < blockCount; block += .2f) //(1.2f-block/blockCount)
        {
            var t = ((float)block / (float)blockCount);
            // x = 16sin^3(t)
            var x = aa * Mathf.Exp(bb*t) * Mathf.Cos(t);
            var y = aa * Mathf.Exp(bb*t) * Mathf.Sin(t);
            var z = (block / blockCount) * 12;
            var position = new Vector3(x, y, z);
            CreateBlock(position, Vector3.zero, "SEGMENT::" + block, trail, Vector3.one, trailBlock, container);
        }

        trails.Add(trail);
    }

    void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, TrailBlock trailBlock, GameObject container)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = Teams.None;
        Block.ownerId = "public";
        Block.PlayerName = "";
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        Block.transform.SetParent(container.transform, false);
        Block.ID = blockId;
        Block.InnerDimensions = scale;
        Block.Trail = trail;
        trail.Add(Block);
    }
}
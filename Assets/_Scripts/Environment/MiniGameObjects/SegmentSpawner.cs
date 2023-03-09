using System.Collections.Generic;
using StarWriter.Core;
using UnityEngine;

public enum PositioningScheme
{
    SphereUniform = 0,
    SphereSurface = 1,
    StraightLine = 2,
    KinkyLine = 3,
    CurvyLine = 4,
    ToroidSurface = 5,
    Cubic = 6,
}

public class SegmentSpawner : MonoBehaviour
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] int numberOfSegments = 1;
    [SerializeField] List<SpawnableAbstractBase> spawnableSegments;
    [SerializeField] PositioningScheme positioningScheme = PositioningScheme.SphereUniform;
    [SerializeField] List<float> spawnSegmentWeights;
    [SerializeField] int Seed;
    List<Trail> trails = new();
    System.Random random = new();
    int spawnedItemCount;
    float sphereRadius = 200f;

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (Seed != 0) random = new System.Random(Seed);

        // Clear out last run
        foreach (Trail trail in trails)
            foreach (var block in trail.TrailList)
                Destroy(block);

        trails.Clear();

        normalizeWeights();

        for (int i=0; i < numberOfSegments; i++)
        {
            var spawned = SpawnRandom();
            PositionSpawnedObject(spawned, positioningScheme);
            spawnedItemCount++;
        }
    }

    void PositionSpawnedObject(GameObject spawned, PositioningScheme positioningScheme)
    {
        switch (positioningScheme)
        {
            case PositioningScheme.SphereUniform:
                spawned.transform.SetPositionAndRotation(Random.insideUnitSphere * sphereRadius, Random.rotation);
                return;
            case PositioningScheme.SphereSurface:
                
                int difficultyAngle = 90;
                spawned.transform.position = Quaternion.Euler(0, 0, random.Next(spawnedItemCount * (360/ numberOfSegments), spawnedItemCount * (360 / numberOfSegments) + 20)) *
                    (Quaternion.Euler(0, random.Next(Mathf.Max(difficultyAngle - 20, 40), Mathf.Max(difficultyAngle - 20, 40)), 0) *
                    (sphereRadius * Vector3.forward));
                spawned.transform.LookAt(Vector3.zero);
                return;
            case PositioningScheme.Cubic:
                // Volumetric Grid, looking at origin
                var volumeSideLength = 100;
                var voxelSideLength = 10;
                var x = random.Next(0, volumeSideLength/voxelSideLength) * voxelSideLength;
                var y = random.Next(0, volumeSideLength/voxelSideLength) * voxelSideLength;
                var z = random.Next(0, volumeSideLength/voxelSideLength) * voxelSideLength;
                spawned.transform.position = new Vector3(x, y, z);
                spawned.transform.LookAt(Vector3.zero, Vector3.up);
                break;
        }
    }

    GameObject SpawnRandom()
    {
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

    void normalizeWeights()
    {
        float totalWeight = 0;
        foreach (var weight in spawnSegmentWeights)
            totalWeight += weight;

        for (int i = 0; i < spawnSegmentWeights.Count; i++)
            spawnSegmentWeights[i] = spawnSegmentWeights[i] * (1 / totalWeight);
    }
}
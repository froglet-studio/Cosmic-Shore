using System.Collections.Generic;
using StarWriter.Core;
using UnityEngine;

// TODO: P1 move to enums folder
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
    [SerializeField] public int numberOfSegments = 1;
    [SerializeField] List<SpawnableAbstractBase> spawnableSegments;
    [SerializeField] PositioningScheme positioningScheme = PositioningScheme.SphereUniform;
    [SerializeField] List<float> spawnSegmentWeights;
    [SerializeField] public int Seed;
    [SerializeField] bool InitializeOnStart;
    [SerializeField] Vector3 origin = Vector3.zero;
    GameObject SpawnedSegmentContainer;
    List<Trail> trails = new();
    System.Random random = new();
    int spawnedItemCount;
    float sphereRadius = 250f;
    public float StraightLineLength = 400f;
    public int DifficultyAngle = 90;

    void Start()
    {
        SpawnedSegmentContainer = new GameObject();
        SpawnedSegmentContainer.name = "SpawnedSegments";

        if (InitializeOnStart)
            Initialize();
    }

    public void Initialize(float difficultyLevel = 1)
    {
        if (Seed != 0) random = new System.Random(Seed);

        // Clear out last run
        foreach (Trail trail in trails)
            foreach (var block in trail.TrailList)
                Destroy(block);

        NukeTheTrails();

        normalizeWeights();

        for (int i=0; i < numberOfSegments; i++)
        {
            var spawned = SpawnRandom(difficultyLevel);
            PositionSpawnedObject(spawned, positioningScheme);
            spawnedItemCount++;
        }
    }

    public void NukeTheTrails()
    {
        trails.Clear();

        if (SpawnedSegmentContainer == null) return;

        foreach (Transform child in SpawnedSegmentContainer.transform)
            Destroy(child.gameObject);
    }

    void PositionSpawnedObject(GameObject spawned, PositioningScheme positioningScheme)
    {
        switch (positioningScheme)
        {
            case PositioningScheme.SphereUniform:
                spawned.transform.SetPositionAndRotation(Random.insideUnitSphere * sphereRadius, Random.rotation);
                return;
            case PositioningScheme.SphereSurface:
                

                spawned.transform.position = Quaternion.Euler(0, 0, random.Next(spawnedItemCount * (360/ numberOfSegments), spawnedItemCount * (360 / numberOfSegments) + 20)) *
                    (Quaternion.Euler(0, random.Next(Mathf.Max(DifficultyAngle - 20, 40), Mathf.Max(DifficultyAngle + 20, 40)), 0) *
                    (sphereRadius * Vector3.forward));
                spawned.transform.LookAt(Vector3.zero);
                return;
            case PositioningScheme.ToroidSurface:
                // TODO: this is not a torus, it's ripped from the sphere
                int toroidDifficultyAngle = 90;
                spawned.transform.position = Quaternion.Euler(0, 0, random.Next(spawnedItemCount * (360 / numberOfSegments), spawnedItemCount * (360 / numberOfSegments) + 20)) *
                    (Quaternion.Euler(0, random.Next(Mathf.Max(toroidDifficultyAngle - 20, 40), Mathf.Max(toroidDifficultyAngle - 20, 40)), 0) *
                    (sphereRadius * Vector3.forward));
                spawned.transform.LookAt(Vector3.zero);
                return;
            case PositioningScheme.StraightLine:
                spawned.transform.position = new Vector3(0, 0, spawnedItemCount*StraightLineLength) + origin;
                spawned.transform.Rotate(Vector3.forward, (float)random.NextDouble() * 180);
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

    GameObject SpawnRandom(float difficultyLevel = 1)
    {
        var spawnWeight = random.NextDouble();
        var spawnIndex = 0;
        var totalWeight = 0f;
        for (int i = 0; i < spawnSegmentWeights.Count && totalWeight < spawnWeight; i++)
        {
            spawnIndex = i;
            totalWeight += spawnSegmentWeights[i];
        }

        return spawnableSegments[spawnIndex].Spawn(difficultyLevel);
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
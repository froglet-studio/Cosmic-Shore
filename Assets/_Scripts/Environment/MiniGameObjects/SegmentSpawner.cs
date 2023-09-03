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
    SphereEmanating = 7
}

public class SegmentSpawner : MonoBehaviour
{
    
    [SerializeField] List<SpawnableAbstractBase> spawnableSegments;
    [SerializeField] PositioningScheme positioningScheme = PositioningScheme.SphereUniform;
    [SerializeField] List<float> spawnSegmentWeights;
    [SerializeField] public int Seed;
    [SerializeField] Transform parent;
    
    [SerializeField] Vector3 origin = Vector3.zero;
    GameObject SpawnedSegmentContainer;
    List<Trail> trails = new();
    System.Random random = new();
    int spawnedItemCount;
    float sphereRadius = 250f;
    public float StraightLineLength = 400f;
    [HideInInspector] public int DifficultyAngle = 90;

    [SerializeField] bool InitializeOnStart;
    [SerializeField] public int numberOfSegments = 1;

    void Start()
    {
        SpawnedSegmentContainer = new GameObject();
        SpawnedSegmentContainer.name = "SpawnedSegments";

        if (InitializeOnStart)
            Initialize();
        if (parent != null) SpawnedSegmentContainer.transform.parent = parent;
    }

    public void Initialize()
    {
        if (Seed != 0)
        {
            random = new System.Random(Seed);
            Random.InitState(Seed);
        }

        // Clear out last run
        foreach (Trail trail in trails)
            foreach (var block in trail.TrailList)
                Destroy(block);

        NukeTheTrails();
        

        normalizeWeights();

        for (int i=0; i < numberOfSegments; i++)
        {
            var spawned = SpawnRandom();
            PositionSpawnedObject(spawned, positioningScheme);
            spawned.transform.parent = SpawnedSegmentContainer.transform;
            spawnedItemCount++;
        }
    }

    public void NukeTheTrails()
    {
        trails.Clear();
        spawnedItemCount = 0;
        if (SpawnedSegmentContainer == null) return;

        foreach (Transform child in SpawnedSegmentContainer.transform)
            Destroy(child.gameObject);
    }

    void PositionSpawnedObject(GameObject spawned, PositioningScheme positioningScheme)
    {
        switch (positioningScheme)
        {
            case PositioningScheme.SphereUniform:
                spawned.transform.SetPositionAndRotation(Random.insideUnitSphere * sphereRadius + origin + transform.position, Random.rotation);
                return;
            case PositioningScheme.SphereSurface:
                spawned.transform.position = Quaternion.Euler(0, 0, random.Next(spawnedItemCount * (360/ numberOfSegments), spawnedItemCount * (360 / numberOfSegments) + 20)) *
                    (Quaternion.Euler(0, random.Next(Mathf.Max(DifficultyAngle - 20, 40), Mathf.Max(DifficultyAngle + 20, 40)), 0) *
                    (sphereRadius * Vector3.forward)) + origin + transform.position;
                spawned.transform.LookAt(Vector3.zero);
                return;
            case PositioningScheme.ToroidSurface:
                // TODO: this is not a torus, it's ripped from the sphere
                int toroidDifficultyAngle = 90;
                spawned.transform.position = Quaternion.Euler(0, 0, random.Next(spawnedItemCount * (360 / numberOfSegments), spawnedItemCount * (360 / numberOfSegments) + 20)) *
                    (Quaternion.Euler(0, random.Next(Mathf.Max(toroidDifficultyAngle - 20, 40), Mathf.Max(toroidDifficultyAngle - 20, 40)), 0) *
                    (sphereRadius * Vector3.forward)) + origin + transform.position;
                spawned.transform.LookAt(Vector3.zero);
                return;
            case PositioningScheme.StraightLine:
                spawned.transform.position = new Vector3(0, 0, spawnedItemCount*StraightLineLength) + origin + transform.position;
                spawned.transform.Rotate(Vector3.forward, (float)random.NextDouble() * 180);
                return;
            case PositioningScheme.Cubic:
                // Volumetric Grid, looking at origin
                var volumeSideLength = 100;
                var voxelSideLength = 10;
                var x = random.Next(0, volumeSideLength/voxelSideLength) * voxelSideLength;
                var y = random.Next(0, volumeSideLength/voxelSideLength) * voxelSideLength;
                var z = random.Next(0, volumeSideLength/voxelSideLength) * voxelSideLength;
                spawned.transform.position = new Vector3(x, y, z) + origin + transform.position;
                spawned.transform.LookAt(Vector3.zero, Vector3.up);
                break;
            case PositioningScheme.SphereEmanating:
                spawned.transform.SetPositionAndRotation(origin + transform.position, Random.rotation);
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
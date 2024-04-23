using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

// TODO: P1 move to enums folder
public enum PositioningScheme
{
    SphereUniform = 0,
    SphereSurface = 1,
    StraightLineRandomOrientation = 2,
    KinkyLine = 3,
    CurvyLine = 4,
    ToroidSurface = 5,
    Cubic = 6,
    SphereEmanating = 7,
    StraightLineConstantRotation = 8,
    CylinderSurfaceWithAngle = 9,
    KinkyLineBranching = 10
}

public class SegmentSpawner : MonoBehaviour
{
    
    [SerializeField] List<SpawnableAbstractBase> spawnableSegments;
    [SerializeField] PositioningScheme positioningScheme = PositioningScheme.SphereUniform;
    [SerializeField] List<float> spawnSegmentWeights;
    [SerializeField] public int Seed;
    [SerializeField] Transform parent;
    
    [SerializeField] public Vector3 origin = Vector3.zero;
    GameObject SpawnedSegmentContainer;
    List<Trail> trails = new();
    System.Random random = new();
    int spawnedItemCount;
    public float Radius = 250f;
    public float StraightLineLength = 400f;
    public float RotationAmount = 10f;
    [HideInInspector] public int DifficultyAngle = 90;

    [SerializeField] bool InitializeOnStart;
    [SerializeField] public int numberOfSegments = 1;

    Vector3 currentDisplacement;
    Quaternion currentRotation;

    [Header("Branching Settings")]
    [SerializeField] float branchProbability = 0.2f;
    [SerializeField] int minBranchAngle = 20;
    [SerializeField] int maxBranchAngle = 20;
    [SerializeField] int minBranches = 1;
    [SerializeField] int maxBranches = 3;
    [SerializeField] float minBranchLengthMultiplier = 0.6f;
    [SerializeField] float maxBranchLengthMultiplier = 0.8f;
    [SerializeField] int maxDepth = 3;
    [SerializeField] int maxTotalSpawnedObjects = 100;
    [SerializeField] List<GameObject> branchPrefabs;

    void Start()
    {
        currentDisplacement = origin + transform.position;
        currentRotation = Quaternion.identity;
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
                spawned.transform.SetPositionAndRotation(Random.insideUnitSphere * Radius + origin + transform.position, Random.rotation);
                return;
            case PositioningScheme.SphereSurface:
                spawned.transform.position = Quaternion.Euler(0, 0, random.Next(spawnedItemCount * (360/ numberOfSegments), spawnedItemCount * (360 / numberOfSegments) + 20)) *
                    (Quaternion.Euler(0, random.Next(Mathf.Max(DifficultyAngle - 20, 40), Mathf.Max(DifficultyAngle + 20, 40)), 0) *
                    (Radius * Vector3.forward)) + origin + transform.position;
                spawned.transform.LookAt(Vector3.zero);
                return;
            case PositioningScheme.KinkyLine:                
                Quaternion rotation;
                spawned.transform.position = currentDisplacement += RandomVectorRotation(StraightLineLength * Vector3.forward, out rotation) ;
                spawned.transform.rotation = currentRotation = rotation;
                return;
            case PositioningScheme.ToroidSurface:
                // TODO: this is not a torus, it's ripped from the sphere
                int toroidDifficultyAngle = 90;
                spawned.transform.position = Quaternion.Euler(0, 0, random.Next(spawnedItemCount * (360 / numberOfSegments), spawnedItemCount * (360 / numberOfSegments) + 20)) *
                    (Quaternion.Euler(0, random.Next(Mathf.Max(toroidDifficultyAngle - 20, 40), Mathf.Max(toroidDifficultyAngle - 20, 40)), 0) *
                    (Radius * Vector3.forward)) + origin + transform.position;
                spawned.transform.LookAt(Vector3.zero);
                return;
            case PositioningScheme.StraightLineRandomOrientation:
                spawned.transform.position = new Vector3(0, 0, spawnedItemCount * StraightLineLength) + origin + transform.position;
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
            case PositioningScheme.StraightLineConstantRotation:
                spawned.transform.position = new Vector3(0, 0, spawnedItemCount * StraightLineLength) + origin + transform.position;
                spawned.transform.Rotate(Vector3.forward, spawnedItemCount * RotationAmount);
                return;
            case PositioningScheme.CylinderSurfaceWithAngle:
                spawned.transform.position = new Vector3(Radius * Mathf.Sin(spawnedItemCount),
                                                         Radius * Mathf.Cos(spawnedItemCount),
                                                         spawnedItemCount * StraightLineLength) + origin + transform.position;
                spawned.transform.Rotate(Vector3.forward + (((float)random.NextDouble() - .4f) * Vector3.right)
                                                         + (((float)random.NextDouble() - .4f) * Vector3.up), (float)random.NextDouble() * 180);
                return;
            case PositioningScheme.KinkyLineBranching:

                // Check if the maximum total spawned objects limit is reached
                if (spawnedItemCount >= maxTotalSpawnedObjects)
                    return;

                // Check if the current kink should branch
                if (random.NextDouble() < branchProbability && maxDepth > 0)
                {
                    // Determine the number of branches for the current kink
                    int numBranches = random.Next(minBranches, maxBranches + 1);

                    // Spawn branches
                    for (int i = 0; i < numBranches; i++)
                    {
                        // Calculate the branch angle
                        float branchAngle = random.Next(minBranchAngle, maxBranchAngle);
                        float branchAngleRad = branchAngle * Mathf.Deg2Rad;

                        // Calculate the direction vector for the branch
                        Vector3 branchDirection = Quaternion.Euler(0f, branchAngleRad * Mathf.Rad2Deg, 0f) * currentRotation * Vector3.forward;

                        // Calculate the branch length
                        float branchLengthMultiplier = Random.Range(minBranchLengthMultiplier, maxBranchLengthMultiplier);
                        float branchLength = StraightLineLength * branchLengthMultiplier;

                        // Spawn the branch object
                        GameObject branch = SpawnRandomBranch();
                        branch.transform.position = currentDisplacement + branchDirection * branchLength;
                        branch.transform.rotation = Quaternion.LookRotation(branchDirection);

                        // Recursively spawn branches for the current branch
                        SpawnBranches(branch, maxDepth - 1, branchDirection, branchLength);
                    }
                }

                // Update the main line
                //Quaternion rotation;
                spawned.transform.position = currentDisplacement += RandomVectorRotation(StraightLineLength * Vector3.forward, out rotation);
                spawned.transform.rotation = currentRotation = rotation;
                return;

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

    private Vector3 RandomVectorRotation(Vector3 vector, out Quaternion rotation)
    {
        float altitude = Random.Range(70, 90);
        float azimuth = Random.Range(0, 360);

        rotation = Quaternion.Euler(0f, 0f, azimuth) * Quaternion.Euler(0f, altitude, 0f);
        Vector3 newVector = rotation * vector;
        return newVector;
    }

    private void SpawnBranches(GameObject parent, int depth, Vector3 direction, float length)
    {
        if (depth <= 0 || spawnedItemCount >= maxTotalSpawnedObjects)
            return;

        // Check if the current branch should spawn more branches
        if (random.NextDouble() < branchProbability)
        {
            // Determine the number of branches for the current branch
            int numBranches = random.Next(minBranches, maxBranches + 1);

            // Spawn branches
            for (int i = 0; i < numBranches; i++)
            {
                // Calculate the branch angle
                float branchAngle = random.Next(minBranchAngle, maxBranchAngle);
                float branchAngleRad = branchAngle * Mathf.Deg2Rad;

                // Calculate the direction vector for the branch
                Vector3 branchDirection = Quaternion.Euler(0f, branchAngleRad * Mathf.Rad2Deg, 0f) * direction;

                // Calculate the branch length
                float branchLengthMultiplier = Random.Range(minBranchLengthMultiplier, maxBranchLengthMultiplier);
                float branchLength = length * branchLengthMultiplier;

                // Spawn the branch object
                GameObject branch = SpawnRandomBranch();
                branch.transform.position = parent.transform.position + branchDirection * branchLength;
                branch.transform.rotation = Quaternion.LookRotation(branchDirection);

                // Recursively spawn branches for the current branch
                SpawnBranches(branch, depth - 1, branchDirection, branchLength);
            }
        }
    }

    private GameObject SpawnRandomBranch()
    {
        // Randomly select a branch prefab from the pool
        int randomIndex = random.Next(0, branchPrefabs.Count);
        GameObject branchPrefab = branchPrefabs[randomIndex];

        // Spawn the branch object
        GameObject branch = Instantiate(branchPrefab);
        branch.transform.parent = SpawnedSegmentContainer.transform;
        spawnedItemCount++;

        return branch;
    }
}
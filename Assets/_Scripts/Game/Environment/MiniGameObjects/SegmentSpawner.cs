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
    KinkyLineBranching = 10,
    MazeGrid = 11,
    CurvyTubeNetwork = 12,
    KinkyTubeNetwork = 13,
    SpiralTower = 14,
    HoneycombGrid = 15,
    HilbertCurveLSystem = 16
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
    [SerializeField] public int NumberOfSegments = 1;

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

    [Header("Maze Grid Settings")]
    [SerializeField] public int GridWidth = 10;
    [SerializeField] public int GridHeight = 10;
    [SerializeField] public int GridThickness = 10;
    [SerializeField] public float CellSize = 10f;

    [Header("Tube Network Settings")]
    [SerializeField] public float Curviness = 0.5f;
    [SerializeField] public float BranchProbability = 0.2f;

    [Header("Spiral Tower Settings")]
    [SerializeField] public float TowerHeight = 100f;
    [SerializeField] public float TowerRadius = 20f;
    [SerializeField] public float RotationsPerUnit = 0.1f;

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

        for (int i=0; i < NumberOfSegments; i++)
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
                spawned.transform.position = Quaternion.Euler(0, 0, random.Next(spawnedItemCount * (360/ NumberOfSegments), spawnedItemCount * (360 / NumberOfSegments) + 20)) *
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
                spawned.transform.position = Quaternion.Euler(0, 0, random.Next(spawnedItemCount * (360 / NumberOfSegments), spawnedItemCount * (360 / NumberOfSegments) + 20)) *
                    (Quaternion.Euler(0, random.Next(Mathf.Max(toroidDifficultyAngle - 20, 40), Mathf.Max(toroidDifficultyAngle - 20, 40)), 0) *
                    (Radius * Vector3.forward)) + origin + transform.position;
                spawned.transform.LookAt(Vector3.zero);
                return;
            case PositioningScheme.StraightLineRandomOrientation:
                spawned.transform.position = new Vector3(0, 0, spawnedItemCount * StraightLineLength) + origin + transform.position;
                spawned.transform.Rotate(Vector3.forward, (float)Random.value * 180);
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
                spawned.transform.Rotate(Vector3.forward + (((float)Random.value - .4f) * Vector3.right)
                                                         + (((float)Random.value - .4f) * Vector3.up), (float)Random.value * 180);
                return;
            case PositioningScheme.KinkyLineBranching:

                // Check if the maximum total spawned objects limit is reached
                if (spawnedItemCount >= maxTotalSpawnedObjects)
                    return;

                // Check if the current kink should branch
                if (Random.value < branchProbability && maxDepth > 0)
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
            case PositioningScheme.MazeGrid:
                PositionInMazeGrid(spawned);
                return;
            case PositioningScheme.CurvyTubeNetwork:
                PositionInCurvyTubeNetwork(spawned);
                return;
            case PositioningScheme.KinkyTubeNetwork:
                PositionInKinkyTubeNetwork(spawned);
                return;
            case PositioningScheme.SpiralTower:
                PositionInSpiralTower(spawned);
                return;
            case PositioningScheme.HoneycombGrid:
                PositionInHoneycombGrid(spawned);
                return;
            case PositioningScheme.HilbertCurveLSystem:
                var hilbertPositioner = GetComponent<HilbertCurveLSystemPositioning>();
                if (hilbertPositioner == null)
                {
                    hilbertPositioner = gameObject.AddComponent<HilbertCurveLSystemPositioning>();
                }
                hilbertPositioner.GenerateHilbertCurve();
                var positions = hilbertPositioner.GetPositions();
                var rotations = hilbertPositioner.GetRotations();
                if (spawnedItemCount < positions.Count)
                {
                    spawned.transform.SetPositionAndRotation(
                        positions[spawnedItemCount] + origin + transform.position,
                        rotations[spawnedItemCount]
                    );
                }
                return;

        }
    }

    GameObject SpawnRandom()
    {
        var spawnWeight = Random.value;
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
        if (Random.value < branchProbability)
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
    void PositionInMazeGrid(GameObject spawned)
    {
        int x = random.Next(0, GridWidth);
        int y = random.Next(0, GridHeight);
        int z = random.Next(0, GridThickness);
        spawned.transform.position = new Vector3(x * CellSize, y * CellSize, z * CellSize) + origin + transform.position;
        spawned.transform.rotation = Quaternion.Euler(random.Next(0, 4) * 90, random.Next(0, 4) * 90, random.Next(0, 4) * 90);
    }

    void PositionInCurvyTubeNetwork(GameObject spawned)
    {
        float t = spawnedItemCount * 0.1f;
        Vector3 position = new Vector3(
            Mathf.Sin(t * Curviness) * TowerRadius,
            t * 10,
            Mathf.Cos(t * Curviness) * TowerRadius
        );
        spawned.transform.position = position + origin + transform.position;
        spawned.transform.LookAt(position + Vector3.up * 10);

        if (random.NextDouble() < BranchProbability)
        {
            // Start a new branch
            currentDisplacement = spawned.transform.position;
            currentRotation = spawned.transform.rotation;
        }
    }

    void PositionInKinkyTubeNetwork(GameObject spawned)
    {
        if (spawnedItemCount % 5 == 0)
        {
            // Make a sharp turn every 5 segments
            currentRotation *= Quaternion.Euler(
                random.Next(-60, 61),
                random.Next(-60, 61),
                random.Next(-60, 61)
            );
        }
        spawned.transform.position = currentDisplacement;
        spawned.transform.rotation = currentRotation;
        currentDisplacement += currentRotation * Vector3.forward * 10;
    }

    void PositionInSpiralTower(GameObject spawned)
    {
        float height = (spawnedItemCount * TowerHeight) / NumberOfSegments;
        float angle = height * RotationsPerUnit * Mathf.PI * 2;
        Vector3 position = new Vector3(
            Mathf.Cos(angle) * TowerRadius,
            height,
            Mathf.Sin(angle) * TowerRadius
        );
        spawned.transform.position = position + origin + transform.position;
        spawned.transform.LookAt(new Vector3(0, height, 0) + origin + transform.position);
    }

    void PositionInHoneycombGrid(GameObject spawned)
    {
        int row = random.Next(0, GridHeight);
        int col = random.Next(0, GridWidth);
        float x = col * CellSize * 1.5f;
        float z = row * CellSize * Mathf.Sqrt(3) + (col % 2 == 0 ? 0 : CellSize * Mathf.Sqrt(3) / 2);
        spawned.transform.position = new Vector3(x, 0, z) + origin + transform.position;
        spawned.transform.rotation = Quaternion.Euler(0, random.Next(0, 6) * 60, 0);
    }
}
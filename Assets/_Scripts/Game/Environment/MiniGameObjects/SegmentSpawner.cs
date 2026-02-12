using CosmicShore.Core;
using CosmicShore.Models.Enums;
using System.Collections.Generic;
using CosmicShore.Soap;
using CosmicShore.Utility;
using UnityEngine;
using Obvious.Soap;

public class SegmentSpawner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameDataSO gameData;
    [SerializeField] List<SpawnableAbstractBase> spawnableSegments;
    
    [Header("Configuration")]
    [SerializeField] PositioningScheme positioningScheme = PositioningScheme.SphereUniform;
    [SerializeField] List<float> spawnSegmentWeights;
    [SerializeField] public int Seed;
    [SerializeField] Transform parent;

    [Header("Positioning")]
    [SerializeField] public Vector3 origin = Vector3.zero;
    [SerializeField] public float Radius = 250f;
    [SerializeField] public float StraightLineLength = 400f;
    [SerializeField] public float RotationAmount = 10f;
    [HideInInspector] public int DifficultyAngle = 90;
    [SerializeField] IntVariable intensityLevelData;
    [SerializeField] bool InitializeOnStart;
    [SerializeField] public int NumberOfSegments = 1;

    [Header("Data")]
    public MazeData[] mazeData;

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

    // Runtime state
    private GameObject SpawnedSegmentContainer;
    private List<Trail> trails = new();
    private System.Random random = new();
    private int spawnedItemCount;
    private Vector3 currentDisplacement;
    private Quaternion currentRotation;

    void Start()
    {
        if (SpawnedSegmentContainer == null) CreateContainer();

        if (InitializeOnStart)
            Initialize();
    }
    
    private void OnEnable()
    {
        if (gameData != null) gameData.OnResetForReplay.OnRaised += ResetTrack;
    }

    private void OnDisable()
    {
        if (gameData != null) gameData.OnResetForReplay.OnRaised -= ResetTrack;
    }
    
    // [Optimization] Logic separated to handle Event response cleanly
    private void ResetTrack()
    {
        NukeTheTrails();
        Initialize();
    }

    private void CreateContainer()
    {
        SpawnedSegmentContainer = new GameObject("SpawnedSegments")
        {
            transform =
            {
                parent = parent ? parent : transform
            }
        };
    }
    
    public void Initialize()
    {
        if (SpawnedSegmentContainer == null) CreateContainer();

        if (Seed != 0)
        {
            random = new System.Random(Seed);
            Random.InitState(Seed);
        }
        
        // [Optimization] Instead of destroying blocks individually (O(N)), NukeTheTrails destroys the parent container (O(1))
        // This check ensures we don't duplicate objects if Initialize is called manually without a Reset
        if (SpawnedSegmentContainer.transform.childCount > 0)
            NukeTheTrails();

        currentDisplacement = origin + transform.position;
        currentRotation = Quaternion.identity;

        normalizeWeights();

        // [Optimization] Cache intensity level once to avoid variable access overhead in the loop
        int currentIntensity = intensityLevelData ? intensityLevelData.Value : 1;

        for (int i = 0; i < NumberOfSegments; i++)
        {
            var spawned = SpawnRandom(currentIntensity);

            if (!spawned) continue;
            PositionSpawnedObject(spawned, positioningScheme);
            spawned.transform.SetParent(SpawnedSegmentContainer.transform);
            spawnedItemCount++;
        }
    }

    public void NukeTheTrails()
    {
        trails.Clear();
        spawnedItemCount = 0;

        // [Optimization] Major performance fix:
        // Instead of iterating through hundreds of children and calling Destroy() on each,
        // we destroy the container itself. This is much faster for the engine to handle.
        if (SpawnedSegmentContainer)
        {
            Destroy(SpawnedSegmentContainer);
        }
        CreateContainer();
    }

    // [Optimization] Pass intensity as parameter to avoid repeated property access
    GameObject SpawnRandom(int intensity)
    {
        if (spawnableSegments == null || spawnableSegments.Count == 0) return null;

        var spawnWeight = Random.value;
        var spawnIndex = 0;
        var totalWeight = 0f;
        
        for (int i = 0; i < spawnSegmentWeights.Count; i++)
        {
            totalWeight += spawnSegmentWeights[i];
            if (!(totalWeight >= spawnWeight)) continue;
            spawnIndex = i;
            break;
        }

        return spawnableSegments[spawnIndex].Spawn(intensity);
    }

    void normalizeWeights()
    {
        if (spawnSegmentWeights == null || spawnSegmentWeights.Count == 0) return;

        float totalWeight = 0;
        foreach (var weight in spawnSegmentWeights)
            totalWeight += weight;
        
        if (totalWeight <= 0) return;

        for (int i = 0; i < spawnSegmentWeights.Count; i++)
            spawnSegmentWeights[i] = spawnSegmentWeights[i] * (1 / totalWeight);
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
                spawned.transform.Rotate(Vector3.forward + (((float)Random.value - .5f) * Vector3.right)
                                                         + (((float)Random.value - .5f) * Vector3.up), (float)Random.value * 180);
                return;
            case PositioningScheme.HexRing:
                spawned.transform.position = new Vector3(Radius * Mathf.Sin(spawnedItemCount),
                                                         Radius * Mathf.Cos(spawnedItemCount),
                                                         spawnedItemCount * StraightLineLength) + origin + transform.position;
                spawned.transform.rotation = Quaternion.Euler(0, (float)Random.value * 0, 0) * spawned.transform.rotation;
                spawned.transform.rotation = Quaternion.Euler(0,0, (float)Random.value * 360) * spawned.transform.rotation;
                return;
            case PositioningScheme.KinkyLineBranching:
                if (spawnedItemCount >= maxTotalSpawnedObjects)
                    return;

                if (Random.value < branchProbability && maxDepth > 0)
                {
                    int numBranches = random.Next(minBranches, maxBranches + 1);
                    for (int i = 0; i < numBranches; i++)
                    {
                        float branchAngle = random.Next(minBranchAngle, maxBranchAngle);
                        float branchAngleRad = branchAngle * Mathf.Deg2Rad;
                        Vector3 branchDirection = Quaternion.Euler(0f, branchAngleRad * Mathf.Rad2Deg, 0f) * currentRotation * Vector3.forward;
                        float branchLengthMultiplier = Random.Range(minBranchLengthMultiplier, maxBranchLengthMultiplier);
                        float branchLength = StraightLineLength * branchLengthMultiplier;

                        GameObject branch = SpawnRandomBranch();
                        branch.transform.position = currentDisplacement + branchDirection * branchLength;
                        SafeLookRotation.TrySet(branch.transform, branchDirection, branch);

                        SpawnBranches(branch, maxDepth - 1, branchDirection, branchLength);
                    }
                }
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
                if (!hilbertPositioner)
                {
                    hilbertPositioner = gameObject.AddComponent<HilbertCurveLSystemPositioning>();
                }
                
                // [Optimization] Cache intensity usage
                int hilbertIntensity = intensityLevelData ? intensityLevelData.Value : 1;
                hilbertPositioner.segmentLength = 60 - (hilbertIntensity * 10);
                hilbertPositioner.GenerateHilbertCurve();
                var positions = hilbertPositioner.GetPositions();
                var rotations = hilbertPositioner.GetRotations();

                // [Safety] Ensure array bounds
                if (hilbertIntensity > 0 && hilbertIntensity <= mazeData.Length)
                {
                     mazeData[hilbertIntensity - 1].walls.Clear();
                     for (int i = 0; i < positions.Count; i++)
                     {
                         mazeData[hilbertIntensity - 1].walls.Add(new MazeData.WallData
                         {
                             position = positions[i],
                             rotation = rotations[i]
                         });
                     }
#if UNITY_EDITOR
                     UnityEditor.EditorUtility.SetDirty(mazeData[hilbertIntensity - 1]);
                     UnityEditor.AssetDatabase.SaveAssets();
#endif
                     if (spawnedItemCount < positions.Count)
                     {
                         spawned.transform.SetPositionAndRotation(
                             Quaternion.Euler(0, 0, RotationAmount) * (positions[spawnedItemCount] + origin + transform.position),
                             Quaternion.Euler(0, 0, RotationAmount) * rotations[spawnedItemCount] 
                         );
                     }
                }
                return;
            case PositioningScheme.SavedMaze:
                int mazeIntensity = intensityLevelData ? intensityLevelData.Value : 1;
                if (mazeIntensity > 0 && mazeIntensity <= mazeData.Length)
                {
                    if (spawnedItemCount < mazeData[mazeIntensity - 1].walls.Count)
                    {
                        spawned.transform.SetPositionAndRotation(
                            Quaternion.Euler(0, 0, RotationAmount) * (mazeData[mazeIntensity - 1].walls[spawnedItemCount].position + origin + transform.position),
                            Quaternion.Euler(0, 0, RotationAmount) * mazeData[mazeIntensity - 1].walls[spawnedItemCount].rotation);
                    }
                }
                return;
            case PositioningScheme.AtOriginNoRotation:
                spawned.transform.SetPositionAndRotation(origin + transform.position, Quaternion.identity);
                return;
        }
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

        if (Random.value < branchProbability)
        {
            int numBranches = random.Next(minBranches, maxBranches + 1);

            for (int i = 0; i < numBranches; i++)
            {
                float branchAngle = random.Next(minBranchAngle, maxBranchAngle);
                float branchAngleRad = branchAngle * Mathf.Deg2Rad;
                Vector3 branchDirection = Quaternion.Euler(0f, branchAngleRad * Mathf.Rad2Deg, 0f) * direction;
                float branchLengthMultiplier = Random.Range(minBranchLengthMultiplier, maxBranchLengthMultiplier);
                float branchLength = length * branchLengthMultiplier;

                GameObject branch = SpawnRandomBranch();
                branch.transform.position = parent.transform.position + branchDirection * branchLength;
                SafeLookRotation.TrySet(branch.transform, branchDirection, branch);

                SpawnBranches(branch, depth - 1, branchDirection, branchLength);
            }
        }
    }

    private GameObject SpawnRandomBranch()
    {
        if (branchPrefabs == null || branchPrefabs.Count == 0) return new GameObject("EmptyBranch");

        int randomIndex = random.Next(0, branchPrefabs.Count);
        GameObject branchPrefab = branchPrefabs[randomIndex];

        GameObject branch = Instantiate(branchPrefab, SpawnedSegmentContainer.transform, true);
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
            currentDisplacement = spawned.transform.position;
            currentRotation = spawned.transform.rotation;
        }
    }

    void PositionInKinkyTubeNetwork(GameObject spawned)
    {
        if (spawnedItemCount % 5 == 0)
        {
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
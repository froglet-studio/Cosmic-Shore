using CosmicShore.Game.Spawning;
using System.Collections.Generic;
using CosmicShore.Soap;
using UnityEngine;
using Obvious.Soap;
using CosmicShore.Core;

public class SegmentSpawner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameDataSO gameData;
    [SerializeField] List<SpawnableBase> spawnableSegments;

    [Header("Intensity-Mapped Spawning")]
    [Tooltip("Optional: map specific spawnables to intensity levels (index 0 = intensity 1). " +
             "When set, overrides random selection for that intensity.")]
    [SerializeField] SpawnableBase[] spawnableByIntensity;

    [Header("Configuration")]
    [SerializeField] List<float> spawnSegmentWeights;
    [SerializeField] public int Seed;
    [SerializeField] Transform parent;
    [SerializeField] IntVariable intensityLevelData;
    [SerializeField] bool InitializeOnStart;
    [SerializeField] public int NumberOfSegments = 1;

    [Header("Segment Layout")]
    [SerializeField] public Vector3 origin = Vector3.zero;
    [SerializeField] public float Radius;
    [SerializeField] public float StraightLineLength;
    [SerializeField] public float RotationAmount;
    [HideInInspector] public int DifficultyAngle = 90;

    // Runtime state
    private GameObject SpawnedSegmentContainer;
    private List<Trail> trails = new();

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
            Random.InitState(Seed);
        }

        if (SpawnedSegmentContainer.transform.childCount > 0)
            NukeTheTrails();

        NormalizeWeights();

        int currentIntensity = intensityLevelData ? intensityLevelData.Value : 1;

        for (int i = 0; i < NumberOfSegments; i++)
        {
            var spawnable = SelectSpawnable(currentIntensity);
            if (spawnable == null) continue;

            if (Seed != 0) spawnable.SetSeed(Seed + i);

            var spawned = spawnable.Spawn(currentIntensity);
            if (!spawned) continue;

            spawned.transform.SetParent(SpawnedSegmentContainer.transform);
            LayoutSegment(spawned.transform, i);
            trails.AddRange(spawnable.GetTrails());
        }
    }

    void LayoutSegment(Transform segment, int index)
    {
        var worldOrigin = origin + transform.position;

        if (Radius > 0 && StraightLineLength == 0)
        {
            segment.position = Random.insideUnitSphere * Radius + worldOrigin;
            segment.rotation = Random.rotation;
        }
        else if (StraightLineLength > 0)
        {
            segment.position = new Vector3(0, 0, index * StraightLineLength) + worldOrigin;
            if (RotationAmount != 0)
                segment.Rotate(Vector3.forward, index * RotationAmount);
        }
        else
        {
            segment.position = worldOrigin;
        }
    }

    public void NukeTheTrails()
    {
        trails.Clear();

        if (SpawnedSegmentContainer)
        {
            Destroy(SpawnedSegmentContainer);
        }
        CreateContainer();
    }

    SpawnableBase SelectSpawnable(int intensity)
    {
        // Check for intensity-specific spawnable first
        if (spawnableByIntensity != null && spawnableByIntensity.Length > 0)
        {
            int index = Mathf.Clamp(intensity - 1, 0, spawnableByIntensity.Length - 1);
            if (spawnableByIntensity[index] != null)
                return spawnableByIntensity[index];
        }

        // Fall back to weighted random selection
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

        return spawnableSegments[spawnIndex];
    }

    void NormalizeWeights()
    {
        if (spawnSegmentWeights == null || spawnSegmentWeights.Count == 0) return;

        float totalWeight = 0;
        foreach (var weight in spawnSegmentWeights)
            totalWeight += weight;

        if (totalWeight <= 0) return;

        for (int i = 0; i < spawnSegmentWeights.Count; i++)
            spawnSegmentWeights[i] = spawnSegmentWeights[i] * (1 / totalWeight);
    }
}

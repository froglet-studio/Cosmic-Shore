using CosmicShore.Game.Spawning;
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Soap;
using UnityEngine;
using Obvious.Soap;
using CosmicShore.Core;

public class SegmentSpawner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameDataSO gameData;

    [Header("Weighted Segments")]
    [Tooltip("Segments selected randomly by weight for each NumberOfSegments slot.")]
    [SerializeField] List<WeightedSpawnable> weightedSegments = new();

    [Header("Guaranteed Shapes")]
    [Tooltip("These always spawn every time, in addition to the weighted segments. " +
             "Use for shape spawnables that must always be present.")]
    [SerializeField] List<SpawnableBase> guaranteedSpawnables = new();

    [Header("Intensity-Mapped Spawning")]
    [Tooltip("Optional: map specific spawnables to intensity levels (index 0 = intensity 1). " +
             "When set, overrides random selection for that intensity.")]
    [SerializeField] SpawnableBase[] spawnableByIntensity;

    [Header("Configuration")]
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
    private float[] _normalizedWeights;

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

        // Collect active player domains so spawned segments cycle through them
        var playerDomains = GetActivePlayerDomains();
        int layoutIndex = 0;

        // Spawn weighted segments (random selection per slot)
        for (int i = 0; i < NumberOfSegments; i++)
        {
            if (playerDomains.Count > 0)
            {
                var segmentDomain = playerDomains[layoutIndex % playerDomains.Count];
                SetDomainOnWeighted(segmentDomain);
            }

            var spawnable = SelectSpawnable(currentIntensity);
            if (spawnable == null) continue;

            SpawnAndLayout(spawnable, currentIntensity, layoutIndex);
            layoutIndex++;
        }

        // Spawn guaranteed shapes (all of them, every time)
        for (int i = 0; i < guaranteedSpawnables.Count; i++)
        {
            var spawnable = guaranteedSpawnables[i];
            if (spawnable == null) continue;

            if (playerDomains.Count > 0)
                spawnable.domain = playerDomains[layoutIndex % playerDomains.Count];

            SpawnAndLayout(spawnable, currentIntensity, layoutIndex);
            layoutIndex++;
        }
    }

    void SpawnAndLayout(SpawnableBase spawnable, int intensity, int layoutIndex)
    {
        if (Seed != 0) spawnable.SetSeed(Seed + layoutIndex);

        var spawned = spawnable.Spawn(intensity);
        if (!spawned) return;

        spawned.transform.SetParent(SpawnedSegmentContainer.transform);
        LayoutSegment(spawned.transform, layoutIndex);
        trails.AddRange(spawnable.GetTrails());
    }

    void LayoutSegment(Transform segment, int index)
    {
        var worldOrigin = origin + transform.position;
        int totalCount = NumberOfSegments + guaranteedSpawnables.Count;

        if (totalCount <= 1)
        {
            segment.SetPositionAndRotation(worldOrigin, Quaternion.identity);
        }
        else if (Radius > 0 && StraightLineLength == 0)
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
            segment.SetPositionAndRotation(worldOrigin, Quaternion.identity);
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
        if (weightedSegments == null || weightedSegments.Count == 0) return null;

        var roll = Random.value;
        var accumulated = 0f;

        for (int i = 0; i < weightedSegments.Count; i++)
        {
            accumulated += _normalizedWeights[i];
            if (accumulated >= roll)
                return weightedSegments[i].spawnable;
        }

        // Fallback to last entry
        return weightedSegments[^1].spawnable;
    }

    void NormalizeWeights()
    {
        if (weightedSegments == null || weightedSegments.Count == 0)
        {
            _normalizedWeights = Array.Empty<float>();
            return;
        }

        _normalizedWeights = new float[weightedSegments.Count];
        float total = 0f;

        for (int i = 0; i < weightedSegments.Count; i++)
            total += weightedSegments[i].weight;

        // If all weights are 0, distribute equally
        if (total <= 0f)
        {
            float equal = 1f / weightedSegments.Count;
            for (int i = 0; i < _normalizedWeights.Length; i++)
                _normalizedWeights[i] = equal;
            return;
        }

        for (int i = 0; i < weightedSegments.Count; i++)
            _normalizedWeights[i] = weightedSegments[i].weight / total;
    }

    List<Domains> GetActivePlayerDomains()
    {
        if (gameData != null && gameData.Players != null && gameData.Players.Count > 0)
        {
            var domains = gameData.Players
                .Select(p => p.Domain)
                .Where(d => d is not (Domains.None or Domains.Unassigned))
                .Distinct()
                .ToList();

            if (domains.Count > 0)
                return domains;
        }

        return new List<Domains> { Domains.Jade, Domains.Ruby, Domains.Gold };
    }

    void SetDomainOnWeighted(Domains domain)
    {
        foreach (var entry in weightedSegments)
            if (entry.spawnable != null) entry.spawnable.domain = domain;

        if (spawnableByIntensity != null)
            foreach (var s in spawnableByIntensity)
                if (s != null) s.domain = domain;
    }

    [Serializable]
    public struct WeightedSpawnable
    {
        [Tooltip("The spawnable to instantiate.")]
        public SpawnableBase spawnable;

        [Tooltip("Relative weight for random selection. Higher = more likely.")]
        public float weight;
    }
}

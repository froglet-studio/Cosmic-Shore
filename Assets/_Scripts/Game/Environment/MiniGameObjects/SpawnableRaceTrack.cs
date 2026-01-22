using CosmicShore.Core;
using UnityEngine;

public class SpawnableRaceTrack : SpawnableAbstractBase
{
    [Header("Block Settings")]
    [SerializeField] Prism prism;
    [SerializeField] Vector3 scale = new Vector3(5, 1, 5);

    [Header("Track Dimensions")]
    [Tooltip("Target lap time in seconds for a ship at the specified speed")]
    [SerializeField] float targetLapTime = 45f;
    [Tooltip("Expected ship speed in units/second")]
    [SerializeField] float expectedShipSpeed = 150f;
    [Tooltip("Number of blocks to spawn along the track")]
    [SerializeField] int blockCount = 600;

    [Header("Track Shape")]
    [Tooltip("Base track width (affects the overall horizontal spread)")]
    [SerializeField] float trackWidth = 500f;
    [Tooltip("Base track depth (affects the overall depth spread)")]
    [SerializeField] float trackDepth = 300f;
    [Tooltip("Maximum elevation variation")]
    [SerializeField] float maxElevation = 80f;

    [Header("Track Features")]
    [Tooltip("How chaotic/varied the track shape is (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] float complexity = 0.5f;
    [Tooltip("Include a corkscrew section")]
    [SerializeField] bool includeCorkscrew = true;
    [Tooltip("Include banked curves")]
    [SerializeField] bool includeBanking = true;
    [Tooltip("Number of major turns/features")]
    [SerializeField] int featureCount = 6;

    [Header("Checkpoints")]
    [Tooltip("Number of checkpoint markers around the track")]
    [SerializeField] int checkpointCount = 8;
    [Tooltip("Optional different prism for checkpoint blocks")]
    [SerializeField] Prism checkpointPrism;
    [Tooltip("Scale multiplier for checkpoint blocks")]
    [SerializeField] float checkpointScaleMultiplier = 2f;
    [Tooltip("Domain for start/finish area")]
    [SerializeField] Domains startFinishDomain = Domains.Ruby;
    [Tooltip("Domain for checkpoint markers")]
    [SerializeField] Domains checkpointDomain = Domains.Jade;

    [Header("Randomization")]
    [Tooltip("Seed for reproducible track generation. Use -1 for random seed.")]
    [SerializeField] int seed = -1;
    [Tooltip("The actual seed used (for reference)")]
    [SerializeField] int actualSeed;

    static int TracksSpawned = 0;

    public override GameObject Spawn()
    {
        // Initialize seeded random
        actualSeed = seed == -1 ? System.Environment.TickCount : seed;
        rng = new System.Random(actualSeed);

        GameObject container = new GameObject();
        container.name = $"RaceTrack_{TracksSpawned++}_Seed{actualSeed}";

        var trail = new Trail();

        // Calculate target track length based on lap time and speed
        float targetLength = targetLapTime * expectedShipSpeed;

        // Generate track parameters from seed
        TrackParameters parameters = GenerateTrackParameters();

        // Pre-calculate positions for smooth orientation
        Vector3[] positions = new Vector3[blockCount];
        for (int i = 0; i < blockCount; i++)
        {
            float t = (float)i / blockCount * Mathf.PI * 2f; // Full loop
            positions[i] = CalculateTrackPosition(t, parameters, targetLength);
        }

        // Spawn blocks with proper orientation
        for (int block = 0; block < blockCount; block++)
        {
            Vector3 position = positions[block];
            float trackProgress = (float)block / blockCount; // 0 to 1 around track

            // Look at next position for orientation (wrap around for last block)
            int nextBlock = (block + 1) % blockCount;
            Vector3 lookPosition = positions[nextBlock];

            // Determine block properties based on position
            Vector3 blockScale = scale;
            Prism blockPrism = prism;
            Domains blockDomain = Domains.Gold;

            // Start/Finish zone (first and last 2% of track)
            bool isStartFinish = trackProgress < 0.02f || trackProgress > 0.98f;

            // Checkpoint positions
            bool isCheckpoint = false;
            if (checkpointCount > 0)
            {
                float checkpointSpacing = 1f / checkpointCount;
                for (int cp = 0; cp < checkpointCount; cp++)
                {
                    float cpPosition = cp * checkpointSpacing;
                    if (Mathf.Abs(trackProgress - cpPosition) < 0.005f)
                    {
                        isCheckpoint = true;
                        break;
                    }
                }
            }

            // Apply visual differentiation
            if (isStartFinish)
            {
                blockScale = scale * 1.5f;
                blockDomain = startFinishDomain;
            }
            else if (isCheckpoint)
            {
                blockScale = scale * checkpointScaleMultiplier;
                blockDomain = checkpointDomain;
                if (checkpointPrism != null) blockPrism = checkpointPrism;
            }

            // Calculate banking if enabled
            if (includeBanking)
            {
                // Bank angle based on curve tightness
                int prevBlock = (block - 1 + blockCount) % blockCount;
                Vector3 toNext = (positions[nextBlock] - position).normalized;
                Vector3 toPrev = (positions[prevBlock] - position).normalized;
                float curvature = Vector3.Cross(toNext, toPrev).magnitude;

                // Widen track on sharp curves for better racing lines
                float curveWidening = 1f + curvature * 0.5f;
                blockScale.x *= curveWidening;
            }

            CreateBlock(position, lookPosition, $"{container.name}::BLOCK::{block}",
                       trail, blockScale, blockPrism, container, blockDomain);
        }

        trails.Add(trail);

        Debug.Log($"[RaceTrack] Generated track with seed {actualSeed}, " +
                  $"approximate length: {EstimateTrackLength(positions):F0} units, " +
                  $"target lap time: {targetLapTime}s at {expectedShipSpeed} units/s");

        return container;
    }

    private TrackParameters GenerateTrackParameters()
    {
        var p = new TrackParameters();

        // Base shape modifiers (how elliptical vs circular)
        p.widthRatio = RandomRange(0.6f, 1.0f);
        p.depthRatio = RandomRange(0.6f, 1.0f);

        // Generate harmonic coefficients for interesting track shape
        // These create the "waviness" and features of the track
        int harmonics = Mathf.Max(3, featureCount);
        p.xHarmonics = new HarmonicCoefficient[harmonics];
        p.zHarmonics = new HarmonicCoefficient[harmonics];
        p.yHarmonics = new HarmonicCoefficient[harmonics];

        for (int i = 0; i < harmonics; i++)
        {
            float amplitude = complexity * Mathf.Pow(0.5f, i); // Decreasing amplitude for higher harmonics

            p.xHarmonics[i] = new HarmonicCoefficient
            {
                frequency = i + 2, // Start at 2 to preserve base shape
                amplitude = RandomRange(-amplitude, amplitude) * trackWidth * 0.2f,
                phase = RandomRange(0f, Mathf.PI * 2f)
            };

            p.zHarmonics[i] = new HarmonicCoefficient
            {
                frequency = i + 2,
                amplitude = RandomRange(-amplitude, amplitude) * trackDepth * 0.2f,
                phase = RandomRange(0f, Mathf.PI * 2f)
            };

            p.yHarmonics[i] = new HarmonicCoefficient
            {
                frequency = i + 1, // Elevation can have lower frequencies
                amplitude = RandomRange(0.3f, 1f) * maxElevation * Mathf.Pow(0.6f, i),
                phase = RandomRange(0f, Mathf.PI * 2f)
            };
        }

        // Corkscrew parameters
        if (includeCorkscrew)
        {
            p.corkscrewStart = RandomRange(0.2f, 0.4f); // Where in the lap (0-1) it starts
            p.corkscrewLength = RandomRange(0.08f, 0.15f); // How long (as fraction of lap)
            p.corkscrewRadius = RandomRange(20f, 40f);
            p.corkscrewTurns = RandomRange(1.5f, 3f);
        }

        // Chicane/S-curve parameters
        p.chicaneCount = rng.Next(1, 4);
        p.chicanePositions = new float[p.chicaneCount];
        p.chicaneIntensities = new float[p.chicaneCount];
        for (int i = 0; i < p.chicaneCount; i++)
        {
            p.chicanePositions[i] = RandomRange(0f, 1f);
            p.chicaneIntensities[i] = RandomRange(0.5f, 1f) * complexity;
        }

        return p;
    }

    private Vector3 CalculateTrackPosition(float t, TrackParameters p, float targetLength)
    {
        // Normalize t to 0-1 range for easier feature placement
        float tNorm = t / (Mathf.PI * 2f);

        // Base elliptical shape scaled to approximate target length
        // Circumference of ellipse ≈ π * (3(a+b) - sqrt((3a+b)(a+3b)))
        float a = trackWidth * 0.5f * p.widthRatio;
        float b = trackDepth * 0.5f * p.depthRatio;
        float approxCircumference = Mathf.PI * (3f * (a + b) - Mathf.Sqrt((3f * a + b) * (a + 3f * b)));
        float scaleFactor = targetLength / approxCircumference;

        a *= scaleFactor;
        b *= scaleFactor;

        float x = a * Mathf.Sin(t);
        float z = b * Mathf.Cos(t);
        float y = 0f;

        // Add harmonic variations for interesting shape
        foreach (var h in p.xHarmonics)
        {
            x += h.amplitude * scaleFactor * Mathf.Sin(t * h.frequency + h.phase);
        }

        foreach (var h in p.zHarmonics)
        {
            z += h.amplitude * scaleFactor * Mathf.Sin(t * h.frequency + h.phase);
        }

        // Elevation profile
        foreach (var h in p.yHarmonics)
        {
            y += h.amplitude * Mathf.Sin(t * h.frequency + h.phase);
        }

        // Add chicanes (tight S-curves)
        foreach (int i in System.Linq.Enumerable.Range(0, p.chicaneCount))
        {
            float chicaneT = p.chicanePositions[i];
            float dist = Mathf.Abs(tNorm - chicaneT);
            if (dist < 0.05f)
            {
                float chicaneEffect = (1f - dist / 0.05f) * p.chicaneIntensities[i];
                float chicaneAngle = (tNorm - chicaneT) / 0.05f * Mathf.PI * 4f;

                // Perpendicular displacement for S-curve
                float perpX = Mathf.Cos(t);
                float perpZ = -Mathf.Sin(t);

                x += perpX * Mathf.Sin(chicaneAngle) * chicaneEffect * 30f * scaleFactor;
                z += perpZ * Mathf.Sin(chicaneAngle) * chicaneEffect * 30f * scaleFactor;
            }
        }

        // Corkscrew section
        if (includeCorkscrew)
        {
            float corkscrewEnd = p.corkscrewStart + p.corkscrewLength;
            if (tNorm >= p.corkscrewStart && tNorm <= corkscrewEnd)
            {
                float corkscrewProgress = (tNorm - p.corkscrewStart) / p.corkscrewLength;
                float corkscrewAngle = corkscrewProgress * Mathf.PI * 2f * p.corkscrewTurns;

                // Smooth blend in/out
                float blend = Mathf.Sin(corkscrewProgress * Mathf.PI);

                // Add vertical corkscrew motion
                y += Mathf.Sin(corkscrewAngle) * p.corkscrewRadius * blend;

                // Add horizontal spiral component
                float perpX = Mathf.Cos(t);
                float perpZ = -Mathf.Sin(t);
                x += perpX * Mathf.Cos(corkscrewAngle) * p.corkscrewRadius * blend * 0.5f;
                z += perpZ * Mathf.Cos(corkscrewAngle) * p.corkscrewRadius * blend * 0.5f;
            }
        }

        return new Vector3(x, y, z);
    }

    private float EstimateTrackLength(Vector3[] positions)
    {
        float length = 0f;
        for (int i = 0; i < positions.Length; i++)
        {
            int next = (i + 1) % positions.Length;
            length += Vector3.Distance(positions[i], positions[next]);
        }
        return length;
    }

    private float RandomRange(float min, float max)
    {
        return (float)(rng.NextDouble() * (max - min) + min);
    }

    // Structs for track parameters
    private struct HarmonicCoefficient
    {
        public float frequency;
        public float amplitude;
        public float phase;
    }

    private class TrackParameters
    {
        public float widthRatio;
        public float depthRatio;
        public HarmonicCoefficient[] xHarmonics;
        public HarmonicCoefficient[] zHarmonics;
        public HarmonicCoefficient[] yHarmonics;

        // Corkscrew
        public float corkscrewStart;
        public float corkscrewLength;
        public float corkscrewRadius;
        public float corkscrewTurns;

        // Chicanes
        public int chicaneCount;
        public float[] chicanePositions;
        public float[] chicaneIntensities;
    }

    /// <summary>
    /// Generate a track with a specific seed (useful for multiplayer synchronization)
    /// </summary>
    public GameObject SpawnWithSeed(int trackSeed)
    {
        seed = trackSeed;
        return Spawn();
    }

    /// <summary>
    /// Get the seed used for the last generated track
    /// </summary>
    public int GetLastSeed() => actualSeed;

    // Cached waypoints for AI/minimap use
    private Vector3[] cachedWaypoints;
    private Vector3[] cachedTangents;

    /// <summary>
    /// Get smoothed waypoints along the track for AI navigation or minimap display
    /// </summary>
    /// <param name="waypointCount">Number of waypoints to return</param>
    /// <returns>Array of world positions along the track</returns>
    public Vector3[] GetWaypoints(int waypointCount = 100)
    {
        if (cachedWaypoints != null && cachedWaypoints.Length == waypointCount)
            return cachedWaypoints;

        // Need to regenerate with same seed
        var tempRng = new System.Random(actualSeed);
        rng = tempRng;

        var parameters = GenerateTrackParameters();
        float targetLength = targetLapTime * expectedShipSpeed;

        cachedWaypoints = new Vector3[waypointCount];
        cachedTangents = new Vector3[waypointCount];

        for (int i = 0; i < waypointCount; i++)
        {
            float t = (float)i / waypointCount * Mathf.PI * 2f;
            cachedWaypoints[i] = CalculateTrackPosition(t, parameters, targetLength);
        }

        // Calculate tangents
        for (int i = 0; i < waypointCount; i++)
        {
            int next = (i + 1) % waypointCount;
            cachedTangents[i] = (cachedWaypoints[next] - cachedWaypoints[i]).normalized;
        }

        return cachedWaypoints;
    }

    /// <summary>
    /// Get the tangent direction at each waypoint
    /// </summary>
    public Vector3[] GetTangents(int waypointCount = 100)
    {
        if (cachedTangents == null || cachedTangents.Length != waypointCount)
            GetWaypoints(waypointCount);
        return cachedTangents;
    }

    /// <summary>
    /// Find the closest point on track to a given position (for lap tracking)
    /// </summary>
    /// <param name="position">World position to check</param>
    /// <param name="trackProgress">Output: normalized progress around track (0-1)</param>
    /// <returns>Closest point on the track centerline</returns>
    public Vector3 GetClosestPointOnTrack(Vector3 position, out float trackProgress)
    {
        var waypoints = GetWaypoints(200); // Higher resolution for accuracy

        float minDist = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < waypoints.Length; i++)
        {
            float dist = Vector3.SqrMagnitude(position - waypoints[i]);
            if (dist < minDist)
            {
                minDist = dist;
                closestIndex = i;
            }
        }

        trackProgress = (float)closestIndex / waypoints.Length;
        return waypoints[closestIndex];
    }

    /// <summary>
    /// Get approximate track length in units
    /// </summary>
    public float GetTrackLength()
    {
        return EstimateTrackLength(GetWaypoints(blockCount));
    }
}
using CosmicShore.Core;
using CosmicShore.Game;
using System.Collections.Generic;
using UnityEngine;

public class SpawnableWaypointTrack : SpawnableAbstractBase
{
    [Header("Waypoints")]
    [Tooltip("List of position sets for each intensity level. The track will close from the last point back to the first.")]
    [SerializeField] public List<CrystalPositionSet> waypoints;

    [Header("Spline Settings")]
    [Tooltip("Enable Catmull-Rom spline per intensity (0=linear, 1=spline). Matches waypoints list by index.")]
    [SerializeField] List<int> useSplinePerIntensity;

    [Header("Block Settings")]
    [SerializeField] Prism prism;
    [SerializeField] Vector3 scale = new Vector3(5, 1, 5);
    [Tooltip("Number of blocks to spawn per segment (between consecutive waypoints)")]
    [SerializeField] int blocksPerSegment = 50;

    [Header("Checkpoints")]
    [Tooltip("Mark waypoint positions with larger checkpoint blocks")]
    [SerializeField] bool markWaypoints = true;
    [Tooltip("Scale multiplier for waypoint marker blocks")]
    [SerializeField] float waypointScaleMultiplier = 2f;
    [Tooltip("Optional different prism for waypoint markers")]
    [SerializeField] Prism waypointPrism;
    [Tooltip("Domain for waypoint markers")]
    [SerializeField] Domains waypointDomain = Domains.Jade;

    [Header("Track Domain")]
    [SerializeField] Domains trackDomain = Domains.Gold;

    [Header("Editor Preview")]
    [Tooltip("Which intensity level to preview in the editor")]
    [SerializeField] int previewIntensityLevel = 0;

    static int TracksSpawned = 0;

    private bool UseSpline(int intensityLevel)
    {
        int index = intensityLevel - 1;
        return useSplinePerIntensity != null &&
               index >= 0 &&
               index < useSplinePerIntensity.Count &&
               useSplinePerIntensity[index] != 0;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private Vector3 GetSplinePoint(List<Vector3> positions, int segment, float t)
    {
        int count = positions.Count;
        Vector3 p0 = positions[((segment - 1) % count + count) % count];
        Vector3 p1 = positions[segment];
        Vector3 p2 = positions[(segment + 1) % count];
        Vector3 p3 = positions[(segment + 2) % count];
        return CatmullRom(p0, p1, p2, p3, t);
    }

    public override GameObject Spawn()
    {
        return Spawn(intensityLevel: 1);
    }

    public override GameObject Spawn(int intensityLevel)
    {
        this.intenstyLevel = intensityLevel;
        if (!IsValidIntensityLevel(intensityLevel))
        {
            Debug.LogError($"[WaypointTrack] Need at least 2 waypoints for intensity level {intensityLevel}.");
            return new GameObject("EmptyTrack");
        }

        GameObject container = new GameObject();
        container.name = $"WaypointTrack_{TracksSpawned++}";

        var trail = new Trail();
        int totalBlocks = 0;

        var positions = waypoints[intensityLevel - 1].positions;
        int segmentCount = positions.Count;
        bool spline = UseSpline(intensityLevel);

        for (int segment = 0; segment < segmentCount; segment++)
        {
            Vector3 startPos = positions[segment];
            Vector3 endPos = positions[(segment + 1) % positions.Count];

            for (int i = 0; i < blocksPerSegment; i++)
            {
                float t = (float)i / blocksPerSegment;

                Vector3 position;
                Vector3 lookTarget;

                if (spline)
                {
                    position = GetSplinePoint(positions, segment, t);

                    if (i < blocksPerSegment - 1)
                    {
                        lookTarget = GetSplinePoint(positions, segment, (float)(i + 1) / blocksPerSegment);
                    }
                    else
                    {
                        lookTarget = GetSplinePoint(positions, (segment + 1) % segmentCount, 0f);
                    }
                }
                else
                {
                    position = Vector3.Lerp(startPos, endPos, t);

                    if (i < blocksPerSegment - 1)
                    {
                        lookTarget = Vector3.Lerp(startPos, endPos, (float)(i + 1) / blocksPerSegment);
                    }
                    else
                    {
                        lookTarget = endPos;
                    }
                }

                // Determine if this is a waypoint marker position
                bool isWaypointMarker = markWaypoints && i == 0;

                Vector3 blockScale = isWaypointMarker ? scale * waypointScaleMultiplier : scale;
                Prism blockPrism = (isWaypointMarker && waypointPrism != null) ? waypointPrism : prism;
                Domains blockDomain = isWaypointMarker ? waypointDomain : trackDomain;

                CreateBlock(position, lookTarget, $"{container.name}::BLOCK::{totalBlocks}",
                           trail, blockScale, blockPrism, container, blockDomain);

                totalBlocks++;
            }
        }

        trails.Add(trail);

        Debug.Log($"[WaypointTrack] Generated track with {positions.Count} waypoints, " +
           $"{totalBlocks} total blocks, spline={spline}, approximate length: {EstimateTrackLength(intensityLevel):F0} units");

        return container;
    }

    /// <summary>
    /// Estimate total track length by summing segment distances (expects 1-based intensity: 1-4)
    /// </summary>
    private float EstimateTrackLength(int intensityLevel)
    {
        if (!IsValidIntensityLevel(intensityLevel)) return 0f;

        var positions = waypoints[intensityLevel - 1].positions;

        if (UseSpline(intensityLevel))
        {
            // Sample spline to estimate arc length
            float length = 0f;
            int samplesPerSegment = 20;
            for (int seg = 0; seg < positions.Count; seg++)
            {
                Vector3 prev = GetSplinePoint(positions, seg, 0f);
                for (int s = 1; s <= samplesPerSegment; s++)
                {
                    float t = (float)s / samplesPerSegment;
                    Vector3 curr = GetSplinePoint(positions, seg, t);
                    length += Vector3.Distance(prev, curr);
                    prev = curr;
                }
            }
            return length;
        }

        float len = 0f;
        for (int i = 0; i < positions.Count; i++)
        {
            int next = (i + 1) % positions.Count;
            len += Vector3.Distance(positions[i], positions[next]);
        }
        return len;
    }

    /// <summary>
    /// Get interpolated positions along the entire track
    /// </summary>
    /// <param name="positionCount">Total number of positions to return</param>
    /// <param name="intensityLevel">Which intensity level track to use</param>
    public Vector3[] GetInterpolatedPositions(int positionCount, int intensityLevel)
    {
        if (!IsValidIntensityLevel(intensityLevel)) return new Vector3[0];

        var waypointPositions = waypoints[intensityLevel - 1].positions;
        if (waypointPositions.Count < 2) return new Vector3[0];

        Vector3[] positions = new Vector3[positionCount];

        if (UseSpline(intensityLevel))
        {
            // Distribute points evenly in parameter space across all segments
            int segmentCount = waypointPositions.Count;
            for (int i = 0; i < positionCount; i++)
            {
                float globalT = (float)i / positionCount * segmentCount;
                int segment = Mathf.Min((int)globalT, segmentCount - 1);
                float localT = globalT - segment;
                positions[i] = GetSplinePoint(waypointPositions, segment, localT);
            }
            return positions;
        }

        float totalLength = EstimateTrackLength(intensityLevel);

        // Calculate segment lengths and cumulative distances
        float[] segmentLengths = new float[waypointPositions.Count];
        float[] cumulativeDistances = new float[waypointPositions.Count + 1];
        cumulativeDistances[0] = 0f;

        for (int i = 0; i < waypointPositions.Count; i++)
        {
            int next = (i + 1) % waypointPositions.Count;
            segmentLengths[i] = Vector3.Distance(waypointPositions[i], waypointPositions[next]);
            cumulativeDistances[i + 1] = cumulativeDistances[i] + segmentLengths[i];
        }

        for (int i = 0; i < positionCount; i++)
        {
            float targetDistance = (float)i / positionCount * totalLength;

            // Find which segment this distance falls into
            int segment = 0;
            for (int s = 0; s < waypointPositions.Count; s++)
            {
                if (targetDistance >= cumulativeDistances[s] && targetDistance < cumulativeDistances[s + 1])
                {
                    segment = s;
                    break;
                }
            }

            // Interpolate within segment
            float segmentProgress = (targetDistance - cumulativeDistances[segment]) / segmentLengths[segment];
            int nextWaypoint = (segment + 1) % waypointPositions.Count;
            positions[i] = Vector3.Lerp(waypointPositions[segment], waypointPositions[nextWaypoint], segmentProgress);
        }

        return positions;
    }

    /// <summary>
    /// Find the closest point on track to a given position
    /// </summary>
    public Vector3 GetClosestPointOnTrack(Vector3 position, out float trackProgress, int intensityLevel)
    {
        var interpolated = GetInterpolatedPositions(200, intensityLevel);

        float minDist = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < interpolated.Length; i++)
        {
            float dist = Vector3.SqrMagnitude(position - interpolated[i]);
            if (dist < minDist)
            {
                minDist = dist;
                closestIndex = i;
            }
        }

        trackProgress = (float)closestIndex / interpolated.Length;
        return interpolated[closestIndex];
    }

    /// <summary>
    /// Check if an intensity level is valid
    /// </summary>
    private bool IsValidIntensityLevel(int intensityLevel)
    {
        int index = intensityLevel - 1;
        return waypoints != null &&
               index >= 0 &&
               index < waypoints.Count &&
               waypoints[index].positions != null &&
               waypoints[index].positions.Count >= 2;
    }

#if UNITY_EDITOR
    private static readonly Color[] IntensityColors =
    {
        Color.green,
        Color.yellow,
        new Color(1f, 0.5f, 0f), // Orange
        Color.red
    };

    private void OnDrawGizmos()
    {
        if (!IsValidIntensityLevel(previewIntensityLevel)) return;

        var positions = waypoints[previewIntensityLevel].positions;
        Gizmos.color = IntensityColors[previewIntensityLevel % IntensityColors.Length];

        // Use 1-based for UseSpline check
        bool spline = UseSpline(previewIntensityLevel + 1);

        if (spline)
        {
            // Draw spline curves
            int samplesPerSegment = 20;
            for (int seg = 0; seg < positions.Count; seg++)
            {
                Vector3 prev = GetSplinePoint(positions, seg, 0f);
                for (int s = 1; s <= samplesPerSegment; s++)
                {
                    float t = (float)s / samplesPerSegment;
                    Vector3 curr = GetSplinePoint(positions, seg, t);
                    Gizmos.DrawLine(prev, curr);
                    prev = curr;
                }
            }
        }
        else
        {
            for (int i = 0; i < positions.Count; i++)
            {
                int next = (i + 1) % positions.Count;
                Gizmos.DrawLine(positions[i], positions[next]);
            }
        }

        // Draw waypoint spheres
        for (int i = 0; i < positions.Count; i++)
        {
            Gizmos.DrawWireSphere(positions[i], 5f);
        }

        // Highlight first waypoint
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(positions[0], 8f);
    }

    private void OnDrawGizmosSelected()
    {
        if (!IsValidIntensityLevel(previewIntensityLevel)) return;

        // Draw interpolated path when selected
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        var positions = GetInterpolatedPositions(100, previewIntensityLevel);

        for (int i = 0; i < positions.Length; i++)
        {
            int next = (i + 1) % positions.Length;
            Gizmos.DrawLine(positions[i], positions[next]);
        }

        // Draw all intensity levels faintly for comparison
        for (int level = 0; level < waypoints.Count; level++)
        {
            if (level == previewIntensityLevel || !IsValidIntensityLevel(level)) continue;

            var levelPositions = waypoints[level].positions;
            Color faintColor = IntensityColors[level % IntensityColors.Length];
            faintColor.a = 0.25f;
            Gizmos.color = faintColor;

            bool levelSpline = UseSpline(level + 1);

            if (levelSpline)
            {
                int samplesPerSegment = 20;
                for (int seg = 0; seg < levelPositions.Count; seg++)
                {
                    Vector3 prev = GetSplinePoint(levelPositions, seg, 0f);
                    for (int s = 1; s <= samplesPerSegment; s++)
                    {
                        float t = (float)s / samplesPerSegment;
                        Vector3 curr = GetSplinePoint(levelPositions, seg, t);
                        Gizmos.DrawLine(prev, curr);
                        prev = curr;
                    }
                }
            }
            else
            {
                for (int i = 0; i < levelPositions.Count; i++)
                {
                    int next = (i + 1) % levelPositions.Count;
                    Gizmos.DrawLine(levelPositions[i], levelPositions[next]);
                }
            }
        }
    }
#endif
}

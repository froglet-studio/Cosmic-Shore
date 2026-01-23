using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

public class SpawnableWaypointTrack : SpawnableAbstractBase
{
    [Header("Waypoints")]
    [Tooltip("List of positions defining the track path. The track will close from the last point back to the first.")]
    [SerializeField] List<Vector3> waypoints = new List<Vector3>();

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

    static int TracksSpawned = 0;

    public override GameObject Spawn()
    {
        if (waypoints == null || waypoints.Count < 2)
        {
            Debug.LogError("[WaypointTrack] Need at least 2 waypoints to create a track.");
            return new GameObject("EmptyTrack");
        }

        GameObject container = new GameObject();
        container.name = $"WaypointTrack_{TracksSpawned++}";

        var trail = new Trail();
        int totalBlocks = 0;

        // Spawn blocks for each segment, including the closing segment
        int segmentCount = waypoints.Count; // Includes wrap-around segment

        for (int segment = 0; segment < segmentCount; segment++)
        {
            Vector3 startPos = waypoints[segment];
            Vector3 endPos = waypoints[(segment + 1) % waypoints.Count]; // Wrap to first for last segment

            // Get the position after endPos for smooth orientation at segment boundaries
            Vector3 nextPos = waypoints[(segment + 2) % waypoints.Count];

            for (int i = 0; i < blocksPerSegment; i++)
            {
                float t = (float)i / blocksPerSegment;
                Vector3 position = Vector3.Lerp(startPos, endPos, t);

                // Calculate look target - interpolate toward next segment for smooth transitions
                Vector3 lookTarget;
                if (i < blocksPerSegment - 1)
                {
                    // Look at next block in same segment
                    lookTarget = Vector3.Lerp(startPos, endPos, (float)(i + 1) / blocksPerSegment);
                }
                else
                {
                    // At end of segment, look toward start of next segment
                    lookTarget = endPos;
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

        Debug.Log($"[WaypointTrack] Generated track with {waypoints.Count} waypoints, " +
                  $"{totalBlocks} total blocks, approximate length: {EstimateTrackLength():F0} units");

        return container;
    }

    /// <summary>
    /// Estimate total track length by summing segment distances
    /// </summary>
    private float EstimateTrackLength()
    {
        float length = 0f;
        for (int i = 0; i < waypoints.Count; i++)
        {
            int next = (i + 1) % waypoints.Count;
            length += Vector3.Distance(waypoints[i], waypoints[next]);
        }
        return length;
    }

    /// <summary>
    /// Add a waypoint to the track
    /// </summary>
    public void AddWaypoint(Vector3 position)
    {
        waypoints.Add(position);
    }

    /// <summary>
    /// Set all waypoints at once
    /// </summary>
    public void SetWaypoints(List<Vector3> newWaypoints)
    {
        waypoints = new List<Vector3>(newWaypoints);
    }

    /// <summary>
    /// Get the current waypoints
    /// </summary>
    public List<Vector3> GetWaypoints()
    {
        return new List<Vector3>(waypoints);
    }

    /// <summary>
    /// Get interpolated positions along the entire track
    /// </summary>
    /// <param name="positionCount">Total number of positions to return</param>
    public Vector3[] GetInterpolatedPositions(int positionCount)
    {
        if (waypoints.Count < 2) return new Vector3[0];

        Vector3[] positions = new Vector3[positionCount];
        float totalLength = EstimateTrackLength();

        // Calculate segment lengths and cumulative distances
        float[] segmentLengths = new float[waypoints.Count];
        float[] cumulativeDistances = new float[waypoints.Count + 1];
        cumulativeDistances[0] = 0f;

        for (int i = 0; i < waypoints.Count; i++)
        {
            int next = (i + 1) % waypoints.Count;
            segmentLengths[i] = Vector3.Distance(waypoints[i], waypoints[next]);
            cumulativeDistances[i + 1] = cumulativeDistances[i] + segmentLengths[i];
        }

        for (int i = 0; i < positionCount; i++)
        {
            float targetDistance = (float)i / positionCount * totalLength;

            // Find which segment this distance falls into
            int segment = 0;
            for (int s = 0; s < waypoints.Count; s++)
            {
                if (targetDistance >= cumulativeDistances[s] && targetDistance < cumulativeDistances[s + 1])
                {
                    segment = s;
                    break;
                }
            }

            // Interpolate within segment
            float segmentProgress = (targetDistance - cumulativeDistances[segment]) / segmentLengths[segment];
            int nextWaypoint = (segment + 1) % waypoints.Count;
            positions[i] = Vector3.Lerp(waypoints[segment], waypoints[nextWaypoint], segmentProgress);
        }

        return positions;
    }

    /// <summary>
    /// Find the closest point on track to a given position
    /// </summary>
    public Vector3 GetClosestPointOnTrack(Vector3 position, out float trackProgress)
    {
        var interpolated = GetInterpolatedPositions(200);

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
    /// Get track length in units
    /// </summary>
    public float GetTrackLength() => EstimateTrackLength();

#if UNITY_EDITOR
    // Draw the track path in the editor for easy visualization
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < waypoints.Count; i++)
        {
            int next = (i + 1) % waypoints.Count;
            Gizmos.DrawLine(waypoints[i], waypoints[next]);
            Gizmos.DrawWireSphere(waypoints[i], 5f);
        }

        // Highlight first waypoint
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(waypoints[0], 8f);
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        // Draw interpolated path when selected
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        var positions = GetInterpolatedPositions(100);

        for (int i = 0; i < positions.Length; i++)
        {
            int next = (i + 1) % positions.Length;
            Gizmos.DrawLine(positions[i], positions[next]);
        }
    }
#endif
}
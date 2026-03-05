using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

public class SpawnableDriftCourse : SpawnableBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
    [SerializeField] Vector3 blockScale = new Vector3(1, 3, 5);
    [SerializeField] float spawnDistance = 5f;
    [SerializeField] Vector3 Orgin;
    [SerializeField] int blocksPerSegment = 10;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = 2000;
        var points = new SpawnPoint[blockCount];

        var position = new Vector3(Orgin.x, Orgin.y, Orgin.z);
        Quaternion rotation = Quaternion.identity;

        for (int block = 0; block < blockCount; block++)
        {
            if (block % blocksPerSegment == 0)
            {
                ChangeDirection(position, out rotation);
            }

            var lookPosition = rotation * Vector3.forward;
            var rot = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
            points[block] = new SpawnPoint(position, rot, blockScale);

            var dir = rotation * Vector3.forward;
            position += spawnDistance * dir;
        }

        return points;
    }

    private Vector3 ChangeDirection(Vector3 direction, out Quaternion rotation)
    {
        float altitude = (float)rng.NextDouble() * (90 - 70) + 70;
        float azimuth = (float)rng.NextDouble() * 360;

        rotation = Quaternion.Euler(0f, 0f, azimuth) * Quaternion.Euler(0f, altitude, 0f);
        Vector3 newDirection = rotation * direction;
        return newDirection;
    }

    protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
    {
        foreach (var td in trailData)
            SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
    }

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(blockScale, spawnDistance, Orgin, blocksPerSegment);
    }
}

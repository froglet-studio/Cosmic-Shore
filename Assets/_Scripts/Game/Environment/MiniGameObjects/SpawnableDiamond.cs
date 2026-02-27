using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns a diamond (rhombus) shape made of prism trails.
/// Four-sided shape with configurable width and height. Scales with intensity.
/// </summary>
public class SpawnableDiamond : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    [Header("Diamond Parameters")]
    [SerializeField] float halfWidth = 12f;
    [SerializeField] float halfHeight = 20f;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = GetScaledBlockCount();
        float sizeMul = GetIntensitySizeMultiplier();
        float w = halfWidth * sizeMul;
        float h = halfHeight * sizeMul;

        // Four vertices: top, right, bottom, left
        var vertices = new Vector3[]
        {
            new(0f, h, 0f),
            new(w, 0f, 0f),
            new(0f, -h, 0f),
            new(-w, 0f, 0f),
        };

        // Calculate total perimeter for even distribution
        float totalLength = 0f;
        var segLengths = new float[4];
        for (int i = 0; i < 4; i++)
        {
            segLengths[i] = Vector3.Distance(vertices[i], vertices[(i + 1) % 4]);
            totalLength += segLengths[i];
        }

        var points = new SpawnPoint[blockCount];
        for (int i = 0; i < blockCount; i++)
        {
            float dist = (float)i / blockCount * totalLength;
            float accumulated = 0f;
            int seg = 0;

            for (seg = 0; seg < 3; seg++)
            {
                if (accumulated + segLengths[seg] > dist) break;
                accumulated += segLengths[seg];
            }

            float frac = (dist - accumulated) / segLengths[seg];
            var position = Vector3.Lerp(vertices[seg], vertices[(seg + 1) % 4], frac);
            var lookPosition = i == 0 ? position : points[i - 1].Position;
            var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
            points[i] = new SpawnPoint(position, rotation, Vector3.one);
        }

        return points;
    }

    protected override SpawnTrailData[] GenerateTrailData()
    {
        var pts = GeneratePoints();
        if (pts == null || pts.Length == 0)
            return System.Array.Empty<SpawnTrailData>();

        return new[] { new SpawnTrailData(pts, true, domain) };
    }

    protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
    {
        foreach (var td in trailData)
            SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
    }

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(halfWidth, halfHeight, baseBlockCount, intensityLevel, seed);
    }
}

using CosmicShore.Core;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns a lightning bolt shape made of prism trails.
/// Zigzag pattern with randomized offsets for organic feel.
/// Block count scales with intensity via GetScaledBlockCount().
/// </summary>
public class SpawnableLightning : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    [Header("Lightning Parameters")]
    [SerializeField] float height = 40f;
    [SerializeField] float width = 12f;
    [SerializeField] int zigzagSegments = 6;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = GetScaledBlockCount();
        float sizeMul = GetIntensitySizeMultiplier();

        // Generate zigzag vertices
        var vertices = new Vector3[zigzagSegments + 1];
        float scaledHeight = height * sizeMul;
        float scaledWidth = width * sizeMul;
        float segmentHeight = scaledHeight * 2f / zigzagSegments;

        for (int i = 0; i <= zigzagSegments; i++)
        {
            float y = scaledHeight - i * segmentHeight;
            float x;
            if (i == 0 || i == zigzagSegments)
                x = 0f; // Top and bottom centered
            else
                x = (i % 2 == 0) ? -scaledWidth * 0.5f : scaledWidth * 0.5f;

            // Add slight randomness for organic feel
            float jitter = (float)rng.NextDouble() * scaledWidth * 0.15f - scaledWidth * 0.075f;
            vertices[i] = new Vector3(x + jitter, y, 0f);
        }

        // Interpolate blocks along the zigzag path
        var points = new SpawnPoint[blockCount];
        float totalLength = 0f;
        var segLengths = new float[zigzagSegments];

        for (int i = 0; i < zigzagSegments; i++)
        {
            segLengths[i] = Vector3.Distance(vertices[i], vertices[i + 1]);
            totalLength += segLengths[i];
        }

        for (int i = 0; i < blockCount; i++)
        {
            float dist = (float)i / blockCount * totalLength;
            float accumulated = 0f;
            int seg = 0;

            for (seg = 0; seg < zigzagSegments - 1; seg++)
            {
                if (accumulated + segLengths[seg] > dist) break;
                accumulated += segLengths[seg];
            }

            float frac = (dist - accumulated) / segLengths[seg];
            var position = Vector3.Lerp(vertices[seg], vertices[seg + 1], frac);
            var lookPosition = i == 0 ? position : points[i - 1].Position;
            var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
            points[i] = new SpawnPoint(position, rotation, Vector3.one);
        }

        return points;
    }

    protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
    {
        foreach (var td in trailData)
            SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
    }

    protected override Prism GetPrismPrefab() => prism;

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(height, width, zigzagSegments, baseBlockCount, intensityLevel, seed, domain);
    }
}

using CosmicShore.Core;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns an infinity/figure-eight (lemniscate of Bernoulli) shape made of prism trails.
/// Block count and size scale with intensity.
/// </summary>
public class SpawnableInfinity : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    [Header("Infinity Parameters")]
    [SerializeField] float size = 18f;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = GetScaledBlockCount();
        float sizeMul = GetIntensitySizeMultiplier();
        float scaledSize = size * sizeMul;
        var points = new SpawnPoint[blockCount];

        for (int i = 0; i < blockCount; i++)
        {
            float t = (float)i / blockCount * Mathf.PI * 2f;
            // Lemniscate parametric form
            float denom = 1f + Mathf.Sin(t) * Mathf.Sin(t);
            float x = scaledSize * Mathf.Cos(t) / denom;
            float y = scaledSize * Mathf.Sin(t) * Mathf.Cos(t) / denom;
            var position = new Vector3(x, y, 0f);
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

    protected override Prism GetPrismPrefab() => prism;

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(size, baseBlockCount, intensityLevel, seed, domain);
    }
}

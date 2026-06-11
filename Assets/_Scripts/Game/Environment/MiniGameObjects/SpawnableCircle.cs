using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns a circle shape made of prism trails.
/// Block count scales with intensity via GetScaledBlockCount().
/// </summary>
public class SpawnableCircle : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    [Header("Circle Parameters")]
    [SerializeField] float radius = 16f;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = GetScaledBlockCount();
        float scaledRadius = radius * GetIntensitySizeMultiplier();
        var points = new SpawnPoint[blockCount];

        for (int i = 0; i < blockCount; i++)
        {
            float t = (float)i / blockCount * Mathf.PI * 2f;
            var position = new Vector3(Mathf.Cos(t) * scaledRadius, Mathf.Sin(t) * scaledRadius, 0f);
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

        // Circle is a closed loop
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
        return System.HashCode.Combine(radius, baseBlockCount, intensityLevel, seed, domain);
    }
}

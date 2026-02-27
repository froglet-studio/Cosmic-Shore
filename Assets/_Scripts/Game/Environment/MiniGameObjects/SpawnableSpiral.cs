using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns a spiral shape made of prism trails.
/// Starts from center and spirals outward. Block count and size scale with intensity.
/// </summary>
public class SpawnableSpiral : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    [Header("Spiral Parameters")]
    [SerializeField] float maxRadius = 20f;
    [SerializeField] float revolutions = 3f;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = GetScaledBlockCount();
        float sizeMul = GetIntensitySizeMultiplier();
        float scaledRadius = maxRadius * sizeMul;
        var points = new SpawnPoint[blockCount];

        for (int i = 0; i < blockCount; i++)
        {
            float t = (float)i / blockCount;
            float angle = t * revolutions * Mathf.PI * 2f;
            float r = t * scaledRadius;
            var position = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
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
        return System.HashCode.Combine(maxRadius, revolutions, baseBlockCount, intensityLevel, seed);
    }
}

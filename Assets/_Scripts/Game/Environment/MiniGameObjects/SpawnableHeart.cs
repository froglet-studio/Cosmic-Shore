using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns a heart shape made of prism trails.
/// Now extends SpawnableShapeBase for trigger collision + intensity scaling.
/// </summary>
public class SpawnableHeart : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = GetScaledBlockCount();
        float sizeMul = GetIntensitySizeMultiplier();
        var points = new SpawnPoint[blockCount];
        for (int block = 0; block < blockCount; block++)
        {
            var t = ((float)block / blockCount) * Mathf.PI * 2;
            var x = Mathf.Pow(Mathf.Sin(t), 3) * 16 * sizeMul;
            var y = ((13 * Mathf.Cos(t)) - (5 * Mathf.Cos(2 * t)) - (2 * Mathf.Cos(3 * t)) - (Mathf.Cos(4 * t))) * sizeMul;
            var position = new Vector3(x, y, 0);
            var rotation = SpawnPoint.LookRotation(position, Vector3.zero, Vector3.up);
            points[block] = new SpawnPoint(position, rotation, Vector3.one);
        }
        return points;
    }

    protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
    {
        foreach (var td in trailData)
            SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
    }

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(baseBlockCount, intensityLevel, seed);
    }
}

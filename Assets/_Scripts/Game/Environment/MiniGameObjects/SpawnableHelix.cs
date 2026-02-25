using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Models.Enums;

public class SpawnableHelix : SpawnableBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
    [SerializeField] Vector3 scale;
    [SerializeField] public float firstOrderRadius = 1;
    [SerializeField] public float secondOrderRadius = 1;
    [SerializeField] float blockCount = 150;

    protected override SpawnPoint[] GeneratePoints()
    {
        int count = (int)blockCount;
        var points = new SpawnPoint[count];

        var xc1 = NextFloat(4, 16);
        var xc2 = NextFloat(0.2f, 2f);
        var xc3 = NextFloat(-5, 5);
        var xc4 = NextFloat(1, 7);
        var yc1 = NextFloat(4, 16);
        var yc2 = NextFloat(0.2f, 2f);
        var yc3 = NextFloat(-5, 5);
        var yc4 = NextFloat(1, 7);

        for (int block = 0; block < count; block++)
        {
            var t = block / blockCount * Mathf.PI * 12;
            var x = firstOrderRadius * (Mathf.Sin(t) * xc1) + (secondOrderRadius * (Mathf.Sin(t * xc2 + xc3) * xc4));
            var y = firstOrderRadius * (Mathf.Cos(t) * yc1) + (secondOrderRadius * (Mathf.Cos(t * yc2 + yc3) * yc4));
            var position = new Vector3(x, y, t * 30f);

            var lookPosition = block == 0 ? position : points[block - 1].Position;
            var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);

            points[block] = new SpawnPoint(position, rotation, scale);
        }

        return points;
    }

    protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
    {
        foreach (var td in trailData)
            SpawnPrismTrail(td.Points, container, prism, td.IsLoop, Domains.Gold);
    }

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(firstOrderRadius, secondOrderRadius, blockCount, seed, scale);
    }

    private float NextFloat(float min, float max)
    {
        return (float)rng.NextDouble() * (max - min) + min;
    }
}

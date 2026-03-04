using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

public class SpawnableZigzag : SpawnableBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
    [SerializeField] float amplitude = 25;
    [SerializeField] float period = 26;
    [SerializeField] int blockCount = 160;

    protected override SpawnPoint[] GeneratePoints()
    {
        var points = new SpawnPoint[blockCount];

        var a = NextFloat(amplitude / 2f, amplitude * 2f);
        var p = NextFloat(period / 2f, period * 2f);
        var pOverTwo = p / 2f;

        for (int block = 0; block < blockCount; block++)
        {
            float t = block;
            float x;
            if (t % p == t % pOverTwo)
                x = (t % pOverTwo / pOverTwo) * a;
            else
                x = a - (t % p / p * a);

            var position = new Vector3(x, 0, t * 1.5f);
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
        return System.HashCode.Combine(amplitude, period, blockCount);
    }

    private float NextFloat(float min, float max)
    {
        return (float)rng.NextDouble() * (max - min) + min;
    }
}

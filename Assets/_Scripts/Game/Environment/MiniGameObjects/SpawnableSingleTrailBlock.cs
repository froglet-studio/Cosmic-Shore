using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

public class SpawnableSingleTrailBlock : SpawnableBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
    [SerializeField] Vector3 blockScale = Vector3.one;

    protected override SpawnPoint[] GeneratePoints()
    {
        return new[] { new SpawnPoint(Vector3.zero, Quaternion.LookRotation(Vector3.forward), blockScale) };
    }

    protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
    {
        foreach (var td in trailData)
            SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
    }

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(blockScale, seed);
    }
}

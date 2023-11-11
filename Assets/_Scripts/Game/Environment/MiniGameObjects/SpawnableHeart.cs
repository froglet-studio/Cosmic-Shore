
using CosmicShore.Core;
using UnityEngine;

public class SpawnableHeart : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    static int HeartsSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "HEART" + HeartsSpawned++;

        var trail = new Trail();

        int blockCount = 60;
        for (int block = 0; block < blockCount; block++)
        {
            var t = ((float)block / (float)blockCount) * Mathf.PI * 2;
            var x = Mathf.Pow(Mathf.Sin(t), 3) * 16;
            var y = (13 * Mathf.Cos(t)) - (5 * Mathf.Cos(2 * t)) - (2 * Mathf.Cos(3 * t)) - (Mathf.Cos(4 * t));
            var position = new Vector3(x, y, 0);
            CreateBlock(position, Vector3.zero, container.name + "::BLOCK::" + block, trail, Vector3.one, trailBlock, container);
        }

        trails.Add(trail);
        return container;
    }
}
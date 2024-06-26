
using CosmicShore.Core;
using UnityEngine;

public class SpawnableHelix : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] Vector3 scale;
    static int ObjectsSpawned = 0;
    [SerializeField] public float firstOrderRadius = 1;
    [SerializeField] public float secondOrderRadius = 1;
    [SerializeField] float blockCount = 150;

    public override GameObject Spawn()
    {
        GameObject container = new();
        container.name = "Wave" + ObjectsSpawned++;

        var trail = new Trail();

        var xc1 = Random.Range(4, 16);
        var xc2 = Random.Range(.2f, 2);
        var xc3 = Random.Range(-5, 5);
        var xc4 = Random.Range(1, 7);
        var yc1 = Random.Range(4, 16);
        var yc2 = Random.Range(.2f, 2);
        var yc3 = Random.Range(-5, 5);
        var yc4 = Random.Range(1, 7);
        for (int block = 0; block < blockCount; block++)
        {
            var t = block / blockCount * Mathf.PI * 12;
            var x = firstOrderRadius * (Mathf.Sin(t) * xc1) + (secondOrderRadius * (Mathf.Sin(t*xc2 + xc3) * xc4));
            var y = firstOrderRadius * (Mathf.Cos(t) * yc1) + (secondOrderRadius * (Mathf.Cos(t*yc2 + yc3) * yc4));
            var position = new Vector3(x, y, t*30f);
            var lookPosition = (block == 0) ? position : trail.GetBlock(block - 1).transform.position;
            CreateBlock(position, lookPosition, container.name + "::BLOCK::" + block, trail, scale, trailBlock, container, Teams.Gold);
        }

        trails.Add(trail);
        return container;
    }
}
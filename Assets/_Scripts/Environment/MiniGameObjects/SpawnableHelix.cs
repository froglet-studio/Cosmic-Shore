using StarWriter.Core;
using UnityEngine;

public class SpawnableHelix : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    static int ObjectsSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "Wave" + ObjectsSpawned++;

        var trail = new Trail();

        int blockCount = 160;
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
            var t = ((float)block / (float)blockCount) * Mathf.PI * 12;

            var x = (Mathf.Sin(t) * xc1) + (Mathf.Sin(t*xc2 + xc3) * xc4);
            var y = (Mathf.Cos(t) * yc1) + (Mathf.Cos(t*yc2 + yc3) * yc4);
            var position = new Vector3(x, y, t*2);
            CreateBlock(position, Vector3.zero, container.name + "::BLOCK::" + block, trail, Vector3.one, trailBlock, container);
        }

        trails.Add(trail);
        return container;
    }
}
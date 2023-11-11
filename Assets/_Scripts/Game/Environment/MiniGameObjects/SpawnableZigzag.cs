
using CosmicShore.Core;
using UnityEngine;

public class SpawnableZigzag : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] float amplitude = 25;
    [SerializeField] float period = 26;
    static int ObjectsSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "Zigzag" + ObjectsSpawned++;

        var trail = new Trail();
        int blockCount = 160;

        var a = Random.Range(amplitude/ 2f, amplitude * 2f);
        var p = Random.Range(period / 2f, period * 2f);
        var pOverTwo = p / 2f;

        for (int block = 0; block < blockCount; block++)
        {
            float t = block;
            float x;
            if (t % p == t % pOverTwo)
                x = (t%pOverTwo / pOverTwo) * a;
            else
                x = a - (t%p/p * a);

            var position = new Vector3(x, 0, t*1.5f);
            CreateBlock(position, Vector3.zero, container.name + "::BLOCK::" + block, trail, Vector3.one, trailBlock, container);
        }

        trails.Add(trail);
        return container;
    }
}
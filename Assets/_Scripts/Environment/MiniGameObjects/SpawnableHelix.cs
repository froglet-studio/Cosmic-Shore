using StarWriter.Core;
using UnityEngine;

public class SpawnableHelix : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] Vector3 scale;
    static int ObjectsSpawned = 0;

    public override GameObject Spawn(int difficultyLevel = 1)
    {
        GameObject container = new GameObject();
        container.name = "Wave" + ObjectsSpawned++;

        var trail = new Trail();

        float blockCount = 150;
        var xc1 = Random.Range(4, 16 * difficultyLevel);
        var xc2 = Random.Range(.2f, 2 * difficultyLevel);
        var xc3 = Random.Range(-5, 5 * difficultyLevel);
        var xc4 = Random.Range(1, 7 * difficultyLevel);
        var yc1 = Random.Range(4, 16 * difficultyLevel);
        var yc2 = Random.Range(.2f, 2 * difficultyLevel);
        var yc3 = Random.Range(-5, 5 * difficultyLevel);
        var yc4 = Random.Range(1, 7 * difficultyLevel);
        for (int block = 0; block < blockCount; block++)
        {
            var t = block / blockCount * Mathf.PI * 12;
            var x = (Mathf.Sin(t) * xc1) + (Mathf.Sin(t*xc2 + xc3) * xc4);
            var y = (Mathf.Cos(t) * yc1) + (Mathf.Cos(t*yc2 + yc3) * yc4);
            var position = new Vector3(x, y, t*30f);
            var lookPosition = position;
            if (block != 0) lookPosition = trail.GetBlock(block - 1).transform.position;
            CreateBlock(position, lookPosition, container.name + "::BLOCK::" + block, trail, scale, trailBlock, container);
        }

        

        trails.Add(trail);
        return container;
    }

    public override GameObject Spawn()
    {
        return Spawn(1);
    }
}
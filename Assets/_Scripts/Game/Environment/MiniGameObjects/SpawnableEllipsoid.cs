
using CosmicShore.Core;
using UnityEditor;
using UnityEngine;

public class SpawnableEllipsoid : SpawnableAbstractBase
{
    [Header("Trail")]
    [SerializeField] protected TrailBlock trailBlock;
    
    [Header("Spawnable Properties")]
    public float maxlength;
    public float maxwidth;
    public float maxheight;

    protected float length;
    protected float width;
    protected float height;
    

    static int SegmentsSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "Ellipsoid" + SegmentsSpawned++;

        length = ((float)rng.Next(1,100)) / 100* maxlength;
        width = ((float)rng.Next(1, 100)) / 100 * maxwidth;
        height = ((float)rng.Next(1, 100)) / 100 * maxheight;

        var trail1 = new Trail(true);
        var trail2 = new Trail(true);
        var trail3 = new Trail(true);

        int blockCount = 30;
        for (int block = 0; block < blockCount; block++)
        {
            var t = ((float)block / (float)blockCount) * Mathf.PI * 2;
            var x = (width / 2) * Mathf.Cos(t);
            var y = (height / 2) * Mathf.Sin(t);
            var position = new Vector3(x, y, 0);
            var lookPosition = position;
            if (block != 0) lookPosition = trail1.GetBlock(block - 1).transform.position;
            CreateBlock(position, lookPosition, container.name + "::BLOCK1::" + block, trail1, trailBlock.transform.localScale, trailBlock, container, Teams.Jade);
        }
        for (int block = 0; block < blockCount; block++)
        {
            var t = ((float)block / (float)blockCount) * Mathf.PI * 2;
            var x = (width / 2) * Mathf.Cos(t);
            var z = (length / 2) * Mathf.Sin(t);
            var position = new Vector3(x, 0, z);
            var lookPosition = position;
            if (block != 0) lookPosition = trail2.GetBlock(block - 1).transform.position;
            CreateBlock(position, lookPosition, container.name + "::BLOCK2::" + block, trail2, trailBlock.transform.localScale, trailBlock, container, Teams.Ruby);
        }
        for (int block = 0; block < blockCount; block++)
        {
            var t = ((float)block / (float)blockCount) * Mathf.PI * 2;
            var y = (height / 2) * Mathf.Cos(t);
            var z = (length / 2) * Mathf.Sin(t);
            var position = new Vector3(0, y, z);
            var lookPosition = position;
            if (block != 0) lookPosition = trail3.GetBlock(block - 1).transform.position;
            CreateBlock(position, lookPosition, container.name + "::BLOCK3::" + block, trail3, trailBlock.transform.localScale, trailBlock, container, Teams.Gold);
        }

        trails.Add(trail1);
        trails.Add(trail2);
        trails.Add(trail3);
        return container;
    }
}
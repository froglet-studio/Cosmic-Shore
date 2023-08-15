using StarWriter.Core;
using UnityEngine;

public class SpawnableEllipsoid : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] float length;
    [SerializeField] float width;
    [SerializeField] float height;

    
    

    static int SegmentsSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "HEART" + SegmentsSpawned++;

        //length *= (float)rng.Next(100);
        //width *= rng.Next(10) / 10;
        //height *= rng.Next(10) / 10;

        var trail = new Trail();

        int blockCount = 30;
        for (int block = 0; block < blockCount; block++)
        {
            var t = ((float)block / (float)blockCount) * Mathf.PI * 2;
            var x = (width / 2) * Mathf.Cos(t);
            var y = (height / 2) * Mathf.Sin(t);
            var position = new Vector3(x, y, 0);
            var lookPosition = position;
            if (block != 0) lookPosition = trail.GetBlock(block - 1).transform.position;
            CreateBlock(position, lookPosition, container.name + "::BLOCK1::" + block, trail, Vector3.one, trailBlock, container);
        }
        for (int block = 0; block < blockCount; block++)
        {
            var t = ((float)block / (float)blockCount) * Mathf.PI * 2;
            var x = (width / 2) * Mathf.Cos(t);
            var z = (length / 2) * Mathf.Sin(t);
            var position = new Vector3(x, 0, z);
            var lookPosition = position;
            if (block != 0) lookPosition = trail.GetBlock(block - 1).transform.position;
            CreateBlock(position, lookPosition, container.name + "::BLOCK2::" + block, trail, Vector3.one, trailBlock, container);
        }
        for (int block = 0; block < blockCount; block++)
        {
            var t = ((float)block / (float)blockCount) * Mathf.PI * 2;
            var y = (height / 2) * Mathf.Cos(t);
            var z = (length / 2) * Mathf.Sin(t);
            var position = new Vector3(0, y, z);
            var lookPosition = position;
            if (block != 0) lookPosition = trail.GetBlock(block - 1).transform.position;
            CreateBlock(position, lookPosition, container.name + "::BLOCK3::" + block, trail, Vector3.one, trailBlock, container);
        }

        trails.Add(trail);
        return container;
    }
}
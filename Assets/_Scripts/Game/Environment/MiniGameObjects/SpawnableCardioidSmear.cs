
using CosmicShore.Core;
using UnityEditor;
using UnityEngine;

public class SpawnableCardioidSmear : SpawnableEllipsoid
{
    //[SerializeField] TrailBlock trailBlock;

    //float length;
    //float width;
    //float height;

    static int SegmentsSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "Pumpkin" + SegmentsSpawned++;

        length = ((float)rng.Next(1, 100)) / 100 * maxlength;
        width = ((float)rng.Next(1, 100)) / 100 * maxwidth;
        height = ((float)rng.Next(1, 100)) / 100 * maxheight;

        int cardioidCount = 12;
        float offsetAngle = Mathf.PI * 2 / cardioidCount;

        for (int cardioidIndex = 0; cardioidIndex < cardioidCount; cardioidIndex++)
        {
            var trail = new Trail(true);
            int blockCount = 30;

            for (int block = 0; block < blockCount; block++)
            {
                var t = ((float)block / (float)blockCount) * Mathf.PI * 2;
                var r = width * (1 - Mathf.Sin(t));

                // Updated cardioid to rotate around Z-axis
                var x = r * Mathf.Cos(t + offsetAngle * cardioidIndex);
                var y = r * Mathf.Sin(t + offsetAngle * cardioidIndex);
                var position = new Vector3(x, y, 0);

                var lookPosition = position;
                if (block != 0) lookPosition = trail.GetBlock(block - 1).transform.position;
                CreateBlock(position, lookPosition, container.name + "::BLOCK" + cardioidIndex + "::" + block, trail, trailBlock.transform.localScale, trailBlock, container);
            }

            trails.Add(trail);
        }

        return container;
    }
}

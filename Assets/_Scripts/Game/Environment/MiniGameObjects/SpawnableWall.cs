using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;


public class SpawnableWall : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] int width = 10;
    [SerializeField] int height = 5;
    [SerializeField] float blockSize = 1f;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject("Wall");
        var trail = new Trail();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 position = new Vector3(x * blockSize, y * blockSize, 0);
                CreateBlock(position, Vector3.forward, container.name + $"::BLOCK::{x}:{y}", trail, Vector3.one * blockSize, trailBlock, container);
            }
        }

        trails.Add(trail);
        return container;
    }
}


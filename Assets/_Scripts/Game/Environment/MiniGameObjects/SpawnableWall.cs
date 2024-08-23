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
    [SerializeField] float padding = .1f;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject("Wall");
        var trail = new Trail();
        var size = new Vector3(1, 1, .1f);
        var blockSpacing = blockSize + padding;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 position = new Vector3(x * blockSpacing, y * blockSpacing, 0);
                var correction = new Vector3(blockSpacing * .5f , blockSpacing * .5f, 0);
                CreateBlock(position + correction, Vector3.up * float.MaxValue, container.name + $"::BLOCK::{x}:{y}", trail, size * blockSize, trailBlock, container, Vector3.forward*float.MaxValue);
            }
        }

        trails.Add(trail);
        return container;
    }
}


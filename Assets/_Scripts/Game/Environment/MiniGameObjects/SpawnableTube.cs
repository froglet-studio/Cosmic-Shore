using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore
{
    public class SpawnableTube : SpawnableAbstractBase
    {
        [SerializeField] TrailBlock trailBlock;
        [SerializeField] int radius = 3;
        [SerializeField] int length = 20;
        [SerializeField] int segments = 8;
        [SerializeField] float blockSize = 1f;

        public override GameObject Spawn()
        {
            GameObject container = new GameObject("Tube");
            var trail = new Trail();

            for (int z = 0; z < length; z++)
            {
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * (2 * Mathf.PI / segments);
                    Vector3 position = new Vector3(
                        Mathf.Cos(angle) * radius * blockSize,
                        Mathf.Sin(angle) * radius * blockSize,
                        z * blockSize
                    );
                    CreateBlock(position, -position.normalized, container.name + $"::BLOCK::{z}:{i}", trail, Vector3.one * blockSize, trailBlock, container);
                }
            }

            trails.Add(trail);
            return container;
        }
    }
}

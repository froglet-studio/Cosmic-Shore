using CosmicShore.Core;
using UnityEngine;

public class SpawnableSingleTrailBlock : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] Vector3 blockScale = Vector3.one;
    static int BlocksSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new();
        container.name = "SingleTrailBlock" + BlocksSpawned++;

        var trail = new Trail();
        CreateBlock(Vector3.zero, Vector3.forward, container.name + "::BLOCK::0", trail, blockScale, trailBlock, container);

        trails.Add(trail);
        return container;
    }
}

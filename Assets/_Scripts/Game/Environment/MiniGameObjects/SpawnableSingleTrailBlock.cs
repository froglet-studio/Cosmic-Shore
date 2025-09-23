using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Serialization;

public class SpawnableSingleTrailBlock : SpawnableAbstractBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
    [SerializeField] Vector3 blockScale = Vector3.one;
    static int BlocksSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new();
        container.name = "SingleTrailBlock" + BlocksSpawned++;

        var trail = new Trail();
        CreateBlock(Vector3.zero, Vector3.forward, container.name + "::BLOCK::0", trail, blockScale, prism, container);

        trails.Add(trail);
        return container;
    }
}

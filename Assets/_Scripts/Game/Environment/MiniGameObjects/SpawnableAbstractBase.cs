using CosmicShore.Core;
using System.Collections.Generic;

using UnityEngine;



public abstract class SpawnableAbstractBase : MonoBehaviour
{
    protected System.Random rng = new System.Random();
    protected int Seed;
    protected List<Trail> trails = new List<Trail>();


    public abstract GameObject Spawn();

    public virtual void SetSeed(int seed)
    {
        Seed = seed;
        rng = new System.Random(Seed);
    }
    public virtual List<Trail> GetTrails()
    {
        return trails;
    }

    protected virtual void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, TrailBlock trailBlock, GameObject container, Teams team = Teams.Blue)
    {
        CreateBlock(position, lookPosition, blockId, trail, scale, trailBlock, container, Vector3.up, team);
    }

    protected virtual void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, TrailBlock trailBlock, GameObject container, Vector3 up, Teams team = Teams.Blue)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = team;
        Block.ownerId = "public";
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - position, up));
        Block.transform.SetParent(container.transform, false);
        Block.ID = blockId;
        Block.TargetScale = scale;
        Block.Trail = trail;
        trail.Add(Block);
    }
}
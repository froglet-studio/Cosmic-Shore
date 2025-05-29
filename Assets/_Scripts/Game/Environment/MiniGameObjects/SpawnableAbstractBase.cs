using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

public abstract class SpawnableAbstractBase : MonoBehaviour
{
    protected System.Random rng = new System.Random();
    protected int Seed;
    protected List<Trail> trails = new List<Trail>();
    public Teams Team = Teams.Blue;

    public virtual GameObject Spawn(int intensityLevel = 1)
    {
        return Spawn();
    }

    public abstract GameObject Spawn();
    public virtual GameObject Spawn(Vector3 position, Quaternion rotation, Teams team)
    {
        transform.position = position;
        transform.rotation = rotation;

        Team = team;
 

        return Spawn();
    }

    public virtual void SetSeed(int seed)
    {
        Seed = seed;
        rng = new System.Random(Seed);
    }
    public virtual List<Trail> GetTrails()
    {
        return trails;
    }

    protected virtual void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, TrailBlock trailBlock, GameObject container, Teams? team = null)
    {
        CreateBlock(position, lookPosition, blockId, trail, scale, trailBlock, container, Vector3.up, team);
    }

    protected virtual void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, TrailBlock trailBlock, GameObject container, Vector3 up, Teams? team = null, bool relativeLook = true, bool flip = true)
    {
        Teams actualTeam = team ?? Team;

        var Block = Instantiate(trailBlock);
        Block.ChangeTeam(actualTeam);
        Block.ownerID = "public";
        if (relativeLook) Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(flip ? position - lookPosition : lookPosition - position, up));
        else Block.transform.SetPositionAndRotation(position, Quaternion.identity);
        Block.transform.SetParent(container.transform, false);
        Block.ownerID = blockId;
        Block.TargetScale = scale;
        Block.Trail = trail;
        trail.Add(Block);
    }
}
using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class SpawnableAbstractBase : MonoBehaviour
{
    protected System.Random rng = new System.Random();
    protected int Seed;
    protected List<Trail> trails = new List<Trail>();
    [FormerlySerializedAs("Team")] public Domains domain = Domains.Blue;
    public abstract GameObject Spawn();

    public virtual GameObject Spawn(int intensityLevel = 1)
    {
        return Spawn();
    }

    public virtual GameObject Spawn(Vector3 position, Quaternion rotation, Domains domain)
    {
        transform.position = position;
        transform.rotation = rotation;
        this.domain = domain;

        return Spawn();
    }

    public virtual GameObject Spawn(Vector3 position, Quaternion rotation, Domains domain, int intensityLevel = 1)
    {
        transform.position = position;
        transform.rotation = rotation;
        this.domain = domain;

        return Spawn(intensityLevel);
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

    protected virtual void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, Prism prism, GameObject container, Domains? team = null)
    {
        CreateBlock(position, lookPosition, blockId, trail, scale, prism, container, Vector3.up, team);
    }

    protected virtual void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, Prism prism, GameObject container, Vector3 up, Domains? team = null, bool relativeLook = true, bool flip = true)
    {
        Domains actualDomain = team ?? domain;

        var Block = Instantiate(prism);
        Block.ChangeTeam(actualDomain);
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
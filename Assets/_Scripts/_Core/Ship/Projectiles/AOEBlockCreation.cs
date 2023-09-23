using System.Collections;
using System.Collections.Generic;
using StarWriter.Core;
using UnityEngine;
using StarWriter.Core.HangerBuilder;

public class AOEBlockCreation : AOEExplosion
{
    [SerializeField] protected TrailBlock trailBlock;
    [SerializeField] float blockCount = 8; // TODO: make int
    [SerializeField] int ringCount = 3;
    [SerializeField] float radius = 30f;
    [SerializeField] protected Vector3 blockScale = new Vector3(20f, 10f, 5f);
    [SerializeField] bool shielded = true;
    protected Material blockMaterial;
    protected List<Trail> trails = new List<Trail>();

    protected override void Start()
    {
        if (shielded) blockMaterial = Hangar.Instance.GetTeamShieldedBlockMaterial(Ship.Team);
        else blockMaterial = Ship.TrailSpawner.GetBlockMaterial();
        base.Start();
    }

    protected override IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);
        
        for (int ring = 0; ring < ringCount; ring++)
        {
            trails.Add(new Trail());
            for (int block = 0; block < blockCount; block++)
                CreateRingBlock(block, ring%2*.5f, ring/2f + 1f, ring, -ring/2f, trails[ring]);
        }
    }

    virtual protected void CreateRingBlock(int i, float phase, float scale, float tilt, float sweep, Trail trail)
    {
        var offset = scale * radius * Mathf.Cos(((i + phase) / blockCount) * 2 * Mathf.PI) * transform.right +
                     scale * radius * Mathf.Sin(((i + phase) / blockCount) * 2 * Mathf.PI) * transform.up +
                     sweep * radius * transform.forward;
        CreateBlock(transform.position + offset, offset + tilt * radius * transform.forward, transform.forward, "::AOE::" + Time.time + "::" + i, trail);
    }

    virtual protected TrailBlock CreateBlock(Vector3 position, Vector3 forward, Vector3 up, string ownerId, Trail trail)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = Team;
        Block.ownerId = Ship.Player.PlayerUUID;
        Block.PlayerName = Ship.Player.PlayerName;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
        Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.ID = Block.ownerId + ownerId + position;  
        Block.TargetScale = blockScale;
        Block.transform.parent = TrailSpawner.TrailContainer.transform;
        Block.Trail = trail;
        if (shielded) Block.Shielded = true;
        trail.Add(Block);
        return Block;
    }
}
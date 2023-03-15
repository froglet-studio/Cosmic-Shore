using System.Collections;
using System.Collections.Generic;
using StarWriter.Core;
using UnityEngine;
using UnityEngine.Serialization;

public class AOEBlockCreation : AOEExplosion
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] float blockCount = 8; // TODO: make int
    [SerializeField] int ringCount = 3;
    [SerializeField] float radius = 30f;
    [SerializeField] Vector3 blockScale = new Vector3(20f, 10f, 5f);
    Material blockMaterial;
    List<Trail> trails = new List<Trail>();

    public void SetBlockMaterial(Material material)
    {
        blockMaterial = material;
    }

    protected override IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);
        
        for (int ring = 0; ring < ringCount; ring++)
        {
            trails.Add(new Trail());
            for (int block = 0; block < blockCount; block++)
            {
                CreateRingBlock(block, ring%2*.5f, ring/2f + 1f, ring, -ring/2f, trails[ring]);
            }
        }
        yield return new WaitForEndOfFrame();
    }

    void CreateRingBlock(int i, float phase, float scale, float tilt, float sweep, Trail trail)
    {
        var position = transform.position +
                             scale * radius * Mathf.Cos(((i + phase) / blockCount) * 2 * Mathf.PI) * transform.right +
                             scale * radius * Mathf.Sin(((i + phase) / blockCount) * 2 * Mathf.PI) * transform.up +
                             sweep * radius * transform.forward;
        CreateBlock(position, position + tilt * radius * transform.forward, "::AOE::" + Time.time + "::" + i, trail);
    }

    void CreateBlock(Vector3 position, Vector3 lookPosition, string ownerId, Trail trail)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = Team;
        Block.ownerId = Ship.Player.PlayerUUID;
        Block.PlayerName = Ship.Player.PlayerName;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.ID = Block.ownerId + ownerId + position;  
        Block.InnerDimensions = blockScale;
        Block.transform.parent = TrailSpawner.TrailContainer.transform;
        Block.Trail = trail;
        trail.Add(Block);
    }
}
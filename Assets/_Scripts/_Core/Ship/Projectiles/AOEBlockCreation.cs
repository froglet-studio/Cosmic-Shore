using System.Collections;
using StarWriter.Core;
using UnityEngine;

public class AOEBlockCreation : AOEExplosion
{
    [SerializeField] Trail trail;
    [SerializeField] float blockCount = 8;
    [SerializeField] float radius = 30f;
    [SerializeField] Vector3 blockScale = new Vector3(20f, 10f, 5f);
    Material blockMaterial;

    public void SetBlockMaterial(Material material)
    {
        blockMaterial = material;
    }

    protected override IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);

        for (int i = 0; i < blockCount; i++)
        {
            CreateRing(i,   0,    1, 0, 0);
            CreateRing(i, .5f, 1.5f, 1, -.5f);
            CreateRing(i,   0,   2f, 2, -1);
        }

        yield return new WaitForEndOfFrame();
    }

    void CreateRing(int i, float phase, float scale, float tilt, float sweep)
    {
        var position = transform.position +
                             scale * radius * Mathf.Cos(((i + phase) / blockCount) * 2 * Mathf.PI) * transform.right +
                             scale * radius * Mathf.Sin(((i + phase) / blockCount) * 2 * Mathf.PI) * transform.up +
                             sweep * radius * transform.forward;
        CreateBlock(position, position + tilt * radius * transform.forward, "::AOE::" + Time.time + "::" + i);
    }

    void CreateBlock(Vector3 position, Vector3 lookPosition, string ownerId)
    {
        var Block = Instantiate(trail);
        Block.Team = Team;
        Block.ownerId = Ship.Player.PlayerUUID;
        Block.PlayerName = Ship.Player.PlayerName;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.ID = Block.ownerId + ownerId;  
        Block.InnerDimensions = blockScale;
        Block.transform.parent = TrailSpawner.TrailContainer.transform;
    }
}
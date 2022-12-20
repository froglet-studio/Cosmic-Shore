using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AOEBlockCreation : AOEExplosion
{
    [SerializeField] Trail trail;
    private Material blockMaterial;
    float blockCount = 8;
    float radius = 30f;

    public void SetBlockMaterial(Material material)
    {
        blockMaterial = material;
    }

    protected override IEnumerator ExplodeCoroutine()
    {
        for (int i = 0; i < blockCount; i++)
        {
            var Block = Instantiate(trail);
            Block.Team = Team;
            Block.PlayerName = Ship.Player.PlayerName;
            Block.MaxScale = 5;
            var position = transform.position +
                              radius * Mathf.Cos((i / blockCount) * 2 * Mathf.PI) * transform.right +
                              radius * Mathf.Sin((i / blockCount) * 2 * Mathf.PI) * transform.up;
            Block.transform.SetPositionAndRotation(position,
                                                   Quaternion.LookRotation(position-transform.position, transform.forward));
            Block = Instantiate(trail);
            Block.Team = Team;
            Block.PlayerName = Ship.Player.PlayerName;
            Block.MaxScale = 5;
            position = transform.position +
                              1.5f * radius * Mathf.Cos(((i + .5f) / blockCount) * 2 * Mathf.PI) * transform.right +
                              1.5f * radius * Mathf.Sin(((i + .5f) / blockCount) * 2 * Mathf.PI) * transform.up;
            Block.transform.SetPositionAndRotation(position,
                                                   Quaternion.LookRotation(position - transform.position, transform.forward));


        }
        yield return new WaitForEndOfFrame();
    }
}

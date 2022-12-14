using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AOEBlockCreation : AOEExplosion
{
    [SerializeField] Trail trail;
    private Material blockMaterial;
    int blockCount = 8;
    float radius = 10f;

    public void SetBlockMaterial(Material material)
    {
        blockMaterial = material;
    }

    protected override IEnumerator ExplodeCoroutine()
    {
        for (int i = 0;i<blockCount;i++)
        {
            var Block = Instantiate(trail);
            Block.Team = Team;
            Block.MaxScale = MaxScale;
            Block.transform.SetPositionAndRotation(transform.position + new Vector3(
                                                        radius * Mathf.Cos((i / blockCount) * 360),
                                                        radius * Mathf.Sin((i / blockCount) * 360),
                                                        0),
                                                   Quaternion.LookRotation(transform.position));
        }
        yield return new WaitForEndOfFrame();
    }
}

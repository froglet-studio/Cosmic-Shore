using CosmicShore;
using System.Collections;
using CosmicShore.Game;
using UnityEngine;

public abstract class Flora : LifeForm
{
    [SerializeField] Vector3 leafSize = new Vector3(4f, 4f, 1f);
    [SerializeField] protected float growPeriod = 3f;
    [SerializeField] public float PlantPeriod = 15f;
    [SerializeField] float stunDuration = 1f;
    protected bool isGrowing = true;

    public abstract void Grow();

    public abstract void Plant();

    public override void AddHealthBlock(HealthBlock healthBlock)
    {
        base.AddHealthBlock(healthBlock);
        healthBlock.TargetScale = leafSize;
    }

    public override void Initialize(Cell cell)
    {
        base.Initialize(cell);
        Plant();
        StartCoroutine(GrowCoroutine());
    }

    public override void RemoveHealthBlock(HealthBlock healthBlock)
    {
        base.RemoveHealthBlock(healthBlock);
        isGrowing = false;
    }

    IEnumerator GrowCoroutine()
    {
        while (true)
        {
            if (isGrowing)
            {
                Grow();
                yield return new WaitForSeconds(growPeriod);
            }
            else
            {
                isGrowing = true;
                yield return new WaitForSeconds(stunDuration);
            }
        }
    }
}

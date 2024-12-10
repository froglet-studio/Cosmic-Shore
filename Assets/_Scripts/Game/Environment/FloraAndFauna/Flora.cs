using CosmicShore;
using System.Collections;
using UnityEngine;

public abstract class Flora : LifeForm
{
    [SerializeField] protected float growPeriod = 3f;
    [SerializeField] public float PlantPeriod = 15f;
    [SerializeField] float stunDuration = 1f;
    protected bool isGrowing = true;

    public abstract void Grow();

    public abstract void Plant();

    protected override void Start()
    {
        base.Start();
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

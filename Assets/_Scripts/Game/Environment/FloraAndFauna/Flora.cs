using CosmicShore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Flora : LifeForm
{
    [SerializeField] protected float growPeriod = 3f;
    [SerializeField] public float PlantPeriod = 15f;

    public abstract void Grow();

    public abstract void Plant();

    protected override void Start()
    {
        base.Start();
        Plant();
        StartCoroutine(GrowCoroutine());
    }

    IEnumerator GrowCoroutine()
    {
        while (true)
        {
            Grow();
            yield return new WaitForSeconds(growPeriod);
        }
    }
}

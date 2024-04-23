using CosmicShore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Flora : LifeForm
{

    [SerializeField] protected float growPeriod = 3f;

    public abstract void Grow();

    protected override void Start()
    {
        base.Start();
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

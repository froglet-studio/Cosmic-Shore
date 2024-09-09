using System.Collections;
using UnityEngine;

public class GrowActionBase : ShipAction
{
    protected float minSize;
    [SerializeField] protected ElementalFloat maxSize;
    [SerializeField] protected float growRate;
    [SerializeField] protected ElementalFloat shrinkRate = new ElementalFloat(1);
    [SerializeField] protected GameObject target;

    protected Coroutine returnToNeutralCoroutine;
    protected Coroutine growCoroutine;
    protected bool growing;

    protected override void Start()
    {
        base.Start();
        minSize = target.transform.localScale.z;
    }

    public override void StartAction()
    {
        if (returnToNeutralCoroutine != null)
        {
            StopCoroutine(returnToNeutralCoroutine);
            returnToNeutralCoroutine = null;
        }

        growing = true;
        growCoroutine = StartCoroutine(GrowCoroutine());
    }

    public override void StopAction()
    {
        if (growCoroutine != null)
        {
            StopCoroutine(growCoroutine);
            growCoroutine = null;
        }

        growing = false;
        returnToNeutralCoroutine = StartCoroutine(ReturnToNeutralCoroutine());
    }

    protected virtual IEnumerator GrowCoroutine()
    {
        while (growing && target.transform.localScale.z < maxSize.Value)
        {
            target.transform.localScale += Time.deltaTime * growRate * Vector3.one;
            yield return null;
        }
    }

    protected virtual IEnumerator ReturnToNeutralCoroutine()
    {
        while (target.transform.localScale.z > minSize)
        {
            target.transform.localScale -= Time.deltaTime * shrinkRate.Value * Vector3.one;
            yield return null;
        }
        target.transform.localScale = minSize * Vector3.one;
    }
}
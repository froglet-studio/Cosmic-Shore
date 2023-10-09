using System.Collections;
using UnityEngine;
public class DriftTrailAction : ShipAction
{
    TrailSpawner trailSpawner;

    protected override void Start()
    {
        base.Start();
        trailSpawner = ship.GetComponent<TrailSpawner>();
    }

    public override void StartAction()
    {
        StartCoroutine(UpdateDotProductCoroutine());
    }

    public override void StopAction()
    {
        StopCoroutine(UpdateDotProductCoroutine());
        trailSpawner.SetDotProduct(1);
    }


    IEnumerator UpdateDotProductCoroutine()
    {
        while (true) 
        {
            trailSpawner.SetDotProduct(Vector3.Dot(ship.ShipStatus.Course, ship.transform.forward));
            yield return null;
        }
    }
}

using CosmicShore.Core;
using System.Collections;
using UnityEngine;
public class DriftTrailAction : ShipAction
{
    #region Events
    public delegate void ChangeDriftAltitude(float dotproduct);
    public event ChangeDriftAltitude OnChangeDriftAltitude;
    #endregion

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
        StopAllCoroutines();
        trailSpawner.SetDotProduct(1);
        if (!ship.ShipStatus.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(1);
    }


    IEnumerator UpdateDotProductCoroutine()
    {
        while (true) 
        {
            var driftAltitude = Vector3.Dot(ship.ShipStatus.Course, ship.transform.forward);
            if (!ship.ShipStatus.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(driftAltitude);
            trailSpawner.SetDotProduct(driftAltitude);
            yield return new WaitForSeconds(.1f);
        }
    }



}

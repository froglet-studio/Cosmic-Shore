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
        trailSpawner = Ship.TrailSpawner;
    }

    public override void StartAction()
    {
        StartCoroutine(UpdateDotProductCoroutine());
    }

    public override void StopAction()
    {
        StopAllCoroutines();
        trailSpawner.SetDotProduct(1);
        if (!Ship.ShipStatus.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(1);
    }


    IEnumerator UpdateDotProductCoroutine()
    {
        while (true) 
        {
            var driftAltitude = Vector3.Dot(Ship.ShipStatus.Course, Ship.Transform.forward);
            if (!Ship.ShipStatus.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(driftAltitude);
            trailSpawner.SetDotProduct(driftAltitude);
            yield return new WaitForSeconds(.05f);
        }
    }



}

using CosmicShore.Core;
using CosmicShore.Game;
using System.Collections;
using UnityEngine;
public class DriftTrailAction : ShipAction
{
    #region Events
    public delegate void ChangeDriftAltitude(float dotproduct);
    public event ChangeDriftAltitude OnChangeDriftAltitude;
    #endregion

    TrailSpawner trailSpawner => ShipStatus.TrailSpawner;

    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
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

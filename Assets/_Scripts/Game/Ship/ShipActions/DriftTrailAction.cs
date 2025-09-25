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

    PrismSpawner PrismSpawner => VesselStatus.PrismSpawner;

    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);
    }

    public override void StartAction()
    {
        StartCoroutine(UpdateDotProductCoroutine());
    }

    public override void StopAction()
    {
        StopAllCoroutines();
        PrismSpawner.SetDotProduct(1);
        if (!Vessel.VesselStatus.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(1);
    }


    IEnumerator UpdateDotProductCoroutine()
    {
        while (true) 
        {
            var driftAltitude = Vector3.Dot(Vessel.VesselStatus.Course, Vessel.Transform.forward);
            if (!Vessel.VesselStatus.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(driftAltitude);
            PrismSpawner.SetDotProduct(driftAltitude);
            yield return new WaitForSeconds(.05f);
        }
    }
}

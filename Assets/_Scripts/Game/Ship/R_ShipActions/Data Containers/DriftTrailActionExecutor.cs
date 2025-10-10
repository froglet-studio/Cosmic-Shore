using System.Collections;
using CosmicShore.Game;
using UnityEngine;

public sealed class DriftTrailActionExecutor : ShipActionExecutorBase
{
    public delegate void ChangeDriftAltitude(float dotproduct);
    public event ChangeDriftAltitude OnChangeDriftAltitude;

    IVessel _ship;
    IVesselStatus _status;
    VesselPrismController vesselPrismController;
    Coroutine _loop;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _ship = shipStatus.Vessel;
        vesselPrismController = _status.VesselPrismController;
    }

    public void Begin(DriftTrailActionSO so, IVessel ship, IVesselStatus status)
    {
        if (_loop != null) return;
        _loop = StartCoroutine(UpdateDotProductCoroutine());
    }

    public void End(DriftTrailActionSO so, IVessel ship, IVesselStatus status)
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        vesselPrismController?.SetDotProduct(1f);
        if (!_status.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(1f);
    }

    IEnumerator UpdateDotProductCoroutine()
    {
        while (true)
        {
            var driftAltitude = Vector3.Dot(_status.Course, _ship.Transform.forward);
            if (!_status.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(driftAltitude);
            vesselPrismController?.SetDotProduct(driftAltitude);
            yield return new WaitForSeconds(0.05f);
        }
    }
}
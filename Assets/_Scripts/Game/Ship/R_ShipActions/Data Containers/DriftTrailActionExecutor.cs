using System.Collections;
using CosmicShore.Game;
using UnityEngine;

public sealed class DriftTrailActionExecutor : ShipActionExecutorBase
{
    public delegate void ChangeDriftAltitude(float dotproduct);
    public event ChangeDriftAltitude OnChangeDriftAltitude;

    IShip _ship;
    IShipStatus _status;
    TrailSpawner _trailSpawner;
    Coroutine _loop;

    public override void Initialize(IShipStatus shipStatus)
    {
        _status = shipStatus;
        _ship = shipStatus.Ship;
        _trailSpawner = _status.TrailSpawner;
    }

    public void Begin(DriftTrailActionSO so, IShip ship, IShipStatus status)
    {
        if (_loop != null) return;
        _loop = StartCoroutine(UpdateDotProductCoroutine());
    }

    public void End(DriftTrailActionSO so, IShip ship, IShipStatus status)
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        _trailSpawner?.SetDotProduct(1f);
        if (!_status.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(1f);
    }

    IEnumerator UpdateDotProductCoroutine()
    {
        while (true)
        {
            var driftAltitude = Vector3.Dot(_status.Course, _ship.Transform.forward);
            if (!_status.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(driftAltitude);
            _trailSpawner?.SetDotProduct(driftAltitude);
            yield return new WaitForSeconds(0.05f);
        }
    }
}
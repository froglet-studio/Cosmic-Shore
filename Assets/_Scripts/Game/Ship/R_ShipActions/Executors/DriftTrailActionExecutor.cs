using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using CosmicShore.Game;
using UnityEngine;

public sealed class DriftTrailActionExecutor : ShipActionExecutorBase
{
    public delegate void ChangeDriftAltitude(float dotproduct);
    public event ChangeDriftAltitude OnChangeDriftAltitude;

    [Header("Events")]
    [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

    IVessel _ship;
    IVesselStatus _status;
    VesselPrismController vesselPrismController;

    CancellationTokenSource _cts;

    void OnEnable()
    {
        if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _ship   = shipStatus.Vessel;
        vesselPrismController = _status.VesselPrismController;
    }

    public void Begin(DriftTrailActionSO so, IVessel ship, IVesselStatus status)
    {
        if (_cts != null) return;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        UpdateDotProductLoopAsync(_cts.Token).Forget();
    }

    public void End(DriftTrailActionSO so, IVessel ship, IVesselStatus status) => EndInternal();

    void EndInternal()
    {
        if (_cts != null)
        {
            try { _cts.Cancel(); }
            catch
            {
                //
            }

            _cts.Dispose();
            _cts = null;
        }

        vesselPrismController?.SetDotProduct(1f);
        if (_status != null && !_status.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(1f);
    }

    void OnTurnEndOfMiniGame() => EndInternal();

    async UniTaskVoid UpdateDotProductLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var driftAltitude = Vector3.Dot(_status.Course, _ship.Transform.forward);
                if (!_status.AutoPilotEnabled) OnChangeDriftAltitude?.Invoke(driftAltitude);
                vesselPrismController?.SetDotProduct(driftAltitude);

                await UniTask.Delay(TimeSpan.FromSeconds(0.05f),
                                    DelayType.DeltaTime,
                                    PlayerLoopTiming.Update,
                                    token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogError($"[DriftTrail] loop error: {e}"); }
    }
}

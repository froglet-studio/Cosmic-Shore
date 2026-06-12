using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using CosmicShore.Game;
using UnityEngine;

/// <summary>
/// Temporarily disables the vessel's geometry colliders, letting it phase through
/// obstacles. Colliders always re-enable after the action's max duration, on early
/// release, or if the executor is disabled mid-phase.
/// </summary>
public sealed class GhostActionExecutor : ShipActionExecutorBase
{
    IVesselStatus _status;
    CancellationTokenSource _cts;

    public bool IsPhasing { get; private set; }

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
    }

    void OnDisable() => End();

    public void Begin(GhostActionSO so, IVesselStatus status)
    {
        End();

        if (!isActiveAndEnabled || so == null)
            return;

        _status = status ?? _status;
        if (_status?.ShipGeometries == null || _status.ShipGeometries.Count == 0)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        PhaseAsync(so.MaxDuration, _cts.Token).Forget();
    }

    public void End()
    {
        if (_cts != null)
        {
            try
            {
                if (!_cts.IsCancellationRequested)
                    _cts.Cancel();
            }
            catch
            {
                // no-op
            }

            _cts.Dispose();
            _cts = null;
        }

        SetCollidersEnabled(true);
        IsPhasing = false;
    }

    async UniTaskVoid PhaseAsync(float duration, CancellationToken token)
    {
        IsPhasing = true;
        SetCollidersEnabled(false);

        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(duration),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                token);
        }
        catch (OperationCanceledException)
        {
            // Released early or torn down — colliders restored below / in End()
        }
        finally
        {
            SetCollidersEnabled(true);
            IsPhasing = false;
        }
    }

    void SetCollidersEnabled(bool enabled)
    {
        var geometries = _status?.ShipGeometries;
        if (geometries == null) return;

        foreach (var geometry in geometries)
        {
            if (!geometry) continue;
            if (geometry.TryGetComponent(out Collider collider))
                collider.enabled = enabled;
        }
    }
}

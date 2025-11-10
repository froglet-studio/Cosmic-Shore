using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;
using Obvious.Soap;

public sealed class OverheatingActionExecutor : ShipActionExecutorBase
{
    public event Action OnHeatBuildStarted;
    public event Action OnOverheated;
    public event Action OnHeatDecayStarted;
    public event Action OnHeatDecayCompleted;

    [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;

    IVesselStatus _status;
    ResourceSystem _resources;
    Resource _heatResource;
    ActionExecutorRegistry _registry;

    CancellationTokenSource _cts;
    bool _isOverheating;

    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    void OnTurnEndOfMiniGame() => End();

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _resources = shipStatus.ResourceSystem;
    }

    public float Heat01 => (_heatResource == null || _heatResource.MaxAmount <= 0f)
        ? 0f
        : Mathf.Clamp01(_heatResource.CurrentAmount / _heatResource.MaxAmount);

    public bool IsOverheating => _isOverheating;

    public void StartOverheat(OverheatingActionSO so, IVesselStatus status, ActionExecutorRegistry registry)
    {
        _registry = registry;
        if (_isOverheating) return;

        _heatResource = _resources.Resources[so.HeatResourceIndex];

        End();

        so.WrappedAction?.StartAction(_registry);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        BuildHeatRoutineAsync(so, _cts.Token).Forget();
    }

    public void StopOverheat(OverheatingActionSO so, IVesselStatus status, ActionExecutorRegistry registry)
    {
        _registry = registry;
        if (_isOverheating) return;

        End();

        so.WrappedAction?.StopAction(_registry);

        OnHeatDecayStarted?.Invoke();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        DecayHeatRoutineAsync(so, _cts.Token).Forget();
    }

    void End()
    {
        if (_cts == null) return;
        try
        {
            _cts.Cancel();
        }
        catch
        {
        }

        _cts.Dispose();
        _cts = null;
    }

    async UniTaskVoid BuildHeatRoutineAsync(OverheatingActionSO so, CancellationToken token)
    {
        OnHeatBuildStarted?.Invoke();

        try
        {
            while (_heatResource.CurrentAmount < _heatResource.MaxAmount)
            {
                _resources.ChangeResourceAmount(so.HeatResourceIndex, so.HeatBuildRate);
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    token);
            }

            _isOverheating = true;
            _status.Overheating = true;
            _heatResource.CurrentAmount = _heatResource.MaxAmount;
            OnOverheated?.Invoke();
            var ctrl = _status?.VesselPrismController;
            if (ctrl)
            {
                ctrl.EnableDangerMode(
                    so.DangerPrismMaterial,
                    so.OverheatScaleMultiplier,
                    so.ScaleLerpSeconds,
                    blendSeconds: .4f,
                    append: true
                );
            }

            so.WrappedAction?.StopAction(_registry);

            await UniTask.Delay(TimeSpan.FromSeconds(so.OverheatDuration),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                token);

            _isOverheating = false;
            _status.Overheating = false;

            OnHeatDecayStarted?.Invoke();
            await DecayHeatRoutineAsync(so, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogError($"[Overheating] BuildHeat error: {e}");
        }
    }

    async UniTask DecayHeatRoutineAsync(OverheatingActionSO so, CancellationToken token)
    {
        try
        {
            while (_heatResource.CurrentAmount > 0)
            {
                _resources.ChangeResourceAmount(so.HeatResourceIndex, -so.HeatDecayRate.Value);
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    token);
            }

            _heatResource.CurrentAmount = 0;
            OnHeatDecayCompleted?.Invoke();
            var ctrl = _status?.VesselPrismController;
            if (ctrl != null)
            {
                ctrl.DisableDangerMode(so.ScaleLerpSeconds);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogError($"[Overheating] DecayHeat error: {e}");
        }
    }
}
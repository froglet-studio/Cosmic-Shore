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
    [SerializeField] private ScriptableEventBool StationaryModeChanged;

    IVesselStatus _status;
    ResourceSystem _resources;
    Resource _heatResource;
    ActionExecutorRegistry _registry;

    CancellationTokenSource _cts;
    bool _isOverheating;

    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        StationaryModeChanged.OnRaised += HandleStationaryModeChanged;
    }

    void OnDisable()
    {
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        StationaryModeChanged.OnRaised -= HandleStationaryModeChanged;
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
        if (status.IsTranslationRestricted) return;

        _heatResource = _resources.Resources[so.HeatResourceIndex];

        End();
        so.WrappedAction?.StartAction(_registry, status);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        BuildHeatRoutineAsync(so, _cts.Token).Forget();
    }



    public void StopOverheat(OverheatingActionSO so, IVesselStatus status, ActionExecutorRegistry registry)
    {
        _registry = registry;
        if (_isOverheating) return;

        End();

        so.WrappedAction?.StopAction(_registry, status);

        OnHeatDecayStarted?.Invoke();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        DecayHeatRoutineAsync(so, _cts.Token).Forget();
    }
    
    private void HandleStationaryModeChanged(bool isStationary)
    {
        if (!isStationary || _status == null) return;

        float s = _status.Speed;
        if (s > 0.01f)
        {
            _status.VesselTransformer?.ModifyVelocity(_status.Course, 1f);
        }
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
            _status.IsOverheating = true;
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

            so.WrappedAction?.StopAction(_registry, _status);

            await UniTask.Delay(TimeSpan.FromSeconds(so.OverheatDuration),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                token);

            _isOverheating = false;
            if (_status != null) _status.IsOverheating = false;

            if (ctrl) ctrl.DisableDangerMode(so.ScaleLerpSeconds);

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
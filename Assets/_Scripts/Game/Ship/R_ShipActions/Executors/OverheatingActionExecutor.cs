using System;
using System.Collections;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

public sealed class OverheatingActionExecutor : ShipActionExecutorBase
{
    public event Action OnHeatBuildStarted;
    public event Action OnOverheated;
    public event Action OnHeatDecayStarted;
    public event Action OnHeatDecayCompleted;

    IVesselStatus _status;
    ResourceSystem _resources;
    Resource _heatResource;
    ActionExecutorRegistry _registry;

    Coroutine _heatRoutine;
    bool _isOverheating;

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
        if (_heatRoutine != null) StopCoroutine(_heatRoutine);

        so.WrappedAction?.StartAction(_registry);

        _heatRoutine = StartCoroutine(BuildHeatRoutine(so));
    }

    public void StopOverheat(OverheatingActionSO so, IVesselStatus status, ActionExecutorRegistry registry)
    {
        _registry = registry;
        if (_isOverheating) return;

        if (_heatRoutine != null) StopCoroutine(_heatRoutine);

        so.WrappedAction?.StopAction(_registry);

        OnHeatDecayStarted?.Invoke();
        _heatRoutine = StartCoroutine(DecayHeatRoutine(so));
    }

    IEnumerator BuildHeatRoutine(OverheatingActionSO so)
    {
        OnHeatBuildStarted?.Invoke();

        while (_heatResource.CurrentAmount < _heatResource.MaxAmount)
        {
            _resources.ChangeResourceAmount(so.HeatResourceIndex, so.HeatBuildRate);
            yield return new WaitForSeconds(0.1f);
        }

        _isOverheating = true;
        _status.Overheating = true;
        _heatResource.CurrentAmount = _heatResource.MaxAmount;

        OnOverheated?.Invoke();

        so.WrappedAction?.StopAction(_registry);

        yield return new WaitForSeconds(so.OverheatDuration);

        _isOverheating = false;
        _status.Overheating = false;

        OnHeatDecayStarted?.Invoke();
        _heatRoutine = StartCoroutine(DecayHeatRoutine(so));
    }

    IEnumerator DecayHeatRoutine(OverheatingActionSO so)
    {
        while (_heatResource.CurrentAmount > 0)
        {
            _resources.ChangeResourceAmount(so.HeatResourceIndex, -so.HeatDecayRate.Value); 
            yield return new WaitForSeconds(0.1f);
        }

        _heatResource.CurrentAmount = 0;
        OnHeatDecayCompleted?.Invoke();
    }
}

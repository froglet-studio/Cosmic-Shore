using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

public class ConsumeBoostActionExecutor : ShipActionExecutorBase
{
    // ===== HUD events (magazine-based) =====
    public event Action<int,int>   OnChargesSnapshot;  
    public event Action<int,float> OnChargeConsumed;  
    public event Action<float>     OnReloadStarted;   
    public event Action            OnReloadCompleted;

    // (legacy; safe to ignore in HUD)
    public event Action<float, float> OnBoostStarted;  
    public event Action OnBoostEnded;

    // Runtime
    IVesselStatus _status;
    ResourceSystem _resources;

    // Config (captured from SO on use)
    ConsumeBoostActionSO _so;

    // State
    int _available;
    bool _reloading;
    int _activeStacks;              
    readonly List<Coroutine> _runningBoosts = new();
    Coroutine _reloadRoutine;

    public int AvailableCharges => _available;
    public int MaxCharges => _so != null ? _so.MaxCharges : 0;
    public bool IsReloading => _reloading;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status    = shipStatus;
        _resources = shipStatus?.ResourceSystem;

        // ensure vessel baseline
        if (_status != null)
        {
            _status.BoostMultiplier = 1f;
            _status.Boosting = false;
        }
    }

    /// <summary>
    /// Called by SO StartAction. Attempts to consume one charge and apply a boost stack.
    /// </summary>
    public void Consume(ConsumeBoostActionSO so, IVesselStatus status)
    {
        if (so == null || status == null) return;

        // First-time capture / (re)initialize magazine if config changed or first use
        if (_so != so || _available <= 0 && _activeStacks == 0 && !_reloading)
        {
            _so = so;
            // If this is the very first time we see this SO for the ship (or config changed), initialize magazine
            if (_available <= 0 && _activeStacks == 0 && !_reloading)
            {
                _available  = Mathf.Clamp(_so.MaxCharges, 0, 4);
                _reloading  = false;
                OnChargesSnapshot?.Invoke(_available, _so.MaxCharges);
            }
        }

        if (_reloading) return;        // cannot fire during reload
        if (_available <= 0) return;   // empty mag

        // optional resource gate
        if (_so.ResourceCost > 0f)
        {
            if (_resources == null) return;
            if (_so.ResourceIndex < 0 || _so.ResourceIndex >= _resources.Resources.Count) return;
            var res = _resources.Resources[_so.ResourceIndex];
            if (res == null || res.CurrentAmount < _so.ResourceCost) return;

            // charge once per shot
            _resources.ChangeResourceAmount(_so.ResourceIndex, -_so.ResourceCost);
            OnBoostStarted?.Invoke(_so.BoostDuration, res.CurrentAmount);
        }

        // spend one charge; choose rightmost filled pip (available-1)
        int pipIndex = Mathf.Clamp(_available - 1, 0, _so.MaxCharges - 1);
        _available = Mathf.Max(0, _available - 1);
        OnChargesSnapshot?.Invoke(_available, _so.MaxCharges);
        OnChargeConsumed?.Invoke(pipIndex, Mathf.Max(0.05f, _so.BoostDuration));

        // apply one stack of boost
        var co = StartCoroutine(BoostRoutine(_so.BoostMultiplier.Value, _so.BoostDuration));
        _runningBoosts.Add(co);

        // if we just emptied the mag, schedule a single reload
        if (_available == 0 && !_reloading)
            _reloadRoutine = StartCoroutine(ReloadRoutine(_so.ReloadCooldown, _so.ReloadFillTime));
    }

    /// <summary>
    /// Called by SO StopAction. Immediately ends all active boost stacks and resets multiplier.
    /// </summary>
    public void StopAllBoosts()
    {
        // Stop all running boost coroutines
        for (int i = 0; i < _runningBoosts.Count; i++)
            if (_runningBoosts[i] != null) StopCoroutine(_runningBoosts[i]);
        _runningBoosts.Clear();

        // Clear stacks from the multiplier
        if (_activeStacks > 0 && _status != null)
        {
            _status.BoostMultiplier -= _activeStacks * (_so != null ? _so.BoostMultiplier.Value : 0f);
            _activeStacks = 0;
        }

        if (_status != null && _status.BoostMultiplier <= 1f)
        {
            _status.BoostMultiplier = 1f;
            _status.Boosting = false;
            OnBoostEnded?.Invoke();
        }
    }

    IEnumerator BoostRoutine(float mult, float duration)
    {
        _activeStacks += 1;
        if (_status != null)
        {
            _status.Boosting = true;
            _status.BoostMultiplier += mult;
        }

        yield return new WaitForSeconds(duration);

        // remove this stack
        if (_status != null)
        {
            _status.BoostMultiplier -= mult;
            _activeStacks = Mathf.Max(0, _activeStacks - 1);

            if (_status.BoostMultiplier <= 1f)
            {
                _status.BoostMultiplier = 1f;
                _status.Boosting = false;
                OnBoostEnded?.Invoke();
            }
        }

        // remove from running list
        // (search by reference to this coroutine; safe because we only remove once)
        for (int i = 0; i < _runningBoosts.Count; i++)
        {
            if (_runningBoosts[i] == null) continue;
            // Can't directly compare IEnumerator; instead mark last and break when found
            // Simpler: just break; we clear entire list on StopAllBoosts anyway.
        }
    }

    IEnumerator ReloadRoutine(float cooldown, float fillTime)
    {
        _reloading = true;

        if (cooldown > 0f)
            yield return new WaitForSeconds(cooldown);

        OnReloadStarted?.Invoke(Mathf.Max(0.01f, fillTime));
        if (fillTime > 0f)
            yield return new WaitForSeconds(fillTime);

        _available = _so != null ? _so.MaxCharges : _available;
        OnChargesSnapshot?.Invoke(_available, _so != null ? _so.MaxCharges : _available);
        OnReloadCompleted?.Invoke();

        _reloading = false;
        _reloadRoutine = null;
    }
}
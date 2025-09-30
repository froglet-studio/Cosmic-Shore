using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

public class ConsumeBoostActionExecutor : ShipActionExecutorBase
{
    // ===== HUD events (magazine-based) =====
    public event Action<int,int>   OnChargesSnapshot;   // count update (HUD decides which pips look full/empty)
    public event Action<int,float> OnChargeConsumed;    // (pipIndex, duration) -> animate this pip draining
    public event Action<float>     OnReloadStarted;     // total fill time for full mag animation (optional)
    public event Action            OnReloadCompleted;

    // (legacy; optional)
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

    // Independent stacks
    class BoostStack
    {
        public float Mult;
        public float Duration;
        public int   PipIndex;   // the pip that was consumed
        public Coroutine Routine; 
    }

    readonly List<BoostStack> _activeStacks = new();
    Coroutine _reloadRoutine;

    public int  AvailableCharges => _available;
    public int  MaxCharges       => _so != null ? _so.MaxCharges : 0;
    public bool IsReloading      => _reloading;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status    = shipStatus;
        _resources = shipStatus?.ResourceSystem;

        if (_status != null)
        {
            _status.BoostMultiplier = 1f;
            _status.Boosting = false;
        }
    }

    /// <summary>
    /// Called by SO StartAction. Attempts to consume one charge and apply an independent boost stack.
    /// </summary>
    public void Consume(ConsumeBoostActionSO so, IVesselStatus status)
    {
        if (so == null || status == null) return;

        // Initial (re)bind and magazine init
        if (_so != so || (_available <= 0 && _activeStacks.Count == 0 && !_reloading))
        {
            _so = so;
            if (_available <= 0 && _activeStacks.Count == 0 && !_reloading)
            {
                _available = Mathf.Clamp(_so.MaxCharges, 0, 4);
                _reloading = false;
                OnChargesSnapshot?.Invoke(_available, _so.MaxCharges);
            }
        }

        if (_reloading) return;
        if (_available <= 0) return;

        // Optional resource gate
        if (_so.ResourceCost > 0f)
        {
            if (_resources == null) return;
            if (_so.ResourceIndex < 0 || _so.ResourceIndex >= _resources.Resources.Count) return;

            var res = _resources.Resources[_so.ResourceIndex];
            if (res == null || res.CurrentAmount < _so.ResourceCost) return;

            _resources.ChangeResourceAmount(_so.ResourceIndex, -_so.ResourceCost);
            OnBoostStarted?.Invoke(_so.BoostDuration, res.CurrentAmount);
        }

        // Choose the rightmost filled pip for this shot
        int pipIndex = Mathf.Clamp(_available - 1, 0, _so.MaxCharges - 1);

        // Tell HUD to animate THIS pip draining over the boost duration
        float duration = Mathf.Max(0.05f, _so.BoostDuration);
        OnChargeConsumed?.Invoke(pipIndex, duration);

        // Spend one charge (count only; HUD snapshot should NOT override animating pip visuals)
        _available = Mathf.Max(0, _available - 1);
        OnChargesSnapshot?.Invoke(_available, _so.MaxCharges);

        // Start independent stack tied to THIS pip
        var stack = new BoostStack
        {
            Mult     = _so.BoostMultiplier.Value,
            Duration = duration,
            PipIndex = pipIndex
        };
        stack.Routine = StartCoroutine(StackRoutine(stack));
        _activeStacks.Add(stack);

        RecalculateMultiplier();

        // If we just emptied the mag, schedule reload (fills 1-by-1)
        if (_available == 0 && !_reloading)
            _reloadRoutine = StartCoroutine(ReloadRoutine(_so.ReloadCooldown, _so.ReloadFillTime));
    }

    public void StopAllBoosts()
    {
        foreach (var st in _activeStacks)
            if (st?.Routine != null) StopCoroutine(st.Routine);
        _activeStacks.Clear();

        RecalculateMultiplier();
    }

    IEnumerator StackRoutine(BoostStack stack)
    {
        yield return new WaitForSeconds(stack.Duration);

        // Remove THIS exact stack
        int idx = _activeStacks.IndexOf(stack);
        if (idx >= 0) _activeStacks.RemoveAt(idx);

        RecalculateMultiplier();
    }

    void RecalculateMultiplier()
    {
        if (_status == null) return;

        if (_activeStacks.Count > 0)
        {
            _status.Boosting = true;

            // baseline + sum of active stacks (additive)
            float total = 1f;
            for (int i = 0; i < _activeStacks.Count; i++)
                total += _activeStacks[i].Mult;

            _status.BoostMultiplier = total;
        }
        else
        {
            _status.Boosting = false;
            _status.BoostMultiplier = 1f;
            OnBoostEnded?.Invoke();
        }
    }

    IEnumerator ReloadRoutine(float cooldown, float fillTime)
    {
        _reloading = true;

        if (cooldown > 0f)
            yield return new WaitForSeconds(cooldown);

        // Optional: tell HUD the total fill animation time (we still push counts per pip)
        OnReloadStarted?.Invoke(Mathf.Max(0.01f, fillTime));

        // Refill pip-by-pip over fillTime (equal slices)
        int toFill = _so.MaxCharges - _available;
        if (toFill > 0)
        {
            float per = Mathf.Max(0.01f, fillTime / toFill);
            while (_available < _so.MaxCharges)
            {
                yield return new WaitForSeconds(per);
                _available++;
                OnChargesSnapshot?.Invoke(_available, _so.MaxCharges);
            }
        }

        OnReloadCompleted?.Invoke();

        _reloading = false;
        _reloadRoutine = null;
    }
}

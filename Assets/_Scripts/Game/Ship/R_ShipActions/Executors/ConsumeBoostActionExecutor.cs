using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

public class ConsumeBoostActionExecutor : ShipActionExecutorBase
{
 
    public event Action<int,int>   OnChargesSnapshot;   
    public event Action<int,float> OnChargeConsumed;   
    public event Action<float>     OnReloadStarted;    
    public event Action            OnReloadCompleted;
    public event Action<int, float> OnReloadPipStarted;      
    public event Action<int, float> OnReloadPipProgress;  
    public event Action<int>        OnReloadPipCompleted;  
    // (legacy; optional)
    public event Action<float, float> OnBoostStarted;  
    public event Action OnBoostEnded;

    IVesselStatus _status;
    ResourceSystem _resources;
    ConsumeBoostActionSO _so;

    int _available;
    bool _reloading;

    class BoostStack
    {
        public float Mult;
        public float Duration;
        public int   PipIndex;  
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
        if (!so || status == null) return;

        if (_reloadRoutine != null)
        {
            StopCoroutine(_reloadRoutine);
            _reloadRoutine = null;
            _reloading = false;
        }
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

        if (_so.ResourceCost > 0f)
        {
            if (!_resources) return;
            if (_so.ResourceIndex < 0 || _so.ResourceIndex >= _resources.Resources.Count) return;

            var res = _resources.Resources[_so.ResourceIndex];
            if (res == null || res.CurrentAmount < _so.ResourceCost) return;

            _resources.ChangeResourceAmount(_so.ResourceIndex, -_so.ResourceCost);
            OnBoostStarted?.Invoke(_so.BoostDuration, res.CurrentAmount);
        }
        int pipIndex = Mathf.Clamp(_available - 1, 0, _so.MaxCharges - 1);

        float duration = Mathf.Max(0.05f, _so.BoostDuration);
        OnChargeConsumed?.Invoke(pipIndex, duration);
        
        _available = Mathf.Max(0, _available - 1);
        OnChargesSnapshot?.Invoke(_available, _so.MaxCharges);

        var stack = new BoostStack
        {
            Mult     = _so.BoostMultiplier.Value,
            Duration = duration,
            PipIndex = pipIndex
        };
        stack.Routine = StartCoroutine(StackRoutine(stack));
        _activeStacks.Add(stack);

        RecalculateMultiplier();

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

    IEnumerator ReloadRoutine(float cooldown, float perPipFillTime)
    {
        _reloading = true;

        if (cooldown > 0f)
            yield return new WaitForSeconds(cooldown);

        perPipFillTime = Mathf.Max(0.01f, perPipFillTime);

        // Fill strictly one pip at a time
        while (_available < _so.MaxCharges)
        {
            int pipIndex = _available; // next empty pip to fill (0-based from left)

            // tell HUD: start animating THIS pip for perPipFillTime
            OnReloadPipStarted?.Invoke(pipIndex, perPipFillTime);

            float t = 0f;
            while (t < perPipFillTime)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / perPipFillTime);
                OnReloadPipProgress?.Invoke(pipIndex, norm);
                yield return null;

                // if someone consumed mid-reload, abort gracefully
                if (!_reloading) yield break;
            }

            // commit this pip as filled
            _available++;
            OnChargesSnapshot?.Invoke(_available, _so.MaxCharges);
            OnReloadPipCompleted?.Invoke(pipIndex);
        }

        OnReloadCompleted?.Invoke();

        _reloading = false;
        _reloadRoutine = null;
    }


}

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

public class ConsumeBoostActionExecutor : ShipActionExecutorBase
{
    public event Action<int, int> OnChargesSnapshot;
    public event Action<int, float> OnChargeConsumed;
    public event Action<float> OnReloadStarted;
    public event Action OnReloadCompleted;
    public event Action<float, float> OnBoostStarted;
    public event Action OnBoostEnded;
    
    [SerializeField, Range(0,4)] private int initialCharges = 4; 

    IVesselStatus _status;
    ResourceSystem _resources;
    ConsumeBoostActionSO _so;

    int _available;
    bool _reloading;

    [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;

    sealed class BoostStack
    {
        public float Mult;
        public float Duration;
        public int PipIndex;
        public CancellationTokenSource Cts;
    }

    readonly List<BoostStack> _activeStacks = new();
    CancellationTokenSource _reloadCts;

    public int AvailableCharges => _available;
    public int MaxCharges => _so != null ? _so.MaxCharges : 0;
    public bool IsReloading => _reloading;

    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    void OnTurnEndOfMiniGame()
    {
        CancelReload();
        CancelAllStacks();
        _reloading = false;
        if (_status == null) return;
        _status.IsBoosting = false;
        _status.BoostMultiplier = 1f;
    }

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _resources = shipStatus?.ResourceSystem;

        if (_status != null)
        {
            _status.BoostMultiplier = 1f;
            _status.IsBoosting = false;
        }

        _available = Mathf.Clamp(initialCharges, 0, 4);
        OnChargesSnapshot?.Invoke(_available, MaxCharges > 0 ? MaxCharges : 4);
    }

    public void Consume(ConsumeBoostActionSO so, IVesselStatus status)
    {
        if (!so || status == null) return;
        
        if (_status is { IsTranslationRestricted: true }) return;
        
        if (_so != so)
        {
            _so = so;
            if (_available <= 0 && _activeStacks.Count == 0 && !_reloading)
            {
                _available = Mathf.Clamp(_so.MaxCharges, 0, 4);
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
            Mult = _status.BoostMultiplier,
            Duration = duration,
            PipIndex = pipIndex,
            Cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy())
        };

        _activeStacks.Add(stack);
        StackRoutineAsync(stack, stack.Cts.Token).Forget();
        RecalculateMultiplier();

        // if (_available != 0 || _reloading) return;
        //
        // _reloadCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        // ReloadRoutineAsync(_so.ReloadCooldown, _so.ReloadFillTime, _reloadCts.Token).Forget();
    }

    void RecalculateMultiplier()
    {
        if (_status == null) return;

        var stacks = _activeStacks.Count;

        if (stacks > 0)
        {
            _status.IsBoosting = true;

            // linear stacking
           // _status.BoostMultiplier = (_so ? _so.BoostMultiplier.Value : 4f) * stacks;
            // multiplicative
             _status.BoostMultiplier = Mathf.Pow( 4f, stacks);
             Debug.Log($"Boost Multiplier Working {_status.BoostMultiplier}");
        }
        else
        {
            _status.IsBoosting = false;
            _status.BoostMultiplier = 1f;
            OnBoostEnded?.Invoke();

            if (!_so || _available != 0 || _reloading) return;
            _reloadCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ReloadRoutineAsync(_so.ReloadCooldown, _so.ReloadFillTime, _reloadCts.Token).Forget();
        }
    }


    async UniTaskVoid ReloadRoutineAsync(float cooldown, float ignoredPerPip, CancellationToken token)
    {
        _reloading = true;

        float total = Mathf.Max(0f, cooldown);
        OnReloadStarted?.Invoke(total);

        try
        {
            float endTime = Time.time + total;
            float nextLog = 0f; 
            while (!token.IsCancellationRequested && Time.time < endTime)
            {
                float remaining = Mathf.Max(0f, endTime - Time.time);

                if (Time.unscaledTime >= nextLog)
                {
                    nextLog = Time.unscaledTime + 0.1f;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            
            token.ThrowIfCancellationRequested();
            _available = _so ? Mathf.Clamp(_so.MaxCharges, 0, 4) : 4;
            
            OnChargesSnapshot?.Invoke(_available, _so ? _so.MaxCharges : 4);
            OnReloadCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[ConsumeBoost] Reload cancelled.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConsumeBoost] ReloadRoutine error: {e}");
        }
        finally
        {
            _reloading = false;
            if (_reloadCts != null)
            {
                _reloadCts.Dispose();
                _reloadCts = null;
            }

        }
    }

    async UniTaskVoid StackRoutineAsync(BoostStack stack, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(stack.Duration), DelayType.DeltaTime, PlayerLoopTiming.Update,
                token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConsumeBoost] StackRoutine error: {e}");
        }
        finally
        {
            int idx = _activeStacks.IndexOf(stack);
            if (idx >= 0) _activeStacks.RemoveAt(idx);
            stack.Cts?.Dispose();
            stack.Cts = null;
            RecalculateMultiplier();
        }
    }
    
    public void StopAllBoosts()
    {
        CancelReload();
        CancelAllStacks();

        _reloading = false;

        if (_status != null)
        {
            _status.IsBoosting = false;
            _status.BoostMultiplier = 1f;
        }

        OnBoostEnded?.Invoke();
        OnChargesSnapshot?.Invoke(_available, _so ? _so.MaxCharges : 4);
    }


    void CancelReload()
    {
        if (_reloadCts == null) return;
        try
        {
            _reloadCts.Cancel();
        }
        catch { /* ignored */ }

        _reloadCts.Dispose();
        _reloadCts = null;
    }


    void CancelAllStacks()
    {
        foreach (var st in _activeStacks)
        {
            try { st?.Cts?.Cancel(); }
            catch
            {
                // ignored
            }
            
            st?.Cts?.Dispose();
            if (st != null) st.Cts = null;
        }
        _activeStacks.Clear();
    }
}
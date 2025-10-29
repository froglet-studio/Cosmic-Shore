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
    public event Action<int,int>     OnChargesSnapshot;
    public event Action<int,float>   OnChargeConsumed;
    public event Action<float>       OnReloadStarted;
    public event Action              OnReloadCompleted;
    public event Action<int,float>   OnReloadPipStarted;
    public event Action<int,float>   OnReloadPipProgress;
    public event Action<int>         OnReloadPipCompleted;

    // (legacy; optional)
    public event Action<float, float> OnBoostStarted;
    public event Action               OnBoostEnded;

    IVesselStatus   _status;
    ResourceSystem  _resources;
    ConsumeBoostActionSO _so;

    int  _available;
    bool _reloading;

    [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;

    sealed class BoostStack
    {
        public float Mult;
        public float Duration;
        public int   PipIndex;
        public CancellationTokenSource Cts;
    }

    readonly List<BoostStack> _activeStacks = new();
    CancellationTokenSource _reloadCts;

    public int  AvailableCharges => _available;
    public int  MaxCharges       => _so != null ? _so.MaxCharges : 0;
    public bool IsReloading      => _reloading;

    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        CancelReload();
        CancelAllStacks();
        _reloading = false;
        if (_status != null)
        {
            _status.Boosting = false;
            _status.BoostMultiplier = 1f;
        }
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    void OnTurnEndOfMiniGame()
    {
        // Stop everything on mini-game turn end
        CancelReload();
        CancelAllStacks();
        _reloading = false;
        if (_status != null)
        {
            _status.Boosting = false;
            _status.BoostMultiplier = 1f;
        }
    }

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

    public void Consume(ConsumeBoostActionSO so, IVesselStatus status)
    {
        if (!so || status == null) return;
        if (_status is { IsTranslationRestricted: true }) return;

        CancelReload();

        if (_so != so || (_available <= 0 && _activeStacks.Count == 0 && !_reloading))
        {
            _so = so;
            if (_available <= 0 && _activeStacks.Count == 0 && !_reloading)
            {
                _available  = Mathf.Clamp(_so.MaxCharges, 0, 4);
                _reloading  = false;
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

        int pipIndex  = Mathf.Clamp(_available - 1, 0, _so.MaxCharges - 1);
        float duration = Mathf.Max(0.05f, _so.BoostDuration);

        OnChargeConsumed?.Invoke(pipIndex, duration);

        _available = Mathf.Max(0, _available - 1);
        OnChargesSnapshot?.Invoke(_available, _so.MaxCharges);

        var stack = new BoostStack
        {
            Mult     = _so.BoostMultiplier.Value,
            Duration = duration,
            PipIndex = pipIndex,
            Cts      = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy())
        };

        _activeStacks.Add(stack);
        StackRoutineAsync(stack, stack.Cts.Token).Forget();

        RecalculateMultiplier();

        if (_available == 0 && !_reloading)
        {
            _reloadCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ReloadRoutineAsync(_so.ReloadCooldown, _so.ReloadFillTime, _reloadCts.Token).Forget();
        }
    }

    public void StopAllBoosts()
    {
        CancelAllStacks();
        RecalculateMultiplier();
    }

    async UniTaskVoid StackRoutineAsync(BoostStack stack, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(stack.Duration),
                                DelayType.DeltaTime,
                                PlayerLoopTiming.Update,
                                token);
        }
        catch (OperationCanceledException) { }
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

    async UniTaskVoid ReloadRoutineAsync(float cooldown, float perPipFillTime, CancellationToken token)
    {
        _reloading = true;
        OnReloadStarted?.Invoke(Mathf.Max(0f, cooldown));

        try
        {
            if (cooldown > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(cooldown),
                                    DelayType.DeltaTime,
                                    PlayerLoopTiming.Update,
                                    token);
            }

            perPipFillTime = Mathf.Max(0.01f, perPipFillTime);

            while (_available < _so.MaxCharges)
            {
                int pipIndex = (_so.MaxCharges - 1) - _available;

                OnReloadPipStarted?.Invoke(pipIndex, perPipFillTime);

                float t = 0f;
                while (t < perPipFillTime)
                {
                    token.ThrowIfCancellationRequested();
                    t += Time.deltaTime;
                    float norm = Mathf.Clamp01(t / perPipFillTime);
                    OnReloadPipProgress?.Invoke(pipIndex, norm);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                _available++;
                OnChargesSnapshot?.Invoke(_available, _so.MaxCharges);
                OnReloadPipCompleted?.Invoke(pipIndex);
            }

            OnReloadCompleted?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[ConsumeBoost] ReloadRoutine error: {e}");
        }
        finally
        {
            _reloading = false;
            CancelReload();
        }
    }

    void CancelReload()
    {
        if (_reloadCts == null) return;
        try { _reloadCts.Cancel(); } catch { }
        _reloadCts.Dispose();
        _reloadCts = null;
    }

    void CancelAllStacks()
    {
        foreach (var st in _activeStacks)
        {
            try { st?.Cts?.Cancel(); } catch { }
            st?.Cts?.Dispose();
            if (st != null) st.Cts = null;
        }

        _activeStacks.Clear();
    }
}

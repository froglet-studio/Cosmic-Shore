using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

public sealed class ChargeBoostActionExecutor : ShipActionExecutorBase
{
    public event Action<float> OnChargeStarted;
    public event Action<float> OnChargeProgress;
    public event Action OnChargeEnded;

    public event Action<float> OnDischargeStarted;
    public event Action<float> OnDischargeProgress;
    public event Action OnDischargeEnded;

    [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;

    IVesselStatus _status;
    ResourceSystem _resources;

    CancellationTokenSource _cts;
    bool _charging;
    float _cooldownUntilUtc;

    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        CancelAll();
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    void OnTurnEndOfMiniGame() => End(); 

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _resources = shipStatus.ResourceSystem;
    }

    public void BeginCharge(ChargeBoostActionSO so, IVesselStatus status)
    {
        if (Time.unscaledTime < _cooldownUntilUtc)
        {
            if (so.Verbose)
                Debug.Log($"[ChargeBoostAction] On cooldown {(_cooldownUntilUtc - Time.unscaledTime):F2}s");
            return;
        }

        End(); // stop any running task
        if (!_resources) return;

        _charging = true;
        float start = GetUnits(so);
        status.IsChargedBoostDischarging = false;
        status.ChargedBoostCharge = BoostMultiplierFrom(so, start);

        OnChargeStarted?.Invoke(start);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        ChargeRoutineAsync(so, _cts.Token).Forget();
    }

    public void BeginDischarge(ChargeBoostActionSO so, IVesselStatus status)
    {
        End(); 
        _charging = false;

        if (!_resources) return;

        float start = GetUnits(so);
        status.IsChargedBoostDischarging = true;

        OnDischargeStarted?.Invoke(start);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        DischargeRoutineAsync(so, _cts.Token).Forget();
    }

    void End()
    {
        CancelAll();
    }

    void CancelAll()
    {
        if (_cts == null) return;
        try
        {
            _cts.Cancel();
        }
        catch
        {
            //
        }
        _cts.Dispose();
        _cts = null;
    }

    async UniTaskVoid ChargeRoutineAsync(ChargeBoostActionSO so, CancellationToken token)
    {
        float perTick = ChargePerSecond(so) * so.TickSeconds;

        try
        {
            while (_charging)
            {
                float before = GetUnits(so);
                AddUnits(so, +perTick);
                float v = GetUnits(so);

                _status.ChargedBoostCharge = BoostMultiplierFrom(so, v);
                OnChargeProgress?.Invoke(v);

                if (v >= so.MaxNormalizedCharge - 1e-4f) break;

                await UniTask.Delay(TimeSpan.FromSeconds(so.TickSeconds),
                                    DelayType.DeltaTime,
                                    PlayerLoopTiming.Update,
                                    token);
            }

            _charging = false;
            SetUnits(so, so.MaxNormalizedCharge);
            _status.ChargedBoostCharge = BoostMultiplierFrom(so, so.MaxNormalizedCharge);
            OnChargeEnded?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogError($"[ChargeBoost] ChargeRoutine error: {e}"); }
    }

    async UniTaskVoid DischargeRoutineAsync(ChargeBoostActionSO so, CancellationToken token)
    {
        float perTick = DischargePerSecond(so) * so.TickSeconds;

        try
        {
            while (GetUnits(so) > 0f)
            {
                float v = GetUnits(so);
                _status.BoostMultiplier = BoostMultiplierFrom(so, v);
                _status.IsBoosting = true;

                OnDischargeProgress?.Invoke(v / so.MaxNormalizedCharge);

                AddUnits(so, -perTick);
                await UniTask.Delay(TimeSpan.FromSeconds(so.TickSeconds),
                                    DelayType.DeltaTime,
                                    PlayerLoopTiming.Update,
                                    token);
            }

            SetUnits(so, 0f);
            _status.BoostMultiplier = 1f;
            _status.IsChargedBoostDischarging = false;
            _status.IsBoosting = false;

            if (so.RechargeCooldownSeconds > 0f)
                _cooldownUntilUtc = Time.unscaledTime + so.RechargeCooldownSeconds;

            OnDischargeEnded?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogError($"[ChargeBoost] DischargeRoutine error: {e}"); }
    }

    float GetUnits(ChargeBoostActionSO so)
    {
        if (!_resources) return 0f;
        var res = _resources.Resources[so.BoostResourceIndex];
        return Mathf.Clamp01(res.CurrentAmount) * so.MaxNormalizedCharge;
    }

    void SetUnits(ChargeBoostActionSO so, float units)
    {
        if (!_resources) return;
        units = Mathf.Clamp(units, 0f, so.MaxNormalizedCharge);

        var res = _resources.Resources[so.BoostResourceIndex];
        float normalized = (so.MaxNormalizedCharge > 0f) ? (units / so.MaxNormalizedCharge) : 0f;
        res.CurrentAmount = normalized;
        OnChargeProgress?.Invoke(units);
    }

    void AddUnits(ChargeBoostActionSO so, float delta) => SetUnits(so, GetUnits(so) + delta);

    float BoostMultiplierFrom(ChargeBoostActionSO so, float rawUnits)
    {
        float t = (so.MaxNormalizedCharge > 0f) ? Mathf.Clamp01(rawUnits / so.MaxNormalizedCharge) : 0f;
        return 1f + (so.MaxBoostMultiplier - 1f) * t;
    }

    float ChargePerSecond(ChargeBoostActionSO so)
        => (so.ChargeTimeToFull > 0f) ? (so.MaxNormalizedCharge / so.ChargeTimeToFull) : so.MaxNormalizedCharge;

    float DischargePerSecond(ChargeBoostActionSO so)
        => (so.DischargeTimeToEmpty > 0f) ? (so.MaxNormalizedCharge / so.DischargeTimeToEmpty) : so.MaxNormalizedCharge;
}

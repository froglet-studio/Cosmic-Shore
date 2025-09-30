using System;
using System.Collections;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

public sealed class ChargeBoostActionExecutor : ShipActionExecutorBase
{
    public event Action<float> OnChargeStarted;
    public event Action<float> OnChargeProgress;
    public event Action OnChargeEnded;

    public event Action<float> OnDischargeStarted;
    public event Action<float> OnDischargeProgress;
    public event Action OnDischargeEnded;

    IVesselStatus _status;
    ResourceSystem _resources;
    Coroutine _loop;
    bool _charging;
    float _cooldownUntilUtc;

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

        StopRunning();
        if (_resources == null) return;

        _charging = true;
        float start = GetUnits(so);
        status.ChargedBoostDischarging = false;
        status.ChargedBoostCharge = BoostMultiplierFrom(so, start);

        OnChargeStarted?.Invoke(start);
        _loop = StartCoroutine(ChargeRoutine(so));
    }

    public void BeginDischarge(ChargeBoostActionSO so, IVesselStatus status)
    {
        StopRunning();
        _charging = false;

        if (!_resources) return;

        float start = GetUnits(so);
        status.ChargedBoostDischarging = true;

        OnDischargeStarted?.Invoke(start);
        _loop = StartCoroutine(DischargeRoutine(so));
    }

    IEnumerator ChargeRoutine(ChargeBoostActionSO so)
    {
        float perTick = ChargePerSecond(so) * so.TickSeconds;

        while (_charging)
        {
            float before = GetUnits(so);
            AddUnits(so, +perTick);
            float v = GetUnits(so);

            _status.ChargedBoostCharge = BoostMultiplierFrom(so, v);
            OnChargeProgress?.Invoke(v);

            if (v >= so.MaxNormalizedCharge - 1e-4f) break;

            yield return new WaitForSeconds(so.TickSeconds);
        }

        _charging = false;
        SetUnits(so, so.MaxNormalizedCharge);
        _status.ChargedBoostCharge = BoostMultiplierFrom(so, so.MaxNormalizedCharge);
        OnChargeEnded?.Invoke();
    }

    IEnumerator DischargeRoutine(ChargeBoostActionSO so)
    {
        float perTick = DischargePerSecond(so) * so.TickSeconds;

        while (GetUnits(so) > 0f)
        {
            float v = GetUnits(so);
            _status.BoostMultiplier = BoostMultiplierFrom(so, v);
            _status.Boosting = true;

            OnDischargeProgress?.Invoke(v);

            AddUnits(so, -perTick);
            yield return new WaitForSeconds(so.TickSeconds);
        }

        SetUnits(so, 0f);
        _status.BoostMultiplier = 1f;
        _status.ChargedBoostDischarging = false;
        _status.Boosting = false;

        if (so.RechargeCooldownSeconds > 0f)
            _cooldownUntilUtc = Time.unscaledTime + so.RechargeCooldownSeconds;

        OnDischargeEnded?.Invoke();
    }

    void StopRunning()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
    }

    float GetUnits(ChargeBoostActionSO so)
    {
        if (_resources == null) return 0f;
        var res = _resources.Resources[so.BoostResourceIndex];
        return Mathf.Clamp01(res.CurrentAmount) * so.MaxNormalizedCharge;
    }

    void SetUnits(ChargeBoostActionSO so, float units)
    {
        if (_resources == null) return;
        units = Mathf.Clamp(units, 0f, so.MaxNormalizedCharge);

        var res = _resources.Resources[so.BoostResourceIndex];
        res.CurrentAmount = (so.MaxNormalizedCharge > 0f) ? (units / so.MaxNormalizedCharge) : 0f;
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
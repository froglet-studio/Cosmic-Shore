using System;
using System.Collections;
using CosmicShore.Game;
using UnityEngine;

public class ChargeBoostAction : ShipAction
{
    [Header("Charge Boost Settings")]
    [SerializeField] float maxBoostMultiplier; 
    [SerializeField] float maxNormalizedCharge;

    [Header("Timing (seconds)")]
    [SerializeField] float chargeTimeToFull;
    [SerializeField] float dischargeTimeToEmpty;

    [Tooltip("Tick cadence for UI/physics updates")]
    [SerializeField] float tickSeconds;

    [Header("Resource slot holding the charged units (0..maxNormalizedCharge)")]
    [SerializeField] int boostBoostResourceIndex;

    [Header("Optional Safety")]
    [SerializeField] float rechargeCooldownSeconds;

    [Header("Debug")]
    [SerializeField] bool verbose;
    
    bool resourceStoresNormalized = true;
    private bool  _charging;
    private float _cooldownUntilUtc;

    public event Action<float> OnChargeStarted, OnChargeProgress, OnDischargeStarted, OnDischargeProgress;
    public event Action OnChargeEnded, OnDischargeEnded;

    public float MaxChargeUnits => maxNormalizedCharge;

    float ChargePerSecond    => (chargeTimeToFull     > 0f) ? (maxNormalizedCharge / chargeTimeToFull)     : maxNormalizedCharge;
    float DischargePerSecond => (dischargeTimeToEmpty > 0f) ? (maxNormalizedCharge / dischargeTimeToEmpty) : maxNormalizedCharge;

    public override void StartAction()
    {
        if (Time.unscaledTime < _cooldownUntilUtc)
        {
            Log($"[StartAction] cooldown {_cooldownUntilUtc - Time.unscaledTime:F2}s");
            return;
        }

        StopAllCoroutines();

        if (!ResourceSystem)
        {
            Log("[StartAction] ResourceSystem NULL");
            return;
        }

        _charging = true;

        // preview multiplier (optional), but vessel speed shouldn’t use it yet
        var start = GetUnits();
        VesselStatus.IsChargedBoostDischarging = false;
        VesselStatus.ChargedBoostCharge = BoostMultiplierFrom(start);

        OnChargeStarted?.Invoke(start);
        StartCoroutine(ChargeRoutine());
    }

    public override void StopAction()
    {
        StopAllCoroutines();
        _charging = false;

        if (!ResourceSystem)
        {
            Log("[StopAction] ResourceSystem NULL");
            return;
        }

        var start = GetUnits();
        VesselStatus.IsChargedBoostDischarging = true;

        OnDischargeStarted?.Invoke(start);
        StartCoroutine(DischargeRoutine());
    }

    IEnumerator ChargeRoutine()
    {
        float perTick = ChargePerSecond * tickSeconds;

        while (_charging)
        {
            float before = GetUnits();
            AddUnits(+perTick);
            float v = GetUnits();

            // preview value (UI can also use events)
            VesselStatus.ChargedBoostCharge = BoostMultiplierFrom(v);
            OnChargeProgress?.Invoke(v);

            if (v >= maxNormalizedCharge - 1e-4f) break;

            yield return new WaitForSeconds(tickSeconds);
        }

        _charging = false;

        // snap to full for determinism
        SetUnits(maxNormalizedCharge);
        VesselStatus.ChargedBoostCharge = BoostMultiplierFrom(maxNormalizedCharge);
        OnChargeEnded?.Invoke();
    }

    IEnumerator DischargeRoutine()
    {
        float perTick = DischargePerSecond * tickSeconds;

        while (GetUnits() > 0f)
        {
            float v = GetUnits();
            Vessel.VesselStatus.BoostMultiplier = BoostMultiplierFrom(v);
            VesselStatus.IsBoosting = true;

            OnDischargeProgress?.Invoke(v);

            AddUnits(-perTick);

            yield return new WaitForSeconds(tickSeconds);
        }

        SetUnits(0f);
        Vessel.VesselStatus.BoostMultiplier = 1f;
        VesselStatus.IsChargedBoostDischarging = false;
        VesselStatus.IsBoosting = false;

        if (rechargeCooldownSeconds > 0f)
            _cooldownUntilUtc = Time.unscaledTime + rechargeCooldownSeconds;

        OnDischargeEnded?.Invoke();
    }

    float GetUnits()
    {
        if (!ResourceSystem) return 0f;

        var raw = ResourceSystem.Resources[boostBoostResourceIndex].CurrentAmount;
        if (resourceStoresNormalized)
        {
            return Mathf.Clamp01(raw) * maxNormalizedCharge;
        }
        return Mathf.Clamp(raw, 0f, maxNormalizedCharge);
    }


    void SetUnits(float units)
    {
        if (!ResourceSystem) return;

        units = Mathf.Clamp(units, 0f, maxNormalizedCharge);
        if (resourceStoresNormalized)
        {
            ResourceSystem
                .Resources[boostBoostResourceIndex]
                .CurrentAmount = (maxNormalizedCharge > 0f) ? (units / maxNormalizedCharge) : 0f;
            return;
        }

        ResourceSystem.Resources[boostBoostResourceIndex].CurrentAmount = units;
    }

    void AddUnits(float delta) => SetUnits(GetUnits() + delta);

    float BoostMultiplierFrom(float rawUnits)
    {
        float t = (maxNormalizedCharge > 0f) ? Mathf.Clamp01(rawUnits / maxNormalizedCharge) : 0f;
        return 1f + (maxBoostMultiplier - 1f) * t;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void Log(string msg) { if (verbose) Debug.Log($"[ChargeBoostAction] {msg}", this); }
}

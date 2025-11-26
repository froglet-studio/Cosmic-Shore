using System;
using System.Collections;
using CosmicShore.Game;
using UnityEngine;

public class ChargeBoostAction : ShipAction
{
    [Header("Charge Boost Settings")] [SerializeField]
    float maxBoostMultiplier = 3f; // stronger top-end boost

    [SerializeField] float maxNormalizedCharge = 5f; // capacity in 'units'

    [Header("Timing (seconds)")] [SerializeField]
    float chargeTimeToFull = 2.0f;

    [SerializeField] float dischargeTimeToEmpty = 2.5f;

    [Tooltip("Tick cadence for UI/physics updates")] [SerializeField]
    float tickSeconds = 0.05f;

    [Header("Resource slot holding the charged units (0..maxNormalizedCharge)")] [SerializeField]
    int boostBoostResourceIndex = 0;

    [Header("Optional Safety")] [SerializeField]
    float rechargeCooldownSeconds = 0f;

    [Header("Debug")] [SerializeField] bool verbose = false;

    private bool _charging;
    private float _cooldownUntilUtc;

    public event Action<float> OnChargeStarted, OnChargeProgress, OnDischargeStarted, OnDischargeProgress;
    public event Action OnChargeEnded, OnDischargeEnded;

    public float MaxChargeUnits => maxNormalizedCharge;

    float ChargePerSecond => (chargeTimeToFull > 0f) ? (maxNormalizedCharge / chargeTimeToFull) : maxNormalizedCharge;

    float DischargePerSecond =>
        (dischargeTimeToEmpty > 0f) ? (maxNormalizedCharge / dischargeTimeToEmpty) : maxNormalizedCharge;

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

        // preview multiplier (optional), but ship speed shouldn’t use it yet
        var start = GetUnits();
        ShipStatus.ChargedBoostDischarging = false;
        ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(start);

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
        ShipStatus.ChargedBoostDischarging = true;

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
            ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(v);
            OnChargeProgress?.Invoke(v);

            if (v >= maxNormalizedCharge - 1e-4f) break;

            yield return new WaitForSeconds(tickSeconds);
        }

        _charging = false;

        // snap to full for determinism
        SetUnits(maxNormalizedCharge);
        ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(maxNormalizedCharge);
        OnChargeEnded?.Invoke();
    }

    IEnumerator DischargeRoutine()
    {
        float perTick = DischargePerSecond * tickSeconds;

        // IMPORTANT: actually apply boost to ship movement while discharging
        while (GetUnits() > 0f)
        {
            float v = GetUnits();
            // Ship code should already multiply movement/thrust by BoostMultiplier.
            Ship.ShipStatus.BoostMultiplier = BoostMultiplierFrom(v);
            ShipStatus.Boosting = true;

            OnDischargeProgress?.Invoke(v);

            // drain after we reported the current value (so UI shows full before first decrement)
            AddUnits(-perTick);

            yield return new WaitForSeconds(tickSeconds);
        }

        // fully ended
        SetUnits(0f);
        Ship.ShipStatus.BoostMultiplier = 1f;
        ShipStatus.ChargedBoostDischarging = false;
        ShipStatus.Boosting = false;

        if (rechargeCooldownSeconds > 0f)
            _cooldownUntilUtc = Time.unscaledTime + rechargeCooldownSeconds;

        OnDischargeEnded?.Invoke();
    }

    // ----- units helpers -----
    float GetUnits()
    {
        if (!ResourceSystem) return 0f;
        return Mathf.Clamp(ResourceSystem.Resources[boostBoostResourceIndex].CurrentAmount, 0f, maxNormalizedCharge);
    }

    void SetUnits(float value)
    {
        if (!ResourceSystem) return;
        ResourceSystem.Resources[boostBoostResourceIndex].CurrentAmount = Mathf.Clamp(value, 0f, maxNormalizedCharge);
    }

    void AddUnits(float delta) => SetUnits(GetUnits() + delta);

    float BoostMultiplierFrom(float rawUnits)
    {
        float t = (maxNormalizedCharge > 0f) ? Mathf.Clamp01(rawUnits / maxNormalizedCharge) : 0f;
        return 1f + (maxBoostMultiplier - 1f) * t;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void Log(string msg)
    {
        if (verbose) Debug.Log($"[ChargeBoostAction] {msg}", this);
    }
}
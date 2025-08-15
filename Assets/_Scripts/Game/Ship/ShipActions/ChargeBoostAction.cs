using System;
using System.Collections;
using UnityEngine;

public class ChargeBoostAction : ShipAction
{
    [Header("Charge Boost Settings")]
    [SerializeField] float maxBoostMultiplier = 2f;

    [SerializeField] float chargePerSecond    = 0.5f;   // slower charge (was 0.33 per 0.1s ~= 3.3/s)
    [SerializeField] float dischargePerSecond = 2.5f;   // similar to your 0.25 per 0.1s ~= 2.5/s

    [SerializeField] float tickSeconds = 0.1f;

    [Tooltip("Resource slot used for the charged-boost meter")]
    [SerializeField] int boostBoostResourceIndex = 0;

    [Tooltip("Maximum charge units. Treat this as capacity (not strictly 0..1).")]
    [SerializeField] float maxNormalizedCharge = 5f;

    [Header("Optional Safety")]
    [Tooltip("After discharge ends, prevent immediate re-charge until this time has passed.")]
    [SerializeField] float rechargeCooldownSeconds = 0f;

    [Header("Debug")]
    [SerializeField] bool verbose = true;

    private bool _charging;
    private float _cooldownUntilUtc; 

    public int BoostResourceIndex => boostBoostResourceIndex;

    public event Action<float> OnChargeStarted;
    public event Action<float> OnChargeProgress;
    public event Action        OnChargeEnded;
    public event Action<float> OnDischargeStarted;
    public event Action<float> OnDischargeProgress;
    public event Action        OnDischargeEnded;

    public float MaxChargeUnits => maxNormalizedCharge;
    public float Normalized01 => (maxNormalizedCharge > 0f) ? Mathf.Clamp01(GetNorm() / maxNormalizedCharge) : 0f;


    public override void StartAction()
    {
        if (Time.unscaledTime < _cooldownUntilUtc)
        {
            Log($"[StartAction] in cooldown ({_cooldownUntilUtc - Time.unscaledTime:F2}s left) — ignoring.");
            return;
        }
        StopAllCoroutines();

        if (ResourceSystem == null)
        {
            Log("[StartAction] ResourceSystem is NULL — cannot charge.");
            return;
        }

        _charging = true;
        var start = GetNorm();
        ShipStatus.ChargedBoostDischarging = false;
        ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(start);

        Log($"[StartAction] begin charge | idx={boostBoostResourceIndex} start={start:F3} " +
            $"maxMult={maxBoostMultiplier:F2} perSec={chargePerSecond:F3} tick={tickSeconds:F3}");

        OnChargeStarted?.Invoke(start);
        StartCoroutine(ChargeRoutine());
    }

    public override void StopAction()
    {
        StopAllCoroutines();
        _charging = false;

        if (ResourceSystem == null)
        {
            Log("[StopAction] ResourceSystem is NULL — cannot discharge.");
            return;
        }

        var start = GetNorm();
        ShipStatus.ChargedBoostDischarging = true;

        Log($"[StopAction] begin discharge | idx={boostBoostResourceIndex} start={start:F3} " +
            $"perSec={dischargePerSecond:F3} tick={tickSeconds:F3}");

        OnDischargeStarted?.Invoke(start);
        StartCoroutine(DischargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        float perTick = chargePerSecond * tickSeconds;

        while (_charging)
        {
            float before = GetNorm();
            AddNorm(+perTick);
            float v = GetNorm();

            ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(v);
            LogTick($"[ChargeTick] {before:F3} -> {v:F3} | mult={ShipStatus.ChargedBoostCharge:F3}");
            OnChargeProgress?.Invoke(v);

            if (Mathf.Approximately(v, maxNormalizedCharge) || v >= maxNormalizedCharge)
            {
                Log($"[ChargeTick] reached full ({maxNormalizedCharge:F2}) — stopping charge.");
                break;
            }

            yield return new WaitForSeconds(tickSeconds);
        }

        _charging = false;
        ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(maxNormalizedCharge); // set to true max
        OnChargeEnded?.Invoke();
        Log("[ChargeEnd] ended | mult set to max.");
    }

    private IEnumerator DischargeRoutine()
    {
        float perTick = dischargePerSecond * tickSeconds;

        while (GetNorm() > 0f)
        {
            float before = GetNorm();
            AddNorm(-perTick);
            float v = GetNorm();

            ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(v);
            LogTick($"[DischargeTick] {before:F3} -> {v:F3} | mult={ShipStatus.ChargedBoostCharge:F3}");
            OnDischargeProgress?.Invoke(v);

            yield return new WaitForSeconds(tickSeconds);
        }

        ShipStatus.ChargedBoostCharge     = 1f;
        ShipStatus.ChargedBoostDischarging = false;

        if (ResourceSystem)
            ResourceSystem.Resources[boostBoostResourceIndex].CurrentAmount = 0f;

        // Start cooldown window to make re-charge only happen on an intentional next press
        if (rechargeCooldownSeconds > 0f)
            _cooldownUntilUtc = Time.unscaledTime + rechargeCooldownSeconds;

        OnDischargeEnded?.Invoke();
        Log("[DischargeEnd] ended | mult reset to 1.0 and resource to 0.");
    }

    // ----- helpers -----
    private float GetNorm()
    {
        if (!ResourceSystem) return 0f;
        return Mathf.Clamp(ResourceSystem.Resources[boostBoostResourceIndex].CurrentAmount, 0f, maxNormalizedCharge);
    }

    private void AddNorm(float delta)
    {
        if (!ResourceSystem) return;

        var r = ResourceSystem.Resources[boostBoostResourceIndex];
        float before = r.CurrentAmount;
        r.CurrentAmount = Mathf.Clamp(before + delta, 0f, maxNormalizedCharge);

        if (Mathf.Approximately(before, r.CurrentAmount) &&
            ((delta > 0f && before >= maxNormalizedCharge) || (delta < 0f && before <= 0f)))
        {
            LogTick($"[AddNorm] clamp hit at {(delta > 0 ? maxNormalizedCharge.ToString("F2") : "0.0")} (no change).");
        }
    }

    // Normalize to 0..1 before mapping to multiplier (fixes scaling when maxNormalizedCharge != 1)
    private float BoostMultiplierFrom(float rawUnits)
    {
        float t = (maxNormalizedCharge > 0f) ? Mathf.Clamp01(rawUnits / maxNormalizedCharge) : 0f;
        return 1f + (maxBoostMultiplier - 1f) * t;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string msg) { if (verbose) Debug.Log($"[ChargeBoostAction] {msg}", this); }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogTick(string msg) { if (verbose) Debug.Log($"[ChargeBoostAction] {msg}", this); }
}

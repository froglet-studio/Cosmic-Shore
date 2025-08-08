using System;
using System.Collections;
using UnityEngine;

public class ChargeBoostAction : ShipAction
{
    [Header("Charge Boost Settings")]
    [SerializeField] float maxBoostMultiplier = 2f;
    [SerializeField] float boostChargeRate = 0.33f;     // per tick
    [SerializeField] float boostDischargeRate = 0.25f;  // per tick
    [SerializeField] float tickSeconds = 0.1f;
    [SerializeField] int boostResourceIndex = 0;
    [SerializeField] float maxNormalizedCharge = 5f;

    [Header("Debug")]
    [SerializeField] bool verbose = true;

    private bool _charging;

    public int ResourceIndex => boostResourceIndex;

    public event Action<float> OnChargeStarted;
    public event Action<float> OnChargeProgress;
    public event Action        OnChargeEnded;
    public event Action<float> OnDischargeStarted;
    public event Action<float> OnDischargeProgress;
    public event Action        OnDischargeEnded;

    public override void StartAction()
    {
        StopAllCoroutines();

        if (ResourceSystem == null)
        {
            Log("[StartAction] ResourceSystem is NULL — cannot charge.");
            return;
        }

        // Reset meter to 0 before charging
        ResourceSystem.Resources[boostResourceIndex].CurrentAmount = 0f;

        _charging = true;
        var start = GetNorm();
        ShipStatus.ChargedBoostDischarging = false;
        ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(start);

        Log($"[StartAction] begin charge | idx={boostResourceIndex} start={start:F3} " +
            $"maxMult={maxBoostMultiplier:F2} rate/tick={boostChargeRate:F3} tick={tickSeconds:F3}");

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

        Log($"[StopAction] begin discharge | idx={boostResourceIndex} start={start:F3} " +
            $"rate/tick={boostDischargeRate:F3} tick={tickSeconds:F3}");

        OnDischargeStarted?.Invoke(start);
        StartCoroutine(DischargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        while (_charging)
        {
            float before = GetNorm();
            AddNorm(+boostChargeRate);
            float v = GetNorm();

            ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(v);

            LogTick($"[ChargeTick] {before:F3} -> {v:F3} | mult={ShipStatus.ChargedBoostCharge:F3}");

            OnChargeProgress?.Invoke(v);

            if (Mathf.Approximately(v, maxNormalizedCharge))
            {
                Log($"[ChargeTick] reached full ({maxNormalizedCharge:F2}) — stopping charge.");
                break;
            }

            yield return new WaitForSeconds(tickSeconds);
        }

        _charging = false;
        ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(1f);
        OnChargeEnded?.Invoke();
        Log("[ChargeEnd] ended | mult set to max.");
    }

    private IEnumerator DischargeRoutine()
    {
        while (GetNorm() > 0)
        {
            float before = GetNorm();
            AddNorm(-boostDischargeRate);
            float v = GetNorm();

            ShipStatus.ChargedBoostCharge = BoostMultiplierFrom(v);

            LogTick($"[DischargeTick] {before:F3} -> {v:F3} | mult={ShipStatus.ChargedBoostCharge:F3}");

            OnDischargeProgress?.Invoke(v);
            yield return new WaitForSeconds(tickSeconds);
        }

        ShipStatus.ChargedBoostCharge = 1f;
        ShipStatus.ChargedBoostDischarging = false;

        if (ResourceSystem)
            ResourceSystem.Resources[boostResourceIndex].CurrentAmount = 0f;

        OnDischargeEnded?.Invoke();
        Log("[DischargeEnd] ended | mult reset to 1.0 and resource to 0.");
    }

    // ----- helpers -----
    private float GetNorm()
    {
        if (!ResourceSystem) return 0f;
        return Mathf.Clamp(ResourceSystem.Resources[boostResourceIndex].CurrentAmount, 0f, maxNormalizedCharge);
    }

    private void AddNorm(float deltaPerTick)
    {
        if (!ResourceSystem) return;

        var r = ResourceSystem.Resources[boostResourceIndex];
        float before = r.CurrentAmount;
        r.CurrentAmount = Mathf.Clamp(before + deltaPerTick, 0f, maxNormalizedCharge);

        if (Mathf.Approximately(before, r.CurrentAmount) && 
            ((deltaPerTick > 0 && before >= maxNormalizedCharge) || (deltaPerTick < 0 && before <= 0f)))
        {
            LogTick($"[AddNorm] clamp hit at {(deltaPerTick > 0 ? maxNormalizedCharge.ToString("F2") : "0.0")} (no change).");
        }
    }

    private float BoostMultiplierFrom(float normalized) => 1f + (maxBoostMultiplier - 1f) * normalized;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string msg) { if (verbose) Debug.Log($"[ChargeBoostAction] {msg}", this); }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogTick(string msg) { if (verbose) Debug.Log($"[ChargeBoostAction] {msg}", this); }
}

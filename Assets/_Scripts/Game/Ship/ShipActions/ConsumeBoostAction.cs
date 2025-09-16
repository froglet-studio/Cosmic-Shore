using System;
using System.Collections;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;

public class ConsumeBoostAction : ShipAction
{
    [Header("Boost Effect")] [SerializeField]
    ElementalFloat boostMultiplier = new(4f);

    [SerializeField] float boostDuration = 4f;

    [Header("Magazine (charges)")] [SerializeField, Range(1, 4)]
    int maxCharges = 4;

    [SerializeField] float reloadCooldown = 3f; // wait before reload begins
    [SerializeField] float reloadFillTime = 0.8f; // HUD fill anim time for ALL pips (0→1)

    [Header("Optional resource gate (one-time spend per shot; set <=0 to ignore)")] [SerializeField]
    int resourceIndex = 1;

    [SerializeField] float resourceCost = 0f;

    // HUD events (magazine-based)
    public event Action<int, int> OnChargesSnapshot; // (available, max) – fire on init & after reload
    public event Action<int, float> OnChargeConsumed; // (pipIndex, durationSeconds) – animate 1→0
    public event Action<float> OnReloadStarted; // (reloadFillTimeSeconds) – animate all pips 0→1
    public event Action OnReloadCompleted; // fired when charges restored

    // (legacy; safe to ignore in HUD)
    public event Action<float, float> OnBoostStarted; // (duration, resourceBeforeSpend)
    public event Action OnBoostEnded;

    int available;
    bool reloading;
    float activeStacks; // track stacked boosts to unwind properly
    Coroutine currentBoost; // last boost coroutine

    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);
        Vessel.VesselStatus.BoostMultiplier = 1f;
        VesselStatus.Boosting = false;

        available = Mathf.Clamp(maxCharges, 0, 4);
        reloading = false;
        activeStacks = 0;
        OnChargesSnapshot?.Invoke(available, maxCharges);
    }

    public override void StartAction()
    {
        if (reloading) return; // cannot fire during reload
        if (available <= 0) return; // empty magazine

        // optional resource gate
        if (resourceCost > 0f)
        {
            var rs = Vessel.VesselStatus.ResourceSystem as ResourceSystem;
            if (rs == null || resourceIndex < 0 || resourceIndex >= rs.Resources.Count) return;
            var res = rs.Resources[resourceIndex];
            if (res.CurrentAmount < resourceCost) return;
            rs.ChangeResourceAmount(resourceIndex, -resourceCost);
            OnBoostStarted?.Invoke(boostDuration, res.CurrentAmount); // legacy
        }

        // spend one charge: choose rightmost filled pip (available-1)
        int pipIndex = Mathf.Clamp(available - 1, 0, maxCharges - 1);
        available = Mathf.Max(0, available - 1);
        OnChargesSnapshot?.Invoke(available, maxCharges);
        OnChargeConsumed?.Invoke(pipIndex, Mathf.Max(0.05f, boostDuration));

        // apply one stack of boost
        currentBoost = StartCoroutine(BoostRoutine());

        // if we just emptied the mag, schedule a single reload
        if (available == 0 && !reloading)
            StartCoroutine(ReloadRoutine());
    }

    public override void StopAction()
    {
        // immediately end current boost effect (doesn't refund a charge or trigger reload)
        if (currentBoost != null)
        {
            StopCoroutine(currentBoost);
            currentBoost = null;
        }

        if (activeStacks > 0)
        {
            Vessel.VesselStatus.BoostMultiplier -= activeStacks * boostMultiplier.Value;
            activeStacks = 0;
        }

        if (Vessel.VesselStatus.BoostMultiplier <= 1f)
        {
            Vessel.VesselStatus.BoostMultiplier = 1f;
            VesselStatus.Boosting = false;
            OnBoostEnded?.Invoke(); // legacy
        }
    }

    IEnumerator BoostRoutine()
    {
        float mult = boostMultiplier.Value;
        activeStacks += 1f;
        VesselStatus.Boosting = true;
        Vessel.VesselStatus.BoostMultiplier += mult;

        yield return new WaitForSeconds(boostDuration);

        Vessel.VesselStatus.BoostMultiplier -= mult;
        activeStacks = Mathf.Max(0f, activeStacks - 1f);

        if (Vessel.VesselStatus.BoostMultiplier <= 1f)
        {
            Vessel.VesselStatus.BoostMultiplier = 1f;
            VesselStatus.Boosting = false;
            OnBoostEnded?.Invoke(); // legacy
        }

        currentBoost = null;
    }

    IEnumerator ReloadRoutine()
    {
        reloading = true;

        // cooldown before refill animation starts
        if (reloadCooldown > 0f)
            yield return new WaitForSeconds(reloadCooldown);

        // tell HUD to animate ALL pips 0→1 over reloadFillTime
        OnReloadStarted?.Invoke(Mathf.Max(0.01f, reloadFillTime));
        if (reloadFillTime > 0f)
            yield return new WaitForSeconds(reloadFillTime);

        // restore full magazine
        available = maxCharges;
        OnChargesSnapshot?.Invoke(available, maxCharges);
        OnReloadCompleted?.Invoke();

        reloading = false;
    }
}
using System;
using UnityEngine;
using System.Collections;
using CosmicShore.Core;
using CosmicShore.Game;

public class OverheatingAction : ShipAction 
{
    public event Action OnHeatBuildStarted;
    public event Action OnOverheated;
    public event Action OnHeatDecayStarted;  
    public event Action OnHeatDecayCompleted;
    
    [SerializeField] ShipAction wrappedAction;
    [SerializeField] int heatResourceIndex = 0;
    [SerializeField] float heatBuildRate = 0.02f;
    [SerializeField] ElementalFloat heatDecayRate = new(0.04f);
    [SerializeField] float overheatDuration = 3f;

    Resource heatResource;
    bool isOverheating = false;
    
    public float Heat01
    {
        get
        {
            if (heatResource == null || heatResource.MaxAmount <= 0f) return 0f;
            return Mathf.Clamp01(heatResource.CurrentAmount / heatResource.MaxAmount);
        }
    }

    public bool IsOverheating => isOverheating;
    
    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);
        wrappedAction.Initialize(vessel);
        heatResource = ResourceSystem.Resources[heatResourceIndex];
    }

    public override void StartAction()
    {
        if (!isOverheating)
        {
            StopAllCoroutines();
            wrappedAction.StartAction();
            StartCoroutine(BuildHeatCoroutine());
        }
    }

    public override void StopAction()
    {
        if (!isOverheating)
        {
            StopAllCoroutines();
            wrappedAction.StopAction();
            OnHeatDecayStarted?.Invoke();
            StartCoroutine(DecayHeatCoroutine());
        }
    }

    IEnumerator BuildHeatCoroutine()
    {
        OnHeatBuildStarted?.Invoke(); 
        while (heatResource.CurrentAmount < heatResource.MaxAmount)
        {
            ResourceSystem.ChangeResourceAmount(heatResourceIndex, heatBuildRate);
            yield return new WaitForSeconds(0.1f);
        }

        isOverheating = true;
        OnOverheated?.Invoke();  
        VesselStatus.Overheating = true;
        heatResource.CurrentAmount = heatResource.MaxAmount;
        wrappedAction.StopAction();

        yield return new WaitForSeconds(overheatDuration);

        isOverheating = false;
        VesselStatus.Overheating = false;
        OnHeatDecayStarted?.Invoke();
        StartCoroutine(DecayHeatCoroutine());
    }

    IEnumerator DecayHeatCoroutine()
    {
        while (heatResource.CurrentAmount > 0)
        {
            ResourceSystem.ChangeResourceAmount(heatResourceIndex, -heatDecayRate.Value);
            yield return new WaitForSeconds(0.1f);
        }
        OnHeatDecayCompleted?.Invoke();
        heatResource.CurrentAmount = 0;
    }
}
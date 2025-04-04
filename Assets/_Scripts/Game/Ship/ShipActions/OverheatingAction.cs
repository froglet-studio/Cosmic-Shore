using UnityEngine;
using System.Collections;
using CosmicShore.Core;
using CosmicShore.Game;

public class OverheatingAction : ShipAction
{
    [SerializeField] ShipAction wrappedAction;
    [SerializeField] int heatResourceIndex = 0;
    [SerializeField] float heatBuildRate = 0.02f;
    [SerializeField] ElementalFloat heatDecayRate = new(0.04f);
    [SerializeField] float overheatDuration = 3f;

    Resource heatResource;
    bool isOverheating = false;

    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
        wrappedAction.Initialize(ship);
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
            StartCoroutine(DecayHeatCoroutine());
        }
    }

    IEnumerator BuildHeatCoroutine()
    {
        while (heatResource.CurrentAmount < heatResource.MaxAmount)
        {
            ResourceSystem.ChangeResourceAmount(heatResourceIndex, heatBuildRate);
            yield return new WaitForSeconds(0.1f);
        }

        isOverheating = true;
        ShipStatus.Overheating = true;
        heatResource.CurrentAmount = heatResource.MaxAmount;
        wrappedAction.StopAction();

        yield return new WaitForSeconds(overheatDuration);

        isOverheating = false;
        ShipStatus.Overheating = false;
        StartCoroutine(DecayHeatCoroutine());
    }

    IEnumerator DecayHeatCoroutine()
    {
        while (heatResource.CurrentAmount > 0)
        {
            ResourceSystem.ChangeResourceAmount(heatResourceIndex, -heatDecayRate.Value);
            yield return new WaitForSeconds(0.1f);
        }
        
        heatResource.CurrentAmount = 0;
    }
}
using UnityEngine;
using System.Collections;
using CosmicShore.Core;

public class OverheatingAction : ShipAction
{
    [SerializeField] ShipAction wrappedAction;
    [SerializeField] int heatResourceIndex = 0;
    [SerializeField] float heatBuildRate = 0.02f;
    [SerializeField] ElementalFloat heatDecayRate = new(0.04f);
    [SerializeField] float overheatDuration = 3f;

    ShipStatus shipStatus;
    Resource heatResource;
    bool isOverheating = false;

    protected override void Start()
    {
        base.Start();
        shipStatus = ship.GetComponent<ShipStatus>();

        BindElementalFloats(ship);
    }

    protected override void InitializeShipAttributes()
    {
        base.InitializeShipAttributes();
        wrappedAction.Ship = ship;
        heatResource = resourceSystem.Resources[heatResourceIndex];
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
            resourceSystem.ChangeResourceAmount(heatResourceIndex, heatBuildRate);
            yield return new WaitForSeconds(0.1f);
        }

        isOverheating = true;
        shipStatus.Overheating = true;
        heatResource.CurrentAmount = heatResource.MaxAmount;
        wrappedAction.StopAction();

        yield return new WaitForSeconds(overheatDuration);

        isOverheating = false;
        shipStatus.Overheating = false;
        StartCoroutine(DecayHeatCoroutine());
    }

    IEnumerator DecayHeatCoroutine()
    {
        while (heatResource.CurrentAmount > 0)
        {
            resourceSystem.ChangeResourceAmount(heatResourceIndex, -heatDecayRate.Value);
            yield return new WaitForSeconds(0.1f);
        }
        
        heatResource.CurrentAmount = 0;
    }
}
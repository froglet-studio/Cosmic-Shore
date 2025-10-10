using CosmicShore.Game;
using System.Collections;
using UnityEngine;

public class GrowTrailAction : GrowActionBase
{
    [SerializeField] float XWeight;
    [SerializeField] float YWeight;
    [SerializeField] float ZWeight;
    [SerializeField] float GapWeight;

    VesselPrismController controller;
    private string scalingDimension;

    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);
        controller = target.GetComponent<VesselPrismController>();

        // Determine the scaling dimension
        scalingDimension = DetermineScalingDimension();
    }

    private string DetermineScalingDimension()
    {
        // Check if only GapWeight is set
        if (XWeight == 0 && YWeight == 0 && ZWeight == 0 && GapWeight > 0)
            return "Gap";

        if (XWeight > YWeight && XWeight > ZWeight)
            return "X";
        else if (YWeight > ZWeight)
            return "Y";
        else if (ZWeight > 0)
            return "Z";

        return "None";
    }

    protected override IEnumerator GrowCoroutine(bool growing)
    {
        while (growing && ShouldContinueScaling(true))
        {
            controller.XScaler += Time.deltaTime * growRate * XWeight;
            controller.YScaler += Time.deltaTime * growRate * YWeight;
            controller.ZScaler += Time.deltaTime * growRate * ZWeight;
            controller.Gap -= Time.deltaTime * growRate * GapWeight * 2; // if gap weight is negative it shrinks blocks both sides

            yield return null;
        }
    }

    private bool ShouldContinueScaling(bool isGrowing)
    {
        switch (scalingDimension)
        {
            case "X":
                return isGrowing ? controller.XScaler < maxSize.Value : controller.XScaler > MinSize;
            case "Y":
                return isGrowing ? controller.YScaler < maxSize.Value : controller.YScaler > MinSize;
            case "Z":
                return isGrowing ? controller.ZScaler < maxSize.Value : controller.ZScaler > MinSize;
            case "Gap":
                return isGrowing ? controller.Gap > MinSize : controller.Gap < maxSize.Value;
        }
        return false;
    }

    protected override IEnumerator ReturnToNeutralCoroutine()
    {
        while (ShouldContinueScaling(false))
        {
            controller.XScaler -= Time.deltaTime * shrinkRate.Value * XWeight;
            controller.YScaler -= Time.deltaTime * shrinkRate.Value * YWeight;
            controller.ZScaler -= Time.deltaTime * shrinkRate.Value * ZWeight;
            controller.Gap += Time.deltaTime * shrinkRate.Value * GapWeight * 2;

            yield return null;
        }
    }
}

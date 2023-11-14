using System.Collections;
using UnityEngine;

public class GrowTrailAction : GrowActionBase
{
    [SerializeField] float XWeight;
    [SerializeField] float YWeight;
    [SerializeField] float ZWeight;
    [SerializeField] float GapWeight;

    TrailSpawner spawner;
    private string scalingDimension;

    protected override void Start()
    {
        base.Start();
        spawner = target.GetComponent<TrailSpawner>();

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

    protected override IEnumerator GrowCoroutine()
    {
        while (growing && ShouldContinueScaling(true))
        {
            spawner.XScaler += Time.deltaTime * growRate * XWeight;
            spawner.YScaler += Time.deltaTime * growRate * YWeight;
            spawner.ZScaler += Time.deltaTime * growRate * ZWeight;
            spawner.Gap -= Time.deltaTime * growRate * GapWeight * 2; // if gap weight is negative it shrinks blocks both sides

            yield return null;
        }
    }

    private bool ShouldContinueScaling(bool isGrowing)
    {
        switch (scalingDimension)
        {
            case "X":
                return isGrowing ? spawner.XScaler < maxSize.Value : spawner.XScaler > minSize;
            case "Y":
                return isGrowing ? spawner.YScaler < maxSize.Value : spawner.YScaler > minSize;
            case "Z":
                return isGrowing ? spawner.ZScaler < maxSize.Value : spawner.ZScaler > minSize;
            case "Gap":
                return isGrowing ? spawner.Gap > minSize : spawner.Gap < maxSize.Value;
        }
        return false;
    }

    protected override IEnumerator ReturnToNeutralCoroutine()
    {
        while (ShouldContinueScaling(false))
        {
            spawner.XScaler -= Time.deltaTime * shrinkRate.Value * XWeight;
            spawner.YScaler -= Time.deltaTime * shrinkRate.Value * YWeight;
            spawner.ZScaler -= Time.deltaTime * shrinkRate.Value * ZWeight;
            spawner.Gap += Time.deltaTime * shrinkRate.Value * GapWeight * 2;

            yield return null;
        }
    }
}

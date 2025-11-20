using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "GrowTrailAction", menuName = "ScriptableObjects/Vessel Actions/Grow Trail")]
public class GrowTrailActionSO : ShipActionSO
{
    [Header("General")]
    [SerializeField] ElementalFloat maxSize = new(3f);
    [SerializeField] float growRate = 1f;
    [SerializeField] ElementalFloat shrinkRate = new(1f);

    [Header("Weights")]
    [SerializeField] float XWeight = 0f;
    [SerializeField] float YWeight = 0f;
    [SerializeField] float ZWeight = 1f;
    [SerializeField] float GapWeight = 0f;

    public float MaxSize => maxSize.Value;
    public float GrowRate => growRate;
    public float ShrinkRate => shrinkRate.Value;
    public float WX => XWeight; public float WY => YWeight; public float WZ => ZWeight; public float WGap => GapWeight;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<GrowTrailActionExecutor>()?.Begin(this, vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<GrowTrailActionExecutor>()?.End();
}
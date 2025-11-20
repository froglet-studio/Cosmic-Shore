using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "GrowSkimmerAction", menuName = "ScriptableObjects/Vessel Actions/Grow Skimmer")]
public class GrowSkimmerActionSO : ShipActionSO
{
    [Header("Size")]
    [SerializeField] ElementalFloat maxSize = new(3f);
    [SerializeField] float growRate = 1.5f;
    [SerializeField] ElementalFloat shrinkRate = new(1f);

    [Header("Boost effect (future hook)")]
    [SerializeField] bool applyBoostWhileGrowing = false;
    [SerializeField] ElementalFloat boostMultiplier = new(1.25f);

    public float MaxSize => maxSize.Value;
    public float GrowRate => growRate;
    public float ShrinkRate => shrinkRate.Value;
    public bool ApplyBoostWhileGrowing => applyBoostWhileGrowing;
    public float BoostMultiplier => boostMultiplier.Value;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<GrowSkimmerActionExecutor>()?.Begin(this, vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<GrowSkimmerActionExecutor>()?.End();
}
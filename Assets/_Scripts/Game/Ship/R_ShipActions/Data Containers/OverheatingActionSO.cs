using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "OverheatingAction", menuName = "ScriptableObjects/Vessel Actions/Overheating")]
public class OverheatingActionSO : ShipActionSO
{
    [Header("Wrapped Action")]
    [SerializeField] ShipActionSO wrappedAction;

    [Header("Heat Settings")]
    [SerializeField] int heatResourceIndex;
    [SerializeField] float heatBuildRate;
    [SerializeField] ElementalFloat heatDecayRate;
    [SerializeField] float overheatDuration;
    [SerializeField] private Material dangerPrismMaterial;
    [SerializeField] private Vector3 overheatScaleMultiplier = new Vector3(0.7f, 1f, 0.7f);
    [SerializeField, Min(0f)] private float scaleLerpSeconds = 0.15f;
    
    public ShipActionSO WrappedAction => wrappedAction;
    public int HeatResourceIndex => heatResourceIndex;
    public float HeatBuildRate => heatBuildRate;
    public ElementalFloat HeatDecayRate => heatDecayRate;
    public float OverheatDuration => overheatDuration;
    public Material DangerPrismMaterial => dangerPrismMaterial;
    public Vector3 OverheatScaleMultiplier => overheatScaleMultiplier;
    public float ScaleLerpSeconds => scaleLerpSeconds;

    public override void Initialize(IVessel ship)
    {
        base.Initialize(ship);
        wrappedAction?.Initialize(ship);
    }
    
    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<OverheatingActionExecutor>()?.StartOverheat(this, vesselStatus, execs);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<OverheatingActionExecutor>()?.StopOverheat(this, vesselStatus, execs);

}
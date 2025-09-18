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

    public ShipActionSO WrappedAction => wrappedAction;
    public int HeatResourceIndex => heatResourceIndex;
    public float HeatBuildRate => heatBuildRate;
    public ElementalFloat HeatDecayRate => heatDecayRate;
    public float OverheatDuration => overheatDuration;

    public override void Initialize(IVessel ship)
    {
        base.Initialize(ship);
        wrappedAction?.Initialize(ship);
    }
    
    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<OverheatingActionExecutor>()?.StartOverheat(this, ShipStatus, execs);

    public override void StopAction(ActionExecutorRegistry execs)
        => execs?.Get<OverheatingActionExecutor>()?.StopOverheat(this, ShipStatus, execs);

}
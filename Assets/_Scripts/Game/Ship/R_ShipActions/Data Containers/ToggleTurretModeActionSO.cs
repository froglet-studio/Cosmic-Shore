using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "ToggleTurretModeAction", menuName = "ScriptableObjects/Vessel Actions/Toggle Turret Mode")]
public class ToggleTurretModeActionSO : ShipActionSO
{
    [Header("Config")]
    [Tooltip("Resource whose gain rate is boosted while planted as a turret.")]
    [SerializeField] int resourceIndex = 0;

    [Tooltip("Multiplier applied to the resource gain rate while stationary.")]
    [SerializeField] float stationaryGainMultiplier = 2f;

    public int ResourceIndex => resourceIndex;
    public float StationaryGainMultiplier => stationaryGainMultiplier;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<ToggleTurretModeActionExecutor>()?.Toggle(this, vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        // NO-OP for toggle
    }
}

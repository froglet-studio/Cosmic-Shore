using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "DetonateProjectilesAction", menuName = "ScriptableObjects/Vessel Actions/Detonate Projectiles")]
public class DetonateProjectilesActionSO : ShipActionSO
{
    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<DetonateProjectilesActionExecutor>()?.Detonate(vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
    }
}

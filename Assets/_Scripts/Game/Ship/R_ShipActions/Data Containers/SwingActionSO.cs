using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "SwingAction", menuName = "ScriptableObjects/Vessel Actions/Swing")]
public class SwingActionSO : ShipActionSO
{
    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        if (vesselStatus?.VesselTransformer is SwingingVesselTransformer swinger)
            swinger.StartSwing();
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        if (vesselStatus?.VesselTransformer is SwingingVesselTransformer swinger)
            swinger.ReleaseSwing();
    }
}

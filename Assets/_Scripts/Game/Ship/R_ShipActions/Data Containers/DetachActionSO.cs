using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "DetachAction", menuName = "ScriptableObjects/Vessel Actions/Detach From Prism")]
public class DetachActionSO : ShipActionSO
{
    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        if (vesselStatus is not { IsAttached: true })
            return;

        vesselStatus.IsAttached = false;
        vesselStatus.AttachedPrism = null;
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
    }
}
